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
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AgentSessionBridge _sessionBridge;
    private readonly IAgentRuntimeCoordinator _runtimeCoordinator;
    private readonly ILLMRuntimeContextAccessor _runtimeContextAccessor;
    private readonly ILogger<FrameworkOrchestratorService> _logger;

    public FrameworkOrchestratorService(
        IServiceScopeFactory scopeFactory,
        AgentSessionBridge sessionBridge,
        IAgentRuntimeCoordinator runtimeCoordinator,
        ILLMRuntimeContextAccessor runtimeContextAccessor,
        ILogger<FrameworkOrchestratorService> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _sessionBridge = sessionBridge ?? throw new ArgumentNullException(nameof(sessionBridge));
        _runtimeCoordinator = runtimeCoordinator ?? throw new ArgumentNullException(nameof(runtimeCoordinator));
        _runtimeContextAccessor = runtimeContextAccessor ?? throw new ArgumentNullException(nameof(runtimeContextAccessor));
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
        var orchestratorCtx = scopedServices.GetRequiredService<HostedOrchestratorResolution>().Context;
        var orchestrator = scopedServices.GetRequiredKeyedService<AIAgent>(HostedOrchestratorResolution.AgentName);
        var sessionStore = scopedServices.GetRequiredKeyedService<AgentSessionStore>(HostedOrchestratorResolution.AgentName);

        // 2. Obter ou criar sessão do framework via AgentSessionStore do hosting
        var session = await sessionStore.GetSessionAsync(orchestrator, sessionId, ct);

        await _runtimeCoordinator.PublishEventAsync(new AgentStreamEvent
        {
            Type = AgentStreamEventType.AgentSelected,
            AgentName = "Orchestrator",
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
            frameworkResponse = await orchestrator.RunAsync(input, session);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Framework orchestrator execution failed");
            return CoreAgentResponse.Error(
                "Erro ao processar via orquestrador do framework.", "Orchestrator");
        }

        // 4. Extrair conteúdo textual da resposta do framework
        var content = ExtractContent(frameworkResponse);

        // 5. Identificar qual especialista foi chamado (via tool calls no histórico)
        var calledAgent = IdentifyCalledAgent(frameworkResponse, orchestratorCtx);

        sw.Stop();

        var response = new CoreAgentResponse
        {
            Content = content,
            AgentName = calledAgent ?? "Orchestrator",
            Success = true,
            SessionId = sessionId,
            Metadata = new Dictionary<string, object>
            {
                ["executionMode"] = "framework-orchestration",
                ["hostingMode"] = "native",
                ["frameworkAgentId"] = orchestrator.Id,
                ["latencyMs"] = sw.ElapsedMilliseconds
            }
        };

        if (calledAgent is not null)
        {
            response.Metadata["delegatedTo"] = calledAgent;
        }

        // 6. Persistir sessão do framework para continuidade via hosting nativo
        await sessionStore.SaveSessionAsync(orchestrator, sessionId, session, ct);

        // 7. Sincronizar evento de volta para o SessionManager
        await _sessionBridge.SyncResponseAsync(sessionId, response.AgentName, input, response);

        _logger.LogInformation(
            "✅ Orchestrator completed in {Elapsed}ms, delegated to: {Agent}",
            sw.ElapsedMilliseconds, calledAgent ?? "(self)");

        return response;
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

    private static string? IdentifyCalledAgent(
        FrameworkAgentResponse frameworkResponse,
        OrchestratorContext orchestratorCtx)
    {
        // Procurar por FunctionCallContent nas mensagens — indica qual tool/agente foi chamado
        var functionCalls = frameworkResponse.Messages
            .SelectMany(m => m.Contents.OfType<FunctionCallContent>())
            .Select(fc => fc.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToList();

        if (functionCalls.Count == 0)
        {
            return null;
        }

        // Filtrar tool calls auxiliares (RAG, SmartRouter, ContextAnalyzer) — não são especialistas
        var specialistCalls = functionCalls
            .Where(name => !OrchestratorAuxiliaryTools.AllToolNames.Contains(name))
            .ToList();

        if (specialistCalls.Count == 0)
        {
            return null;
        }

        // Mapear tool name de volta para agent name
        foreach (var binding in orchestratorCtx.SpecialistBindings)
        {
            if (specialistCalls.Contains(binding.Tool.Name, StringComparer.OrdinalIgnoreCase))
            {
                return binding.Agent.Name;
            }
        }

        return specialistCalls.FirstOrDefault();
    }
}
