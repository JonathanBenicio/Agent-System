using System.Runtime.CompilerServices;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Infrastructure.Configuration;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgenticSystem.Infrastructure.Security;

/// <summary>Protects the complete MAF request, including context and tool rounds, before provider dispatch.</summary>
internal sealed class FidesProtectedChatClient(IChatClient inner, IServiceProvider services,
    ILoggerFactory loggerFactory) : DelegatingChatClient(inner)
{
    private async Task<(IEnumerable<ChatMessage> Messages, ChatOptions? Options)> ProtectAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options, CancellationToken ct)
    {
        try
        {
            var protection = new FidesMessageProtection(
                services.GetRequiredService<ITenantContextAccessor>(),
                services.GetRequiredService<IFidesTenantPolicyStore>(),
                services.GetRequiredService<IFidesMediaScanner>(),
                services.GetRequiredService<IOptions<FidesSecuritySettings>>(),
                loggerFactory.CreateLogger<FidesDataProtectionMiddleware>());
            var request = messages.ToList();
            var hasInstructions = !string.IsNullOrEmpty(options?.Instructions);
            if (hasInstructions)
                request.Add(new ChatMessage(ChatRole.System, options!.Instructions));
            var sanitized = (await protection.ProtectAsync(request, ct)
                ?? throw new InvalidOperationException(FidesMessageProtection.BlockedMessage)).ToList();
            if (hasInstructions)
            {
                options = options!.Clone();
                options.Instructions = sanitized[^1].Text;
                sanitized.RemoveAt(sanitized.Count - 1);
            }
            return (sanitized, options);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            throw new InvalidOperationException(FidesMessageProtection.BlockedMessage, ex);
        }
    }

    public override async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages,
        ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var request = await ProtectAsync(messages, options, cancellationToken);
        return await base.GetResponseAsync(request.Messages, request.Options, cancellationToken);
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var request = await ProtectAsync(messages, options, cancellationToken);
        await foreach (var update in base.GetStreamingResponseAsync(request.Messages, request.Options, cancellationToken))
            yield return update;
    }
}
