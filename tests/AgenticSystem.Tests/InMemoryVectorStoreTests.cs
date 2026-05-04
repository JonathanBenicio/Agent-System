using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.Memory;

namespace AgenticSystem.Tests;

public class InMemoryVectorStoreTests
{
    private readonly ILogger<InMemoryVectorStore> _logger;
    private readonly InMemoryVectorStore _sut;

    public InMemoryVectorStoreTests()
    {
        _logger = Substitute.For<ILogger<InMemoryVectorStore>>();
        _sut = new InMemoryVectorStore(_logger);
    }

    [Fact]
    public async Task UpsertAsync_AddsDocument()
    {
        var doc = new EmbeddingDocument
        {
            Id = "doc-1",
            Content = "Test document about agents",
            Type = "note",
            Collection = "test"
        };

        await _sut.UpsertAsync(doc);

        var result = await _sut.SearchAsync("agents", SearchScope.All, 5);
        result.Matches.Should().NotBeEmpty();
    }

    [Fact]
    public async Task SearchAsync_FindsRelevantDocuments()
    {
        await _sut.UpsertAsync(new EmbeddingDocument
        {
            Id = "doc-1",
            Content = "Information about calendar scheduling",
            Type = "note",
            Collection = "notes"
        });
        await _sut.UpsertAsync(new EmbeddingDocument
        {
            Id = "doc-2",
            Content = "Guide for cooking recipes",
            Type = "note",
            Collection = "notes"
        });

        var result = await _sut.SearchAsync("calendar", SearchScope.All, 5);
        result.Matches.Should().Contain(m => m.Id == "doc-1");
    }

    [Fact]
    public async Task SearchWithFiltersAsync_FiltersCorrectly()
    {
        await _sut.UpsertAsync(new EmbeddingDocument
        {
            Id = "d1",
            Content = "Agent documentation",
            Type = "agent",
            Collection = "agents",
            Metadata = new Dictionary<string, string> { ["tier"] = "Support" }
        });
        await _sut.UpsertAsync(new EmbeddingDocument
        {
            Id = "d2",
            Content = "Agent analysis",
            Type = "agent",
            Collection = "agents",
            Metadata = new Dictionary<string, string> { ["tier"] = "Master" }
        });

        var result = await _sut.SearchWithFiltersAsync("agent", new Dictionary<string, string> { ["tier"] = "Support" });
        result.Matches.Should().HaveCount(1);
        result.Matches.First().Id.Should().Be("d1");
    }

    [Fact]
    public async Task GetCollectionsAsync_ReturnsDistinctCollections()
    {
        await _sut.UpsertAsync(new EmbeddingDocument { Id = "1", Content = "a", Collection = "alpha" });
        await _sut.UpsertAsync(new EmbeddingDocument { Id = "2", Content = "b", Collection = "beta" });
        await _sut.UpsertAsync(new EmbeddingDocument { Id = "3", Content = "c", Collection = "alpha" });

        var collections = await _sut.GetCollectionsAsync();
        collections.Should().BeEquivalentTo(new[] { "alpha", "beta" });
    }

    [Fact]
    public async Task CleanupOldDocumentsAsync_DoesNotRemoveRecentDocs()
    {
        // UpsertAsync sets IndexedAt = DateTime.UtcNow, so both docs are recent
        await _sut.UpsertAsync(new EmbeddingDocument { Id = "d1", Content = "first document", Collection = "test" });
        await _sut.UpsertAsync(new EmbeddingDocument { Id = "d2", Content = "second document", Collection = "test" });

        await _sut.CleanupOldDocumentsAsync(TimeSpan.FromDays(5));

        var result = await _sut.SearchAsync("document", SearchScope.All, 10);
        result.Matches.Should().HaveCount(2);
    }

    [Fact]
    public async Task UpsertAsync_UpdatesExistingDocument()
    {
        await _sut.UpsertAsync(new EmbeddingDocument { Id = "same", Content = "version 1", Collection = "test" });
        await _sut.UpsertAsync(new EmbeddingDocument { Id = "same", Content = "version 2 updated", Collection = "test" });

        var result = await _sut.SearchAsync("updated", SearchScope.All, 5);
        result.Matches.Should().Contain(m => m.Id == "same");
    }
}
