using System.Security.Claims;
using AgenticSystem.Api.Controllers;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.LLM.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace AgenticSystem.Tests;

public class LLMControllerPlatformAdminTests
{
    [Fact]
    public async Task ProviderAdministration_ForbidsTenantAdminWithoutPlatformAdminRecord()
    {
        var options = new DbContextOptionsBuilder<AgenticDbContext>()
            .UseInMemoryDatabase($"llm-admin-{Guid.NewGuid():N}")
            .Options;
        var tenantAccessor = Substitute.For<ITenantContextAccessor>();
        await using var db = new AgenticDbContext(options, tenantAccessor);
        await db.Database.EnsureCreatedAsync();

        var controller = new LLMController(Substitute.For<ILLMAdministrationService>(), db);
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "tenant-admin-1"),
                new Claim(ClaimTypes.Role, "Admin")
            ],
            authenticationType: "test"))
        };
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        var executingContext = new ActionExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            controller);
        var nextCalled = false;

        await controller.OnActionExecutionAsync(executingContext, () =>
        {
            nextCalled = true;
            return Task.FromResult(new ActionExecutedContext(actionContext, new List<IFilterMetadata>(), controller));
        });

        nextCalled.Should().BeFalse();
        executingContext.Result.Should().BeOfType<ForbidResult>();
    }
}
