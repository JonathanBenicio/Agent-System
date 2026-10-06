using System.Runtime.CompilerServices;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Infrastructure.Configuration;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgenticSystem.Infrastructure.Security;

public sealed class FidesDataProtectionMiddleware : DelegatingAIAgent
{
    private readonly FidesMessageProtection _protection;

    public FidesDataProtectionMiddleware(AIAgent innerAgent, ITenantContextAccessor tenantContext,
        IFidesTenantPolicyStore policyStore, IFidesMediaScanner mediaScanner,
        IOptions<FidesSecuritySettings> settings, ILogger<FidesDataProtectionMiddleware> logger)
        : base(innerAgent) => _protection = new(tenantContext, policyStore, mediaScanner, settings, logger);

    protected override async Task<AgentResponse> RunCoreAsync(IEnumerable<ChatMessage> messages,
        AgentSession? session, AgentRunOptions? options, CancellationToken cancellationToken)
    {
        var protectedMessages = await _protection.ProtectAsync(messages, cancellationToken);
        return protectedMessages is null
            ? new AgentResponse(new ChatMessage(ChatRole.Assistant, FidesMessageProtection.BlockedMessage))
            : await base.RunCoreAsync(protectedMessages, session, options, cancellationToken);
    }

    protected override async IAsyncEnumerable<AgentResponseUpdate> RunCoreStreamingAsync(
        IEnumerable<ChatMessage> messages, AgentSession? session, AgentRunOptions? options,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var protectedMessages = await _protection.ProtectAsync(messages, cancellationToken);
        if (protectedMessages is null)
        {
            yield return new AgentResponseUpdate { Contents = [new TextContent(FidesMessageProtection.BlockedMessage)] };
            yield break;
        }
        await foreach (var update in base.RunCoreStreamingAsync(protectedMessages, session, options, cancellationToken))
            yield return update;
    }
}
