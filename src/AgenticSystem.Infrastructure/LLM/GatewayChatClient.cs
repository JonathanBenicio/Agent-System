using System.Runtime.CompilerServices;
using AgenticSystem.Core.Interfaces;
using Microsoft.Extensions.AI;

namespace AgenticSystem.Infrastructure.LLM;

/// <summary>Routes a platform-configured provider through its Gateway controls.</summary>
internal sealed class GatewayChatClient : IChatClient
{
    private readonly IChatClient _inner;
    private readonly IServiceGateway _gateway;
    private readonly string _providerName;

    public GatewayChatClient(IChatClient inner, IServiceGateway gateway, string providerName)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
        _providerName = string.IsNullOrWhiteSpace(providerName)
            ? throw new ArgumentException("Provider name is required.", nameof(providerName))
            : providerName;
    }

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var response = await _gateway.ExecuteAsync(
            _providerName,
            ct => _inner.GetResponseAsync(messages, options, ct),
            cancellationToken);

        if (!response.Success || response.Data is null)
        {
            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException(cancellationToken);
            throw new HttpRequestException(response.ErrorMessage ?? $"Gateway call to '{_providerName}' failed.");
        }

        return response.Data;
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default) =>
        _gateway.ExecuteStreamingAsync(
            _providerName,
            ct => _inner.GetStreamingResponseAsync(messages, options, ct),
            cancellationToken);

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        return serviceType == typeof(IChatClient)
            ? this
            : _inner.GetService(serviceType, serviceKey);
    }

    public void Dispose()
    {
        // The provider clients are owned by LLMManager and may be shared with other requests.
    }
}
