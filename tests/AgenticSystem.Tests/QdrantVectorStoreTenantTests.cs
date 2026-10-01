using System.Net;
using System.Text.Json;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.Configuration;
using AgenticSystem.Infrastructure.Memory;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace AgenticSystem.Tests;

public sealed class QdrantVectorStoreTenantTests
{
    [Fact]
    public async Task UpsertAsync_WithoutTenant_RejectsDocument()
    {
        var sut = CreateStore(new RecordingHandler());

        var act = () => sut.UpsertAsync(new EmbeddingDocument
        {
            Id = "doc-1",
            Content = "sensitive",
            Collection = "documents"
        });

        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("document");
    }

    [Fact]
    public async Task UpsertAsync_WithTenant_WritesTenantToPayload()
    {
        var handler = new RecordingHandler();
        var sut = CreateStore(handler);

        await sut.UpsertAsync(new EmbeddingDocument
        {
            Id = "doc-1",
            TenantId = "tenant-a",
            Content = "sensitive",
            Collection = "documents"
        });

        using var payload = JsonDocument.Parse(handler.RequestBody!);
        payload.RootElement.GetProperty("points")[0].GetProperty("payload")
            .GetProperty("tenantId").GetString().Should().Be("tenant-a");
    }

    private static QdrantVectorStore CreateStore(HttpMessageHandler handler)
    {
        var settings = new MemorySettings
        {
            Qdrant = new QdrantSettings { Url = "https://qdrant.test" }
        };
        return new QdrantVectorStore(
            new HttpClient(handler),
            Options.Create(settings),
            Substitute.For<ILogger<QdrantVectorStore>>());
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}
