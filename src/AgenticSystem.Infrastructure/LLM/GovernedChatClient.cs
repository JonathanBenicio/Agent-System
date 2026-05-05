using System.Text;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Infrastructure.Configuration;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgenticSystem.Infrastructure.LLM;

public sealed class GovernedChatClient : IChatClient
{
    private readonly IChatClient _inner;
    private readonly IQualityGateService _qualityGateService;
    private readonly ChatClientMiddlewareOptions _options;
    private readonly ILogger<GovernedChatClient> _logger;
    private readonly SemaphoreSlim _concurrencyGate;
    private bool _disposed;

    public GovernedChatClient(
        IChatClient inner,
        IQualityGateService qualityGateService,
        IOptions<ChatClientMiddlewareOptions> options,
        ILogger<GovernedChatClient> logger)
    {
        _inner = inner;
        _qualityGateService = qualityGateService;
        _options = options.Value;
        _logger = logger;
        _concurrencyGate = new SemaphoreSlim(Math.Max(1, _options.MaxConcurrentRequests));
    }

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var messageList = messages as IList<ChatMessage> ?? messages.ToList();
        var input = RenderInput(messageList);

        if (!_options.Enabled)
        {
            return await _inner.GetResponseAsync(messageList, options, cancellationToken);
        }

        await ValidateRequestAsync(input, options, cancellationToken);
        await AcquireSlotAsync(options, cancellationToken);

        try
        {
            var response = await _inner.GetResponseAsync(messageList, options, cancellationToken);
            await ValidateResponseAsync(input, response.Text, options, _options.RejectInvalidResponses, cancellationToken);
            return response;
        }
        finally
        {
            _concurrencyGate.Release();
        }
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var messageList = messages as IList<ChatMessage> ?? messages.ToList();
        var input = RenderInput(messageList);
        var buffer = _options.EnableResponseValidation ? new StringBuilder() : null;

        if (!_options.Enabled)
        {
            await foreach (var update in _inner.GetStreamingResponseAsync(messageList, options, cancellationToken))
            {
                yield return update;
            }

            yield break;
        }

        await ValidateRequestAsync(input, options, cancellationToken);
        await AcquireSlotAsync(options, cancellationToken);

        try
        {
            await foreach (var update in _inner.GetStreamingResponseAsync(messageList, options, cancellationToken))
            {
                if (buffer is not null && !string.IsNullOrWhiteSpace(update.Text))
                {
                    buffer.Append(update.Text);
                }

                yield return update;
            }

            if (buffer is not null)
            {
                await ValidateResponseAsync(
                    input,
                    buffer.ToString(),
                    options,
                    _options.RejectInvalidStreamingResponses,
                    cancellationToken);
            }
        }
        finally
        {
            _concurrencyGate.Release();
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        ArgumentNullException.ThrowIfNull(serviceType);

        if (serviceType == typeof(IChatClient))
        {
            return this;
        }

        return _inner.GetService(serviceType, serviceKey);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _concurrencyGate.Dispose();
        _inner.Dispose();
    }

    private async Task AcquireSlotAsync(ChatOptions? options, CancellationToken cancellationToken)
    {
        var acquired = await _concurrencyGate.WaitAsync(
            TimeSpan.FromSeconds(Math.Max(1, _options.QueueWaitTimeoutSeconds)),
            cancellationToken);

        if (!acquired)
        {
            _logger.LogWarning(
                "Chat request for model {ModelId} timed out while waiting for a governed pipeline slot",
                options?.ModelId ?? "default");
            throw new TimeoutException("Timed out while waiting for a chat pipeline execution slot.");
        }
    }

    private async Task ValidateRequestAsync(string input, ChatOptions? options, CancellationToken cancellationToken)
    {
        if (!_options.EnableRequestValidation)
        {
            return;
        }

        var report = await _qualityGateService.ValidateRequestAsync(input, BuildMetadata(options), cancellationToken);
        if (report.OverallPassed)
        {
            return;
        }

        var issues = string.Join("; ", report.Results.SelectMany(result => result.Issues).Where(issue => !string.IsNullOrWhiteSpace(issue)));
        _logger.LogWarning("Chat request blocked by quality gates for model {ModelId}: {Issues}", options?.ModelId ?? "default", issues);
        throw new InvalidOperationException($"Chat request blocked by quality gates: {issues}");
    }

    private async Task ValidateResponseAsync(
        string input,
        string output,
        ChatOptions? options,
        bool rejectOnFailure,
        CancellationToken cancellationToken)
    {
        if (!_options.EnableResponseValidation)
        {
            return;
        }

        var report = await _qualityGateService.ValidateResponseAsync(input, output, BuildMetadata(options), cancellationToken);
        if (report.OverallPassed)
        {
            return;
        }

        var issues = string.Join("; ", report.Results.SelectMany(result => result.Issues).Where(issue => !string.IsNullOrWhiteSpace(issue)));
        _logger.LogWarning("Chat response failed quality gates for model {ModelId}: {Issues}", options?.ModelId ?? "default", issues);

        if (rejectOnFailure)
        {
            throw new InvalidOperationException($"Chat response blocked by quality gates: {issues}");
        }
    }

    private static string RenderInput(IEnumerable<ChatMessage> messages)
    {
        return string.Join(
            Environment.NewLine,
            messages
                .Where(message => !string.IsNullOrWhiteSpace(message.Text))
                .Select(message => $"[{message.Role.Value}] {message.Text}"));
    }

    private static Dictionary<string, object> BuildMetadata(ChatOptions? options)
    {
        return new Dictionary<string, object>
        {
            ["modelId"] = options?.ModelId ?? string.Empty,
            ["temperature"] = options?.Temperature ?? 0f,
            ["maxOutputTokens"] = options?.MaxOutputTokens ?? 0
        };
    }
}