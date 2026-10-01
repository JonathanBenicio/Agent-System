using System.Text;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.Exceptions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Infrastructure.LLM;

/// <summary>
/// Checks the persisted tenant ceiling before a provider call and records the
/// provider's usage after a successful response.
/// </summary>
internal sealed class TenantQuotaChatClient : IChatClient
{
    private readonly IChatClient _inner;
    private readonly string _provider;
    private readonly string _modelId;
    private readonly ILLMRuntimeContextAccessor _runtimeContextAccessor;
    private readonly IQuotaEnforcer _quotaEnforcer;
    private readonly ITokenAuditService _tokenAuditService;
    private readonly ILogger _logger;

    public TenantQuotaChatClient(
        IChatClient inner,
        string provider,
        string modelId,
        ILLMRuntimeContextAccessor runtimeContextAccessor,
        IQuotaEnforcer quotaEnforcer,
        ITokenAuditService tokenAuditService,
        ILogger logger)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _provider = provider;
        _modelId = modelId;
        _runtimeContextAccessor = runtimeContextAccessor;
        _quotaEnforcer = quotaEnforcer;
        _tokenAuditService = tokenAuditService;
        _logger = logger;
    }

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var messageList = messages as IList<ChatMessage> ?? messages.ToList();
        var tenantId = GetTenantId();
        if (tenantId is null)
            return await _inner.GetResponseAsync(messageList, options, cancellationToken);

        var estimatedInputTokens = EstimateTokens(messageList.Select(message => message.Text));
        var estimatedOutputTokens = GetOutputTokenEstimate(options);
        await EnsureQuotaAsync(tenantId, estimatedInputTokens, estimatedOutputTokens, cancellationToken);

        var response = await _inner.GetResponseAsync(messageList, options, cancellationToken);
        await RecordUsageAsync(
            tenantId,
            estimatedInputTokens,
            EstimateTokens([response.Text]),
            response.Usage,
            cancellationToken);
        return response;
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var messageList = messages as IList<ChatMessage> ?? messages.ToList();
        var tenantId = GetTenantId();
        if (tenantId is null)
        {
            await foreach (var update in _inner.GetStreamingResponseAsync(messageList, options, cancellationToken))
                yield return update;
            yield break;
        }

        var estimatedInputTokens = EstimateTokens(messageList.Select(message => message.Text));
        var estimatedOutputTokens = GetOutputTokenEstimate(options);
        await EnsureQuotaAsync(tenantId, estimatedInputTokens, estimatedOutputTokens, cancellationToken);

        UsageDetails? usage = null;
        var outputText = new StringBuilder();
        await foreach (var update in _inner.GetStreamingResponseAsync(messageList, options, cancellationToken))
        {
            foreach (var usageContent in update.Contents.OfType<UsageContent>())
                usage = usageContent.Details;
            outputText.Append(update.Text);
            yield return update;
        }

        await RecordUsageAsync(
            tenantId,
            estimatedInputTokens,
            EstimateTokens([outputText.ToString()]),
            usage,
            cancellationToken);
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        return serviceType == typeof(IChatClient) ? this : _inner.GetService(serviceType, serviceKey);
    }

    public void Dispose()
    {
        // The wrapped provider client is owned by LLMManager, not by this per-call decorator.
    }

    private string? GetTenantId()
    {
        var tenantId = _runtimeContextAccessor.Current?.TenantId;
        return string.IsNullOrWhiteSpace(tenantId) ? null : tenantId;
    }

    private async Task EnsureQuotaAsync(
        string tenantId,
        int estimatedInputTokens,
        int estimatedOutputTokens,
        CancellationToken ct)
    {
        var estimatedCost = await _tokenAuditService.CalculateCostAsync(
            _provider, _modelId, estimatedInputTokens, estimatedOutputTokens, ct: ct);
        var check = await _quotaEnforcer.CheckQuotaAsync(
            tenantId,
            estimatedInputTokens + estimatedOutputTokens,
            (double)estimatedCost,
            ct);
        if (!check.Allowed)
            throw new QuotaExceededException(check.DenialReason ?? "The request exceeds the tenant's configured usage limit.");
    }

    private async Task RecordUsageAsync(
        string tenantId,
        int estimatedInputTokens,
        int estimatedOutputTokens,
        UsageDetails? usage,
        CancellationToken ct)
    {
        var inputTokens = ReadTokenCount(usage?.InputTokenCount, estimatedInputTokens);
        var outputTokens = ReadTokenCount(usage?.OutputTokenCount, estimatedOutputTokens);
        var cachedTokens = ReadTokenCount(usage?.CachedInputTokenCount, 0);
        var totalTokens = checked(inputTokens + outputTokens);
        var cost = await _tokenAuditService.CalculateCostAsync(
            _provider, _modelId, inputTokens, outputTokens, cachedTokens, ct);

        await _quotaEnforcer.RecordUsageAsync(tenantId, totalTokens, (double)cost, ct);

        var context = _runtimeContextAccessor.Current;
        if (context is null)
            return;

        try
        {
            await _tokenAuditService.RecordTokenUsageAsync(new TokenUsageRecord
            {
                SessionId = context.SessionId ?? string.Empty,
                TenantId = tenantId,
                AgentName = string.Empty,
                Provider = _provider,
                ModelId = _modelId,
                PromptTokens = inputTokens,
                CompletionTokens = outputTokens,
                CachedTokens = cachedTokens,
                CalculatedCost = cost,
                Timestamp = DateTime.UtcNow
            }, ct);
        }
        catch (Exception ex)
        {
            // Quota totals are the enforcement source of truth; secondary FinOps
            // audit failure must be visible without losing the successful chat.
            _logger.LogError(ex, "Failed to persist token audit for tenant {TenantId}", tenantId);
        }
    }

    private static int GetOutputTokenEstimate(ChatOptions? options) =>
        options?.MaxOutputTokens is > 0 and <= int.MaxValue ? options.MaxOutputTokens.Value : 2000;

    private static int EstimateTokens(IEnumerable<string?> text) =>
        Math.Max(0, (text.Where(value => !string.IsNullOrEmpty(value)).Sum(value => value!.Length) + 3) / 4);

    private static int ReadTokenCount<T>(T? value, int fallback) where T : struct, IConvertible
    {
        if (!value.HasValue)
            return fallback;

        var count = value.Value.ToInt64(System.Globalization.CultureInfo.InvariantCulture);
        return (int)Math.Clamp(count, 0, int.MaxValue);
    }
}
