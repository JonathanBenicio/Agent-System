#pragma warning disable MAAI001 // Required experimental MAF session-store integration; reviewed under issue #120.

using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using AgenticSystem.Core.Exceptions;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using CoreAgentResponse = AgenticSystem.Core.Models.AgentResponse;
using FrameworkAgentResponse = Microsoft.Agents.AI.AgentResponse;

namespace AgenticSystem.Infrastructure.AgentFramework;

/// <summary>
/// Implementação de IFrameworkOrchestratorService usando o Microsoft Agent Framework.
/// O orquestrador é um ChatClientAgent que recebe tool bindings dos especialistas.
/// O LLM decide qual tool/agente chamar com base no input do usuário.
/// </summary>
public class FrameworkOrchestratorService : IFrameworkOrchestratorService
{
    private readonly OrchestratorMetadata _orchestratorMetadata;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IAgentExecutionPreProcessingPipeline _preProcessingPipeline;
    private readonly IAgentRuntimeCoordinator _runtimeCoordinator;
    private readonly ILLMRuntimeContextAccessor _runtimeContextAccessor;
    private readonly IAgentExecutionPostProcessingPipeline _postProcessingPipeline;
    private readonly ILogger<FrameworkOrchestratorService> _logger;

    public FrameworkOrchestratorService(
        OrchestratorMetadata orchestratorMetadata,
        IServiceScopeFactory scopeFactory,
        IAgentExecutionPreProcessingPipeline preProcessingPipeline,
        IAgentRuntimeCoordinator runtimeCoordinator,
        ILLMRuntimeContextAccessor runtimeContextAccessor,
        IAgentExecutionPostProcessingPipeline postProcessingPipeline,
        ILogger<FrameworkOrchestratorService> logger)
    {
        _orchestratorMetadata = orchestratorMetadata ?? throw new ArgumentNullException(nameof(orchestratorMetadata));
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _preProcessingPipeline = preProcessingPipeline ?? throw new ArgumentNullException(nameof(preProcessingPipeline));
        _runtimeCoordinator = runtimeCoordinator ?? throw new ArgumentNullException(nameof(runtimeCoordinator));
        _runtimeContextAccessor = runtimeContextAccessor ?? throw new ArgumentNullException(nameof(runtimeContextAccessor));
        _postProcessingPipeline = postProcessingPipeline ?? throw new ArgumentNullException(nameof(postProcessingPipeline));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<CoreAgentResponse> ExecuteAsync(
        string sessionId,
        string input,
        UserContext context,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        using var runtimeScope = _runtimeContextAccessor.BeginScope(context, sessionId);
        using var serviceScope = _scopeFactory.CreateScope();
        var scopedServices = serviceScope.ServiceProvider;

        _logger.LogInformation(
            "🎯 Orchestrator processing request via hosted framework agent: {Input}",
            input[..Math.Min(50, input.Length)]);

        // 1. Obter agentes ativos e construir o agente orquestrador de forma assíncrona
        var activeAgents = (await serviceScope.ServiceProvider.GetRequiredService<IAgentFactory>().GetAllAgentsAsync())
            .Where(a => a.IsActive).ToList();

        var hostBuilder = serviceScope.ServiceProvider.GetRequiredService<OrchestratorHostBuilder>();
        var orchestratorAgent = await hostBuilder.BuildAsync(activeAgents, sessionId, ct);

        // Armazenar no estado scoped antes de resolver OrchestratorContext
        var contextState = serviceScope.ServiceProvider.GetRequiredService<OrchestratorContextState>();
        contextState.OrchestratorAgent = orchestratorAgent;
        contextState.ActiveAgents = activeAgents;

        var orchestratorCtx = scopedServices.GetRequiredService<OrchestratorContext>();
        var orchestrator = scopedServices.GetRequiredKeyedService<AIAgent>(_orchestratorMetadata.Name);
        var frameworkFactory = scopedServices.GetRequiredService<AgentFrameworkFactory>();

        // 2. Obter ou criar sessão do framework via AgentSessionStore do hosting
        var session = await frameworkFactory.GetOrCreateSessionAsync(orchestrator, sessionId, ct);
        var preProcessingResult = await PreProcessHostedInputAsync(sessionId, input, context, ct);

        await _runtimeCoordinator.PublishEventAsync(new AgentStreamEvent
        {
            Type = AgentStreamEventType.AgentSelected,
            AgentName = _orchestratorMetadata.Name,
            Message = "Framework orchestrator delegating to specialists",
            Data = new Dictionary<string, object>
            {
                ["specialistCount"] = activeAgents.Count,
                ["mode"] = "framework-orchestration"
            }
        }, ct);

        // 3. O supervisor chama especialistas como AIFunctions dentro do ChatClientAgent.
        FrameworkAgentResponse frameworkResponse;
        try
        {
            var messages = new List<ChatMessage> { new(ChatRole.User, preProcessingResult.EffectiveInput) };
            frameworkResponse = await orchestrator.RunAsync(messages, session, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Framework handoff orchestration failed");
            var quotaError = QuotaExceededException.Find(ex);
            if (quotaError is not null)
                return CoreAgentResponse.Error(quotaError.Message, _orchestratorMetadata.Name);

            return CoreAgentResponse.Error(
                "Erro ao processar via orquestrador de handoff do framework.", _orchestratorMetadata.Name);
        }

        // 4. Extrair conteúdo textual da resposta do framework
        var content = ExtractContent(frameworkResponse, _logger);

        _logger.LogInformation(
            "📝 Workflow extraction: {MsgCount} messages, content length: {Length}, isEmpty: {IsEmpty}",
            frameworkResponse.Messages.Count, content.Length, string.IsNullOrWhiteSpace(content));

        // 5. Identificar qual especialista foi chamado (via tool calls no histórico ou eventos de handoff)
        var specialistCalls = GetSpecialistToolCalls(frameworkResponse);
        var handoffEvent = ExtractHandoffAgent(frameworkResponse);
        var calledBinding = FindCalledBinding(orchestratorCtx, specialistCalls);
        var calledAgent = calledBinding?.Agent.Name ?? handoffEvent ?? specialistCalls.FirstOrDefault();

        IAgent? resolvedAgent = calledBinding?.Agent;
        if (resolvedAgent is null && !string.IsNullOrEmpty(calledAgent))
        {
            var sanitizedCalledName = SanitizeAgentName(calledAgent);
            var matchingAgentInfo = activeAgents.FirstOrDefault(a => SanitizeAgentName(a.Name) == sanitizedCalledName);
            if (matchingAgentInfo != null)
            {
                var agentFactory = serviceScope.ServiceProvider.GetRequiredService<IAgentFactory>();
                resolvedAgent = await agentFactory.ResolveAgentAsync(matchingAgentInfo);
            }
        }

        sw.Stop();

        // Persist the supervisor and each specialist session that actually ran.
        foreach (var binding in orchestratorCtx.SpecialistBindings.Where(binding =>
                     specialistCalls.Contains(binding.Tool.Name, StringComparer.OrdinalIgnoreCase)))
        {
            await frameworkFactory.PersistSessionAsync(sessionId, binding.FrameworkAgent, binding.Session, ct);
        }

        // 6. Persistir sessão do framework para continuidade via hosting nativo
        await frameworkFactory.PersistSessionAsync(sessionId, orchestrator, session, ct);

        return await PostProcessHostedResponseAsync(
            sessionId,
            input,
            context,
            content,
            resolvedAgent ?? calledBinding?.Agent,
            calledAgent,
            orchestrator.Id ?? string.Empty,
            sw.Elapsed,
            ct);
    }

    private static string? ExtractHandoffAgent(FrameworkAgentResponse response)
    {
        foreach (var msg in response.Messages)
        {
            foreach (var fc in msg.Contents.OfType<FunctionCallContent>())
            {
                if (fc.Name.StartsWith("handoff_to_", StringComparison.OrdinalIgnoreCase))
                {
                    return fc.Name["handoff_to_".Length..];
                }
                if (fc.Name.StartsWith("handoff_", StringComparison.OrdinalIgnoreCase))
                {
                    return fc.Name["handoff_".Length..];
                }
            }
        }
        return null;
    }

    internal Task<AgentExecutionPreProcessingResult> PreProcessHostedInputAsync(
        string sessionId,
        string input,
        UserContext context,
        CancellationToken ct = default)
    {
        return _preProcessingPipeline.ProcessAsync(new AgentExecutionPreProcessingContext
        {
            SessionId = sessionId,
            Input = input,
            UserContext = context,
            ValidateRequest = true,
            ApplyCorrectionRules = true,
            Metadata = new Dictionary<string, object>
            {
                ["executionMode"] = "framework-orchestration",
                ["targetAgent"] = _orchestratorMetadata.Name
            }
        }, ct);
    }

    internal async Task<CoreAgentResponse> PostProcessHostedResponseAsync(
        string sessionId,
        string input,
        UserContext context,
        string content,
        IAgent? calledAgent,
        string? calledAgentName,
        string frameworkAgentId,
        TimeSpan latency,
        CancellationToken ct = default)
    {
        var hasContent = !string.IsNullOrWhiteSpace(content);
        var errorMessage = hasContent ? null : "O orquestrador não retornou conteúdo textual.";
        var response = new CoreAgentResponse
        {
            Content = hasContent ? content : $"Erro: {errorMessage}",
            AgentName = calledAgentName ?? _orchestratorMetadata.Name,
            AgentTier = calledAgent?.Tier ?? AgentTier.Chief,
            Success = hasContent,
            ErrorMessage = errorMessage,
            SessionId = sessionId,
            Metadata = new Dictionary<string, object>
            {
                ["executionMode"] = "framework-orchestration",
                ["hostingMode"] = "native",
                ["frameworkAgentId"] = frameworkAgentId,
                ["latencyMs"] = latency.TotalMilliseconds
            }
        };

        if (calledAgentName is not null)
        {
            response.Metadata["delegatedTo"] = calledAgentName;
        }

        if (!response.Metadata.ContainsKey("appliedCorrectionRules"))
        {
            response.Metadata["appliedCorrectionRules"] = 0;
        }

        var analysis = BuildAnalysis(calledAgent, response.AgentName, response.AgentTier);

        _logger.LogInformation(
            "✅ Orchestrator completed in {Elapsed}ms, delegated to: {Agent}",
            latency.TotalMilliseconds, calledAgentName ?? "(self)");

        return await _postProcessingPipeline.ProcessAsync(new AgentExecutionPostProcessingContext
        {
            SessionId = sessionId,
            Input = input,
            UserContext = context,
            Analysis = analysis,
            Response = response,
            Latency = latency,
            ValidateResponse = false,
            RunReflection = true,
            LearnFromReflection = true,
            EventContext = new Dictionary<string, object>
            {
                ["source"] = "AgentFramework",
                ["hostingMode"] = "native",
                ["success"] = response.Success
            },
            ArtifactData = new Dictionary<string, object>
            {
                ["hostingMode"] = "native",
                ["frameworkAgentId"] = frameworkAgentId
            }
        }, ct);
    }

    private static AnalysisResult BuildAnalysis(IAgent? agent, string agentName, AgentTier agentTier)
    {
        return new AnalysisResult
        {
            PrimaryDomain = agent?.Domain ?? "orchestration",
            Intent = IntentType.Chat,
            RecommendedTier = agentTier,
            EstimatedAgent = agentName,
            RequiredTools = agent?.AvailableTools.ToList() ?? new List<string>(),
            Confidence = 1
        };
    }

    private static string ExtractContent(FrameworkAgentResponse frameworkResponse, ILogger? logger = null)
    {
        // Extrair texto das mensagens do assistant
        var content = string.Join("\n", frameworkResponse.Messages
            .Where(m => m.Role == ChatRole.Assistant)
            .SelectMany(m => m.Contents.OfType<TextContent>())
            .Select(t => t.Text)).Trim();

        if (string.IsNullOrWhiteSpace(content))
        {
            content = string.Join("\n", frameworkResponse.Messages
                .Where(m => m.Role == ChatRole.Assistant)
                .Select(m => m.Text)).Trim();
        }

        if (string.IsNullOrWhiteSpace(content) && logger != null)
        {
            logger.LogWarning(
                "⚠️ ExtractContent returned empty. Messages: {Count}, Roles: {Roles}, TotalContents: {TotalContents}",
                frameworkResponse.Messages.Count,
                string.Join(", ", frameworkResponse.Messages.Select(m => m.Role.ToString())),
                frameworkResponse.Messages.Sum(m => m.Contents.Count()));
        }

        return content ?? string.Empty;
    }

    private static List<string> GetSpecialistToolCalls(FrameworkAgentResponse frameworkResponse)
    {
        var functionCalls = frameworkResponse.Messages
            .SelectMany(m => m.Contents.OfType<FunctionCallContent>())
            .Select(fc => fc.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToList();

        if (functionCalls.Count == 0)
        {
            return new List<string>();
        }

        // Filtrar tool calls auxiliares (RAG, SmartRouter, ContextAnalyzer) — não são especialistas
        var specialistCalls = functionCalls
            .Where(name => !OrchestratorAuxiliaryTools.AllToolNames.Contains(name))
            .ToList();

        if (specialistCalls.Count == 0)
        {
            return new List<string>();
        }

        return specialistCalls;
    }

    private static AgentToolBinding? FindCalledBinding(
        OrchestratorContext orchestratorCtx,
        IReadOnlyCollection<string> specialistCalls)
    {
        if (specialistCalls.Count == 0)
        {
            return null;
        }

        foreach (var binding in orchestratorCtx.SpecialistBindings)
        {
            if (specialistCalls.Contains(binding.Tool.Name, StringComparer.OrdinalIgnoreCase))
            {
                return binding;
            }
        }

        return null;
    }

    private static string SanitizeAgentName(string name)
    {
        if (string.IsNullOrEmpty(name)) return string.Empty;
        return new string(name.Where(c => char.IsLetterOrDigit(c)).ToArray()).ToLowerInvariant();
    }
}
