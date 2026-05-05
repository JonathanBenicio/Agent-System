using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.LLM.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.AI;
using Microsoft.Extensions.VectorData;

namespace AgenticSystem.Tests;

public class AgenticVectorStoreAdapterTests
{
    private readonly IVectorStore _agenticStore;
    private readonly IEmbeddingProvider _embeddingProvider;
    private readonly AgenticVectorStoreAdapter _sut;

    public AgenticVectorStoreAdapterTests()
    {
        _agenticStore = Substitute.For<IVectorStore>();
        _embeddingProvider = Substitute.For<IEmbeddingProvider>();
        _sut = new AgenticVectorStoreAdapter(_agenticStore, _embeddingProvider);
    }

    [Fact]
    public void GetCollection_ThrowsNotSupported()
    {
        var act = () => _sut.GetCollection<string, Dictionary<string, object?>>("test");
        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void GetDynamicCollection_ThrowsNotSupported()
    {
        var act = () => _sut.GetDynamicCollection("test", new VectorStoreCollectionDefinition());
        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public async Task ListCollectionNamesAsync_ReturnsCollections()
    {
        _agenticStore.GetCollectionsAsync().Returns(new[] { "collection-a", "collection-b" });

        var result = new List<string>();
        await foreach (var name in _sut.ListCollectionNamesAsync())
        {
            result.Add(name);
        }

        result.Should().BeEquivalentTo(new[] { "collection-a", "collection-b" });
    }

    [Fact]
    public async Task ListCollectionNamesAsync_ReturnsEmpty_WhenNoCollections()
    {
        _agenticStore.GetCollectionsAsync().Returns(Array.Empty<string>());

        var result = new List<string>();
        await foreach (var name in _sut.ListCollectionNamesAsync())
        {
            result.Add(name);
        }

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task CollectionExistsAsync_ReturnsTrue_WhenCollectionExists()
    {
        _agenticStore.GetCollectionsAsync().Returns(new[] { "docs", "code" });

        var result = await _sut.CollectionExistsAsync("docs");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task CollectionExistsAsync_ReturnsFalse_WhenCollectionNotFound()
    {
        _agenticStore.GetCollectionsAsync().Returns(new[] { "docs" });

        var result = await _sut.CollectionExistsAsync("missing");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task EnsureCollectionDeletedAsync_DoesNotThrow()
    {
        var act = () => _sut.EnsureCollectionDeletedAsync("test");
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void GetService_ReturnsAgenticStore_WhenRequestedTypeMatches()
    {
        var result = _sut.GetService(typeof(IVectorStore));

        result.Should().BeSameAs(_agenticStore);
    }

    [Fact]
    public void GetService_ReturnsNull_ForOtherTypes()
    {
        var result = _sut.GetService(typeof(IEmbeddingProvider));

        result.Should().BeNull();
    }

    [Fact]
    public async Task SearchAsync_DelegatesToAgenticStore()
    {
        var expected = new SearchResult();
        _agenticStore.SearchAsync("query", SearchScope.All, 5).Returns(expected);

        var result = await _sut.SearchAsync("query", SearchScope.All, 5);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task UpsertAsync_GeneratesEmbedding_AndDelegates()
    {
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };
        _embeddingProvider.GenerateEmbeddingAsync("test content", Arg.Any<CancellationToken>())
            .Returns(embedding);

        await _sut.UpsertAsync("doc-1", "test content", "my-collection", "text", new Dictionary<string, string> { ["key"] = "val" });

        await _embeddingProvider.Received(1).GenerateEmbeddingAsync("test content", Arg.Any<CancellationToken>());
        await _agenticStore.Received(1).UpsertAsync(Arg.Is<EmbeddingDocument>(d =>
            d.Id == "doc-1" &&
            d.Content == "test content" &&
            d.Collection == "my-collection" &&
            d.Type == "text" &&
            d.Embedding == embedding &&
            d.Metadata["key"] == "val"));
    }

    [Fact]
    public async Task UpsertAsync_UsesEmptyMetadata_WhenNull()
    {
        _embeddingProvider.GenerateEmbeddingAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new float[] { 0.1f });

        await _sut.UpsertAsync("doc-2", "content", "col", "type");

        await _agenticStore.Received(1).UpsertAsync(Arg.Is<EmbeddingDocument>(d =>
            d.Metadata != null && d.Metadata.Count == 0));
    }
}
