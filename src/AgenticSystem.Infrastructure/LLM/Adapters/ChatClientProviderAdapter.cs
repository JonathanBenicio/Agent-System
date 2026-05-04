using AgenticSystem.Core.LLM.Interfaces;
using AgenticSystem.Core.LLM.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
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
    private string _defaultModel;
    private bool _enabled;
    private int _priority;

    public ChatClientProviderAdapter(
        IChatClient chatClient,
        ILogger<ChatClientProviderAdapter> logger,
        string name = "M.E.AI",
        string defaultModel = "gpt-4o",
        bool enabled = true,
        int priority = 0)
    {
        _chatClient = chatClient;
        _logger = logger;
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

            var response = await _chatClient.GetResponseAsync(messages, options, ct);
            sw.Stop();

            var content = response.Text ?? string.Empty;
            var usage = MapUsage(response.Usage);

            _logger.LogDebug("🤖 M.E.AI [{Model}] {Tokens} tokens in {Latency}ms",
                model, usage.TotalTokens, sw.ElapsedMilliseconds);

            return LLMResponse.Ok(content, model, Name, usage);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "❌ M.E.AI [{Model}] failed after {Latency}ms", model, sw.ElapsedMilliseconds);
            return LLMResponse.Fail(ex.Message, Name);
        }
    }

    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        if (!_enabled) return false;

        try
        {
            // Simple ping: send a minimal message to verify connectivity
            var messages = new List<Microsoft.Extensions.AI.ChatMessage>
            {
                new(ChatRole.User, "ping")
            };
            var options = new ChatOptions { MaxOutputTokens = 1 };
            await _chatClient.GetResponseAsync(messages, options, ct);
            return true;
        }
        catch
        {
            return false;
        }
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
