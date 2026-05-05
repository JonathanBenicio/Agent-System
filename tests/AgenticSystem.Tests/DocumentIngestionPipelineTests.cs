using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.Documents;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace AgenticSystem.Tests;

public class DocumentIngestionPipelineTests
{
    private readonly IChunkingStrategy _chunkingStrategy;
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
    private readonly IVectorStore _vectorStore;
    private readonly DocumentIngestionPipeline _pipeline;

    public DocumentIngestionPipelineTests()
    {
        var markdownParser = new MarkdownParser(Substitute.For<ILogger<MarkdownParser>>());
        var plainTextParser = new PlainTextParser(Substitute.For<ILogger<PlainTextParser>>());
        var parsers = new IDocumentParser[] { markdownParser, plainTextParser };

        _chunkingStrategy = Substitute.For<IChunkingStrategy>();
        _embeddingGenerator = Substitute.For<IEmbeddingGenerator<string, Embedding<float>>>();
        _vectorStore = Substitute.For<IVectorStore>();

        _pipeline = new DocumentIngestionPipeline(
            parsers, _chunkingStrategy, _embeddingGenerator, _vectorStore,
            Substitute.For<ILogger<DocumentIngestionPipeline>>());
    }

    [Fact]
    public async Task IngestAsync_ValidMarkdown_ShouldSucceed()
    {
        var doc = CreateRawDocument("# Hello\nWorld content.", "test.md", DocumentType.Markdown);
        SetupMocks(1);

        var result = await _pipeline.IngestAsync(doc);

        result.Success.Should().BeTrue();
        result.ChunksCreated.Should().Be(1);
        result.FileName.Should().Be("test.md");
        result.ContentHash.Should().NotBeNullOrEmpty();
        await _vectorStore.Received(1).UpsertAsync(Arg.Any<EmbeddingDocument>());
    }

    [Fact]
    public async Task IngestAsync_UnsupportedType_ShouldFail()
    {
        var doc = CreateRawDocument("data", "image.png", DocumentType.Image);

        var result = await _pipeline.IngestAsync(doc);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("No parser registered");
    }

    [Fact]
    public async Task IngestAsync_ZeroChunks_ShouldFail()
    {
        var doc = CreateRawDocument("# Title\nContent.", "test.md", DocumentType.Markdown);
        _chunkingStrategy.ChunkAsync(Arg.Any<ParsedDocument>(), Arg.Any<ChunkingConfig>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<DocumentChunk>>(new List<DocumentChunk>()));

        var result = await _pipeline.IngestAsync(doc);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("0 chunks");
    }

    [Fact]
    public async Task IngestAsync_ShouldCallEmbeddingProviderWithChunkContents()
    {
        var doc = CreateRawDocument("# Test\nContent A.\n\n## More\nContent B.", "multi.md", DocumentType.Markdown);
        SetupMocks(2);

        await _pipeline.IngestAsync(doc);

        await _embeddingGenerator.Received(1).GenerateAsync(
            Arg.Is<IEnumerable<string>>(texts => texts.Count() == 2),
            Arg.Any<EmbeddingGenerationOptions?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IngestAsync_ShouldUpsertEachChunkToVectorStore()
    {
        var doc = CreateRawDocument("# A\nOne.\n\n# B\nTwo.\n\n# C\nThree.", "three.md", DocumentType.Markdown);
        SetupMocks(3);

        await _pipeline.IngestAsync(doc);

        await _vectorStore.Received(3).UpsertAsync(Arg.Any<EmbeddingDocument>());
    }

    [Fact]
    public async Task IngestBatchAsync_ShouldProcessAllDocuments()
    {
        var docs = new[]
        {
            CreateRawDocument("# A\nFirst.", "a.md", DocumentType.Markdown),
            CreateRawDocument("Second doc.", "b.txt", DocumentType.PlainText),
        };
        SetupMocks(1);

        var results = await _pipeline.IngestBatchAsync(docs);

        results.Should().HaveCount(2);
        results.Should().OnlyContain(r => r.Success);
    }

    [Fact]
    public async Task IngestBatchAsync_PartialFailure_ShouldContinue()
    {
        var docs = new[]
        {
            CreateRawDocument("# Valid\nContent.", "ok.md", DocumentType.Markdown),
            CreateRawDocument("binary", "img.png", DocumentType.Image), // no parser
        };
        SetupMocks(1);

        var results = await _pipeline.IngestBatchAsync(docs);

        results.Should().HaveCount(2);
        results.Count(r => r.Success).Should().Be(1);
        results.Count(r => !r.Success).Should().Be(1);
    }

    private void SetupMocks(int chunkCount)
    {
        var chunks = Enumerable.Range(0, chunkCount)
            .Select(i => new DocumentChunk
            {
                DocumentId = "doc-1",
                Content = $"Chunk {i} content",
                Index = i,
                TokenCount = 10,
                Metadata = new ChunkMetadata
                {
                    FileName = "test.md",
                    Collection = "default",
                    ContentType = "document"
                }
            })
            .ToList();

        _chunkingStrategy.ChunkAsync(Arg.Any<ParsedDocument>(), Arg.Any<ChunkingConfig>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<DocumentChunk>>(chunks));

        var embeddings = Enumerable.Range(0, chunkCount)
            .Select(_ => new Embedding<float>(new float[] { 0.1f, 0.2f, 0.3f }))
            .ToList();

        _embeddingGenerator.GenerateAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<EmbeddingGenerationOptions?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<GeneratedEmbeddings<Embedding<float>>>(new GeneratedEmbeddings<Embedding<float>>(embeddings)));
    }

    private static RawDocument CreateRawDocument(string text, string fileName, DocumentType type)
    {
        return new RawDocument
        {
            FileName = fileName,
            Type = type,
            TextContent = text,
            Content = System.Text.Encoding.UTF8.GetBytes(text),
            Source = "test"
        };
    }
}
