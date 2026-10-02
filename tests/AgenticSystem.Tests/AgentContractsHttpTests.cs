using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using AgenticSystem.Api.Controllers;
using AgenticSystem.Api.Extensions;
using AgenticSystem.Api.Middleware;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;
using AgenticSystem.Infrastructure.Persistence;
using AgenticSystem.Infrastructure.Persistence.Entities;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using AgenticSystem.Infrastructure.AgentFramework;
using Microsoft.Extensions.Logging;
namespace AgenticSystem.Tests;

public sealed class AgentContractsHttpTests
{
    [Fact]
    public async Task ToolRoutesDispatchListGetExecuteDeleteToManager()
    {
        await using var app = await AuthFixture.CreateAsync();
        app.SetAuthorization(true);
        var tool = Substitute.For<ITool>();
        tool.Id.Returns("contract-tool"); tool.Name.Returns("Contract Tool");
        app.Tools.GetAvailableToolsAsync(null).Returns([tool]);
        app.Tools.GetTool("contract-tool").Returns(tool);
        app.Tools.GetTool("missing").Returns((ITool?)null);
        app.Tools.ExecuteToolAsync("contract-tool", Arg.Any<ToolInput>(), Arg.Any<CancellationToken>())
            .Returns(ToolResult.Ok("executed"));
        app.Tools.UnregisterTool("contract-tool").Returns(true);
        (await app.Client.GetAsync("/api/agent/tools")).StatusCode.Should().Be(HttpStatusCode.OK);
        var get = await app.Client.GetAsync("/api/agent/tools/contract-tool");
        get.StatusCode.Should().Be(HttpStatusCode.OK);
        (await get.Content.ReadAsStringAsync()).Should().Contain("Contract Tool");
        var executed = await app.Client.PostAsJsonAsync("/api/agent/tools/contract-tool/execute", new ToolInput { Action = "inspect", UserId = "spoofed" });
        executed.StatusCode.Should().Be(HttpStatusCode.OK);
        (await executed.Content.ReadAsStringAsync()).Should().Contain("executed");
        (await app.Client.DeleteAsync("/api/agent/tools/contract-tool")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        await app.Tools.Received(1).GetAvailableToolsAsync(null);
        app.Tools.Received(1).GetTool("contract-tool");
        await app.Tools.Received(1).ExecuteToolAsync("contract-tool", Arg.Is<ToolInput>(i => i.Action == "inspect" && i.UserId == app.KeyId), Arg.Any<CancellationToken>());
        app.Tools.Received(1).UnregisterTool("contract-tool");
        (await app.Client.GetAsync("/api/agent/tools/missing")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("GET", "/api/agent/tools")]
    [InlineData("GET", "/api/agent/tools/contract-tool")]
    [InlineData("POST", "/api/agent/tools/contract-tool/execute")]
    [InlineData("DELETE", "/api/agent/tools/contract-tool")]
    public async Task ToolRoutesRejectRevokedMembershipBeforeManager(string method, string path)
    {
        await using var app = await AuthFixture.CreateAsync();
        app.SetAuthorization(true);
        await app.RevokeMembershipAsync();
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method == "POST") request.Content = JsonContent.Create(new ToolInput { Action = "inspect" });
        (await app.Client.SendAsync(request)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        app.Tools.ReceivedCalls().Should().BeEmpty();
    }

    [Theory]
    [InlineData("POST", "/api/agent/tools/contract-tool/execute")]
    [InlineData("DELETE", "/api/agent/tools/contract-tool")]
    public async Task ViewerCannotWriteTools(string method, string path)
    {
        await using var app = await AuthFixture.CreateAsync(role: "Viewer");
        app.SetAuthorization(true);
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method == "POST") request.Content = JsonContent.Create(new ToolInput { Action = "inspect" });
        (await app.Client.SendAsync(request)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        app.Tools.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task YamlSaveReopenAndVisualUpdatePreserveSpecificationInRealRepository()
    {
        await using var app = await AuthFixture.CreateAsync();
        app.SetAuthorization(true);
        var yaml = """
            metadata:
              name: contract-agent
              description: 'Quoted: "review" # safe'
              tier: Specialist
              domain: finance
            execution:
              autonomyLevel: Assisted
              maxConcurrency: 7
              timeoutSeconds: 123
            abilities:
              capabilities: ["code-review", "document-analysis"]
              allowedTools: ["contract-tool"]
            instructions: |-2
              line one: "quoted" # literal
                indented C:\folder
              line three
            configuration: {custom: retained}
            """;
        var parsed = await app.Client.PostAsJsonAsync("/api/agent/agents/validate-yaml", new { yaml });
        parsed.StatusCode.Should().Be(HttpStatusCode.OK);
        var validation = await parsed.Content.ReadFromJsonAsync<YamlValidationResult>();
        validation!.IsValid.Should().BeTrue(string.Join(";", validation.Errors.Select(e => e.Message)));
        var saved = await app.Client.PostAsJsonAsync("/api/agent/agents/save-yaml", new { yaml });
        saved.StatusCode.Should().Be(HttpStatusCode.OK);
        var reopened = await app.Client.GetFromJsonAsync<AgentInfo>("/api/agent/agents/contract-agent");
        reopened!.Description.Should().Be("Quoted: \"review\" # safe");
        reopened.Domain.Should().Be("finance");
        reopened.Tier.Should().Be(AgentTier.Specialist);
        reopened.AutonomyLevel.Should().Be(AutonomyLevel.Assisted);
        reopened.Instructions.Should().Be(validation.Specification!.Instructions);
        reopened.Capabilities.Should().Equal("code-review", "document-analysis");
        reopened.AvailableTools.Should().Equal("contract-tool");
        reopened.Configuration["maxConcurrency"].ToString().Should().Be("7");
        reopened.Configuration["timeoutSeconds"].ToString().Should().Be("123");
        reopened.Configuration["custom"].ToString().Should().Be("retained");
        var update = validation.Specification;
        update.Instructions = "Updated prompt\nsecond line";
        update.Configuration["timeoutSeconds"] = 456;
        (await app.Client.PutAsJsonAsync("/api/agent/agents/contract-agent", update)).StatusCode.Should().Be(HttpStatusCode.OK);
        var after = await app.Client.GetFromJsonAsync<AgentInfo>("/api/agent/agents/contract-agent");
        after!.Instructions.Should().Be(update.Instructions);
        after.Configuration["timeoutSeconds"].ToString().Should().Be("456");
        after.Capabilities.Should().Equal(update.Capabilities);
    }

    [Fact]
    public async Task PublishedTemplatePassesRealYamlValidator()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../templates/agent-manifest-template.yaml"));
        var result = await new AgentYamlValidator().ValidateAsync(await File.ReadAllTextAsync(path));
        result.IsValid.Should().BeTrue(string.Join(";", result.Errors.Select(e => e.Message)));
        result.Specification!.Name.Should().Be("security-auditor");
        result.Specification.Instructions.Should().Contain("OWASP");
        result.Specification.Capabilities.Should().Contain("code-review");
    }
    private sealed class AuthFixture : IAsyncDisposable
    {
        public const string TenantId = "review-a";
        public const string Key = "review-regression-synthetic-api-key";
        private readonly WebApplication _application;
        private readonly TenantContextAccessor _tenant;
        public HttpClient Client { get; private set; } = null!;
        public string KeyId { get; } = Guid.NewGuid().ToString();
        public IToolManager Tools { get; } = Substitute.For<IToolManager>();
        public IDynamicAgentRepository Repository { get; private set; } = null!;

        private AuthFixture()
        {
            _tenant = new TenantContextAccessor();
            var dbName = $"review-auth-{Guid.NewGuid():N}";
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = "Development",
                ApplicationName = typeof(AuthController).Assembly.GetName().Name
            });
            builder.Configuration.Sources.Clear();
            builder.WebHost.UseTestServer();
            var services = builder.Services;
            services.AddSingleton<ITenantContextAccessor>(_tenant);
            services.AddSingleton<ISystemOperationContextAccessor, SystemOperationContextAccessor>();
            services.AddDbContextFactory<AgenticDbContext>(options => options.UseInMemoryDatabase(dbName));
            services.AddScoped<ITenantStore, EfTenantStore>();
            services.AddScoped<ITenantResolver, TenantResolver>();
            services.AddScoped<IPermissionService, PostgresPermissionService>();
            services.AddSingleton(Substitute.For<IQuotaEnforcer>());
            services.AddSingleton(Tools);
            services.AddSingleton(Substitute.For<ISkillManager>());
            services.AddSingleton(Substitute.For<IMetaAgent>());
            var isolation = Substitute.For<ITenantIsolationEnforcer>();
            isolation.CanCreateAgentAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
            services.AddSingleton(isolation);
            Repository = new InMemoryDynamicAgentRepository(_tenant);
            services.AddSingleton(Repository);
            services.AddScoped<IAgentFactory, HierarchicalAgentFactory>();
            services.AddScoped<IAgentYamlValidator, AgentYamlValidator>();
            services.AddScoped<IAgentConfigurationService, AgentConfigurationService>();
            services.AddApiSecurity(builder.Configuration, builder.Environment);
            services.AddControllers().AddApplicationPart(typeof(AuthController).Assembly);
            services.AddRateLimiter(options => options.AddPolicy("ProtocolEndpoints",
                _ => RateLimitPartition.GetNoLimiter("review-auth")));
            _application = builder.Build();
            _application.UseRouting();
            _application.UseAuthentication();
            _application.UseMiddleware<TenantMiddleware>();
            _application.UseAuthorization();
            _application.UseRateLimiter();
            _application.MapControllers();
        }

        public static async Task<AuthFixture> CreateAsync(bool active = true, string membershipTenant = TenantId, string role = "Owner")
        {
            var app = new AuthFixture();
            await app._application.StartAsync();
            app.Client = app._application.GetTestClient();
            await using var db = app._application.Services.GetRequiredService<IDbContextFactory<AgenticDbContext>>()
                .CreateDbContext();
            await db.Database.EnsureCreatedAsync();
            db.Tenants.Add(new Tenant
            {
                Id = TenantId, Name = "Review A", Slug = TenantId, IsActive = active,
                Plan = TenantPlan.Pro, Limits = TenantLimits.ProTier()
            });
            db.Tenants.Add(new Tenant { Id = "review-b", Name = "Review B", Slug = "review-b" });
            using (app._tenant.BeginScope(new TenantContext { TenantId = TenantId }))
            {
                db.AccessApiKeys.Add(new AccessApiKeyEntity
                {
                    Id = Guid.Parse(app.KeyId), Name = "Review key", TenantId = TenantId, Role = "Admin",
                    IsEnabled = true, KeyHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Key))).ToLowerInvariant()
                });
                await db.SaveChangesAsync();
            }
            using (app._tenant.BeginScope(new TenantContext { TenantId = membershipTenant }))
            {
                db.TenantMemberships.Add(new TenantMembershipEntity
                {
                    TenantId = membershipTenant, SubjectId = app.KeyId, SubjectType = "ApiKey", Role = role
                });
                await db.SaveChangesAsync();
            }
            return app;
        }

        public void SetAuthorization(bool bearer) =>
            Client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", bearer ? $"Bearer {Key}" : Key);

        public async Task RevokeMembershipAsync()
        {
            using var scope = _tenant.BeginScope(new TenantContext { TenantId = TenantId });
            await using var db = _application.Services.GetRequiredService<IDbContextFactory<AgenticDbContext>>().CreateDbContext();
            db.TenantMemberships.Remove(await db.TenantMemberships.SingleAsync(item => item.SubjectId == KeyId));
            await db.SaveChangesAsync();
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await _application.DisposeAsync();
        }
    }
}
