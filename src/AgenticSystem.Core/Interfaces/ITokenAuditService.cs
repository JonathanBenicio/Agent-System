using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Interfaces;

/// <summary>
/// Serviço de auditoria de consumo de tokens e gestão de FinOps para IA.
/// Suporta rastreamento 100%, cálculo baseado em Effective Dates e publicação SignalR.
/// </summary>
public interface ITokenAuditService
{
    Task RecordTokenUsageAsync(TokenUsageRecord record, CancellationToken ct = default);
    Task<decimal> CalculateCostAsync(string provider, string modelId, int promptTokens, int completionTokens, int cachedTokens = 0, CancellationToken ct = default);
    Task PublishTurnCostSummaryAsync(string sessionId, string tenantId, CancellationToken ct = default);
    Task<IEnumerable<LlmPricingRule>> GetPricingRulesAsync(string? provider = null, CancellationToken ct = default);
    Task SetPricingRuleAsync(LlmPricingRule rule, CancellationToken ct = default);
}
