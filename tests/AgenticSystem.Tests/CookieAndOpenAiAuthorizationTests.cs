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

namespace AgenticSystem.Tests;

public sealed class CookieAndOpenAiAuthorizationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OpenAi_KeyMembershipRevoked_DeniesBeforeOrchestration(bool bearer)
    {
        await using var app = await AuthFixture.CreateAsync();
        app.SetAuthorization(bearer);
        (await app.ChatAsync()).StatusCode.Should().Be(HttpStatusCode.OK);
        await app.Orchestrator.Received(1).ExecuteAsync("client-correlation", "hello",
            Arg.Is<UserContext>(user => user.UserId == app.KeyId && user.TenantId == AuthFixture.TenantId),
            Arg.Any<CancellationToken>());

        await app.RevokeMembershipAsync();
        (await app.ChatAsync()).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await app.Orchestrator.Received(1).ExecuteAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<UserContext>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OpenAi_InactiveTenant_DeniesBeforeOrchestration(bool bearer)
    {
        await using var app = await AuthFixture.CreateAsync(active: false);
        app.SetAuthorization(bearer);
        (await app.ChatAsync()).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await app.Orchestrator.DidNotReceiveWithAnyArgs().ExecuteAsync(default!, default!, default!, default);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OpenAi_TenantSpoof_DeniesBeforeOrchestration(bool bearer)
    {
        await using var app = await AuthFixture.CreateAsync();
        app.SetAuthorization(bearer);
        app.Client.DefaultRequestHeaders.Add("X-Tenant-Id", "review-b");
        (await app.ChatAsync()).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await app.Orchestrator.DidNotReceiveWithAnyArgs().ExecuteAsync(default!, default!, default!, default);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OpenAi_MembershipInAnotherTenant_DoesNotAuthorizeKeyTenant(bool bearer)
    {
        await using var app = await AuthFixture.CreateAsync(membershipTenant: "review-b");
        app.SetAuthorization(bearer);
        (await app.ChatAsync()).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await app.Orchestrator.DidNotReceiveWithAnyArgs().ExecuteAsync(default!, default!, default!, default);
    }

    [Fact]
    public async Task Cookie_LoginRestoreLogout_PreservesMembershipRoleAndNeverReturnsKey()
    {
        await using var app = await AuthFixture.CreateAsync();
        var login = await app.Client.PostAsJsonAsync("/api/auth/login", new { apiKey = AuthFixture.Key });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        var cookie = login.Headers.GetValues("Set-Cookie").Single();
        cookie.Should().Contain("httponly").And.Contain("secure").And.Contain("samesite=strict");
        (await login.Content.ReadAsStringAsync()).Should().NotContain(AuthFixture.Key);
        app.Client.DefaultRequestHeaders.Add("Cookie", cookie.Split(';')[0]);

        var session = await app.Client.GetAsync("/api/auth/session");
        session.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await session.Content.ReadAsStringAsync();
        body.Should().NotContain(AuthFixture.Key);
        using var identity = JsonDocument.Parse(body);
        identity.RootElement.GetProperty("userId").GetString().Should().Be(app.KeyId);
        identity.RootElement.GetProperty("roles")[0].GetString().Should().Be("Viewer");

        var logout = await app.Client.PostAsync("/api/auth/logout", null);
        logout.StatusCode.Should().Be(HttpStatusCode.OK);
        logout.Headers.GetValues("Set-Cookie").Single().Should().Contain("expires=");
        app.Client.DefaultRequestHeaders.Remove("Cookie");
        (await app.Client.GetAsync("/api/auth/session")).IsSuccessStatusCode.Should().BeFalse();
    }

    [Fact]
    public async Task Cookie_RevokedMembership_CanClearCookieButCannotLoginAgain()
    {
        await using var app = await AuthFixture.CreateAsync();
        app.Client.DefaultRequestHeaders.Add("Cookie", $"agentic_api_key={AuthFixture.Key}");
        await app.RevokeMembershipAsync();
        (await app.Client.GetAsync("/api/auth/session")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var logout = await app.Client.PostAsync("/api/auth/logout", null);
        logout.StatusCode.Should().Be(HttpStatusCode.OK);
        logout.Headers.GetValues("Set-Cookie").Single().Should().Contain("expires=");
        var login = await app.Client.PostAsJsonAsync("/api/auth/login", new { apiKey = AuthFixture.Key });
        login.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        login.Headers.Contains("Set-Cookie").Should().BeFalse();
    }

    private sealed class AuthFixture : IAsyncDisposable
    {
        public const string TenantId = "review-a";
        public const string Key = "review-regression-synthetic-api-key";
        private readonly WebApplication _application;
        private readonly TenantContextAccessor _tenant;
        public HttpClient Client { get; private set; } = null!;
        public string KeyId { get; } = Guid.NewGuid().ToString();
        public IFrameworkOrchestratorService Orchestrator { get; }

        private AuthFixture()
        {
            _tenant = new TenantContextAccessor();
            Orchestrator = Substitute.For<IFrameworkOrchestratorService>();
            Orchestrator.ExecuteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<UserContext>(),
                    Arg.Any<CancellationToken>())
                .Returns(_ =>
                {
                    _tenant.CurrentContext!.Plan.Should().Be(TenantPlan.Pro);
                    return Task.FromResult(new AgentResponse { Content = "allowed", AgentName = "review-agent" });
                });
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
            services.AddSingleton(Orchestrator);
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

        public static async Task<AuthFixture> CreateAsync(bool active = true, string membershipTenant = TenantId)
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
                    TenantId = membershipTenant, SubjectId = app.KeyId, SubjectType = "ApiKey", Role = "Viewer"
                });
                await db.SaveChangesAsync();
            }
            return app;
        }

        public void SetAuthorization(bool bearer) =>
            Client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", bearer ? $"Bearer {Key}" : Key);

        public async Task<HttpResponseMessage> ChatAsync()
        {
            var response = await Client.PostAsJsonAsync("/v1/chat/completions",
                new { model = "agentic-system", user = "client-correlation", messages = new[] { new { role = "user", content = "hello" } } });
            if (response.StatusCode == HttpStatusCode.InternalServerError)
            {
                var detail = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException(detail[..Math.Min(detail.Length, 1000)]);
            }
            return response;
        }

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
