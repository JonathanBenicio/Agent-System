using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using FrameworkAgentResponse = Microsoft.Agents.AI.AgentResponse;

namespace AgenticSystem.Infrastructure.AgentFramework;

/// <summary>
/// Middleware do Agent Framework que executa reflexão pós-ação via IReflectionEngine.
/// Intercepta RunCoreAsync, delega ao inner agent e avalia qualidade da resposta.
/// Registrado no pipeline via .UseReflection() extension method.
/// </summary>
public class ReflectionDelegatingAgent : DelegatingAIAgent
{
    private readonly IReflectionEngine _reflectionEngine;
    private readonly ILogger _logger;

    public ReflectionDelegatingAgent(
        AIAgent innerAgent,
        IReflectionEngine reflectionEngine,
        ILogger logger)
        : base(innerAgent)
    {
        _reflectionEngine = reflectionEngine ?? throw new ArgumentNullException(nameof(reflectionEngine));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task<FrameworkAgentResponse> RunCoreAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session,
        AgentRunOptions? options,
        CancellationToken cancellationToken)
    {
        var response = await base.RunCoreAsync(messages, session, options, cancellationToken);

        try
        {
            var responseText = ExtractResponseText(response);
            var userInput = ExtractLastUserMessage(messages);
            var agentName = InnerAgent.Name ?? "Unknown";
            var sessionId = session?.GetHashCode().ToString() ?? "unknown";

            // Heurística de confiança baseada no conteúdo da resposta
            var confidence = EstimateConfidence(responseText);

            var reflection = await _reflectionEngine.ReflectAsync(
                sessionId, agentName, userInput, responseText, confidence);

            if (reflection.Severity >= ReflectionSeverity.Warning)
            {
                _logger.LogWarning(
                    "Reflection middleware flagged response: agent={Agent}, confidence={Confidence:F2}, severity={Severity}",
                    agentName, confidence, reflection.Severity);
            }
        }
        catch (Exception ex)
        {
            // Middleware não deve impedir a resposta — log e continua
            _logger.LogWarning(ex, "Reflection middleware error — response returned without reflection");
        }

        return response;
    }

    private static string ExtractResponseText(FrameworkAgentResponse response)
    {
        var text = string.Join("\n", response.Messages
            .Where(m => m.Role == ChatRole.Assistant)
            .SelectMany(m => m.Contents.OfType<TextContent>())
            .Select(t => t.Text));

        if (string.IsNullOrWhiteSpace(text))
        {
            text = string.Join("\n", response.Messages
                .Where(m => m.Role == ChatRole.Assistant)
                .Select(m => m.Text));
        }

        return text ?? string.Empty;
    }

    private static string ExtractLastUserMessage(IEnumerable<ChatMessage> messages)
    {
        return messages
            .LastOrDefault(m => m.Role == ChatRole.User)
            ?.Text ?? string.Empty;
    }

    private static double EstimateConfidence(string responseText)
    {
        if (string.IsNullOrWhiteSpace(responseText))
            return 0.1;

        if (responseText.Length < 20)
            return 0.4;

        // Indicadores de baixa confiança
        var lowConfidenceIndicators = new[]
        {
            "não tenho certeza", "não sei", "não posso", "desculpe",
            "i'm not sure", "i don't know", "i cannot"
        };

        var lower = responseText.ToLowerInvariant();
        if (lowConfidenceIndicators.Any(i => lower.Contains(i)))
            return 0.5;

        return 0.85;
    }
}
