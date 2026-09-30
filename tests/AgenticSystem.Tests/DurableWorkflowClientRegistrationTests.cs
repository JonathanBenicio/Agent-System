#pragma warning disable MAAI001 // The test verifies the experimental MAF DurableTask client registration used by the backend.

using DurableTask.PostgreSQL;
using FluentAssertions;
using Microsoft.Agents.AI.DurableTask;
using Microsoft.Agents.AI.DurableTask.Workflows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.DurableTask.Client;

namespace AgenticSystem.Tests;

public class DurableWorkflowClientRegistrationTests
{
    [Fact]
    public async Task ConfigureDurableWorkflows_RegistersPublicWorkflowClientWithoutReflection()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDurableTaskPostgreSql(new PostgreSqlOrchestrationServiceSettings
        {
            ConnectionString = "Host=127.0.0.1;Port=55432;Database=validation;Username=validation;Password=validation",
            TaskHubName = "AgenticSystemHub",
            AutoDeploySchema = false
        });
        services.ConfigureDurableWorkflows(
            static options => options.MaxSupersteps = 37,
            clientBuilder: static builder => builder.UseOrchestrationService());

        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });

        var client = provider.GetRequiredService<IWorkflowClient>();

        client.Should().NotBeNull();
        provider.GetRequiredService<DurableOptions>().Workflows.MaxSupersteps.Should().Be(37);
    }
}
