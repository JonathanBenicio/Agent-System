using AgenticSystem.Core.Extensions;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Infrastructure.Extensions;
using AgenticSystem.Infrastructure.Gateway;
using AgenticSystem.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Npgsql;

namespace AgenticSystem.Tests;

public sealed class GatewayProductionRegistrationIntegrationTests
{
    [RequiresPostgresFact]
    public async Task ProductionPostgresComposition_StartsGatewayRegistrationFromHostConfiguration()
    {
        var connectionString = GetIsolatedConnectionString();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AgenticSystem:LocalExecution:StorageMode"] = "PostgreSQL",
                ["AgenticSystem:Encryption:Key"] = "isolated-integration-test-encryption-key",
                ["AgenticSystem:Memory:VectorStoreType"] = "InMemory",
                ["AgenticSystem:Ollama:Enabled"] = "false",
                ["AgenticSystem:OpenAI:Enabled"] = "true",
                ["AgenticSystem:OpenAI:ApiKey"] = "integration-test-platform-key",
                ["AgenticSystem:Gemini:Enabled"] = "false",
                ["AgenticSystem:Claude:Enabled"] = "false",
                ["AgenticSystem:OpenRouter:Enabled"] = "false",
                ["ConnectionStrings:SessionStore"] = connectionString
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(Environments.Production);
        services.AddSingleton(environment);
        services.AddSingleton(Substitute.For<IOnnxEventBroadcaster>());
        services.AddSingleton(Substitute.For<IChatClient>());
        services.AddAgenticSystemCore();
        services.AddAgenticSystemInfrastructure(configuration);
        services.UseLocalExecutionStorageMode(configuration);

        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });

        provider.GetRequiredService<IPlatformConfigStore>().Should().BeOfType<PostgresPlatformConfigStore>();
        var gateway = provider.GetRequiredService<IServiceGateway>();
        var gatewayRegistration = provider.GetServices<IHostedService>()
            .OfType<GatewayProviderRegistrationHostedService>()
            .Should().ContainSingle().Subject;

        await gatewayRegistration.StartAsync(CancellationToken.None);

        var registrations = await gateway.GetAllServicesStatusAsync();
        registrations.Should().ContainSingle().Which.Name.Should().Be("OpenAI");
        registrations.Single().IsEnabled.Should().BeTrue();
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
