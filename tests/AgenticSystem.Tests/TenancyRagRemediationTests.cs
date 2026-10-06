using System.Net;
using System.Text;
using System.Text.Json;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;
using AgenticSystem.Infrastructure.Configuration;
using AgenticSystem.Infrastructure.Memory;
using AgenticSystem.Infrastructure.Persistence;
using AgenticSystem.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using AgenticSystem.Api.Controllers;
using AgenticSystem.Infrastructure.RAG;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AgenticSystem.Tests;

public class TenancyRagRemediationTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task DocumentUpload_UsesWriteAclBeforePipelineAndSeparatesRoomFromSource(bool batch, bool allowed)
    {
        using var db = NewDb();
        var rooms = Substitute.For<IKnowledgeRoomService>();
        rooms.GetRoomAsync("room", "A", "user", Arg.Any<CancellationToken>()).Returns(new KnowledgeRoom { Id = "room" });
        rooms.CanWriteRoomAsync("room", "A", "user", Arg.Any<CancellationToken>()).Returns(allowed);
        var pipeline = Substitute.For<IDocumentIngestionPipeline>();
        pipeline.IngestAsync(Arg.Any<RawDocument>(), Arg.Any<ChunkingConfig>(), Arg.Any<CancellationToken>())
            .Returns(IngestionResult.Fail("doc", "file.txt", "test rejection"));
        pipeline.IngestBatchAsync(Arg.Any<IEnumerable<RawDocument>>(), Arg.Any<ChunkingConfig>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<IngestionResult>());
        var tenant = Substitute.For<ITenantContextAccessor>();
        tenant.CurrentTenantId.Returns("A");
        var env = Substitute.For<IWebHostEnvironment>();
        env.WebRootPath.Returns(Path.GetTempPath());
        var controller = new DocumentController(pipeline, NullLogger<DocumentController>.Instance, db, Substitute.For<IRerankingSettingsAccessor>(), tenant, rooms, env)
        {
            ControllerContext = new() { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user")], "test")) } }
        };
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("hello"));
        var file = new FormFile(stream, 0, stream.Length, "file", "file.txt") { Headers = new HeaderDictionary(), ContentType = "text/plain" };
        var result = batch ? await controller.IngestBatch(new FormFileCollection { file }, "session", "room")
            : await controller.IngestDocument(file, "session", "room");
        if (!allowed)
        {
            result.Should().BeOfType<NotFoundObjectResult>();
            await pipeline.DidNotReceive().IngestAsync(Arg.Any<RawDocument>(), Arg.Any<ChunkingConfig>(), Arg.Any<CancellationToken>());
            await pipeline.DidNotReceive().IngestBatchAsync(Arg.Any<IEnumerable<RawDocument>>(), Arg.Any<ChunkingConfig>(), Arg.Any<CancellationToken>());
        }
        else if (batch)
            await pipeline.Received(1).IngestBatchAsync(Arg.Is<IEnumerable<RawDocument>>(docs => docs.All(d => d.Source == "session")), Arg.Is<ChunkingConfig>(c => c.RoomId == "room" && c.Collection == "session" && c.TenantId == "A"), Arg.Any<CancellationToken>());
        else
            await pipeline.Received(1).IngestAsync(Arg.Is<RawDocument>(d => d.Source == "session"), Arg.Is<ChunkingConfig>(c => c.RoomId == "room" && c.Collection == "session" && c.TenantId == "A"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DynamicAgents_SameNameIsIndependentAndMissingContextFails()
    {
        var accessor = new TenantContextAccessor();
        var repo = new InMemoryDynamicAgentRepository(accessor);
        Action noContext = () => repo.GetAllAsync();
        noContext.Should().Throw<InvalidOperationException>();
        using (accessor.BeginScope(new() { TenantId = "A" }))
            await repo.SaveAsync(new() { Name = "same", Description = "A" });
        using (accessor.BeginScope(new() { TenantId = "B" }))
        {
            (await repo.GetAllAsync()).Should().BeEmpty();
            (await repo.GetByNameAsync("same")).Should().BeNull();
            await repo.SaveAsync(new() { Name = "same", Description = "B" });
            (await repo.GetByNameAsync("SAME"))!.Description.Should().Be("B");
            (await repo.DeactivateAsync("same")).Should().BeTrue();
        }
        using (accessor.BeginScope(new() { TenantId = "A" }))
        {
            (await repo.GetAllAsync()).Should().ContainSingle().Which.Description.Should().Be("A");
            (await repo.GetByNameAsync("same"))!.Description.Should().Be("A");
        }
        foreach (var operation in new Action[] { () => repo.SaveAsync(new() { Name = "x" }), () => repo.GetByNameAsync("x"), () => repo.DeactivateAsync("x") })
            operation.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData("Reader", false)]
    [InlineData("Editor", true)]
    [InlineData("Admin", true)]
    [InlineData("invalid", false)]
    public async Task RoomWrite_RequiresEditorOrAdminAndRealRoom(string role, bool expected)
    {
        using var db = NewDb();
        db.Add(new KnowledgeRoomEntity { Id = "room", TenantId = "A", Name = "Room" });
        db.Add(new KnowledgeRoomPermissionEntity { Id = "acl", TenantId = "A", RoomId = "room", UserId = "user", Role = role });
        await db.SaveChangesAsync();
        var store = new PostgresKnowledgeRoomStore(db);
        (await store.CanWriteRoomAsync("room", "A", "user")).Should().Be(expected);
        (await store.CanWriteRoomAsync("room", "B", "user")).Should().BeFalse();
        (await store.CanWriteRoomAsync("room", "A", "other")).Should().BeFalse();
        (await store.CanWriteRoomAsync("missing", "A", "user")).Should().BeFalse();
    }

    [Theory]
    [InlineData("Reader", false, false, false)]
    [InlineData("Editor", false, false, true)]
    [InlineData("Admin", true, false, false)]
    [InlineData("Editor", false, true, false)]
    public async Task SupportGrant_DoesNotReplaceWriteAclOrSurviveRevocation(string role, bool revoked, bool expired, bool expected)
    {
        using var db = NewDb();
        db.Add(new KnowledgeRoomEntity { Id = "room", TenantId = "A", Name = "Room" });
        db.Add(new TenantSupportGrantEntity { Id = "grant", TenantId = "A", UserId = "support", Scope = "room:room", ExpiresAt = DateTime.UtcNow.AddHours(expired ? -1 : 1), RevokedAt = revoked ? DateTime.UtcNow : null });
        await db.SaveChangesAsync();
        var store = new PostgresKnowledgeRoomStore(db);
        (await store.CanWriteRoomAsync("room", "A", "support")).Should().BeFalse();
        db.Add(new KnowledgeRoomPermissionEntity { Id = "grant", TenantId = "A", RoomId = "room", UserId = "support", Role = role });
        await db.SaveChangesAsync();
        (await store.CanWriteRoomAsync("room", "A", "support")).Should().Be(expected);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task VectorFallback_AllowListAndTenantAreBothEnforced(bool sqliteAdapter)
    {
        var factory = new TestFactory();
        IVectorStore store = sqliteAdapter
            ? new SqliteVectorStore(factory, NullLogger<SqliteVectorStore>.Instance)
            : new InMemoryVectorStore(NullLogger<InMemoryVectorStore>.Instance);
        await store.UpsertAsync(new() { Id = "allowed", TenantId = "A", Collection = "session", Content = "data", Metadata = new() { ["room_id"] = "one" } });
        await store.UpsertAsync(new() { Id = "other-room", TenantId = "A", Collection = "session", Content = "data", Metadata = new() { ["room_id"] = "two" } });
        if (!sqliteAdapter) await store.UpsertAsync(new() { Id = "other-tenant", TenantId = "B", Content = "data", Metadata = new() { ["room_id"] = "one" } });
        else
        {
            using var otherTenantDb = NewDb(factory.Database, "B");
            otherTenantDb.VectorDocuments.Add(new() { Id = "other-tenant", TenantId = "B", Content = "data", MetadataJson = "{\"room_id\":\"one\"}" });
            await otherTenantDb.SaveChangesAsync();
        }
        await store.UpsertAsync(new() { Id = "unassociated", TenantId = "A", Content = "data" });
        var found = await store.SearchWithFiltersAsync("*", new() { ["room_ids"] = " one,three ", ["tenant_id"] = "A" });
        found.Matches.Should().ContainSingle().Which.Id.Should().Be("allowed");
        (await store.SearchWithFiltersAsync("*", new() { ["room_ids"] = " , ", ["tenant_id"] = "A" })).Matches.Should().BeEmpty();
        (await store.SearchWithFiltersAsync("*", new() { ["room_ids"] = "missing", ["tenant_id"] = "A" })).Matches.Should().BeEmpty();
    }

    [Fact]
    public async Task Pinecone_UsesRoomInAndTenantEqAndEmptyMakesNoRequest()
    {
        var handler = new CaptureHandler();
        var store = new PineconeVectorStore(new HttpClient(handler), Options.Create(new PineconeSettings { Host = "https://pinecone.test", ApiKey = "fake" }), NullLogger<PineconeVectorStore>.Instance);
        await store.UpsertAsync(new() { Id = "doc", TenantId = "A", Metadata = new() { ["room_id"] = "one" } });
        using (var upsert = JsonDocument.Parse(handler.Body!))
        {
            upsert.RootElement.GetProperty("vectors")[0].GetProperty("metadata").GetProperty("room_id").GetString().Should().Be("one");
            upsert.RootElement.GetProperty("vectors")[0].GetProperty("metadata").GetProperty("tenant_id").GetString().Should().Be("A");
        }
        await store.SearchWithFiltersAsync("query", new() { ["room_ids"] = "one,two", ["tenant_id"] = "A" });
        using var body = JsonDocument.Parse(handler.Body!);
        var filter = body.RootElement.GetProperty("filter");
        filter.GetProperty("room_id").GetProperty("$in").GetArrayLength().Should().Be(2);
        filter.GetProperty("tenant_id").GetProperty("$eq").GetString().Should().Be("A");
        filter.TryGetProperty("room_ids", out _).Should().BeFalse();
        var count = handler.Count;
        (await store.SearchWithFiltersAsync("query", new() { ["room_ids"] = ", " })).Matches.Should().BeEmpty();
        handler.Count.Should().Be(count);
    }

    [Fact]
    public async Task InMemoryRooms_ReaderCannotWriteAndTenantAclCannotLeak()
    {
        var store = new InMemoryKnowledgeRoomStore();
        await store.CreateRoomAsync("A", "owner", new() { Id = "room" });
        await store.AddOrUpdatePermissionAsync("room", "reader", KnowledgeRoomRole.Reader, "A", "owner");
        (await store.GetRoomAsync("room", "A", "reader")).Should().NotBeNull();
        (await store.CanWriteRoomAsync("room", "A", "reader")).Should().BeFalse();
        (await store.CanWriteRoomAsync("room", "A", "owner")).Should().BeTrue();
        (await store.CanWriteRoomAsync("room", "B", "owner")).Should().BeFalse();
        await store.AddOrUpdatePermissionAsync("room", "reader", KnowledgeRoomRole.Editor, "A", "owner");
        (await store.CanWriteRoomAsync("room", "A", "reader")).Should().BeTrue();
    }

    private static AgenticDbContext NewDb(string? database = null, string tenantId = "A")
    {
        var tenant = Substitute.For<ITenantContextAccessor>();
        tenant.CurrentTenantId.Returns(tenantId);
        return new AgenticDbContext(new DbContextOptionsBuilder<AgenticDbContext>().UseInMemoryDatabase(database ?? Guid.NewGuid().ToString()).Options, tenant);
    }
    private sealed class TestFactory : IDbContextFactory<AgenticDbContext>
    {
        public string Database { get; } = Guid.NewGuid().ToString();
        public AgenticDbContext CreateDbContext() => NewDb(Database);
    }
    private sealed class CaptureHandler : HttpMessageHandler
    {
        public string? Body { get; private set; }
        public int Count { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Count++;
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new(HttpStatusCode.OK) { Content = new StringContent("{\"matches\":[]}", Encoding.UTF8, "application/json") };
        }
    }
}
