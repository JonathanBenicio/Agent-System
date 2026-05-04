using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;

namespace AgenticSystem.Tests;

public class ContextBudgetManagerTests
{
    private readonly ContextBudgetManager _sut;

    public ContextBudgetManagerTests()
    {
        var logger = Substitute.For<ILogger<ContextBudgetManager>>();
        _sut = new ContextBudgetManager(logger);
    }

    [Theory]
    [InlineData(ComplexityLevel.Simple, 2000)]
    [InlineData(ComplexityLevel.Moderate, 4000)]
    [InlineData(ComplexityLevel.Complex, 6000)]
    [InlineData(ComplexityLevel.RequiresPlanning, 8000)]
    public void ResolveBudget_ReturnsCorrectMaxTokens(ComplexityLevel complexity, int expectedTokens)
    {
        var analysis = new AnalysisResult { Complexity = complexity };

        var budget = _sut.ResolveBudget(analysis);

        budget.MaxTokens.Should().Be(expectedTokens);
    }

    [Fact]
    public void ResolveBudget_ReadIntent_UsesPrecisionStrategy()
    {
        var analysis = new AnalysisResult
        {
            Complexity = ComplexityLevel.Moderate,
            Intent = IntentType.Read
        };

        var budget = _sut.ResolveBudget(analysis);

        budget.Strategy.Should().Be(ContextStrategy.PrecisionFocused);
    }

    [Fact]
    public void ResolveBudget_CreateIntent_UsesCreativityStrategy()
    {
        var analysis = new AnalysisResult
        {
            Complexity = ComplexityLevel.Moderate,
            Intent = IntentType.Create
        };

        var budget = _sut.ResolveBudget(analysis);

        budget.Strategy.Should().Be(ContextStrategy.CreativityFocused);
    }

    [Fact]
    public async Task TrimContextToBudgetAsync_WithinBudget_ReturnsUnchanged()
    {
        var ragContext = new RAGContext
        {
            TotalTokensUsed = 1000,
            Chunks = new List<RankedChunk>
            {
                new() { Content = "test content", ReRankedScore = 0.9 }
            }
        };

        var budget = new ContextBudget { MaxTokens = 2000 };

        var result = await _sut.TrimContextToBudgetAsync(ragContext, budget);

        result.Should().BeSameAs(ragContext);
    }

    [Fact]
    public async Task TrimContextToBudgetAsync_OverBudget_TrimsChunks()
    {
        var ragContext = new RAGContext
        {
            TotalTokensUsed = 5000,
            Chunks = new List<RankedChunk>
            {
                new() { Content = new string('a', 4000), ReRankedScore = 0.9 },
                new() { Content = new string('b', 4000), ReRankedScore = 0.5 },
                new() { Content = new string('c', 4000), ReRankedScore = 0.3 }
            }
        };

        var budget = new ContextBudget { MaxTokens = 2000 };

        var result = await _sut.TrimContextToBudgetAsync(ragContext, budget);

        result.Chunks.Count.Should().BeLessThan(ragContext.Chunks.Count);
    }

    [Fact]
    public async Task AllocateContextAsync_CalculatesAllocation()
    {
        var budget = new ContextBudget { MaxTokens = 4000 };
        var ragContext = new RAGContext
        {
            TotalTokensUsed = 2000,
            CandidatesRetrieved = 10,
            CandidatesAfterReRank = 5
        };

        var allocation = await _sut.AllocateContextAsync(budget, ragContext);

        allocation.TotalTokensBudget.Should().Be(4000);
        allocation.TokensUsed.Should().Be(2000);
        allocation.ChunksIncluded.Should().Be(5);
        allocation.ChunksExcluded.Should().Be(5);
    }
}
