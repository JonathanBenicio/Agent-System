using System.Text.Json;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;
using AgenticSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Caching.Memory;
using AgenticSystem.Api.Extensions;
using AgenticSystem.Api.Controllers;
using Swashbuckle.AspNetCore.Swagger;
using System.Text.Json.Serialization;

var repositoryRoot = AppContext.BaseDirectory;
while (!Directory.Exists(Path.Combine(repositoryRoot, "src", "AgenticSystem.Api")))
    repositoryRoot = Directory.GetParent(repositoryRoot)?.FullName ?? throw new InvalidOperationException("Repository root not found.");

if (args.Contains("--openapi"))
{
    // Inspect production MVC/Swagger registrations without starting the API or loading its user secrets.
    var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Validation" });
    builder.Services.AddControllers().AddApplicationPart(typeof(ChatController).Assembly).AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });
    builder.Services.AddApiSwagger();
    await using var app = builder.Build();
    var document = app.Services.GetRequiredService<ISwaggerProvider>().GetSwagger("v1");
    var contracts = document.Paths.Where(p => p.Value.Operations is not null).SelectMany(p => p.Value.Operations!.Select(o => new
    {
        method = o.Key.ToString().ToUpperInvariant(), path = p.Key,
        responses = o.Value.Responses?.Keys,
        parameters = o.Value.Parameters?.Select(x => new { x.Name, x.In, x.Required }),
        requestContentTypes = o.Value.RequestBody?.Content?.Keys
    })).OrderBy(x => x.path).ThenBy(x => x.method).ToList();
    var outputFile = Path.Combine(repositoryRoot, "tests", "TestResults", "backend-documentation", "openapi-contracts.json");
    Directory.CreateDirectory(Path.GetDirectoryName(outputFile)!);
    await File.WriteAllTextAsync(outputFile, JsonSerializer.Serialize(new { scope = "MVC/Swagger production registrations; not full running API pipeline", contracts }, new JsonSerializerOptions { WriteIndented = true }));
    using var schemaText = new StringWriter(System.Globalization.CultureInfo.InvariantCulture);
    document.SerializeAsV3(new Microsoft.OpenApi.OpenApiJsonWriter(schemaText));
    await File.WriteAllTextAsync(Path.Combine(Path.GetDirectoryName(outputFile)!, "openapi-release.json"), schemaText.ToString());
    Console.WriteLine($"OpenAPI controller contracts: {contracts.Count}");
    return;
}

// Deliberately fixed isolated database. No production connection string accepted.
const string connection = "Host=127.0.0.1;Port=55432;Database=backend_validation;Username=validation;Password=validation_local_only";
var accessor = new TenantContextAccessor();
var options = new DbContextOptionsBuilder<AgenticDbContext>().UseNpgsql(connection, o => o.UseVector()).Options;
var factory = new ValidationFactory(options, accessor);
var tenant = "store-" + Guid.NewGuid().ToString("N")[..8];
var otherTenant = tenant + "-other";
var results = new List<object>();
async Task Test(string id, string criterion, Func<Task<string>> operation)
{
    try { var detail = await operation(); results.Add(new { id, criterion, result = "passed", detail }); Console.WriteLine($"{id}: passed — {detail}"); }
    catch (Exception ex) { var detail = ex.GetType().Name + ": " + ex.Message; results.Add(new { id, criterion, result = "failed", detail }); Console.WriteLine($"{id}: failed — {detail}"); }
}
void Check(bool valid, string message) { if (!valid) throw new InvalidOperationException(message); }
using var scope = accessor.BeginScope(new TenantContext { TenantId = tenant });
await using (var db = factory.CreateDbContext())
{
    db.Tenants.Add(new Tenant { Id = tenant, Name = "Store validation", Slug = tenant, Limits = TenantLimits.FreeTier() });
    db.Tenants.Add(new Tenant { Id = otherTenant, Name = "Other validation", Slug = otherTenant, Limits = TenantLimits.FreeTier() });
    await db.SaveChangesAsync();
}
using var generator = new OllamaEmbeddingGenerator(new Uri("http://127.0.0.1:11435"), "nomic-embed-text");
var vector = new PostgresVectorStore(factory, NullLogger<PostgresVectorStore>.Instance, generator);
await Test("STORE-01", "real pgvector semantic query excludes another tenant", async () =>
{
    var embedding = (await generator.GenerateAsync("Authorized validation document.")).Vector.ToArray();
    await vector.UpsertAsync(new EmbeddingDocument { Id = tenant + "-authorized", TenantId = tenant, Content = "Authorized validation document.", Type = "document", Collection = tenant, Embedding = embedding, Metadata = new() { ["room_id"] = tenant + "-room" } });
    await vector.UpsertAsync(new EmbeddingDocument { Id = tenant + "-denied", TenantId = tenant, Content = "Unauthorized validation document.", Type = "document", Collection = tenant, Embedding = embedding, Metadata = new() { ["roomId"] = tenant + "-private" } });
    using (accessor.BeginScope(new TenantContext { TenantId = otherTenant }))
        await vector.UpsertAsync(new EmbeddingDocument { Id = otherTenant + "-document", TenantId = otherTenant, Content = "Authorized validation document.", Type = "document", Collection = tenant, Embedding = embedding });
    var found = await vector.SearchWithFiltersAsync("Authorized validation document.", new() { ["collection"] = tenant });
    Check(found.Matches.Count > 0 && found.Matches.All(m => !m.Id.StartsWith(otherTenant)), "cross-tenant match or empty result");
    return $"matches={found.Matches.Count}; foreign tenant excluded; embeddings real";
});
await Test("STORE-02", "SQL room prefilter returns only allowed room", async () =>
{
    var found = await vector.SearchWithFiltersAsync("Authorized validation document.", new() { ["collection"] = tenant, ["room_ids"] = tenant + "-room" });
    Check(found.Matches.Count == 1 && found.Matches[0].Id == tenant + "-authorized", "unexpected room matches=" + found.Matches.Count);
    return "one authorized room match";
});
await Test("STORE-03", "empty allowed-room list fails closed", async () =>
{
    var found = await vector.SearchWithFiltersAsync("Authorized validation document.", new() { ["collection"] = tenant, ["room_ids"] = "" });
    Check(found.Matches.Count == 0, "empty room_ids returned matches=" + found.Matches.Count);
    return "zero matches";
});
var repository = new TenantQuotaRepository(factory, NullLogger<TenantQuotaRepository>.Instance);
await Test("QUOTA-01", "daily counters survive new repository/context", async () =>
{
    await repository.GetOrCreateAsync(tenant);
    await repository.IncrementUsageAsync(tenant, 10, 0.01);
    var reloaded = await new TenantQuotaRepository(factory, NullLogger<TenantQuotaRepository>.Instance).GetOrCreateAsync(tenant);
    Check(reloaded.CurrentDailyTokens == 10 && reloaded.CurrentDailyRequests == 1, "counters not persisted");
    return "tokens=10 requests=1 from new repository";
});
await Test("QUOTA-02", "eight concurrent increments either accounted or explicit failures", async () =>
{
    var failures = 0;
    await Task.WhenAll(Enumerable.Range(0, 8).Select(async _ => { try { await repository.IncrementUsageAsync(tenant, 1, 0); } catch { Interlocked.Increment(ref failures); } }));
    var snapshot = await repository.GetOrCreateAsync(tenant);
    Check(failures == 0 && snapshot.CurrentDailyTokens == 18 && snapshot.CurrentDailyRequests == 9, $"failures={failures} tokens={snapshot.CurrentDailyTokens} requests={snapshot.CurrentDailyRequests}");
    return "all eight increments persisted";
});
await Test("QUOTA-03", "persisted token threshold blocks request", async () =>
{
    await repository.UpsertConfigAsync(tenant, new QuotaConfig { OwnerId = tenant, MaxTokensPerDay = 1, MaxDailyBudgetUsd = 50 });
    using var cache = new MemoryCache(new MemoryCacheOptions());
    var enforcer = new QuotaEnforcer(cache, repository, NullLogger<QuotaEnforcer>.Instance);
    var allowed = await enforcer.CheckQuotaAsync(tenant);
    Check(!allowed.Allowed, "over-limit tenant allowed");
    return "denied after persisted token threshold";
});
await Test("QUOTA-04", "daily reset persists zero counters", async () =>
{
    await using (var db = factory.CreateDbContext())
    {
        var quota = await db.TenantQuotas.SingleAsync(q => q.TenantId == tenant);
        quota.LastResetAt = DateTime.UtcNow.Date.AddDays(-1);
        await db.SaveChangesAsync();
    }
    await repository.ResetDailyCountersAsync();
    await using var checkDb = factory.CreateDbContext();
    var reset = await checkDb.TenantQuotas.AsNoTracking().SingleAsync(q => q.TenantId == tenant);
    Check(reset.CurrentDailyTokens == 0 && reset.CurrentDailyRequests == 0 && reset.LastResetAt.Date == DateTime.UtcNow.Date, "reset not persisted");
    return "persisted counters=0 and current UTC reset date";
});
var output = Path.Combine(repositoryRoot, "tests", "TestResults", "backend-documentation", "store-results.json");
Directory.CreateDirectory(Path.GetDirectoryName(output)!);
await File.WriteAllTextAsync(output, JsonSerializer.Serialize(new { baseline = "f8de7a6", services = "real PostgreSQL/pgvector and Ollama embeddings", results }, new JsonSerializerOptions { WriteIndented = true }));
Environment.ExitCode = results.Any(r => JsonSerializer.Serialize(r).Contains("\"failed\"")) ? 1 : 0;

sealed class ValidationFactory(DbContextOptions<AgenticDbContext> options, ITenantContextAccessor accessor) : IDbContextFactory<AgenticDbContext>
{
    public AgenticDbContext CreateDbContext() => new(options, accessor);
    public Task<AgenticDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult(CreateDbContext());
}
