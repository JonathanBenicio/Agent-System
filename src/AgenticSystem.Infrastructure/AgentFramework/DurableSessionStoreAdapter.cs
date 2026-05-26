using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask.Client.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Agents.AI.DurableTask;
using AgenticSystem.Core.Interfaces;
using Microsoft.DurableTask.Entities;

namespace AgenticSystem.Infrastructure.AgentFramework;

/// <summary>
/// Adaptador de sessão durável herdando do AgentSessionStore do MAF.
/// Persiste estados de sessão duravelmente usando Durable Task Entities com isolamento de Tenant.
/// </summary>
public sealed class DurableSessionStoreAdapter : AgentSessionStore
{
    private readonly DurableTaskClient _durableClient;
    private readonly ITenantContextAccessor _tenantContext;
    private readonly ILogger<DurableSessionStoreAdapter> _logger;
    private const string FrameworkStateKey = "frameworkSessionState";

    public DurableSessionStoreAdapter(
        DurableTaskClient durableClient,
        ITenantContextAccessor tenantContext,
        ILogger<DurableSessionStoreAdapter> logger)
    {
        _durableClient = durableClient ?? throw new ArgumentNullException(nameof(durableClient));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public override async ValueTask SaveSessionAsync(
        AIAgent agent,
        string conversationId,
        AgentSession session,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);
        ArgumentNullException.ThrowIfNull(session);

        try
        {
            var serialized = await agent.SerializeSessionAsync(session, cancellationToken: cancellationToken);
            var stateJson = serialized.GetRawText();
            
            var tenantId = _tenantContext.CurrentTenantId ?? "default";
            var runtimeKey = BuildRuntimeKey(agent.Name);
            
            // Isolamento estrito Multi-Tenant no EntityInstanceId
            var entityId = new EntityInstanceId("AgentSessionEntity", $"{tenantId}:{conversationId}:{runtimeKey}");

            // Envia um sinal para gravar o estado na entidade durável
            await _durableClient.Entities.SignalEntityAsync(entityId, "SetState", stateJson, null, cancellationToken);
        
            _logger.LogDebug(
                "🛡️ [DurableTask] Framework session signalled: Tenant={TenantId}, ConversationId={ConversationId}, Agent={AgentName}, Entity={EntityId}",
                tenantId,
                conversationId,
                agent.Name,
                entityId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist durable framework session for ConversationId={ConversationId}", conversationId);
        }
    }

    public override async ValueTask<AgentSession> GetSessionAsync(
        AIAgent agent,
        string conversationId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);

        var tenantId = _tenantContext.CurrentTenantId ?? "default";
        var runtimeKey = BuildRuntimeKey(agent.Name);
        var entityId = new EntityInstanceId("AgentSessionEntity", $"{tenantId}:{conversationId}:{runtimeKey}");

        try
        {
            var entity = await _durableClient.Entities.GetEntityAsync<string>(entityId, cancellationToken);
            if (entity != null && !string.IsNullOrWhiteSpace(entity.State))
            {
                using var doc = JsonDocument.Parse(entity.State);
                var restored = await agent.DeserializeSessionAsync(doc.RootElement, cancellationToken: cancellationToken);

                _logger.LogDebug(
                    "🛡️ [DurableTask] Framework session restored: Tenant={TenantId}, ConversationId={ConversationId}, Agent={AgentName}",
                    tenantId,
                    conversationId,
                    agent.Name);

                return restored;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to restore durable framework session, creating new one. ConversationId={ConversationId}", conversationId);
        }

        var newSession = await agent.CreateSessionAsync(cancellationToken);

        _logger.LogDebug(
            "🆕 [DurableTask] New durable framework session created: Tenant={TenantId}, ConversationId={ConversationId}, Agent={AgentName}",
            tenantId,
            conversationId,
            agent.Name);

        return newSession;
    }

    private static string BuildRuntimeKey(string? agentName)
    {
        if (string.IsNullOrWhiteSpace(agentName))
            return "default";

        return agentName.Trim().ToLowerInvariant();
    }
}
