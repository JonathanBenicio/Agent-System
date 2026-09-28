using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Infrastructure.AgentFramework;

/// <summary>
/// Proxy Singleton que permite expor um agente Scoped (como o Orquestrador) 
/// para integrações de protocolo que resolvem dependências na raiz (A2A, AG-UI).
/// </summary>
public sealed class ScopedAgentProxy : AIAgent
{
    private readonly IServiceProvider _rootServiceProvider;
    private readonly string _targetAgentKey;

    public ScopedAgentProxy(IServiceProvider rootServiceProvider, string targetAgentKey, string name, string description = "") 
    {
        _rootServiceProvider = rootServiceProvider;
        _targetAgentKey = targetAgentKey;
    }

    public override string Name => "AgenticSystem";
    public override string Description => "Agentic System Protocol Proxy";

    private async Task<AIAgent> GetInitializedAgentAsync(IServiceProvider scopedProvider, string sessionId, CancellationToken cancellationToken)
    {
        var contextState = scopedProvider.GetRequiredService<OrchestratorContextState>();
        if (contextState.OrchestratorAgent == null)
        {
            var agentFactory = scopedProvider.GetRequiredService<IAgentFactory>();
            var activeAgents = (await agentFactory.GetAllAgentsAsync())
                .Where(a => a.IsActive).ToList();
            var hostBuilder = scopedProvider.GetRequiredService<OrchestratorHostBuilder>();
            var orchestratorAgent = await hostBuilder.BuildAsync(activeAgents, sessionId, cancellationToken);
            contextState.OrchestratorAgent = orchestratorAgent;
            contextState.ActiveAgents = activeAgents;
        }
        return scopedProvider.GetRequiredKeyedService<AIAgent>(_targetAgentKey);
    }

    protected override async Task<Microsoft.Agents.AI.AgentResponse> RunCoreAsync(IEnumerable<ChatMessage> messages, Microsoft.Agents.AI.AgentSession? session = null, AgentRunOptions? options = null, CancellationToken cancellationToken = default)
    {
        // Cria o escopo real apenas quando o framework tentar executar o agente
        await using var scope = _rootServiceProvider.CreateAsyncScope();
        var agent = await GetInitializedAgentAsync(scope.ServiceProvider, "default_session", cancellationToken);
        return await agent.RunAsync(messages, session, options, cancellationToken);
    }

    protected override async IAsyncEnumerable<AgentResponseUpdate> RunCoreStreamingAsync(IEnumerable<ChatMessage> messages, Microsoft.Agents.AI.AgentSession? session = null, AgentRunOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await using var scope = _rootServiceProvider.CreateAsyncScope();
        var agent = await GetInitializedAgentAsync(scope.ServiceProvider, "default_session", cancellationToken);
        
        // O escopo se mantém vivo pelo tempo que durar o stream (ex: SSE do AG-UI)
        await foreach (var update in agent.RunStreamingAsync(messages, session, options, cancellationToken))
        {
            yield return update;
        }
    }

    protected override async ValueTask<Microsoft.Agents.AI.AgentSession> CreateSessionCoreAsync(CancellationToken cancellationToken = default)
    {
        await using var scope = _rootServiceProvider.CreateAsyncScope();
        var agent = await GetInitializedAgentAsync(scope.ServiceProvider, "default_session", cancellationToken);
        return await agent.CreateSessionAsync(cancellationToken);
    }

    protected override async ValueTask<Microsoft.Agents.AI.AgentSession> DeserializeSessionCoreAsync(JsonElement serializedSession, JsonSerializerOptions? jsonSerializerOptions = null, CancellationToken cancellationToken = default)
    {
        await using var scope = _rootServiceProvider.CreateAsyncScope();
        var agent = await GetInitializedAgentAsync(scope.ServiceProvider, "default_session", cancellationToken);
        return await agent.DeserializeSessionAsync(serializedSession, jsonSerializerOptions, cancellationToken);
    }

    protected override async ValueTask<JsonElement> SerializeSessionCoreAsync(Microsoft.Agents.AI.AgentSession session, JsonSerializerOptions? jsonSerializerOptions = null, CancellationToken cancellationToken = default)
    {
        await using var scope = _rootServiceProvider.CreateAsyncScope();
        var agent = await GetInitializedAgentAsync(scope.ServiceProvider, "default_session", cancellationToken);
        return await agent.SerializeSessionAsync(session, jsonSerializerOptions, cancellationToken);
    }
}
