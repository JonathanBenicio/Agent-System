using System.Diagnostics;
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

        // 1. Resolver o hosted agent nativo e o contexto de specialist bindings da execução atual
        var orchestratorCtx = scopedServices.GetRequiredService<OrchestratorContext>();
        var orchestrator = scopedServices.GetRequiredKeyedService<AIAgent>(_orchestratorMetadata.Name);
        var sessionStore = scopedServices.GetRequiredKeyedService<AgentSessionStore>(_orchestratorMetadata.Name);

        // 2. Obter ou criar sessão do framework via AgentSessionStore do hosting
        var session = await sessionStore.GetSessionAsync(orchestrator, sessionId, ct);
        var preProcessingResult = await PreProcessHostedInputAsync(sessionId, input, context, ct);

        await _runtimeCoordinator.PublishEventAsync(new AgentStreamEvent
        {
            Type = AgentStreamEventType.AgentSelected,
            AgentName = _orchestratorMetadata.Name,
            Message = "Framework orchestrator delegating to specialists",
            Data = new Dictionary<string, object>
            {
                ["specialistCount"] = orchestratorCtx.SpecialistBindings.Count,
                ["mode"] = "framework-orchestration"
            }
        }, ct);

        // 3. Executar via framework — o LLM decide qual tool/agente chamar
        FrameworkAgentResponse frameworkResponse;
        try
        {
            frameworkResponse = await orchestrator.RunAsync(preProcessingResult.EffectiveInput, session);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Framework orchestrator execution failed");
            return CoreAgentResponse.Error(
                "Erro ao processar via orquestrador do framework.", _orchestratorMetadata.Name);
        }

        // 4. Extrair conteúdo textual da resposta do framework
        var content = ExtractContent(frameworkResponse);

        // 5. Identificar qual especialista foi chamado (via tool calls no histórico)
        var specialistCalls = GetSpecialistToolCalls(frameworkResponse);
        var calledBinding = FindCalledBinding(orchestratorCtx, specialistCalls);
        var calledAgent = calledBinding?.Agent.Name ?? specialistCalls.FirstOrDefault();

        sw.Stop();

        // 6. Persistir sessão do framework para continuidade via hosting nativo
        await sessionStore.SaveSessionAsync(orchestrator, sessionId, session, ct);

        return await PostProcessHostedResponseAsync(
            sessionId,
            input,
            context,
            content,
            calledBinding?.Agent,
            calledAgent,
            orchestrator.Id ?? string.Empty,
            sw.Elapsed,
            ct);
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
        var response = new CoreAgentResponse
        {
            Content = content,
            AgentName = calledAgentName ?? _orchestratorMetadata.Name,
            AgentTier = calledAgent?.Tier ?? AgentTier.Chief,
            Success = true,
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

    private static string ExtractContent(FrameworkAgentResponse frameworkResponse)
    {
        // Extrair texto das mensagens do assistant
        var content = string.Join("\n", frameworkResponse.Messages
            .Where(m => m.Role == ChatRole.Assistant)
            .SelectMany(m => m.Contents.OfType<TextContent>())
            .Select(t => t.Text));

        if (string.IsNullOrWhiteSpace(content))
        {
            content = string.Join("\n", frameworkResponse.Messages
                .Where(m => m.Role == ChatRole.Assistant)
                .Select(m => m.Text));
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
}
