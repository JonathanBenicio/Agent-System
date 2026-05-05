using System.Runtime.CompilerServices;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Infrastructure.AgentFramework;

/// <summary>
/// IChatClient que delega para o pipeline completo do orquestrador (IFrameworkOrchestratorService),
/// garantindo que requisições via A2A/AG-UI tenham acesso a tools, RAG, especialistas e middleware.
///
/// Registrado como keyed IChatClient e passado ao AddAIAgent via chatClientServiceKey,
/// substituindo o comportamento "shallow" do agent de protocolo.
/// </summary>
public sealed class ProtocolOrchestratorChatClient : IChatClient
{
    private readonly IFrameworkOrchestratorService _orchestrator;
    private readonly ILogger<ProtocolOrchestratorChatClient> _logger;
    private bool _disposed;

    public ProtocolOrchestratorChatClient(
        IFrameworkOrchestratorService orchestrator,
        ILogger<ProtocolOrchestratorChatClient> logger)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);

        var messageList = messages as IList<ChatMessage> ?? messages.ToList();

        var lastUserMessage = messageList.LastOrDefault(m => m.Role == ChatRole.User);
        var input = lastUserMessage?.Text ?? string.Empty;

        if (string.IsNullOrWhiteSpace(input))
        {
            return new ChatResponse(new ChatMessage(ChatRole.Assistant,
                "No user message provided."));
        }

        var sessionId = DeriveSessionId(options);

        _logger.LogInformation(
            "🔌 Protocol request → full orchestrator pipeline. SessionId: {SessionId}, Input: {Input}",
            sessionId, input[..Math.Min(50, input.Length)]);

        var context = new UserContext
        {
            UserId = "protocol-client",
            TenantId = Tenant.DefaultTenantId
        };

        var response = await _orchestrator.ExecuteAsync(sessionId, input, context, cancellationToken);

        var chatMessage = new ChatMessage(ChatRole.Assistant, response.Content);
        var chatResponse = new ChatResponse(chatMessage);

        if (response.Metadata.Count > 0)
        {
            chatResponse.AdditionalProperties ??= new AdditionalPropertiesDictionary();
            foreach (var kvp in response.Metadata)
            {
                chatResponse.AdditionalProperties[kvp.Key] = kvp.Value;
            }
        }

        _logger.LogInformation(
            "✅ Protocol response from {Agent}, success: {Success}",
            response.AgentName, response.Success);

        return chatResponse;
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // O orquestrador não suporta streaming nativo no momento.
        // Fallback para resposta completa emitida como chunk único.
        var response = await GetResponseAsync(messages, options, cancellationToken);
        var text = response.Text ?? string.Empty;

        if (string.IsNullOrEmpty(text))
            yield break;

        yield return new ChatResponseUpdate(ChatRole.Assistant, text);
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        ArgumentNullException.ThrowIfNull(serviceType);

        if (serviceType == typeof(IChatClient))
            return this;

        return null;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
    }

    /// <summary>
    /// Deriva um session ID estável a partir do contexto da conversação.
    /// A2A tasks e AG-UI conversations fornecem IDs via metadata;
    /// fallback para GUID único por request.
    /// </summary>
    private static string DeriveSessionId(ChatOptions? options)
    {
        // Tentar extrair conversation/task ID dos metadados do protocolo
        if (options?.AdditionalProperties is { } props)
        {
            if (props.TryGetValue("conversationId", out var convId) && convId is string cid && !string.IsNullOrWhiteSpace(cid))
                return $"protocol-{cid}";

            if (props.TryGetValue("taskId", out var taskId) && taskId is string tid && !string.IsNullOrWhiteSpace(tid))
                return $"protocol-{tid}";
        }

        // Fallback: sessão única por request
        return $"protocol-{Guid.NewGuid():N}";
    }
}
