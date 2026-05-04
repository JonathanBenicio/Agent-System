using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Core.Services;

/// <summary>
/// Maturity Level 2 — Controla orçamento semântico de contexto por request.
/// </summary>
public class ContextBudgetManager : IContextBudgetManager
{
    private readonly ILogger<ContextBudgetManager> _logger;

    public ContextBudgetManager(ILogger<ContextBudgetManager> logger)
    {
        _logger = logger;
    }

    public ContextBudget ResolveBudget(AnalysisResult analysis)
    {
        var budget = new ContextBudget();

        switch (analysis.Complexity)
        {
            case ComplexityLevel.Simple:
                budget.MaxTokens = 2000;
                budget.Strategy = ContextStrategy.Minimal;
                budget.RecentMemoryWeight = 0.6;
                budget.DomainKnowledgeWeight = 0.2;
                budget.EpisodicWeight = 0.1;
                budget.DecisionHistoryWeight = 0.1;
                break;

            case ComplexityLevel.Moderate:
                budget.MaxTokens = 4000;
                budget.Strategy = ContextStrategy.Balanced;
                break;

            case ComplexityLevel.Complex:
                budget.MaxTokens = 6000;
                budget.Strategy = ContextStrategy.PrecisionFocused;
                budget.RecentMemoryWeight = 0.3;
                budget.DomainKnowledgeWeight = 0.4;
                budget.EpisodicWeight = 0.15;
                budget.DecisionHistoryWeight = 0.15;
                break;

            case ComplexityLevel.RequiresPlanning:
                budget.MaxTokens = 8000;
                budget.Strategy = ContextStrategy.CreativityFocused;
                budget.RecentMemoryWeight = 0.2;
                budget.DomainKnowledgeWeight = 0.3;
                budget.EpisodicWeight = 0.3;
                budget.DecisionHistoryWeight = 0.2;
                break;
        }

        if (analysis.Intent == IntentType.Read)
        {
            budget.Strategy = ContextStrategy.PrecisionFocused;
        }
        else if (analysis.Intent == IntentType.Create)
        {
            budget.Strategy = ContextStrategy.CreativityFocused;
        }

        _logger.LogDebug("Budget resolved: {MaxTokens} tokens, strategy: {Strategy}", budget.MaxTokens, budget.Strategy);
        return budget;
    }

    public Task<ContextAllocation> AllocateContextAsync(ContextBudget budget, RAGContext ragContext)
    {
        var totalTokens = budget.MaxTokens;

        var allocation = new ContextAllocation
        {
            TotalTokensBudget = totalTokens,
            StrategyUsed = budget.Strategy,
            RecentMemoryTokens = (int)(totalTokens * budget.RecentMemoryWeight),
            DomainKnowledgeTokens = (int)(totalTokens * budget.DomainKnowledgeWeight),
            EpisodicTokens = (int)(totalTokens * budget.EpisodicWeight),
            DecisionHistoryTokens = (int)(totalTokens * budget.DecisionHistoryWeight),
            TokensUsed = ragContext.TotalTokensUsed,
            ChunksIncluded = ragContext.CandidatesAfterReRank,
            ChunksExcluded = ragContext.CandidatesRetrieved - ragContext.CandidatesAfterReRank,
            EstimatedCost = ragContext.TotalTokensUsed * 0.00003
        };

        _logger.LogDebug("Context allocated: {Used}/{Budget} tokens, {Included} chunks included",
            allocation.TokensUsed, allocation.TotalTokensBudget, allocation.ChunksIncluded);

        return Task.FromResult(allocation);
    }

    public Task<RAGContext> TrimContextToBudgetAsync(RAGContext ragContext, ContextBudget budget)
    {
        if (ragContext.TotalTokensUsed <= budget.MaxTokens)
            return Task.FromResult(ragContext);

        var trimmed = new RAGContext
        {
            Query = ragContext.Query,
            StrategyUsed = ragContext.StrategyUsed,
            RetrievalTime = ragContext.RetrievalTime,
            ReRankTime = ragContext.ReRankTime,
            TotalTime = ragContext.TotalTime,
            CandidatesRetrieved = ragContext.CandidatesRetrieved
        };

        var tokensRemaining = budget.MaxTokens;
        var orderedChunks = ragContext.Chunks.OrderByDescending(c => c.ReRankedScore).ToList();

        foreach (var chunk in orderedChunks)
        {
            var estimatedTokens = chunk.Content.Length / 4;
            if (tokensRemaining - estimatedTokens < 0) break;

            trimmed.Chunks.Add(chunk);
            tokensRemaining -= estimatedTokens;
        }

        trimmed.TotalTokensUsed = budget.MaxTokens - tokensRemaining;
        trimmed.CandidatesAfterReRank = trimmed.Chunks.Count;
        trimmed.BuiltContext = string.Join("\n\n", trimmed.Chunks.Select(c => c.Content));

        _logger.LogInformation("Trimmed context from {Original} to {Trimmed} tokens ({Removed} chunks removed)",
            ragContext.TotalTokensUsed, trimmed.TotalTokensUsed,
            ragContext.Chunks.Count - trimmed.Chunks.Count);

        return Task.FromResult(trimmed);
    }
}
