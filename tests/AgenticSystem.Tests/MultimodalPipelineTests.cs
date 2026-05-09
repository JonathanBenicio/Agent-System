using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.Documents;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace AgenticSystem.Tests;

public class MultimodalPipelineTests
{
    private readonly IChunkingStrategy _chunkingStrategy;
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
    private readonly IVectorStore _vectorStore;
    private readonly IMultimodalProcessor _multimodalProcessor;
    private readonly DocumentIngestionPipeline _pipeline;

    public MultimodalPipelineTests()
    {
        _chunkingStrategy = Substitute.For<IChunkingStrategy>();
        _embeddingGenerator = Substitute.For<IEmbeddingGenerator<string, Embedding<float>>>();
        _vectorStore = Substitute.For<IVectorStore>();
        _multimodalProcessor = Substitute.For<IMultimodalProcessor>();

        _pipeline = new DocumentIngestionPipeline(
            [], // No standard parsers needed for multimodal test
            _chunkingStrategy,
            _embeddingGenerator,
            _vectorStore,
            Substitute.For<ILogger<DocumentIngestionPipeline>>(),
            _multimodalProcessor);
    }

    [Fact]
    public async Task IngestAsync_AudioFile_ShouldRouteToMultimodalProcessor()
    {
        // Arrange
        var doc = new RawDocument
        {
            Id = "audio-1",
            FileName = "interview.mp3",
            Type = DocumentType.Audio,
            Content = new byte[] { 0x01, 0x02, 0x03 }
        };

        _multimodalProcessor.ProcessAsync(Arg.Any<Stream>(), "interview.mp3", "audio/mpeg")
            .Returns(new MultimodalDocument
            {
                FileName = "interview.mp3",
                ExtractedContents = new List<ExtractedContent>
                {
                    new() { ExtractionType = ExtractionType.AudioTranscription, Content = "Hello, this is a test transcription." }
                }
            });

        _embeddingGenerator.GenerateAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<EmbeddingGenerationOptions>(), Arg.Any<CancellationToken>())
            .Returns(new GeneratedEmbeddings<Embedding<float>>(new[] { new Embedding<float>(new float[1536]) }));

        // Act
        var result = await _pipeline.IngestAsync(doc);

        // Assert
        result.Success.Should().BeTrue();
        result.ChunksCreated.Should().Be(1);
        await _multimodalProcessor.Received(1).ProcessAsync(Arg.Any<Stream>(), "interview.mp3", "audio/mpeg");
        await _vectorStore.Received(1).UpsertAsync(Arg.Is<EmbeddingDocument>(d => d.Content.Contains("test transcription")));
    }

    [Fact]
    public async Task IngestAsync_ImageFile_ShouldRouteToMultimodalProcessor()
    {
        // Arrange
        var doc = new RawDocument
        {
            Id = "img-1",
            FileName = "diagram.png",
            Type = DocumentType.Image,
            Content = new byte[] { 0x01, 0x02, 0x03 }
        };

        _multimodalProcessor.ProcessAsync(Arg.Any<Stream>(), "diagram.png", "image/png")
            .Returns(new MultimodalDocument
            {
                FileName = "diagram.png",
                ExtractedContents = new List<ExtractedContent>
                {
                    new() { ExtractionType = ExtractionType.OCR, Content = "Text from image" },
                    new() { ExtractionType = ExtractionType.ImageCaption, Content = "A description of the diagram" }
                }
            });

        _embeddingGenerator.GenerateAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<EmbeddingGenerationOptions>(), Arg.Any<CancellationToken>())
            .Returns(new GeneratedEmbeddings<Embedding<float>>(new[] { new Embedding<float>(new float[1536]), new Embedding<float>(new float[1536]) }));

        // Act
        var result = await _pipeline.IngestAsync(doc);

        // Assert
        result.Success.Should().BeTrue();
        result.ChunksCreated.Should().Be(2);
        await _multimodalProcessor.Received(1).ProcessAsync(Arg.Any<Stream>(), "diagram.png", "image/png");
        await _vectorStore.Received(2).UpsertAsync(Arg.Any<EmbeddingDocument>());
    }
}
