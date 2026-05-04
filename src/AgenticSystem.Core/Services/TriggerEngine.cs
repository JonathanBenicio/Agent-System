using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Services;

/// <summary>
/// ML21 — Motor de avaliação de regras condicionais.
/// Consulta fonte (HTTP), avalia condição (JSONPath/Threshold/Regex) e dispara ação via delivery channels.
/// </summary>
public class TriggerEngine : ITriggerEngine
{
    private readonly IScheduledTaskStore _store;
    private readonly IEnumerable<IDeliveryChannel> _channels;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<TriggerEngine> _logger;

    public TriggerEngine(
        IScheduledTaskStore store,
        IEnumerable<IDeliveryChannel> channels,
        IHttpClientFactory httpClientFactory,
        ILogger<TriggerEngine> logger)
    {
        _store = store;
        _channels = channels;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task RegisterRuleAsync(TriggerRule rule, CancellationToken ct = default)
    {
        await _store.SaveRuleAsync(rule, ct);
        _logger.LogInformation("📋 Regra registrada: {Name} [{Id}]", rule.Name, rule.Id);
    }

    public async Task<TriggerRule?> GetRuleAsync(string ruleId, CancellationToken ct = default)
    {
        return await _store.GetRuleAsync(ruleId, ct);
    }

    public async Task<IReadOnlyList<TriggerRule>> GetAllRulesAsync(CancellationToken ct = default)
    {
        return await _store.GetAllRulesAsync(ct);
    }

    public async Task RemoveRuleAsync(string ruleId, CancellationToken ct = default)
    {
        await _store.DeleteRuleAsync(ruleId, ct);
        _logger.LogInformation("🗑️ Regra removida: {RuleId}", ruleId);
    }

    public async Task EnableRuleAsync(string ruleId, CancellationToken ct = default)
    {
        var rule = await _store.GetRuleAsync(ruleId, ct)
            ?? throw new InvalidOperationException($"Rule {ruleId} not found");
        rule.Enabled = true;
        await _store.SaveRuleAsync(rule, ct);
    }

    public async Task DisableRuleAsync(string ruleId, CancellationToken ct = default)
    {
        var rule = await _store.GetRuleAsync(ruleId, ct)
            ?? throw new InvalidOperationException($"Rule {ruleId} not found");
        rule.Enabled = false;
        await _store.SaveRuleAsync(rule, ct);
    }

    public async Task<TriggerEvaluationResult> EvaluateAsync(TriggerRule rule, CancellationToken ct = default)
    {
        var result = new TriggerEvaluationResult
        {
            RuleId = rule.Id,
            RuleName = rule.Name,
            ExpectedValue = rule.Condition.ExpectedValue
        };

        try
        {
            if (!rule.Enabled)
            {
                result.ConditionMet = false;
                return result;
            }

            // 1. Fetch source data
            var sourceResponse = await FetchSourceAsync(rule.Source, ct);

            // 2. Evaluate condition
            result.ActualValue = sourceResponse;
            result.ConditionMet = EvaluateCondition(rule.Condition, sourceResponse);
            result.EvaluatedAt = DateTime.UtcNow;

            // 3. If condition met → deliver notifications
            if (result.ConditionMet)
            {
                rule.LastTriggeredAt = DateTime.UtcNow;
                rule.ExecutionCount++;
                await _store.SaveRuleAsync(rule, ct);

                var payload = new TriggerNotificationPayload
                {
                    TriggerName = rule.Name,
                    Timestamp = DateTime.UtcNow,
                    ConditionResult = $"{rule.Condition.Type}: {result.ActualValue} (expected: {result.ExpectedValue})",
                    SuggestedAction = rule.Action.Description,
                    ActualValue = result.ActualValue,
                    ExpectedValue = result.ExpectedValue
                };

                await DeliverToChannelsAsync(payload, rule.DeliveryChannels, rule.Action.Parameters, ct);

                _logger.LogInformation("🔔 Trigger disparado: {RuleName} — condição satisfeita", rule.Name);
            }
            else
            {
                _logger.LogDebug("⏭️ Trigger não disparado: {RuleName} — condição não satisfeita", rule.Name);
            }
        }
        catch (Exception ex)
        {
            result.ErrorMessage = ex.Message;
            result.ConditionMet = false;
            _logger.LogError(ex, "❌ Erro ao avaliar trigger: {RuleName}", rule.Name);
        }

        return result;
    }

    public async Task<IReadOnlyList<TriggerEvaluationResult>> EvaluateAllAsync(CancellationToken ct = default)
    {
        var rules = await _store.GetAllRulesAsync(ct);
        var results = new List<TriggerEvaluationResult>();

        foreach (var rule in rules.Where(r => r.Enabled))
        {
            var result = await EvaluateAsync(rule, ct);
            results.Add(result);
        }

        return results;
    }

    private async Task<string> FetchSourceAsync(TriggerSource source, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient("TriggerEngine");

        var request = new HttpRequestMessage(
            source.Type == TriggerSourceType.HttpPost ? HttpMethod.Post : HttpMethod.Get,
            source.Endpoint);

        foreach (var header in source.Headers)
        {
            request.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        if (source.Body != null && source.Type == TriggerSourceType.HttpPost)
        {
            request.Content = new StringContent(source.Body, System.Text.Encoding.UTF8, "application/json");
        }

        var response = await client.SendAsync(request, ct);

        // For StatusCode condition type, return the status code
        if (source.Type == TriggerSourceType.HealthCheck)
        {
            return ((int)response.StatusCode).ToString();
        }

        return await response.Content.ReadAsStringAsync(ct);
    }

    private static bool EvaluateCondition(TriggerCondition condition, string sourceResponse)
    {
        return condition.Type switch
        {
            ConditionType.StatusCode => sourceResponse == condition.Expression,
            ConditionType.Contains => sourceResponse.Contains(condition.Expression, StringComparison.OrdinalIgnoreCase),
            ConditionType.Regex => Regex.IsMatch(sourceResponse, condition.Expression),
            ConditionType.Threshold => EvaluateThreshold(condition, sourceResponse),
            ConditionType.JsonPath => EvaluateJsonPath(condition, sourceResponse),
            _ => false
        };
    }

    private static bool EvaluateThreshold(TriggerCondition condition, string sourceResponse)
    {
        // Expression format: ">90", "<50", ">=100", "<=10", "==42"
        if (string.IsNullOrEmpty(condition.Expression) || sourceResponse == null)
            return false;

        if (!double.TryParse(ExtractNumericValue(sourceResponse), out var actual))
            return false;

        var expression = condition.Expression.Trim();

        if (expression.StartsWith(">=") && double.TryParse(expression[2..], out var gte))
            return actual >= gte;
        if (expression.StartsWith("<=") && double.TryParse(expression[2..], out var lte))
            return actual <= lte;
        if (expression.StartsWith("==") && double.TryParse(expression[2..], out var eq))
            return Math.Abs(actual - eq) < 0.001;
        if (expression.StartsWith(">") && double.TryParse(expression[1..], out var gt))
            return actual > gt;
        if (expression.StartsWith("<") && double.TryParse(expression[1..], out var lt))
            return actual < lt;

        return false;
    }

    private static bool EvaluateJsonPath(TriggerCondition condition, string sourceResponse)
    {
        // Simplified JSONPath: supports "$.property" or "$.nested.property"
        try
        {
            using var doc = JsonDocument.Parse(sourceResponse);
            var path = condition.Expression.TrimStart('$', '.');
            var parts = path.Split('.');

            JsonElement current = doc.RootElement;
            foreach (var part in parts)
            {
                if (!current.TryGetProperty(part, out current))
                    return false;
            }

            var actualValue = current.ToString();
            return condition.ExpectedValue != null && actualValue == condition.ExpectedValue;
        }
        catch
        {
            return false;
        }
    }

    private static string? ExtractNumericValue(string input)
    {
        // Try parsing as pure number first
        if (double.TryParse(input, out _))
            return input;

        // Try extracting from JSON
        try
        {
            using var doc = JsonDocument.Parse(input);
            if (doc.RootElement.ValueKind == JsonValueKind.Number)
                return doc.RootElement.GetDouble().ToString();
        }
        catch { }

        // Extract first number from string
        var match = Regex.Match(input, @"-?\d+\.?\d*");
        return match.Success ? match.Value : null;
    }

    private async Task DeliverToChannelsAsync(
        TriggerNotificationPayload payload,
        string[] channelNames,
        Dictionary<string, string> config,
        CancellationToken ct)
    {
        foreach (var channelName in channelNames)
        {
            var channel = _channels.FirstOrDefault(c =>
                c.ChannelName.Equals(channelName, StringComparison.OrdinalIgnoreCase));

            if (channel == null)
            {
                _logger.LogWarning("⚠️ Canal de entrega não encontrado: {Channel}", channelName);
                continue;
            }

            var result = await channel.SendAsync(payload, config, ct);
            if (result.Status == DeliveryStatus.Failed)
            {
                _logger.LogWarning("⚠️ Entrega falhou no canal {Channel}: {Error}",
                    channelName, result.ErrorMessage);
            }
        }
    }
}
