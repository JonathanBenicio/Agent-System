using System.Runtime.CompilerServices;
using System.Text;
using AgenticSystem.Core.Interfaces;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Infrastructure.AI;

/// <summary>
/// Intercepta requisições ao LLM para fornecer Semantic Caching.
/// Ignora cache para requisições com tools ativadas.
/// </summary>
public class SemanticCacheChatClient : DelegatingChatClient
{
    private readonly ISemanticCacheService _cacheService;
    private readonly ILogger<SemanticCacheChatClient> _logger;
    private readonly string _agentName;
    private readonly double _threshold;

    public SemanticCacheChatClient(
        IChatClient innerClient,
        ISemanticCacheService cacheService,
        string agentName,
        double similarityThreshold,
        ILogger<SemanticCacheChatClient> logger) : base(innerClient)
    {
        _cacheService = cacheService;
        _agentName = agentName;
        _threshold = similarityThreshold;
        _logger = logger;
    }

    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> chatMessages, 
        ChatOptions? options = null, 
        CancellationToken cancellationToken = default)
    {
        var messagesList = chatMessages as IList<ChatMessage> ?? chatMessages.ToList();

        // Bypass cache if there are tools/functions involved (we shouldn't cache dynamic behavior)
        bool hasTools = options?.Tools != null && options.Tools.Any();
        
        string prompt = ExtractPrompt(messagesList);
        
        if (!hasTools && !string.IsNullOrWhiteSpace(prompt))
        {
            var cacheResult = await _cacheService.GetCachedResponseAsync(prompt, _agentName, _threshold, cancellationToken);
            if (cacheResult.IsHit && !string.IsNullOrWhiteSpace(cacheResult.CachedResponse))
            {
                var message = new ChatMessage(ChatRole.Assistant, cacheResult.CachedResponse);
                return new ChatResponse(new[] { message });
            }
        }

        // Cache miss or bypassed
        var response = await base.GetResponseAsync(messagesList, options, cancellationToken);
        
        // Save to cache if conditions are met
        if (!hasTools && !string.IsNullOrWhiteSpace(prompt) && response.Text != null)
        {
            await _cacheService.SetCachedResponseAsync(prompt, response.Text, _agentName, TimeSpan.FromDays(1), cancellationToken);
        }

        return response;
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> chatMessages, 
        ChatOptions? options = null, 
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var messagesList = chatMessages as IList<ChatMessage> ?? chatMessages.ToList();
        bool hasTools = options?.Tools != null && options.Tools.Any();
        string prompt = ExtractPrompt(messagesList);

        if (!hasTools && !string.IsNullOrWhiteSpace(prompt))
        {
            var cacheResult = await _cacheService.GetCachedResponseAsync(prompt, _agentName, _threshold, cancellationToken);
            if (cacheResult.IsHit && !string.IsNullOrWhiteSpace(cacheResult.CachedResponse))
            {
                // Simulate streaming by yielding chunks
                var chunks = cacheResult.CachedResponse.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                foreach (var chunk in chunks)
                {
                    yield return new ChatResponseUpdate(ChatRole.Assistant, chunk + " ");
                }
                yield break;
            }
        }

        // Cache miss or bypassed
        var sb = new StringBuilder();
        await foreach (var update in base.GetStreamingResponseAsync(messagesList, options, cancellationToken))
        {
            if (update.Text != null)
            {
                sb.Append(update.Text);
            }
            yield return update;
        }

        // Save complete streamed response to cache
        if (!hasTools && !string.IsNullOrWhiteSpace(prompt) && sb.Length > 0)
        {
            await _cacheService.SetCachedResponseAsync(prompt, sb.ToString(), _agentName, TimeSpan.FromDays(1), cancellationToken);
        }
    }

    private static string ExtractPrompt(IEnumerable<ChatMessage> chatMessages)
    {
        var sb = new StringBuilder();
        foreach (var msg in chatMessages)
        {
            if (msg.Role == ChatRole.System || msg.Role == ChatRole.User)
            {
                sb.Append(msg.Role.Value);
                sb.Append(": ");
                sb.Append(msg.Text);
                sb.Append("\n");
            }
        }
        return sb.ToString().Trim();
    }
}
