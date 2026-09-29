using System.Security.Claims;
using AgenticSystem.Api.Auth;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;

namespace AgenticSystem.Tests;

public sealed class TenantSessionIsolationKeyProviderTests
{
    [Fact]
    public async Task UsesRuntimeOwnerAndTenantWhenSignalRHasNoHttpContextDuringAgentExecution()
    {
        var tenant = new TenantContextAccessor();
        var runtime = new LLMRuntimeContextAccessor();
        var provider = new TenantSessionIsolationKeyProvider(new HttpContextAccessor(), tenant, runtime);

        using (runtime.BeginScope(new UserContext { TenantId = "tenant-a", UserId = "user-a" }, "session"))
            (await provider.GetIsolationKeyAsync()).Should().Be("tenant-a:user-a");

        (await provider.GetIsolationKeyAsync()).Should().BeNull();
    }

    [Fact]
    public async Task FallsBackToAuthenticatedHttpIdentityAndActiveTenant()
    {
        var tenant = new TenantContextAccessor();
        var http = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, "user-a")], "test"))
            }
        };
        var provider = new TenantSessionIsolationKeyProvider(http, tenant, new LLMRuntimeContextAccessor());

        using (tenant.BeginScope(new TenantContext { TenantId = "tenant-a" }))
            (await provider.GetIsolationKeyAsync()).Should().Be("tenant-a:user-a");
    }
}
