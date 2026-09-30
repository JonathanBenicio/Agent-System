using Xunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using AgenticSystem.Core.Extensions;
using AgenticSystem.Infrastructure.Extensions;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Infrastructure.AgentFramework;
using Microsoft.Extensions.AI;
using NSubstitute;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.IO;
using System;

namespace AgenticSystem.Tests;

public class DependencyInjectionValidationTests
{
    [Fact]
    public void ValidateDependencyInjectionLifetimesAndScopes()
    {
        var services = new ServiceCollection();

        // 1. Setup Configuration mock
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AgenticSystem:Ollama:Enabled"] = "true",
                ["AgenticSystem:Ollama:Priority"] = "1",
                ["AgenticSystem:Memory:VectorStoreType"] = "InMemory",
                ["AgenticSystem:LocalExecution:StorageMode"] = "PostgreSQL",
                ["ConnectionStrings:SessionStore"] = "Host=localhost;Database=di_validation;Username=di_validation"
            })
            .Build();

        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        var environment = Substitute.For<Microsoft.Extensions.Hosting.IHostEnvironment>();
        environment.EnvironmentName.Returns("Development");
        services.AddSingleton(environment);
        services.AddSingleton(Substitute.For<IOnnxEventBroadcaster>());

        // Register required ChatClient and generic dependencies that the infrastructure resolves/needs
        var chatClientMock = Substitute.For<IChatClient>();
        services.AddSingleton<IChatClient>(chatClientMock);

        // 2. Invoke our registration extensions
        services.AddAgenticSystemCore();
        services.AddAgenticSystemInfrastructure(configuration);
        services.UseLocalExecutionStorageMode(configuration);

        // 3. Build ServiceProvider with scope and build validation enabled
        var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });

        provider.Should().NotBeNull();

        // 4. Resolve key services within a scope to verify resolve works without captive dependencies or errors
        using (var scope = provider.CreateScope())
        {
            // Verify scoped dependencies
            var agentFrameworkFactory = scope.ServiceProvider.GetService<AgentFrameworkFactory>();
            agentFrameworkFactory.Should().NotBeNull();

            var directAgentExecutionService = scope.ServiceProvider.GetService<IDirectAgentExecutionService>();
            directAgentExecutionService.Should().NotBeNull();

            var agentCollaborationWorkflow = scope.ServiceProvider.GetService<IAgentCollaborationWorkflow>();
            agentCollaborationWorkflow.Should().NotBeNull();

            var orchestratorContextState = scope.ServiceProvider.GetService<OrchestratorContextState>();
            orchestratorContextState.Should().NotBeNull();

            var metaAgent = scope.ServiceProvider.GetService<IMetaAgent>();
            metaAgent.Should().NotBeNull();

            var adminConsole = scope.ServiceProvider.GetService<IAdminConsole>();
            adminConsole.Should().NotBeNull();
        }
    }
}
