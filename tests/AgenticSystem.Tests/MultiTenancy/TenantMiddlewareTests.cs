using System.Security.Claims;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;
using AgenticSystem.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace AgenticSystem.Tests.MultiTenancy;

public class TenantMiddlewareTests
{
    private readonly ITenantStore _store;
    private readonly ITenantResolver _resolver;
    private readonly ILogger<TenantMiddleware> _logger;

    public TenantMiddlewareTests()
    {
        _store = new InMemoryTenantStore();
        _resolver = new TenantResolver(_store, Substitute.For<ILogger<TenantResolver>>());
        _logger = Substitute.For<ILogger<TenantMiddleware>>();
    }

    private TenantMiddleware CreateMiddleware(RequestDelegate? next = null)
    {
        return new TenantMiddleware(next ?? (_ => Task.CompletedTask), _logger);
    }

    [Fact]
    public async Task InvokeAsync_WithTenantHeader_PopulatesTenantContext()
    {
        var middleware = CreateMiddleware();
        var tenantContext = new TenantContext();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers[TenantMiddleware.TenantIdHeaderName] = Tenant.DefaultTenantId;

        await middleware.InvokeAsync(httpContext, tenantContext, _resolver);

        tenantContext.TenantId.Should().Be(Tenant.DefaultTenantId);
        tenantContext.IsAuthenticated.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_WithJwtClaim_PopulatesTenantContext()
    {
        // Add a tenant first
        var tenant = new Tenant
        {
            Id = "jwt-tenant",
            Name = "JWT Corp",
            Slug = "jwt-corp",
            Plan = TenantPlan.Enterprise,
            Limits = TenantLimits.EnterpriseTier(),
            IsActive = true
        };
        await _store.SaveAsync(tenant);

        var middleware = CreateMiddleware();
        var tenantContext = new TenantContext();
        var httpContext = new DefaultHttpContext();

        // Set claim
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(TenantMiddleware.TenantIdClaimType, "jwt-tenant")
        }, "TestAuth");
        httpContext.User = new ClaimsPrincipal(identity);

        await middleware.InvokeAsync(httpContext, tenantContext, _resolver);

        tenantContext.TenantId.Should().Be("jwt-tenant");
        tenantContext.TenantName.Should().Be("JWT Corp");
        tenantContext.Plan.Should().Be(TenantPlan.Enterprise);
    }

    [Fact]
    public async Task InvokeAsync_ClaimTakesPrecedence_OverHeader()
    {
        var tenant = new Tenant
        {
            Id = "claim-tenant",
            Name = "Claim Corp",
            Slug = "claim-corp",
            Plan = TenantPlan.Pro,
            Limits = TenantLimits.ProTier(),
            IsActive = true
        };
        await _store.SaveAsync(tenant);

        var middleware = CreateMiddleware();
        var tenantContext = new TenantContext();
        var httpContext = new DefaultHttpContext();

        // Set both claim and header
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(TenantMiddleware.TenantIdClaimType, "claim-tenant")
        }, "TestAuth");
        httpContext.User = new ClaimsPrincipal(identity);
        httpContext.Request.Headers[TenantMiddleware.TenantIdHeaderName] = Tenant.DefaultTenantId;

        await middleware.InvokeAsync(httpContext, tenantContext, _resolver);

        tenantContext.TenantId.Should().Be("claim-tenant");
    }

    [Fact]
    public async Task InvokeAsync_NoTenantInfo_UsesDefaultValues()
    {
        var middleware = CreateMiddleware();
        var tenantContext = new TenantContext();
        var httpContext = new DefaultHttpContext();

        await middleware.InvokeAsync(httpContext, tenantContext, _resolver);

        // Default values from TenantContext constructor
        tenantContext.TenantId.Should().Be(Tenant.DefaultTenantId);
        tenantContext.TenantName.Should().Be("Default");
    }

    [Fact]
    public async Task InvokeAsync_UnknownTenant_KeepsDefaults()
    {
        var middleware = CreateMiddleware();
        var tenantContext = new TenantContext();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers[TenantMiddleware.TenantIdHeaderName] = "unknown-tenant";

        await middleware.InvokeAsync(httpContext, tenantContext, _resolver);

        tenantContext.TenantId.Should().Be(Tenant.DefaultTenantId);
    }

    [Fact]
    public async Task InvokeAsync_CallsNextMiddleware()
    {
        var nextCalled = false;
        var middleware = CreateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(new DefaultHttpContext(), new TenantContext(), _resolver);

        nextCalled.Should().BeTrue();
    }
}
