using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using AgenticSystem.Core.Interfaces;
using FrameworkAgentResponse = Microsoft.Agents.AI.AgentResponse;

namespace AgenticSystem.Infrastructure.AgentFramework;

/// <summary>
/// Middleware do Agent Framework que executa quality gates pré e pós-execução.
/// Intercepta RunCoreAsync para validar input (pre) e output (post) via IQualityGateService.
/// Registrado no pipeline via .UseQualityGates() extension method.
/// </summary>
public class QualityGateDelegatingAgent : DelegatingAIAgent
{
    private readonly IQualityGateService _qualityGateService;
    private readonly ILogger _logger;

    public QualityGateDelegatingAgent(
        AIAgent innerAgent,
        IQualityGateService qualityGateService,
        ILogger logger)
        : base(innerAgent)
    {
        _qualityGateService = qualityGateService ?? throw new ArgumentNullException(nameof(qualityGateService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task<FrameworkAgentResponse> RunCoreAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session,
        AgentRunOptions? options,
        CancellationToken cancellationToken)
    {
        var userInput = ExtractLastUserMessage(messages);

        // Pre-execution quality gate
        try
        {
            var preReport = await _qualityGateService.ValidateRequestAsync(
                userInput, metadata: null, ct: cancellationToken);

            if (!preReport.OverallPassed)
            {
                _logger.LogWarning(
                    "Quality gate pre-execution FAILED: score={Score:F1}, issues={Issues}",
                    preReport.AverageScore,
                    string.Join("; ", preReport.Results.SelectMany(r => r.Issues)));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Quality gate pre-execution check error — proceeding with execution");
        }

        // Delegate to inner agent
        var response = await base.RunCoreAsync(messages, session, options, cancellationToken);

        // Post-execution quality gate
        try
        {
            var responseText = ExtractResponseText(response);
            var postReport = await _qualityGateService.ValidateResponseAsync(
                userInput, responseText, metadata: null, ct: cancellationToken);

            if (!postReport.OverallPassed)
            {
                _logger.LogWarning(
                    "Quality gate post-execution FAILED: score={Score:F1}, issues={Issues}",
                    postReport.AverageScore,
                    string.Join("; ", postReport.Results.SelectMany(r => r.Issues)));
            }
            else
            {
                _logger.LogDebug(
                    "Quality gate post-execution PASSED: score={Score:F1}",
                    postReport.AverageScore);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Quality gate post-execution check error — response returned as-is");
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
}
