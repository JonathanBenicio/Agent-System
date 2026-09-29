using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;
using AgenticSystem.Infrastructure.Persistence;
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
    public async Task SaveExecution_PreservesDefinitionSnapshotAndTenantOwnership()
    {
        var connectionString = GetIsolatedConnectionString();
        var tenantContext = new TenantContextAccessor();
        var options = new DbContextOptionsBuilder<AgenticDbContext>()
            .UseNpgsql(connectionString, postgres => postgres.UseVector())
            .Options;
        var factory = new FakeDbContextFactory
        {
            ContextCreator = () => new AgenticDbContext(options, tenantContext)
        };
        var store = new PostgresWorkflowStore(factory, NullLogger<PostgresWorkflowStore>.Instance);
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
            || target.Database != "backend_validation"
            || target.Username != "validation"
            || efTarget.Host != target.Host
            || efTarget.Port != target.Port
            || efTarget.Database != target.Database
            || efTarget.Username != target.Username
            || efTarget.Password != target.Password)
        {
            throw new InvalidOperationException(
                "PostgreSQL integration tests are restricted to tests/backend-validation/compose.yml (127.0.0.1:55432/backend_validation). Set both connection variables to that isolated database.");
        }

        return connectionString;
    }
}
