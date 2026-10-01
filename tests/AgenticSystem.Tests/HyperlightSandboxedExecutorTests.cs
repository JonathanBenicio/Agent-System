using AgenticSystem.Infrastructure.Configuration;
using AgenticSystem.Infrastructure.Extensions;
using AgenticSystem.Infrastructure.Security;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace AgenticSystem.Tests;

public sealed class HyperlightSandboxedExecutorTests
{
    [Fact]
    public async Task DisabledFeatureDoesNotInvokeTheSandbox()
    {
        var runner = Substitute.For<IHyperlightCodeActRunner>();
        var sut = CreateExecutor(enabled: false, environmentName: "Lab", runner);

        var result = await sut.ExecuteCodeAsync("javascript", "console.log('hello')");

        sut.IsAvailable.Should().BeFalse();
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("disabled");
        await runner.DidNotReceive().ExecuteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FeatureFlagOutsideLabDoesNotInvokeTheSandbox()
    {
        var runner = Substitute.For<IHyperlightCodeActRunner>();
        var sut = CreateExecutor(enabled: true, environmentName: "Development", runner);

        var result = await sut.ExecuteCodeAsync("javascript", "console.log('hello')");

        sut.IsAvailable.Should().BeFalse();
        result.Success.Should().BeFalse();
        await runner.DidNotReceive().ExecuteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SafetyGateRejectsHostProcessAccessBeforeCallingSandbox()
    {
        var runner = Substitute.For<IHyperlightCodeActRunner>();
        var sut = CreateExecutor(enabled: true, environmentName: "Lab", runner);

        var result = await sut.ExecuteCodeAsync("javascript", "process.exit(1)");

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("safety gate");
        await runner.DidNotReceive().ExecuteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AcceptedCodeReturnsOnlyTheSandboxOutput()
    {
        var runner = Substitute.For<IHyperlightCodeActRunner>();
        runner.ExecuteAsync("console.log(2 + 2)", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult("sandbox-output:4"));
        var sut = CreateExecutor(enabled: true, environmentName: "Lab", runner);

        var result = await sut.ExecuteCodeAsync("javascript", "console.log(2 + 2)");

        result.Success.Should().BeTrue();
        result.Output.Should().Be("sandbox-output:4");
    }

    [Fact]
    public async Task OfficialHyperlightRunnerExecutesJavaScript()
    {
        using var runner = new HyperlightCodeActRunner();

        var output = await runner.ExecuteAsync("console.log(2 + 2)");

        output.Should().Contain("4");
    }

    [Theory]
    [InlineData(false, "Lab")]
    [InlineData(true, "Development")]
    public void ToolIsNotRegisteredWhenDisabledOrOutsideLab(bool enabled, string environmentName)
    {
        var hostEnvironment = Substitute.For<IHostEnvironment>();
        hostEnvironment.EnvironmentName.Returns(environmentName);
        var toolCatalog = Substitute.For<AgenticSystem.Core.Interfaces.IPlatformToolCatalog>();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHttpClient();
        services.AddSingleton<AgenticSystem.Core.Interfaces.IPlatformToolCatalog>(toolCatalog);
        services.AddSingleton<IHostEnvironment>(hostEnvironment);
        services.AddSingleton<IOptions<HyperlightExecutionSettings>>(
            Options.Create(new HyperlightExecutionSettings { Enabled = enabled }));
        services.AddSingleton<IHyperlightCodeActRunner>(Substitute.For<IHyperlightCodeActRunner>());
        services.AddSingleton<HyperlightSandboxedExecutor>();

        using var provider = services.BuildServiceProvider();
        provider.SeedInfrastructureTools();

        toolCatalog.DidNotReceive().RegisterPlatformTool(Arg.Is<AgenticSystem.Core.Interfaces.ITool>(tool =>
            tool.Id == "hyperlight-execute-code"));
    }

    [Fact]
    public void ToolIsRegisteredWhenEnabledInLab()
    {
        var hostEnvironment = Substitute.For<IHostEnvironment>();
        hostEnvironment.EnvironmentName.Returns("Lab");
        var toolCatalog = Substitute.For<AgenticSystem.Core.Interfaces.IPlatformToolCatalog>();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHttpClient();
        services.AddSingleton<AgenticSystem.Core.Interfaces.IPlatformToolCatalog>(toolCatalog);
        services.AddSingleton<IHostEnvironment>(hostEnvironment);
        services.AddSingleton<IOptions<HyperlightExecutionSettings>>(
            Options.Create(new HyperlightExecutionSettings { Enabled = true }));
        services.AddSingleton<IHyperlightCodeActRunner>(Substitute.For<IHyperlightCodeActRunner>());
        services.AddSingleton<HyperlightSandboxedExecutor>();

        using var provider = services.BuildServiceProvider();
        provider.SeedInfrastructureTools();

        toolCatalog.Received(1).RegisterPlatformTool(Arg.Is<AgenticSystem.Core.Interfaces.ITool>(tool =>
            tool.Id == "hyperlight-execute-code"));
    }

    private static HyperlightSandboxedExecutor CreateExecutor(
        bool enabled,
        string environmentName,
        IHyperlightCodeActRunner runner)
    {
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(environmentName);
        return new HyperlightSandboxedExecutor(
            Options.Create(new HyperlightExecutionSettings { Enabled = enabled }),
            environment,
            runner,
            Substitute.For<ILogger<HyperlightSandboxedExecutor>>());
    }
}
