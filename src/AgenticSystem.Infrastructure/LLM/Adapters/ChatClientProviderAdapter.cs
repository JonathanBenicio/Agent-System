using AgenticSystem.Core.LLM.Interfaces;
using AgenticSystem.Core.LLM.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Diagnostics;

namespace AgenticSystem.Infrastructure.LLM.Adapters;

/// <summary>
/// Adapter que wrapa um IChatClient (Microsoft.Extensions.AI) como ILLMProvider.
/// Permite usar qualquer provider M.E.AI (Azure OpenAI, Ollama, etc.) no pipeline existente.
/// </summary>
public class ChatClientProviderAdapter : ILLMProvider
{
    private readonly IChatClient _chatClient;
    private readonly ILogger<ChatClientProviderAdapter> _logger;
    private readonly bool _enableStreaming;
    private volatile string _defaultModel;
    private volatile bool _enabled;
    private volatile int _priority;
    private DateTime _lastSuccessAt = DateTime.UtcNow;
    private DateTime _lastFailureAt = DateTime.MinValue;
    private static readonly TimeSpan CircuitBreakerWindow = TimeSpan.FromMinutes(2);

    public ChatClientProviderAdapter(
        IChatClient chatClient,
        ILogger<ChatClientProviderAdapter> logger,
        string name = "M.E.AI",
        string defaultModel = "gpt-4o",
        bool enabled = true,
        int priority = 0,
        bool enableStreaming = false)
    {
        _chatClient = chatClient;
        _logger = logger;
        _enableStreaming = enableStreaming;
        Name = name;
        _defaultModel = defaultModel;
        _enabled = enabled;
        _priority = priority;
    }

    public string Name { get; }
    public string DefaultModel => _defaultModel;
    public bool IsEnabled => _enabled;
    public int Priority => _priority;

    public void Configure(string? apiKey, string? defaultModel, bool? enabled, int? priority)
    {
        if (defaultModel is not null) _defaultModel = defaultModel;
        if (enabled.HasValue) _enabled = enabled.Value;
        if (priority.HasValue) _priority = priority.Value;
        // apiKey is ignored — IChatClient manages its own credentials
    }

    public async Task<LLMResponse> GenerateAsync(LLMRequest request, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var model = request.Model ?? DefaultModel;

        try
        {
            var messages = MapMessages(request);
            var options = new ChatOptions
            {
                Temperature = (float?)request.Parameters.Temperature,
                MaxOutputTokens = request.Parameters.MaxTokens,
                TopP = (float?)request.Parameters.TopP,
                FrequencyPenalty = (float?)request.Parameters.FrequencyPenalty,
                PresencePenalty = (float?)request.Parameters.PresencePenalty,
                StopSequences = request.Parameters.Stop,
                ModelId = model
            };

            var (content, usage) = _enableStreaming
                ? await GenerateWithStreamingAsync(messages, options, ct)
                : await GenerateWithSingleResponseAsync(messages, options, ct);

            sw.Stop();

            _logger.LogDebug("🤖 M.E.AI [{Model}] {Tokens} tokens in {Latency}ms",
                model, usage.TotalTokens, sw.ElapsedMilliseconds);

            _lastSuccessAt = DateTime.UtcNow;
            return LLMResponse.Ok(content, model, Name, usage);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _lastFailureAt = DateTime.UtcNow;
            _logger.LogError(ex, "❌ M.E.AI [{Model}] failed after {Latency}ms", model, sw.ElapsedMilliseconds);
            return LLMResponse.Fail(ex.Message, Name);
        }
    }

    private async Task<(string Content, UsageInfo Usage)> GenerateWithSingleResponseAsync(
        List<Microsoft.Extensions.AI.ChatMessage> messages,
        ChatOptions options,
        CancellationToken ct)
    {
        var response = await _chatClient.GetResponseAsync(messages, options, ct);
        return (response.Text ?? string.Empty, MapUsage(response.Usage));
    }

    private async Task<(string Content, UsageInfo Usage)> GenerateWithStreamingAsync(
        List<Microsoft.Extensions.AI.ChatMessage> messages,
        ChatOptions options,
        CancellationToken ct)
    {
        var sb = new StringBuilder();

        await foreach (var update in _chatClient.GetStreamingResponseAsync(messages, options, ct))
        {
            if (!string.IsNullOrEmpty(update.Text))
            {
                sb.Append(update.Text);
            }
        }

        return (sb.ToString(), new UsageInfo());
    }

    public Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        if (!_enabled) return Task.FromResult(false);

        // Circuit breaker: if last call succeeded recently, assume available
        // If last failure is more recent than last success and within window, assume unavailable
        if (_lastFailureAt > _lastSuccessAt && (DateTime.UtcNow - _lastFailureAt) < CircuitBreakerWindow)
            return Task.FromResult(false);

        return Task.FromResult(true);
    }

    private static List<Microsoft.Extensions.AI.ChatMessage> MapMessages(LLMRequest request)
    {
        var messages = new List<Microsoft.Extensions.AI.ChatMessage>();

        if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
        {
            messages.Add(new Microsoft.Extensions.AI.ChatMessage(ChatRole.System, request.SystemPrompt));
        }

        if (request.Messages?.Count > 0)
        {
            foreach (var msg in request.Messages)
            {
                var role = msg.Role.ToLowerInvariant() switch
                {
                    "system" => ChatRole.System,
                    "assistant" => ChatRole.Assistant,
                    _ => ChatRole.User
                };
                messages.Add(new Microsoft.Extensions.AI.ChatMessage(role, msg.Content));
            }
        }
        else if (!string.IsNullOrWhiteSpace(request.Prompt))
        {
            messages.Add(new Microsoft.Extensions.AI.ChatMessage(ChatRole.User, request.Prompt));
        }

        return messages;
    }

    private static UsageInfo MapUsage(UsageDetails? usage)
    {
        if (usage is null) return new UsageInfo();

        return new UsageInfo
        {
            PromptTokens = (int)(usage.InputTokenCount ?? 0),
            CompletionTokens = (int)(usage.OutputTokenCount ?? 0)
        };
    }
}
