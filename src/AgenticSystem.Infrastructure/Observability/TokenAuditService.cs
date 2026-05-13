using System.Diagnostics;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.Persistence;
using AgenticSystem.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Infrastructure.Observability;

public class TokenAuditService : ITokenAuditService
{
    private readonly IDbContextFactory<AgenticDbContext> _dbContextFactory;
    private readonly IEventBus _eventBus;
    private readonly ILogger<TokenAuditService> _logger;
    private static readonly ActivitySource TokenActivitySource = new("AgenticSystem.Gateway");

    public TokenAuditService(
        IDbContextFactory<AgenticDbContext> dbContextFactory,
        IEventBus eventBus,
        ILogger<TokenAuditService> logger)
    {
        _dbContextFactory = dbContextFactory;
        _eventBus = eventBus;
        _logger = logger;
    }

    public async Task RecordTokenUsageAsync(TokenUsageRecord record, CancellationToken ct = default)
    {
        using var activity = TokenActivitySource.StartActivity("RecordTokenUsage");
        activity?.SetTag("ai.provider", record.Provider);
        activity?.SetTag("ai.model", record.ModelId);
        activity?.SetTag("ai.tokens.prompt", record.PromptTokens);
        activity?.SetTag("ai.tokens.completion", record.CompletionTokens);
        activity?.SetTag("ai.tokens.cached", record.CachedTokens);
        activity?.SetTag("ai.cost", record.CalculatedCost);

        try
        {
            using var db = await _dbContextFactory.CreateDbContextAsync(ct);

            // Record into CostEntries
            var entry = new CostEntryEntity
            {
                ServiceName = $"{record.Provider}:{record.ModelId}",
                Category = "LLM_Inference",
                TenantId = record.TenantId,
                Cost = record.CalculatedCost,
                RecordedAt = record.Timestamp
            };

            db.CostEntries.Add(entry);

            // Record audit entry
            var audit = new AuditEntryEntity
            {
                Id = Guid.NewGuid().ToString("N"),
                Timestamp = record.Timestamp,
                Category = "AI_Inference",
                Action = "TokenConsumption",
                TenantId = record.TenantId,
                SessionId = record.SessionId,
                AgentName = record.AgentName,
                ModelUsed = record.ModelId,
                Cost = record.CalculatedCost,
                TraceId = activity?.TraceId.ToString(),
                Description = $"Consumed P:{record.PromptTokens}, C:{record.CompletionTokens}, Cached:{record.CachedTokens} tokens. Cost: ${record.CalculatedCost}",
                DetailsJson = System.Text.Json.JsonSerializer.Serialize(record)
            };

            db.AuditEntries.Add(audit);

            await db.SaveChangesAsync(ct);
            _logger.LogInformation("🪙 Token usage recorded for session {SessionId}: P:{Prompt}, C:{Completion}, Cost:${Cost}",
                record.SessionId, record.PromptTokens, record.CompletionTokens, record.CalculatedCost);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _logger.LogError(ex, "❌ Failed to record token usage for session {SessionId}", record.SessionId);
            throw;
        }
    }

    public async Task<decimal> CalculateCostAsync(string provider, string modelId, int promptTokens, int completionTokens, int cachedTokens = 0, CancellationToken ct = default)
    {
        using var db = await _dbContextFactory.CreateDbContextAsync(ct);

        // Find applicable pricing rule (most recent EffectiveDate <= now)
        var rule = await db.LlmPricingRules
            .Where(r => r.IsActive && r.Provider == provider && r.ModelId == modelId && r.EffectiveDate <= DateTime.UtcNow)
            .OrderByDescending(r => r.EffectiveDate)
            .FirstOrDefaultAsync(ct);

        if (rule is null)
        {
            // Fallback default pricing
            rule = new LlmPricingRuleEntity
            {
                CostPerMillionPromptTokens = 2.50m,
                CostPerMillionCompletionTokens = 10.00m,
                CostPerMillionCachedTokens = 1.25m
            };
        }

        var promptCost = (promptTokens / 1_000_000m) * rule.CostPerMillionPromptTokens;
        var completionCost = (completionTokens / 1_000_000m) * rule.CostPerMillionCompletionTokens;
        var cachedCost = (cachedTokens / 1_000_000m) * rule.CostPerMillionCachedTokens;

        return Math.Round(promptCost + completionCost + cachedCost, 6);
    }

    public async Task PublishTurnCostSummaryAsync(string sessionId, string tenantId, CancellationToken ct = default)
    {
        using var activity = TokenActivitySource.StartActivity("PublishTurnCostSummary");
        activity?.SetTag("ai.session_id", sessionId);

        try
        {
            using var db = await _dbContextFactory.CreateDbContextAsync(ct);

            var entries = await db.AuditEntries
                .Where(e => e.SessionId == sessionId && e.Category == "AI_Inference")
                .ToListAsync(ct);

            var totalCost = entries.Sum(e => e.Cost ?? 0m);

            // Get last turn cost (most recent entry)
            var lastEntry = entries.OrderByDescending(e => e.Timestamp).FirstOrDefault();
            var lastCost = lastEntry?.Cost ?? 0m;

            var summary = new TurnCostSummary
            {
                SessionId = sessionId,
                TenantId = tenantId,
                AccumulatedSessionCost = totalCost,
                LastTurnCost = lastCost,
                UpdatedAt = DateTime.UtcNow
            };

            // Publish via EventBus
            var busEvent = new SystemBusEvent
            {
                EventType = "FinOps.TurnCostUpdated",
                Source = "TokenAuditService",
                TenantId = tenantId,
                Payload = new Dictionary<string, object>
                {
                    { "TurnCostSummary", summary }
                }
            };

            await _eventBus.PublishAsync(busEvent, ct);
            _logger.LogInformation("📢 Turn cost summary published for session {SessionId}: Accumulated ${TotalCost}", sessionId, totalCost);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _logger.LogError(ex, "❌ Failed to publish turn cost summary for session {SessionId}", sessionId);
            throw;
        }
    }

    public async Task<IEnumerable<LlmPricingRule>> GetPricingRulesAsync(string? provider = null, CancellationToken ct = default)
    {
        using var db = await _dbContextFactory.CreateDbContextAsync(ct);

        var query = db.LlmPricingRules.AsNoTracking().Where(r => r.IsActive);
        if (!string.IsNullOrWhiteSpace(provider))
        {
            query = query.Where(r => r.Provider == provider);
        }

        var rules = await query.ToListAsync(ct);
        return rules.Select(r => new LlmPricingRule
        {
            Id = r.Id,
            Provider = r.Provider,
            ModelId = r.ModelId,
            CostPerMillionPromptTokens = r.CostPerMillionPromptTokens,
            CostPerMillionCompletionTokens = r.CostPerMillionCompletionTokens,
            CostPerMillionCachedTokens = r.CostPerMillionCachedTokens,
            EffectiveDate = r.EffectiveDate,
            IsActive = r.IsActive
        });
    }

    public async Task SetPricingRuleAsync(LlmPricingRule rule, CancellationToken ct = default)
    {
        using var db = await _dbContextFactory.CreateDbContextAsync(ct);

        var entity = new LlmPricingRuleEntity
        {
            Id = string.IsNullOrWhiteSpace(rule.Id) ? Guid.NewGuid().ToString("N") : rule.Id,
            Provider = rule.Provider,
            ModelId = rule.ModelId,
            CostPerMillionPromptTokens = rule.CostPerMillionPromptTokens,
            CostPerMillionCompletionTokens = rule.CostPerMillionCompletionTokens,
            CostPerMillionCachedTokens = rule.CostPerMillionCachedTokens,
            EffectiveDate = rule.EffectiveDate,
            IsActive = rule.IsActive
        };

        db.LlmPricingRules.Add(entity);
        await db.SaveChangesAsync(ct);
        _logger.LogInformation("📝 Pricing rule set for {Provider}:{ModelId} effective {Date}", rule.Provider, rule.ModelId, rule.EffectiveDate);
    }
}
