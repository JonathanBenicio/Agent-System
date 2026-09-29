using System.Text.Json;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;
using AgenticSystem.Infrastructure.Persistence;
using AgenticSystem.Infrastructure.Persistence.Entities;
using AgenticSystem.Infrastructure.AgentFramework;
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
var validationOutputDirectory = Environment.GetEnvironmentVariable("BACKEND_VALIDATION_OUTPUT_DIR")
    ?? Path.Combine(repositoryRoot, "tests", "TestResults", "backend-core-remediation", "current");
var coreResultsPath = Path.Combine(validationOutputDirectory, "core-results.json");
var historicalOutputDirectory = Path.Combine(repositoryRoot, "tests", "TestResults", "backend-documentation", "current");
if (Path.GetFullPath(validationOutputDirectory).Equals(Path.GetFullPath(historicalOutputDirectory), StringComparison.OrdinalIgnoreCase))
    throw new InvalidOperationException("Refusing to overwrite historical backend-documentation validation artifacts.");

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
    var outputFile = Path.Combine(validationOutputDirectory, "openapi-contracts.json");
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
if (args.Contains("--legacy-backfill"))
{
    var databaseName = "backfill_" + Guid.NewGuid().ToString("N")[..12];
    var adminConnection = new Npgsql.NpgsqlConnectionStringBuilder(connection) { Database = "postgres" };
    var fixtureConnection = new Npgsql.NpgsqlConnectionStringBuilder(connection) { Database = databaseName };
    await using (var admin = new Npgsql.NpgsqlConnection(adminConnection.ConnectionString))
    {
        await admin.OpenAsync();
        await using var create = admin.CreateCommand();
        create.CommandText = $"CREATE DATABASE \"{databaseName}\"";
        await create.ExecuteNonQueryAsync();
    }

    try
    {
        var legacyAccessor = new TenantContextAccessor();
        var legacyOptions = new DbContextOptionsBuilder<AgenticDbContext>()
            .UseNpgsql(fixtureConnection.ConnectionString, provider => provider.UseVector()).Options;
        var legacyFactory = new ValidationFactory(legacyOptions, legacyAccessor);
        var legacyTenant = "legacy-tenant";
        var legacyUser = "legacy-user";
        var legacyApiKeyId = Guid.NewGuid();
        using var legacyScope = legacyAccessor.BeginScope(new TenantContext { TenantId = legacyTenant });
        await using (var db = legacyFactory.CreateDbContext())
        {
            var migrations = db.Database.GetMigrations().ToList();
            if (migrations.Count < 2) throw new InvalidOperationException("Expected a pre-membership migration target.");
            await db.Database.MigrateAsync(migrations[^2]);
            db.Tenants.Add(new Tenant { Id = legacyTenant, Name = "Legacy fixture", Slug = legacyTenant, Limits = TenantLimits.FreeTier() });
            await db.SaveChangesAsync();
            db.RoleAssignments.Add(new RoleAssignmentEntity
            {
                Id = Guid.NewGuid().ToString("N"), UserId = legacyUser, RoleId = "Admin", TenantId = legacyTenant,
                GrantedBy = "legacy-issuer", GrantedAt = DateTime.UtcNow.AddDays(-30)
            });
            db.AccessApiKeys.Add(new AccessApiKeyEntity
            {
                Id = legacyApiKeyId, TenantId = legacyTenant, KeyHash = new string('a', 64), Name = "Legacy Viewer",
                Role = "Viewer", IsEnabled = true, CreatedAt = DateTime.UtcNow.AddDays(-20)
            });
            await db.SaveChangesAsync();
            await db.Database.MigrateAsync();
        }

        using (legacyAccessor.BeginScope(new TenantContext { TenantId = legacyTenant }))
        {
            await using var db = legacyFactory.CreateDbContext();
            var userMembership = await db.TenantMemberships.IgnoreQueryFilters()
                .SingleAsync(item => item.SubjectId == legacyUser && item.SubjectType == "User");
            var keyMembership = await db.TenantMemberships.IgnoreQueryFilters()
                .SingleAsync(item => item.SubjectId == legacyApiKeyId.ToString() && item.SubjectType == "ApiKey");
            if (userMembership.Role != "Admin" || userMembership.TenantId != legacyTenant || userMembership.GrantedBy != "legacy-issuer")
                throw new InvalidOperationException("Legacy user membership did not preserve the tenant, role, or grant provenance.");
            if (keyMembership.Role != "Viewer" || keyMembership.TenantId != legacyTenant)
                throw new InvalidOperationException("Legacy API key membership did not preserve the tenant and role.");
            if (await db.PlatformAdministrators.IgnoreQueryFilters().AnyAsync())
                throw new InvalidOperationException("Legacy tenant roles were incorrectly promoted to platform administrators.");
        }

        Directory.CreateDirectory(validationOutputDirectory);
        await File.WriteAllTextAsync(Path.Combine(validationOutputDirectory, "legacy-backfill.json"), JsonSerializer.Serialize(new
        {
            result = "passed",
            appliedFrom = "pre-membership migration",
            tenantId = legacyTenant,
            userRole = "Admin",
            apiKeyRole = "Viewer",
            platformAdministrators = 0
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("Legacy membership backfill fixture passed.");
    }
    finally
    {
        await using var admin = new Npgsql.NpgsqlConnection(adminConnection.ConnectionString);
        await admin.OpenAsync();
        await using var drop = admin.CreateCommand();
        drop.CommandText = $"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)";
        await drop.ExecuteNonQueryAsync();
    }

    return;
}
var accessor = new TenantContextAccessor();
var options = new DbContextOptionsBuilder<AgenticDbContext>().UseNpgsql(connection, o => o.UseVector()).Options;
var factory = new ValidationFactory(options, accessor);
if (args.Contains("--maf-session-fixture"))
{
    var directory = validationOutputDirectory;
    using var core = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(directory, "core-results.json")));
    var run = core.RootElement.GetProperty("run").GetString()!;
    var sessionId = core.RootElement.GetProperty("chatSessionId").GetString();
    if (string.IsNullOrWhiteSpace(sessionId)) throw new InvalidOperationException("No successful real chat session is recorded.");
    var tenantId = run + "-a";
    var userId = run + "-alice";
    using var tenantScope = accessor.BeginScope(new TenantContext { TenantId = tenantId });
    var store = new PostgresSessionStore(factory, NullLogger<PostgresSessionStore>.Instance);
    var session = await store.GetAsync(sessionId)
        ?? throw new InvalidOperationException("Real chat session was not persisted.");
    if (session.UserId != userId || session.TenantId != tenantId)
        throw new InvalidOperationException("Real chat session ownership does not match its JWT principal and tenant.");
    var frameworkState = session.RuntimeSettings.FirstOrDefault(item => item.Key.StartsWith("frameworkSessionState:", StringComparison.Ordinal));
    if (frameworkState.Value is not string stateJson || string.IsNullOrWhiteSpace(stateJson))
        throw new InvalidOperationException("MAF serialized state was not persisted with the real chat session.");
    var stateHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(stateJson))).ToLowerInvariant();
    await File.WriteAllTextAsync(Path.Combine(directory, "maf-session-fixture.json"), JsonSerializer.Serialize(new
    {
        baseline = core.RootElement.GetProperty("baseline").GetString(),
        run,
        sessionId,
        tenantId,
        userId,
        stateKey = frameworkState.Key,
        stateHash,
        stateLength = stateJson.Length
    }, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine("Real MAF serialized session fixture persisted.");
    return;
}
if (args.Contains("--maf-restore-verify"))
{
    var directory = validationOutputDirectory;
    using var fixture = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(directory, "maf-session-fixture.json")));
    using var core = JsonDocument.Parse(await File.ReadAllTextAsync(coreResultsPath));
    var run = core.RootElement.GetProperty("run").GetString()!;
    var sessionId = fixture.RootElement.GetProperty("sessionId").GetString()!;
    var tenantId = fixture.RootElement.GetProperty("tenantId").GetString()!;
    if (fixture.RootElement.GetProperty("run").GetString() != run ||
        fixture.RootElement.GetProperty("baseline").GetString() != core.RootElement.GetProperty("baseline").GetString())
        throw new InvalidOperationException("MAF fixture belongs to another validation run/revision.");

    using var tenantScope = accessor.BeginScope(new TenantContext { TenantId = tenantId });
    var store = new PostgresSessionStore(factory, NullLogger<PostgresSessionStore>.Instance);
    var session = await store.GetAsync(sessionId) ?? throw new InvalidOperationException("Resumed MAF session is missing.");
    if (!session.RuntimeSettings.TryGetValue("frameworkSessionRestoredAt:orchestrator", out var restoredAt))
        throw new InvalidOperationException("The restarted API did not successfully deserialize the MAF orchestrator state.");

    await File.WriteAllTextAsync(Path.Combine(directory, "maf-session-after-restore.json"), JsonSerializer.Serialize(new
    {
        result = "passed",
        sessionId,
        tenantId,
        restoredAt,
        stateLength = ((string)session.RuntimeSettings["frameworkSessionState:orchestrator"]).Length
    }, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine("MAF serialized session restore after API restart passed.");
    return;
}
if (args.Contains("--session-fixture"))
{
    // Synthetic known messages via the real store, independent of failed LLM conversations.
    var directory = validationOutputDirectory;
    using var core = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(directory, "core-results.json")));
    var run = core.RootElement.GetProperty("run").GetString()!;
    if (!System.Text.RegularExpressions.Regex.IsMatch(run, "^doc-[a-f0-9]{8}$")) throw new InvalidOperationException("Invalid diagnostic run.");
    var fixtureTenant = run + "-a";
    using var fixtureScope = accessor.BeginScope(new TenantContext { TenantId = fixtureTenant });
    await using var db = factory.CreateDbContext();
    if (!await db.Tenants.AnyAsync(t => t.Id == fixtureTenant)) throw new InvalidOperationException("Core diagnostic tenant missing.");
    var fixtureId = "persistence-" + Guid.NewGuid().ToString("N");
    var prompt = "Persistence fixture prompt " + run;
    var answer = "Persistence fixture answer " + run;
    var store = new PostgresSessionStore(factory, NullLogger<PostgresSessionStore>.Instance);
    await store.SaveAsync(new SessionData
    {
        Id = fixtureId, UserId = run + "-alice", TenantId = fixtureTenant, StartedAt = DateTime.UtcNow,
        Events = [new AgentEvent { SessionId = fixtureId, AgentName = "PersistenceFixture", UserInput = prompt, AgentResponse = answer }]
    });
    await File.WriteAllTextAsync(Path.Combine(directory, "session-fixture.json"), JsonSerializer.Serialize(new
    {
        run, baseline = core.RootElement.GetProperty("baseline").GetString(), sessionId = fixtureId, prompt, answer,
        context = "Synthetic known messages saved through real PostgreSQL session store; no successful LLM chat claimed."
    }, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine("Known-message persistence fixture saved.");
    return;
}
using var diagnosticRun = JsonDocument.Parse(await File.ReadAllTextAsync(coreResultsPath));
var sourceCommit = diagnosticRun.RootElement.GetProperty("baseline").GetString() ?? "unknown";
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
float[]? validationEmbedding = null;
await Test("STORE-01", "real pgvector semantic query excludes another tenant", async () =>
{
    validationEmbedding = (await generator.GenerateAsync("Authorized validation document.")).Vector.ToArray();
    await vector.UpsertAsync(new EmbeddingDocument { Id = tenant + "-authorized", TenantId = tenant, Content = "Authorized validation document.", Type = "document", Collection = tenant, Embedding = validationEmbedding, Metadata = new() { ["room_id"] = tenant + "-room" } });
    await vector.UpsertAsync(new EmbeddingDocument { Id = tenant + "-denied", TenantId = tenant, Content = "Unauthorized validation document.", Type = "document", Collection = tenant, Embedding = validationEmbedding, Metadata = new() { ["roomId"] = tenant + "-private" } });
    using (accessor.BeginScope(new TenantContext { TenantId = otherTenant }))
        await vector.UpsertAsync(new EmbeddingDocument { Id = otherTenant + "-document", TenantId = otherTenant, Content = "Authorized validation document.", Type = "document", Collection = tenant, Embedding = validationEmbedding });
    var found = await vector.SearchWithFiltersAsync("Authorized validation document.", new() { ["collection"] = tenant });
    Check(found.Matches.Count > 0 && found.Matches.All(m => !m.Id.StartsWith(otherTenant)), "cross-tenant match or empty result");
    return $"matches={found.Matches.Count}; foreign tenant excluded; embeddings real";
});
await Test("STORE-02", "SQL room prefilter returns only allowed room", async () =>
{
    Check(validationEmbedding is not null, "STORE-01 embedding unavailable");
    for (var index = 0; index < 60; index++)
    {
        await vector.UpsertAsync(new EmbeddingDocument
        {
            Id = $"{tenant}-denied-{index:D2}", TenantId = tenant, Content = "Authorized validation document.",
            Type = "document", Collection = tenant, Embedding = validationEmbedding!,
            Metadata = new() { ["room_id"] = $"{tenant}-forbidden-{index:D2}" }
        });
    }
    var found = await vector.SearchWithFiltersAsync("Authorized validation document.", new() { ["collection"] = tenant, ["room_ids"] = tenant + "-room" });
    Check(found.Matches.Count == 1 && found.Matches[0].Id == tenant + "-authorized", "unexpected room matches=" + found.Matches.Count);
    return "one authorized room match among 61 same-tenant room-tagged candidates";
});
await Test("STORE-03", "empty allowed-room list fails closed", async () =>
{
    var found = await vector.SearchWithFiltersAsync("Authorized validation document.", new() { ["collection"] = tenant, ["room_ids"] = "" });
    Check(found.Matches.Count == 0, "empty room_ids returned matches=" + found.Matches.Count);
    return "zero matches";
});
await Test("SKILL-01", "defaults are tenant-scoped, stable, and preserve customized legacy entries", async () =>
{
    var source = new DbAgentSkillsSource(factory, NullLogger<DbAgentSkillsSource>.Instance);
    using (accessor.BeginScope(new TenantContext { TenantId = tenant }))
    {
        await using var db = factory.CreateDbContext();
        db.AgentSkills.Add(new DbSkillEntity
        {
            Id = tenant + "-custom-coding", TenantId = tenant, Name = "Coding Assistant", Domain = "work",
            Type = "Instruction", SystemPromptFragment = "Customized tenant instruction", IsSystem = true
        });
        await db.SaveChangesAsync();

        var first = (await source.LoadSkillsAsync()).ToList();
        var second = (await source.LoadSkillsAsync()).ToList();
        Check(first.Count == 4 && second.Count == 4, $"first={first.Count} second={second.Count}");
        Check(first.Select(skill => skill.Id).Order().SequenceEqual(second.Select(skill => skill.Id).Order()), "skill IDs changed between loads");
        Check(first.Any(skill => skill.Id == tenant + "-custom-coding"), "customized legacy skill was replaced");
    }

    using (accessor.BeginScope(new TenantContext { TenantId = otherTenant }))
    {
        var otherSkills = (await source.LoadSkillsAsync()).ToList();
        Check(otherSkills.Count == 4, $"other tenant defaults={otherSkills.Count}");
        Check(otherSkills.All(skill => !skill.Id.Contains(tenant, StringComparison.Ordinal)), "skill ID leaked another tenant identifier");
    }

    var raceTenant = tenant + "-race";
    await using (var db = factory.CreateDbContext())
    {
        db.Tenants.Add(new Tenant { Id = raceTenant, Name = "Skills race validation", Slug = raceTenant, Limits = TenantLimits.FreeTier() });
        await db.SaveChangesAsync();
    }
    using (accessor.BeginScope(new TenantContext { TenantId = raceTenant }))
    {
        var concurrent = await Task.WhenAll(source.LoadSkillsAsync(), source.LoadSkillsAsync());
        Check(concurrent.All(skills => skills.Count() == 4), "concurrent seeding returned an incomplete catalog");
    }
    return "customized ID retained; defaults stable and isolated; concurrent catalog has 4 entries";
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
await Test("QUOTA-05", "saved quota cannot exceed the tenant plan ceiling", async () =>
{
    await repository.UpsertConfigAsync(tenant, new QuotaConfig
    {
        OwnerId = tenant,
        RequestsPerMinute = 1000,
        MaxTokensPerDay = 1_000_000,
        MaxDailyBudgetUsd = 1000
    });
    using (accessor.BeginScope(new TenantContext
    {
        TenantId = tenant,
        Plan = TenantPlan.Free,
        Limits = TenantLimits.EnterpriseTier()
    }))
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var enforcer = new QuotaEnforcer(cache, repository, NullLogger<QuotaEnforcer>.Instance, accessor);
        var result = await enforcer.CheckQuotaAsync(tenant, estimatedTokens: TenantLimits.FreeTier().MaxTokensPerDay + 1);
        Check(!result.Allowed && result.DenialReason == "Daily token quota exceeded", "plan ceiling did not deny overage");
    }
    return "configured 1,000,000-token quota remained capped at Free plan limit";
});
await Test("QUOTA-06", "two independent repository and context-factory instances account concurrent updates", async () =>
{
    var multiTenant = tenant + "-multi-instance";
    using (accessor.BeginScope(new TenantContext { TenantId = multiTenant }))
    {
        await using var db = factory.CreateDbContext();
        db.Tenants.Add(new Tenant { Id = multiTenant, Name = "Multi-instance quota fixture", Slug = multiTenant, Limits = TenantLimits.FreeTier() });
        await db.SaveChangesAsync();
    }

    var firstInstance = new TenantQuotaRepository(new ValidationFactory(options, accessor), NullLogger<TenantQuotaRepository>.Instance);
    var secondInstance = new TenantQuotaRepository(new ValidationFactory(options, accessor), NullLogger<TenantQuotaRepository>.Instance);
    await Task.WhenAll(Enumerable.Range(0, 32).Select(index =>
        (index % 2 == 0 ? firstInstance : secondInstance).IncrementUsageAsync(multiTenant, 1, 0.01)));
    var snapshot = await new TenantQuotaRepository(new ValidationFactory(options, accessor), NullLogger<TenantQuotaRepository>.Instance)
        .GetOrCreateAsync(multiTenant);
    Check(snapshot.CurrentDailyTokens == 32 && snapshot.CurrentDailyRequests == 32 && Math.Abs(snapshot.CurrentDailyCostUsd - 0.32) < 0.0001,
        $"tokens={snapshot.CurrentDailyTokens} requests={snapshot.CurrentDailyRequests} cost={snapshot.CurrentDailyCostUsd}");
    return "32 updates from two independent repository/context-factory instances persisted exactly once";
});
var output = Path.Combine(validationOutputDirectory, "store-results.json");
Directory.CreateDirectory(validationOutputDirectory);
await File.WriteAllTextAsync(output, JsonSerializer.Serialize(new { baseline = sourceCommit, services = "real PostgreSQL/pgvector and Ollama embeddings", results }, new JsonSerializerOptions { WriteIndented = true }));
Environment.ExitCode = results.Any(r => JsonSerializer.Serialize(r).Contains("\"failed\"")) ? 1 : 0;

sealed class ValidationFactory(DbContextOptions<AgenticDbContext> options, ITenantContextAccessor accessor) : IDbContextFactory<AgenticDbContext>
{
    public AgenticDbContext CreateDbContext() => new(options, accessor);
    public Task<AgenticDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult(CreateDbContext());
}
