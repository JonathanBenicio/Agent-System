using System.Text.Json;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;
using AgenticSystem.Core.Tools;
using AgenticSystem.Infrastructure.Persistence;
using AgenticSystem.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgenticSystem.Tests;

public sealed class TenantAnalyticsScopeRegressionTests
{
    [Theory]
    [InlineData("costs", "totalCost", 1)]
    [InlineData("performance", "totalExecutions", 1)]
    [InlineData("sessions", "totalSessions", 1)]
    [InlineData("workflows", "totalWorkflows", 1)]
    public async Task QueryUsesLiveScopedContextAndCannotSeeOtherTenant(string action, string field, int expected)
    {
        var accessor = new TenantContextAccessor();
        var services = new ServiceCollection();
        services.AddSingleton<ITenantContextAccessor>(accessor);
        var databaseName = $"analytics-live-{Guid.NewGuid():N}";
        services.AddDbContext<AgenticDbContext>(options => options.UseInMemoryDatabase(databaseName));
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        foreach (var tenantId in new[] { "a", "b" })
        {
            using var tenant = accessor.BeginScope(new TenantContext { TenantId = tenantId });
            using var seed = provider.CreateScope();
            var db = seed.ServiceProvider.GetRequiredService<AgenticDbContext>();
            await db.Database.EnsureCreatedAsync();
            db.CostEntries.Add(new CostEntryEntity { TenantId = tenantId, ServiceName = "review", Category = "llm", Cost = 1 });
            db.AgentPerformanceMetrics.Add(new AgentPerformanceMetricEntity
                { TenantId = tenantId, AgentName = "review", LatencyMs = 12, Success = true });
            db.SessionRecords.Add(new SessionRecordEntity { Id = tenantId, TenantId = tenantId, UserId = "u" });
            db.WorkflowExecutions.Add(new WorkflowExecutionEntity { Id = tenantId, TenantId = tenantId, Status = "Completed" });
            await db.SaveChangesAsync();
        }
        using var current = accessor.BeginScope(new TenantContext { TenantId = "a" });
        var tool = new TenantAnalyticsTool(provider, NullLogger<TenantAnalyticsTool>.Instance);
        var result = await tool.ExecuteAsync(new ToolInput { Action = action });
        result.Success.Should().BeTrue();
        JsonSerializer.SerializeToElement(result.Data).GetProperty(field).GetDecimal().Should().Be(expected);
    }
}
