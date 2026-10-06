using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;
using AgenticSystem.Infrastructure.Persistence;
using AgenticSystem.Infrastructure.Tools;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Npgsql;
using Pgvector.EntityFrameworkCore;

namespace AgenticSystem.Tests;

public class PostgresWorkflowExecutionStoreTests
{
    [RequiresPostgresFact]
    public async Task WorkflowDefinitionStore_RejectsCrossTenantGlobalIdCollision()
    {
        var connectionString = GetIsolatedConnectionString();
        var tenantContext = new TenantContextAccessor();
        var systemOperations = new SystemOperationContextAccessor();
        var options = new DbContextOptionsBuilder<AgenticDbContext>()
            .UseNpgsql(connectionString, postgres => postgres.UseVector())
            .Options;
        var factory = new FakeDbContextFactory { ContextCreator = () => new AgenticDbContext(options, tenantContext) };
        var store = new PostgresWorkflowStore(factory, NullLogger<PostgresWorkflowStore>.Instance, tenantContext, systemOperations);
        var definition = new WorkflowDefinition
        {
            Id = $"tenant-definition-{Guid.NewGuid():N}",
            Name = "Tenant A definition",
            Steps = [new WorkflowStep { Id = "step", Name = "Step", StepType = WorkflowStepType.Wait }]
        };

        try
        {
            using (tenantContext.BeginScope(new TenantContext { TenantId = "tenant-definition-a" }))
                await store.SaveDefinitionAsync("tenant-definition-a", definition);

            using (tenantContext.BeginScope(new TenantContext { TenantId = "tenant-definition-b" }))
            {
                (await store.GetDefinitionAsync("tenant-definition-b", definition.Id)).Should().BeNull();
                var overwrite = () => store.SaveDefinitionAsync(
                    "tenant-definition-b",
                    new WorkflowDefinition { Id = definition.Id, Name = "Tenant B overwrite" });
                await overwrite.Should().ThrowAsync<InvalidOperationException>()
                    .WithMessage("Workflow definition tenant ownership cannot be changed.");
            }

            using (tenantContext.BeginScope(new TenantContext { TenantId = "tenant-definition-a" }))
                (await store.GetDefinitionAsync("tenant-definition-a", definition.Id))!.Name.Should().Be("Tenant A definition");
        }
        finally
        {
            using var tenantScope = tenantContext.BeginScope(new TenantContext { TenantId = "tenant-definition-a" });
            await store.DeleteDefinitionAsync("tenant-definition-a", definition.Id);
        }
    }

    [RequiresPostgresFact]
    public async Task BannerTool_StartsPollableTenantWorkflowInCanonicalPostgresStore()
    {
        var connectionString = GetIsolatedConnectionString();
        var tenantContext = new TenantContextAccessor();
        var systemOperations = new SystemOperationContextAccessor();
        var options = new DbContextOptionsBuilder<AgenticDbContext>()
            .UseNpgsql(connectionString, postgres => postgres.UseVector())
            .Options;
        var factory = new FakeDbContextFactory { ContextCreator = () => new AgenticDbContext(options, tenantContext) };
        var store = new PostgresWorkflowStore(factory, NullLogger<PostgresWorkflowStore>.Instance, tenantContext, systemOperations);
        var engine = new DefaultWorkflowEngine(
            store,
            Substitute.For<IDirectAgentRequestExecutor>(),
            Substitute.For<IToolManager>(),
            NullLogger<DefaultWorkflowEngine>.Instance);
        var tool = new BannerProductionTool(tenantContext, store, engine);
        var definition = new WorkflowDefinition
        {
            Id = $"banner-production-{Guid.NewGuid():N}",
            Name = "Banner Production Workflow",
            Steps = [new WorkflowStep { Id = "render", Name = "Render", StepType = WorkflowStepType.Agent, AgentName = "BannerAgent" }]
        };
        string? executionId = null;

        using var tenantScope = tenantContext.BeginScope(new TenantContext { TenantId = "tenant-banner" });
        try
        {
            await store.SaveDefinitionAsync("tenant-banner", definition);
            var result = await tool.ExecuteAsync(new ToolInput
            {
                Action = "generate",
                UserId = "user-banner",
                Parameters = new Dictionary<string, object>
                {
                    ["imagePath"] = "listing.jpg",
                    ["price"] = 425000m,
                    ["bedrooms"] = 3
                }
            });

            result.Success.Should().BeTrue();
            executionId = result.Metadata!["executionId"].ToString();
            executionId.Should().NotBeNullOrWhiteSpace();
            result.Metadata["statusUrl"].Should().Be($"/api/workflow/executions/{executionId}");
            var execution = await store.GetExecutionAsync("tenant-banner", executionId!);
            execution.Should().NotBeNull();
            execution!.WorkflowId.Should().Be(definition.Id);
            execution.InitiatedBy.Should().Be("user-banner");
            (await store.GetExecutionAsync("tenant-other", executionId!)).Should().BeNull();
        }
        finally
        {
            if (executionId is not null)
                await store.DeleteExecutionAsync("tenant-banner", executionId);
            await store.DeleteDefinitionAsync("tenant-banner", definition.Id);
        }
    }

    [RequiresPostgresFact]
    public async Task PendingWait_IsPersistedAndIsNotClaimedBeforeItsScheduledTime()
    {
        var connectionString = GetIsolatedConnectionString();
        var tenantContext = new TenantContextAccessor();
        var systemOperations = new SystemOperationContextAccessor();
        var options = new DbContextOptionsBuilder<AgenticDbContext>()
            .UseNpgsql(connectionString, postgres => postgres.UseVector())
            .Options;
        var factory = new FakeDbContextFactory { ContextCreator = () => new AgenticDbContext(options, tenantContext) };
        var store = new PostgresWorkflowStore(factory, NullLogger<PostgresWorkflowStore>.Instance, tenantContext, systemOperations);
        var definition = new WorkflowDefinition
        {
            Id = $"scheduled-wait-{Guid.NewGuid():N}",
            Name = "Scheduled wait",
            Steps = [new WorkflowStep { Id = "wait", Name = "Wait", StepType = WorkflowStepType.Wait, Timeout = TimeSpan.FromSeconds(5) }]
        };
        var engine = new DefaultWorkflowEngine(
            store,
            Substitute.For<IDirectAgentRequestExecutor>(),
            Substitute.For<IToolManager>(),
            NullLogger<DefaultWorkflowEngine>.Instance);
        string? executionId = null;

        using var tenantScope = tenantContext.BeginScope(new TenantContext { TenantId = "tenant-wait" });
        try
        {
            var started = await engine.StartAsync("tenant-wait", definition, initiatedBy: "user-wait");
            executionId = started.Id;
            var firstClaim = await ClaimAsync(store, systemOperations, "first-process-worker", TimeSpan.FromMinutes(1));
            firstClaim.Should().NotBeNull();
            await engine.ProcessClaimedExecutionAsync(firstClaim!);
            await store.ReleaseExecutionLeaseAsync(firstClaim!);

            var restored = await store.GetExecutionAsync("tenant-wait", started.Id);
            restored!.Status.Should().Be(WorkflowExecutionStatus.Pending);
            var waitUntilUtc = restored.StepExecutions.Should().ContainSingle().Which.WaitUntilUtc;
            waitUntilUtc.Should().NotBeNull();
            waitUntilUtc!.Value.Should().BeAfter(DateTime.UtcNow);
            (await ClaimAsync(store, systemOperations, "early-worker", TimeSpan.FromMinutes(1))).Should().BeNull();

            var remainingWait = waitUntilUtc.Value - DateTime.UtcNow;
            if (remainingWait > TimeSpan.Zero)
                await Task.Delay(remainingWait + TimeSpan.FromMilliseconds(50));

            // Recreate the store/engine objects to exercise persisted wait recovery.
            var restartedStore = new PostgresWorkflowStore(factory, NullLogger<PostgresWorkflowStore>.Instance, tenantContext, systemOperations);
            var restartedEngine = new DefaultWorkflowEngine(
                restartedStore,
                Substitute.For<IDirectAgentRequestExecutor>(),
                Substitute.For<IToolManager>(),
                NullLogger<DefaultWorkflowEngine>.Instance);
            var dueClaim = await ClaimAsync(restartedStore, systemOperations, "restarted-worker", TimeSpan.FromMinutes(1));
            dueClaim.Should().NotBeNull();
            dueClaim!.ExecutionId.Should().Be(started.Id);
            await restartedEngine.ProcessClaimedExecutionAsync(dueClaim);
            await restartedStore.ReleaseExecutionLeaseAsync(dueClaim);
            (await restartedStore.GetExecutionAsync("tenant-wait", started.Id))!.Status.Should().Be(WorkflowExecutionStatus.Completed);
        }
        finally
        {
            if (executionId is not null)
                await store.DeleteExecutionAsync("tenant-wait", executionId);
            await store.DeleteDefinitionAsync("tenant-wait", definition.Id);
        }
    }

    [RequiresPostgresFact]
    public async Task WorkflowLease_AllowsOnlyOneWorkerAndRecoversExpiredClaim()
    {
        var connectionString = GetIsolatedConnectionString();
        var tenantContext = new TenantContextAccessor();
        var systemOperations = new SystemOperationContextAccessor();
        var options = new DbContextOptionsBuilder<AgenticDbContext>()
            .UseNpgsql(connectionString, postgres => postgres.UseVector())
            .Options;
        var factory = new FakeDbContextFactory { ContextCreator = () => new AgenticDbContext(options, tenantContext) };
        var store = new PostgresWorkflowStore(factory, NullLogger<PostgresWorkflowStore>.Instance, tenantContext, systemOperations);
        var execution = new WorkflowExecution
        {
            Id = $"lease-{Guid.NewGuid():N}",
            TenantId = "tenant-lease",
            WorkflowId = "lease-definition",
            WorkflowName = "Lease recovery",
            WorkflowDefinitionSnapshotJson = "{}",
            Status = WorkflowExecutionStatus.Pending
        };

        using var tenantScope = tenantContext.BeginScope(new TenantContext { TenantId = execution.TenantId });
        try
        {
            await store.SaveExecutionAsync(execution.TenantId, execution);
            var competingClaims = await Task.WhenAll(
                ClaimAsync(store, systemOperations, "worker-a", TimeSpan.FromMinutes(1)),
                ClaimAsync(store, systemOperations, "worker-b", TimeSpan.FromMinutes(1)));
            var first = competingClaims.Should().ContainSingle(claim => claim != null).Which;
            first!.ExecutionId.Should().Be(execution.Id);
            var staleState = await store.GetExecutionAsync(execution.TenantId, execution.Id);

            await using (var connection = new NpgsqlConnection(connectionString))
            {
                await connection.OpenAsync();
                await using var expire = new NpgsqlCommand(
                    "UPDATE workflow_executions SET lease_expires_at = @expired WHERE id = @id AND \"TenantId\" = @tenant",
                    connection);
                expire.Parameters.AddWithValue("expired", DateTime.UtcNow.AddSeconds(-1));
                expire.Parameters.AddWithValue("id", execution.Id);
                expire.Parameters.AddWithValue("tenant", execution.TenantId);
                (await expire.ExecuteNonQueryAsync()).Should().Be(1);
            }

            var newWorkerId = first.WorkerId == "worker-a" ? "worker-b" : "worker-a";
            var recovered = await ClaimAsync(store, systemOperations, newWorkerId, TimeSpan.FromMinutes(1));
            recovered.Should().NotBeNull();
            recovered!.WorkerId.Should().NotBe(first.WorkerId);
            (await store.RenewExecutionLeaseAsync(first, TimeSpan.FromMinutes(1))).Should().BeFalse();
            staleState!.Status = WorkflowExecutionStatus.Running;
            var staleWrite = () => store.SaveExecutionAsync(execution.TenantId, staleState);
            await staleWrite.Should().ThrowAsync<WorkflowExecutionLeaseLostException>();
        }
        finally
        {
            await store.DeleteExecutionAsync(execution.TenantId, execution.Id);
        }
    }

    [RequiresPostgresFact]
    public async Task SaveExecution_PreservesDefinitionSnapshotAndTenantOwnership()
    {
        var connectionString = GetIsolatedConnectionString();
        var tenantContext = new TenantContextAccessor();
        var systemOperations = new SystemOperationContextAccessor();
        var options = new DbContextOptionsBuilder<AgenticDbContext>()
            .UseNpgsql(connectionString, postgres => postgres.UseVector())
            .Options;
        var factory = new FakeDbContextFactory
        {
            ContextCreator = () => new AgenticDbContext(options, tenantContext)
        };
        var store = new PostgresWorkflowStore(factory, NullLogger<PostgresWorkflowStore>.Instance, tenantContext, systemOperations);
        var definition = new WorkflowDefinition
        {
            Id = "snapshot-definition",
            Name = "Tenant workflow",
            Version = 7,
            Steps =
            [
                new WorkflowStep { Id = "approval", Name = "Review", StepType = WorkflowStepType.Approval },
                new WorkflowStep
                {
                    Id = "agent-1",
                    Name = "Agent",
                    StepType = WorkflowStepType.Agent,
                    AgentName = "Researcher",
                    DependsOn = ["approval"]
                }
            ]
        };
        var executionId = string.Empty;
        var agentExecutor = Substitute.For<IDirectAgentRequestExecutor>();
        agentExecutor.ExecuteAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<UserContext>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new AgentResponse { Success = true, Content = "Agent completed" });
        var engine = new DefaultWorkflowEngine(
            store,
            agentExecutor,
            Substitute.For<IToolManager>(),
            NullLogger<DefaultWorkflowEngine>.Instance);

        try
        {
            using (tenantContext.BeginScope(new TenantContext { TenantId = "tenant-a" }))
            {
                await store.SaveDefinitionAsync("tenant-a", definition);
                var started = await engine.StartAsync("tenant-a", definition, initiatedBy: "user-a");
                executionId = started.Id;
                await ProcessClaimedExecutionAsync(store, engine, started.Id, systemOperations);
                var waiting = await WaitForExecutionAsync(store, "tenant-a", started.Id, WorkflowExecutionStatus.WaitingForApproval);

                waiting.WorkflowDefinitionVersion.Should().Be(7);
                waiting.WorkflowDefinitionHash.Should().NotBeNullOrWhiteSpace();
                var restoredDefinition = JsonSerializer.Deserialize<WorkflowDefinition>(waiting.WorkflowDefinitionSnapshotJson);
                restoredDefinition.Should().BeEquivalentTo(definition);

                var editedDefinition = new WorkflowDefinition
                {
                    Id = definition.Id,
                    Name = definition.Name,
                    Version = 8,
                    Steps =
                    [
                        new WorkflowStep { Id = "approval", Name = "Review", StepType = WorkflowStepType.Approval },
                        new WorkflowStep
                        {
                            Id = "agent-1",
                            Name = "Agent",
                            StepType = WorkflowStepType.Agent,
                            AgentName = "EditedResearcher",
                            DependsOn = ["approval"]
                        }
                    ]
                };
                await store.SaveDefinitionAsync("tenant-a", editedDefinition);

                await engine.ApproveAsync("tenant-a", started.Id, "approver-a");
                await ProcessClaimedExecutionAsync(store, engine, started.Id, systemOperations);
                var completed = await WaitForExecutionAsync(store, "tenant-a", started.Id, WorkflowExecutionStatus.Completed);
                completed.WorkflowDefinitionVersion.Should().Be(7);
                await agentExecutor.Received(1).ExecuteAsync(
                    started.Id,
                    Arg.Any<string>(),
                    Arg.Is<UserContext>(context => context.TenantId == "tenant-a"),
                    "Researcher",
                    Arg.Any<CancellationToken>());
                await agentExecutor.DidNotReceive().ExecuteAsync(
                    started.Id,
                    Arg.Any<string>(),
                    Arg.Any<UserContext>(),
                    "EditedResearcher",
                    Arg.Any<CancellationToken>());
            }

            using (tenantContext.BeginScope(new TenantContext { TenantId = "tenant-b" }))
            {
                (await store.GetExecutionAsync("tenant-b", executionId)).Should().BeNull();
            }
        }
        finally
        {
            using (tenantContext.BeginScope(new TenantContext { TenantId = "tenant-a" }))
            {
                if (!string.IsNullOrWhiteSpace(executionId))
                    await store.DeleteExecutionAsync("tenant-a", executionId);
                await store.DeleteDefinitionAsync("tenant-a", definition.Id);
            }
        }
    }

    private static async Task<WorkflowExecutionClaim?> ClaimAsync(
        IWorkflowStore store, SystemOperationContextAccessor systemOperations, string workerId, TimeSpan duration)
    {
        using var scope = systemOperations.BeginScope(SystemOperationKind.ClaimWorkflowExecutions);
        return await store.ClaimNextExecutionAsync(workerId, duration);
    }

    private static async Task ProcessClaimedExecutionAsync(IWorkflowStore store, IWorkflowEngine engine, string executionId, SystemOperationContextAccessor systemOperations)
    {
        var claim = await ClaimAsync(store, systemOperations, "postgres-workflow-test", TimeSpan.FromMinutes(1));
        claim.Should().NotBeNull();
        claim!.ExecutionId.Should().Be(executionId);
        try
        {
            await engine.ProcessClaimedExecutionAsync(claim);
        }
        finally
        {
            await store.ReleaseExecutionLeaseAsync(claim);
        }
    }

    private static async Task<WorkflowExecution> WaitForExecutionAsync(
        IWorkflowStore store,
        string tenantId,
        string executionId,
        WorkflowExecutionStatus expected)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var execution = await store.GetExecutionAsync(tenantId, executionId);
            if (execution?.Status == expected) return execution;
            await Task.Delay(50);
        }

        throw new TimeoutException($"Workflow execution '{executionId}' did not reach {expected}.");
    }

    private static string GetIsolatedConnectionString()
    {
        var connectionString = Environment.GetEnvironmentVariable("AGENTIC_TEST_POSTGRES");
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        var target = new NpgsqlConnectionStringBuilder(connectionString);
        var efConnectionString = Environment.GetEnvironmentVariable("AGENTIC_EF_CONNECTION");
        ArgumentException.ThrowIfNullOrWhiteSpace(efConnectionString);
        var efTarget = new NpgsqlConnectionStringBuilder(efConnectionString);
        if (target.Host != "127.0.0.1"
            || target.Port != 55432
            || string.IsNullOrWhiteSpace(target.Database)
            || !target.Database.StartsWith("review_pr152_", StringComparison.Ordinal)
            || target.Username != "validation"
            || efTarget.Host != target.Host
            || efTarget.Port != target.Port
            || efTarget.Database != target.Database
            || efTarget.Username != target.Username
            || efTarget.Password != target.Password)
        {
            throw new InvalidOperationException(
                "PostgreSQL integration tests are restricted to an isolated review_pr152_* database from tests/backend-validation/compose.yml (127.0.0.1:55432). Set both connection variables to that database.");
        }

        return connectionString;
    }
}
