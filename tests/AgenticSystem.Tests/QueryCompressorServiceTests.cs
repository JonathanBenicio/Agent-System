using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace AgenticSystem.Tests;

public class QueryCompressorServiceTests
{
    private readonly QueryCompressorService _sut;

    public QueryCompressorServiceTests()
    {
        var logger = Substitute.For<ILogger<QueryCompressorService>>();
        _sut = new QueryCompressorService(logger);
    }

    [Fact]
    public async Task CompressAsync_WithNone_ReturnsOriginalQuery()
    {
        var result = await _sut.CompressAsync("hello world", QueryCompressionStrategy.None);

        result.OriginalQuery.Should().Be("hello world");
        result.CompressedText.Should().Be("hello world");
        result.StrategyUsed.Should().Be(QueryCompressionStrategy.None);
        result.CompressionRatio.Should().Be(1.0);
    }

    [Fact]
    public async Task CompressAsync_WithRemoveRedundancy_RemovesDuplicateWords()
    {
        var query = "how to create create a service service in dotnet dotnet";
        var result = await _sut.CompressAsync(query, QueryCompressionStrategy.RemoveRedundancy);

        result.StrategyUsed.Should().Be(QueryCompressionStrategy.RemoveRedundancy);
        result.RemovedRedundancies.Should().NotBeEmpty();
        result.CompressedText.Should().NotBeNullOrWhiteSpace();
        result.CompressionRatio.Should().BeLessThanOrEqualTo(1.0);
    }

    [Fact]
    public async Task CompressAsync_WithExtractKeyTerms_ExtractsTerms()
    {
        var query = "how do I create a new controller in ASP.NET Core with dependency injection";
        var result = await _sut.CompressAsync(query, QueryCompressionStrategy.ExtractKeyTerms);

        result.StrategyUsed.Should().Be(QueryCompressionStrategy.ExtractKeyTerms);
        result.ExtractedKeyTerms.Should().NotBeEmpty();
        result.CompressedText.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task CompressAsync_WithSemanticNormalization_NormalizesIntent()
    {
        var query = "how can I build a REST API endpoint?";
        var result = await _sut.CompressAsync(query, QueryCompressionStrategy.SemanticNormalization);

        result.StrategyUsed.Should().Be(QueryCompressionStrategy.SemanticNormalization);
        result.NormalizedIntent.Should().NotBeNullOrWhiteSpace();
        result.CompressedText.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task CompressAsync_WithHybridCompression_AppliesAllStrategies()
    {
        var query = "how how to create create a service with dependency injection in dotnet core";
        var result = await _sut.CompressAsync(query, QueryCompressionStrategy.HybridCompression);

        result.StrategyUsed.Should().Be(QueryCompressionStrategy.HybridCompression);
        result.CompressedText.Should().NotBeNullOrWhiteSpace();
        result.ExtractedKeyTerms.Should().NotBeEmpty();
        result.NormalizedIntent.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task CompressAsync_EmptyQuery_ReturnsEmptyResult()
    {
        var result = await _sut.CompressAsync("", QueryCompressionStrategy.RemoveRedundancy);

        result.OriginalQuery.Should().BeEmpty();
        result.CompressedText.Should().BeEmpty();
    }

    [Fact]
    public async Task CompressAsync_TracksMetrics()
    {
        await _sut.CompressAsync("query one", QueryCompressionStrategy.RemoveRedundancy);
        await _sut.CompressAsync("query two", QueryCompressionStrategy.ExtractKeyTerms);
        await _sut.CompressAsync("query three", QueryCompressionStrategy.SemanticNormalization);

        var metrics = _sut.GetMetrics();

        metrics.TotalQueriesCompressed.Should().Be(3);
        metrics.StrategyUsage.Should().HaveCount(3);
        metrics.AverageCompressionRatio.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task CompressWithContextAsync_UsesAnalysisContext()
    {
        var context = new AnalysisResult
        {
            PrimaryDomain = "dotnet",
            Intent = IntentType.Create
        };

        var result = await _sut.CompressWithContextAsync("create a service", context);

        result.CompressedText.Should().NotBeNullOrWhiteSpace();
        result.ConfidenceScore.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task CompressAsync_CompressionRatio_IsCorrectlyCalculated()
    {
        var query = "the the the same same word repeated repeated many times";
        var result = await _sut.CompressAsync(query, QueryCompressionStrategy.RemoveRedundancy);

        result.OriginalTokenCount.Should().BeGreaterThan(0);
        result.CompressedTokenCount.Should().BeGreaterThan(0);
        result.CompressionRatio.Should().Be(
            (double)result.CompressedTokenCount / result.OriginalTokenCount,
            "compression ratio should be compressed/original");
    }

    [Fact]
    public async Task CompressAsync_KeyTerms_FiltersStopwords()
    {
        var query = "how do I create a new service in the system";
        var result = await _sut.CompressAsync(query, QueryCompressionStrategy.ExtractKeyTerms);

        // Short stopwords should not appear as key terms
        result.ExtractedKeyTerms.Should().NotContain("do");
        result.ExtractedKeyTerms.Should().NotContain("a");
        result.ExtractedKeyTerms.Should().NotContain("the");
        result.ExtractedKeyTerms.Should().NotContain("in");
        result.ExtractedKeyTerms.Should().NotContain("I");
    }

    [Fact]
    public async Task GetMetrics_WhenNoQueries_ReturnsEmptyMetrics()
    {
        var metrics = _sut.GetMetrics();

        metrics.TotalQueriesCompressed.Should().Be(0);
        metrics.AverageCompressionRatio.Should().Be(0);
        metrics.AverageConfidenceScore.Should().Be(0);
        metrics.StrategyUsage.Should().BeEmpty();
    }
}
