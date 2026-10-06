using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.Tools;
using AgenticSystem.Infrastructure.Persistence;
using AgenticSystem.Infrastructure.Persistence.Entities;
using Xunit;

namespace AgenticSystem.Tests;

public class TenantAnalyticsToolTests
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TenantAnalyticsTool> _logger;
    private readonly TenantAnalyticsTool _sut;
    private readonly AgenticDbContext _dbContext;

    public TenantAnalyticsToolTests()
    {
        var dbName = $"analytics-tests-{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<AgenticDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        var tenantAccessor = Substitute.For<ITenantContextAccessor>();
        tenantAccessor.CurrentTenantId.Returns("test-tenant");

        _dbContext = new AgenticDbContext(options, tenantAccessor);
        _dbContext.Database.EnsureCreated();

        _serviceProvider = Substitute.For<IServiceProvider>();
        _logger = Substitute.For<ILogger<TenantAnalyticsTool>>();

        var serviceScope = Substitute.For<IServiceScope>();
        var serviceScopeFactory = Substitute.For<IServiceScopeFactory>();

        serviceScope.ServiceProvider.Returns(_serviceProvider);
        serviceScopeFactory.CreateScope().Returns(serviceScope);
        _serviceProvider.GetService(typeof(IServiceScopeFactory)).Returns(serviceScopeFactory);

        // Make the ServiceProvider return our real in-memory db context
        _serviceProvider.GetService(typeof(AgenticDbContext)).Returns(_dbContext);
        // And when resolving by reflection
        _serviceProvider.GetService(Arg.Is<Type>(t => t.Name == "AgenticDbContext")).Returns(_dbContext);

        _sut = new TenantAnalyticsTool(_serviceProvider, _logger);
    }

    [Fact]
    public async Task ExecuteAsync_CostsAction_ComputesCorrectTotals()
    {
        // Arrange
        var now = DateTime.UtcNow;
        _dbContext.CostEntries.AddRange(new List<CostEntryEntity>
        {
            new() { Cost = 10.50m, ServiceName = "Ollama", Category = "LLM", RecordedAt = now.AddDays(-2), TenantId = "test-tenant" },
            new() { Cost = 5.00m, ServiceName = "OpenAI", Category = "LLM", RecordedAt = now.AddDays(-5), TenantId = "test-tenant" },
            new() { Cost = 25.00m, ServiceName = "Postgres", Category = "Database", RecordedAt = now.AddDays(-40), TenantId = "test-tenant" } // outside default 30 days
        });
        await _dbContext.SaveChangesAsync();

        var input = new ToolInput
        {
            Action = "costs",
            Parameters = new Dictionary<string, object> { { "days", 30 } }
        };

        // Act
        var result = await _sut.ExecuteAsync(input, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        
        var json = System.Text.Json.JsonSerializer.Serialize(result.Data);
        var data = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(json);
        data.GetProperty("totalCost").GetDecimal().Should().Be(15.50m);
        data.GetProperty("days").GetInt32().Should().Be(30);

        var byService = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, decimal>>(data.GetProperty("byService").GetRawText()) ?? new();
        byService["Ollama"].Should().Be(10.50m);
        byService["OpenAI"].Should().Be(5.00m);
        byService.ContainsKey("Postgres").Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_PerformanceAction_ComputesAverages()
    {
        // Arrange
        var now = DateTime.UtcNow;
        _dbContext.AgentPerformanceMetrics.AddRange(new List<AgentPerformanceMetricEntity>
        {
            new() { Success = true, LatencyMs = 150.0, AgentName = "SupportAgent", RecordedAt = now.AddDays(-1), TenantId = "test-tenant" },
            new() { Success = false, LatencyMs = 300.0, AgentName = "SupportAgent", RecordedAt = now.AddDays(-2), TenantId = "test-tenant" },
            new() { Success = true, LatencyMs = 100.0, AgentName = "GeneralAgent", RecordedAt = now.AddDays(-3), TenantId = "test-tenant" }
        });
        await _dbContext.SaveChangesAsync();

        var input = new ToolInput
        {
            Action = "performance",
            Parameters = new Dictionary<string, object> { { "days", 7 } }
        };

        // Act
        var result = await _sut.ExecuteAsync(input, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        var json = System.Text.Json.JsonSerializer.Serialize(result.Data);
        var data = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(json);
        data.GetProperty("totalExecutions").GetInt32().Should().Be(3);
        Math.Round(data.GetProperty("avgLatencyMs").GetDouble(), 2).Should().Be(183.33);
        Math.Round(data.GetProperty("successRate").GetDouble(), 2).Should().Be(0.67);
    }

    [Fact]
    public async Task ExecuteAsync_SessionsAction_ComputesCounts()
    {
        // Arrange
        _dbContext.SessionRecords.AddRange(new List<SessionRecordEntity>
        {
            new() { Id = "s1", EndedAt = null, TenantId = "test-tenant" },
            new() { Id = "s2", EndedAt = DateTime.UtcNow, TenantId = "test-tenant" },
            new() { Id = "s3", EndedAt = null, TenantId = "test-tenant" }
        });
        await _dbContext.SaveChangesAsync();

        var input = new ToolInput { Action = "sessions" };

        // Act
        var result = await _sut.ExecuteAsync(input, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        var json = System.Text.Json.JsonSerializer.Serialize(result.Data);
        var data = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(json);
        data.GetProperty("activeSessions").GetInt32().Should().Be(2);
        data.GetProperty("totalSessions").GetInt32().Should().Be(3);
    }

    [Fact]
    public async Task ExecuteAsync_WorkflowsAction_GroupsByStatus()
    {
        // Arrange
        _dbContext.WorkflowExecutions.AddRange(new List<WorkflowExecutionEntity>
        {
            new() { Id = "w1", Status = "Completed", TenantId = "test-tenant", WorkflowName = "W1", VariablesJson = "{}" },
            new() { Id = "w2", Status = "Running", TenantId = "test-tenant", WorkflowName = "W1", VariablesJson = "{}" },
            new() { Id = "w3", Status = "Failed", TenantId = "test-tenant", WorkflowName = "W1", VariablesJson = "{}" },
            new() { Id = "w4", Status = "Completed", TenantId = "test-tenant", WorkflowName = "W1", VariablesJson = "{}" }
        });
        await _dbContext.SaveChangesAsync();

        var input = new ToolInput { Action = "workflows" };

        // Act
        var result = await _sut.ExecuteAsync(input, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        var json = System.Text.Json.JsonSerializer.Serialize(result.Data);
        var data = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(json);
        data.GetProperty("totalWorkflows").GetInt32().Should().Be(4);

        var byStatus = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, int>>(data.GetProperty("byStatus").GetRawText()) ?? new();
        byStatus["Completed"].Should().Be(2);
        byStatus["Running"].Should().Be(1);
        byStatus["Failed"].Should().Be(1);
    }
}
