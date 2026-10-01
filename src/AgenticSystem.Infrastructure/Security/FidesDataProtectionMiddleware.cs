using System.Text;
using AgenticSystem.Core.Interfaces;
using System.Text.RegularExpressions;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.Configuration;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using FrameworkAgentResponse = Microsoft.Agents.AI.AgentResponse;

namespace AgenticSystem.Infrastructure.Security;

public sealed class FidesDataProtectionMiddleware : DelegatingAIAgent
{
    private const string BlockedMessage = "Mensagem bloqueada pelo FIDES: não foi possível verificar ou redigir com segurança todos os dados sensíveis. Envie uma versão textual ou uma cópia da mídia já redigida.";

    private readonly ITenantContextAccessor _tenantContext;
    private readonly IFidesTenantPolicyStore _policyStore;
    private readonly IFidesMediaScanner _mediaScanner;
    private readonly ILogger<FidesDataProtectionMiddleware> _logger;
    private readonly TimeSpan _mediaScanTimeout;

    public FidesDataProtectionMiddleware(
        AIAgent innerAgent,
        ITenantContextAccessor tenantContext,
        IFidesTenantPolicyStore policyStore,
        IFidesMediaScanner mediaScanner,
        IOptions<FidesSecuritySettings> settings,
        ILogger<FidesDataProtectionMiddleware> logger)
        : base(innerAgent)
    {
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _policyStore = policyStore ?? throw new ArgumentNullException(nameof(policyStore));
        _mediaScanner = mediaScanner ?? throw new ArgumentNullException(nameof(mediaScanner));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _mediaScanTimeout = TimeSpan.FromSeconds(Math.Clamp(settings.Value.MediaScanTimeoutSeconds, 1, 60));
    }

    protected override async Task<FrameworkAgentResponse> RunCoreAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session,
        AgentRunOptions? options,
        CancellationToken cancellationToken)
    {
        string tenantId;
        FidesTenantPolicy policy;
        try
        {
            tenantId = _tenantContext.CurrentTenantId;
            policy = await _policyStore.GetAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            _logger.LogError("FIDES policy could not be loaded; blocking the provider call.");
            return BlockedResponse();
        }

        var transformed = await ProtectMessagesAsync(messages, policy, tenantId, cancellationToken);
        if (transformed.Blocked)
            return BlockedResponse();

        if (transformed.DetectedCategories.Count > 0)
        {
            _logger.LogInformation(
                "FIDES redacted categories {Categories} for tenant {TenantId}.",
                string.Join(",", transformed.DetectedCategories),
                tenantId);
        }

        return await base.RunCoreAsync(transformed.Messages, session, options, cancellationToken);
    }

    private async Task<ProtectedMessagesResult> ProtectMessagesAsync(
        IEnumerable<ChatMessage> messages,
        FidesTenantPolicy policy,
        string tenantId,
        CancellationToken ct)
    {
        var protectedMessages = new List<ChatMessage>();
        var detectedCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var message in messages)
        {
            if (message.Role != ChatRole.User && message.Role != ChatRole.System)
            {
                protectedMessages.Add(message);
                continue;
            }

            var protectedMessage = new ChatMessage(message.Role, string.Empty);
            foreach (var content in message.Contents)
            {
                if (content is TextContent textContent)
                {
                    try
                    {
                        protectedMessage.Contents.Add(new TextContent(
                            ApplyBuiltInMasks(textContent.Text, policy, detectedCategories)));
                    }
                    catch (RegexMatchTimeoutException)
                    {
                        _logger.LogError("FIDES detector exceeded its timeout for tenant {TenantId}; blocking the provider call.", tenantId);
                        return ProtectedMessagesResult.BlockedResult;
                    }
                }
                else if (content is DataContent dataContent)
                {
                    var replacement = await ProtectDataContentAsync(dataContent, policy, detectedCategories, tenantId, ct);
                    if (replacement is null)
                        return ProtectedMessagesResult.BlockedResult;
                    protectedMessage.Contents.Add(replacement);
                }
                else
                {
                    // URI and other non-text payloads cannot be inspected without safe local bytes.
                    _logger.LogWarning("FIDES received uninspectable content for tenant {TenantId}; blocking the provider call.", tenantId);
                    return ProtectedMessagesResult.BlockedResult;
                }
            }

            protectedMessages.Add(protectedMessage);
        }

        return new ProtectedMessagesResult(protectedMessages, detectedCategories, false);
    }

    private async Task<AIContent?> ProtectDataContentAsync(
        DataContent dataContent,
        FidesTenantPolicy policy,
        HashSet<string> detectedCategories,
        string tenantId,
        CancellationToken ct)
    {
        var mediaType = dataContent.MediaType ?? string.Empty;
        if (mediaType.StartsWith("text/", StringComparison.OrdinalIgnoreCase) ||
            mediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var text = Encoding.UTF8.GetString(dataContent.Data.Span);
                return new DataContent(
                    Encoding.UTF8.GetBytes(ApplyBuiltInMasks(text, policy, detectedCategories)),
                    mediaType);
            }
            catch (RegexMatchTimeoutException)
            {
                _logger.LogError("FIDES text detector exceeded its timeout for tenant {TenantId}; blocking the provider call.", tenantId);
                return null;
            }
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(_mediaScanTimeout);
        FidesMediaScanResult scan;
        try
        {
            scan = await _mediaScanner
                .ScanAndRedactAsync(dataContent.Data, mediaType, policy, timeout.Token)
                .WaitAsync(_mediaScanTimeout, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            _logger.LogError("FIDES media scan failed for tenant {TenantId}; blocking the provider call.", tenantId);
            return null;
        }

        foreach (var category in scan.DetectedCategories)
            detectedCategories.Add(category);

        return scan.Status switch
        {
            FidesMediaScanStatus.NoSensitiveContent
                when scan.SanitizedContent is { Length: > 0 } && !string.IsNullOrWhiteSpace(scan.SanitizedMediaType)
                => new DataContent(scan.SanitizedContent, scan.SanitizedMediaType),
            FidesMediaScanStatus.NoSensitiveContent => dataContent,
            FidesMediaScanStatus.SensitiveContentRedacted
                when scan.RedactedContent is { Length: > 0 } && !string.IsNullOrWhiteSpace(scan.RedactedMediaType)
                => new DataContent(scan.RedactedContent, scan.RedactedMediaType),
            _ => BlockMedia(tenantId)
        };
    }

    private string ApplyBuiltInMasks(
        string input,
        FidesTenantPolicy policy,
        HashSet<string> detectedCategories)
    {
        if (string.IsNullOrWhiteSpace(input))
            return input;

        var result = input;
        foreach (var detector in FidesBuiltInRules.All)
        {
            if (!FidesBuiltInRules.IsEnabled(detector.Name, policy) || !detector.Pattern.IsMatch(result))
                continue;

            detectedCategories.Add(detector.Name);
            result = detector.Pattern.Replace(result, detector.Mask);
        }

        return result;
    }

    private AIContent? BlockMedia(string tenantId)
    {
        _logger.LogWarning("FIDES could not confidently redact sensitive media for tenant {TenantId}; blocking the provider call.", tenantId);
        return null;
    }

    private static FrameworkAgentResponse BlockedResponse() =>
        new(new ChatMessage(ChatRole.Assistant, BlockedMessage));

    private sealed record ProtectedMessagesResult(
        IEnumerable<ChatMessage> Messages,
        IReadOnlyCollection<string> DetectedCategories,
        bool Blocked)
    {
        public static ProtectedMessagesResult BlockedResult { get; } = new([], [], true);
    }
}
