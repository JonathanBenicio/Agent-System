using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using Microsoft.Extensions.DependencyInjection;

namespace AgenticSystem.Infrastructure.AgentFramework;

/// <summary>
/// Keeps singleton callers from capturing the scoped framework execution service.
/// </summary>
public sealed class ScopedDirectAgentExecutionService(IServiceScopeFactory scopeFactory) : IDirectAgentExecutionService
{
    public async Task<AgentResponse> ExecuteDirectAsync(
        IAgent agent,
        string sessionId,
        string input,
        UserContext context,
        CancellationToken ct = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var executor = scope.ServiceProvider.GetRequiredService<AgentFrameworkDirectExecutionService>();
        return await executor.ExecuteDirectAsync(agent, sessionId, input, context, ct);
    }
}
