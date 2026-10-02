using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;
using AgenticSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgenticSystem.Tests;

public sealed class WorkflowSystemCapabilityTests
{
    [Fact]
    public async Task PostgresClaimRejectsMissingAndWrongCapabilityBeforeOpeningDatabase()
    {
        var factory = new UnopenedFactory();
        var operations = new SystemOperationContextAccessor();
        var store = new PostgresWorkflowStore(factory, NullLogger<PostgresWorkflowStore>.Instance,
            new TenantContextAccessor(), operations);
        await ((Func<Task>)(() => store.ClaimNextExecutionAsync("worker", TimeSpan.FromSeconds(20))))
            .Should().ThrowAsync<InvalidOperationException>();
        using (operations.BeginScope(SystemOperationKind.ApiKeyAuthentication))
            await ((Func<Task>)(() => store.ClaimNextExecutionAsync("worker", TimeSpan.FromSeconds(20))))
                .Should().ThrowAsync<InvalidOperationException>();
        factory.OpenCount.Should().Be(0);
    }

    [Fact]
    public async Task WorkerClaimsUnderSystemCapabilityAndExecutesUnderTenantThenRestoresBoth()
    {
        var operations = new SystemOperationContextAccessor();
        var tenants = new TenantContextAccessor();
        var store = Substitute.For<IWorkflowStore>();
        store.ClaimNextExecutionAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                operations.Require(SystemOperationKind.ClaimWorkflowExecutions);
                tenants.CurrentContext.Should().BeNull();
                return new WorkflowExecutionClaim("tenant-a", "execution", call.ArgAt<string>(0));
            });
        var engine = Substitute.For<IWorkflowEngine>();
        engine.ProcessClaimedExecutionAsync(Arg.Any<WorkflowExecutionClaim>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                tenants.CurrentTenantId.Should().Be("tenant-a");
                operations.Current.Should().BeNull();
                return Task.CompletedTask;
            });
        var services = new ServiceCollection();
        services.AddSingleton<ISystemOperationContextAccessor>(operations);
        services.AddSingleton<ITenantContextAccessor>(tenants);
        services.AddSingleton(store);
        services.AddSingleton(engine);
        using var provider = services.BuildServiceProvider();
        var worker = new WorkflowExecutionBackgroundService(provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<WorkflowExecutionBackgroundService>.Instance);
        (await worker.ProcessNextAsync(CancellationToken.None)).Should().BeTrue();
        operations.Current.Should().BeNull();
        tenants.CurrentContext.Should().BeNull();
        await engine.Received(1).ProcessClaimedExecutionAsync(Arg.Any<WorkflowExecutionClaim>(), Arg.Any<CancellationToken>());
    }

    private sealed class UnopenedFactory : IDbContextFactory<AgenticDbContext>
    {
        public int OpenCount { get; private set; }
        public AgenticDbContext CreateDbContext()
        {
            OpenCount++;
            throw new InvalidOperationException("Database must not open before system authorization.");
        }
        public Task<AgenticDbContext> CreateDbContextAsync(CancellationToken ct = default)
            => Task.FromResult(CreateDbContext());
    }
}
