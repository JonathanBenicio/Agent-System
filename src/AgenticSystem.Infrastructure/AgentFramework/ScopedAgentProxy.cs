using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

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
    protected override async Task<AgentResponse> RunCoreAsync(IEnumerable<ChatMessage> messages, AgentSession? session = null, AgentRunOptions? options = null, CancellationToken cancellationToken = default)
    {
        // Cria o escopo real apenas quando o framework tentar executar o agente
        await using var scope = _rootServiceProvider.CreateAsyncScope();
        var agent = scope.ServiceProvider.GetRequiredKeyedService<AIAgent>(_targetAgentKey);
        return await agent.RunAsync(messages, session, options, cancellationToken);
    }

    protected override async IAsyncEnumerable<AgentResponseUpdate> RunCoreStreamingAsync(IEnumerable<ChatMessage> messages, AgentSession? session = null, AgentRunOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await using var scope = _rootServiceProvider.CreateAsyncScope();
        var agent = scope.ServiceProvider.GetRequiredKeyedService<AIAgent>(_targetAgentKey);
        
        // O escopo se mantém vivo pelo tempo que durar o stream (ex: SSE do AG-UI)
        await foreach (var update in agent.RunStreamingAsync(messages, session, options, cancellationToken))
        {
            yield return update;
        }
    }

    protected override async ValueTask<AgentSession> CreateSessionCoreAsync(CancellationToken cancellationToken = default)
    {
        await using var scope = _rootServiceProvider.CreateAsyncScope();
        var agent = scope.ServiceProvider.GetRequiredKeyedService<AIAgent>(_targetAgentKey);
        return await agent.CreateSessionAsync(cancellationToken);
    }

    protected override async ValueTask<AgentSession> DeserializeSessionCoreAsync(JsonElement serializedSession, JsonSerializerOptions? jsonSerializerOptions = null, CancellationToken cancellationToken = default)
    {
        await using var scope = _rootServiceProvider.CreateAsyncScope();
        var agent = scope.ServiceProvider.GetRequiredKeyedService<AIAgent>(_targetAgentKey);
        return await agent.DeserializeSessionAsync(serializedSession, jsonSerializerOptions, cancellationToken);
    }

    protected override async ValueTask<JsonElement> SerializeSessionCoreAsync(AgentSession session, JsonSerializerOptions? jsonSerializerOptions = null, CancellationToken cancellationToken = default)
    {
        await using var scope = _rootServiceProvider.CreateAsyncScope();
        var agent = scope.ServiceProvider.GetRequiredKeyedService<AIAgent>(_targetAgentKey);
        return await agent.SerializeSessionAsync(session, jsonSerializerOptions, cancellationToken);
    }
}
