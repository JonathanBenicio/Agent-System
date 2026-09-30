using System.Security.Claims;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;
using AgenticSystem.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NSubstitute;
using FluentAssertions;
using Xunit;

namespace AgenticSystem.Tests.MultiTenancy;

public class TenantMiddlewareTests
{
    private readonly ITenantStore _store;
    private readonly ITenantResolver _resolver;
    private readonly ITenantContextAccessor _tenantContextAccessor;
    private readonly IPermissionService _permissionService;
    private readonly ILogger<TenantMiddleware> _logger;

    public TenantMiddlewareTests()
    {
        _store = Substitute.For<ITenantStore>();
        _resolver = new TenantResolver(_store, Substitute.For<ILogger<TenantResolver>>());
        _tenantContextAccessor = Substitute.For<ITenantContextAccessor>();
        _permissionService = Substitute.For<IPermissionService>();
        _tenantContextAccessor.BeginScope(Arg.Any<TenantContext>()).Returns(Substitute.For<IDisposable>());
        _logger = Substitute.For<ILogger<TenantMiddleware>>();
    }

    private TenantMiddleware CreateMiddleware(RequestDelegate? next = null)
    {
        return new TenantMiddleware(next ?? (_ => Task.CompletedTask), _logger);
    }

    [Fact]
    public async Task InvokeAsync_WithTenantHeader_PopulatesTenantContext()
    {
        var tenant = new Tenant
        {
            Id = "test-tenant",
            Name = "Test Tenant",
            Slug = "test-tenant",
            Plan = TenantPlan.Pro,
            Limits = TenantLimits.ProTier(),
            IsActive = true
        };
        _store.GetByIdAsync("test-tenant", Arg.Any<CancellationToken>()).Returns(tenant);

        var middleware = CreateMiddleware();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers[TenantMiddleware.TenantIdHeaderName] = "test-tenant";

        await middleware.InvokeAsync(httpContext, _resolver, _tenantContextAccessor);

        _tenantContextAccessor.Received(1).BeginScope(Arg.Is<TenantContext>(tc =>
            tc.TenantId == "test-tenant" && tc.IsAuthenticated));
    }

    [Fact]
    public async Task InvokeAsync_WithJwtClaim_PopulatesTenantContext()
    {
        var tenant = new Tenant
        {
            Id = "jwt-tenant",
            Name = "JWT Corp",
            Slug = "jwt-corp",
            Plan = TenantPlan.Enterprise,
            Limits = TenantLimits.EnterpriseTier(),
            IsActive = true
        };
        _store.GetByIdAsync("jwt-tenant", Arg.Any<CancellationToken>()).Returns(tenant);

        var middleware = CreateMiddleware();
        var httpContext = new DefaultHttpContext();

        // Set claim
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(TenantMiddleware.TenantIdClaimType, "jwt-tenant"),
            new Claim(ClaimTypes.NameIdentifier, "user-1")
        }, "TestAuth");
        httpContext.User = new ClaimsPrincipal(identity);
        _permissionService.GetRolesAsync("user-1", Arg.Any<CancellationToken>()).Returns(
            [new RoleAssignment { UserId = "user-1", RoleName = "Viewer", TenantId = "jwt-tenant" }]);

        await middleware.InvokeAsync(httpContext, _resolver, _tenantContextAccessor, _permissionService);

        _tenantContextAccessor.Received(1).BeginScope(Arg.Is<TenantContext>(tc =>
            tc.TenantId == "jwt-tenant" &&
            tc.TenantName == "JWT Corp" &&
            tc.Plan == TenantPlan.Enterprise));
    }

    [Fact]
    public async Task InvokeAsync_HeaderMismatch_IsDenied_EvenWhenUserIsAdmin()
    {
        var claimTenant = new Tenant
        {
            Id = "claim-tenant",
            Name = "Claim Corp",
            Slug = "claim-corp",
            Plan = TenantPlan.Pro,
            Limits = TenantLimits.ProTier(),
            IsActive = true
        };
        var headerTenant = new Tenant
        {
            Id = "header-tenant",
            Name = "Header Corp",
            Slug = "header-corp",
            Plan = TenantPlan.Pro,
            Limits = TenantLimits.ProTier(),
            IsActive = true
        };

        _store.GetByIdAsync("claim-tenant", Arg.Any<CancellationToken>()).Returns(claimTenant);
        _store.GetByIdAsync("header-tenant", Arg.Any<CancellationToken>()).Returns(headerTenant);

        var middleware = CreateMiddleware();
        var httpContext = new DefaultHttpContext();

        // Set both claim and header - header should take precedence
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(TenantMiddleware.TenantIdClaimType, "claim-tenant"),
            new Claim(ClaimTypes.Role, "Admin")
        }, "TestAuth");
        httpContext.User = new ClaimsPrincipal(identity);
        httpContext.Request.Headers[TenantMiddleware.TenantIdHeaderName] = "header-tenant";

        await middleware.InvokeAsync(httpContext, _resolver, _tenantContextAccessor, _permissionService);

        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        _tenantContextAccessor.DidNotReceive().BeginScope(Arg.Any<TenantContext>());
    }

    [Fact]
    public async Task InvokeAsync_HeaderAndClaimMismatch_ReturnsForbidden_WhenUserIsNotAdmin()
    {
        var middleware = CreateMiddleware();
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();

        // Set both claim and header with mismatch and NO admin role
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(TenantMiddleware.TenantIdClaimType, "tenant-a")
        }, "TestAuth");
        httpContext.User = new ClaimsPrincipal(identity);
        httpContext.Request.Headers[TenantMiddleware.TenantIdHeaderName] = "tenant-b";

        await middleware.InvokeAsync(httpContext, _resolver, _tenantContextAccessor, _permissionService);

        // Should return 403 Forbidden
        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        
        // Next middleware should not be called
        _tenantContextAccessor.DidNotReceive().BeginScope(Arg.Any<TenantContext>());
    }

    [Fact]
    public async Task InvokeAsync_NoTenantInfo_DoesNotCallBeginScope()
    {
        var middleware = CreateMiddleware();
        var httpContext = new DefaultHttpContext();

        await middleware.InvokeAsync(httpContext, _resolver, _tenantContextAccessor);

        // No tenant resolved = no BeginScope called (public/anonymous route)
        _tenantContextAccessor.DidNotReceive().BeginScope(Arg.Any<TenantContext>());
    }

    [Fact]
    public async Task InvokeAsync_UnknownTenant_IsRejectedOnPublicRoutesToo()
    {
        _store.GetByIdAsync("unknown-tenant", Arg.Any<CancellationToken>()).Returns((Tenant?)null);

        var middleware = CreateMiddleware();
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();
        httpContext.Request.Headers[TenantMiddleware.TenantIdHeaderName] = "unknown-tenant";

        await middleware.InvokeAsync(httpContext, _resolver, _tenantContextAccessor);

        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        _tenantContextAccessor.DidNotReceive().BeginScope(Arg.Any<TenantContext>());
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

        await middleware.InvokeAsync(new DefaultHttpContext(), _resolver, _tenantContextAccessor);

        nextCalled.Should().BeTrue();
    }
}
