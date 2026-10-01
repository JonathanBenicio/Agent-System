#pragma warning disable MAAI001 // The tests exercise experimental MAF hosting APIs used by the API host.

using FluentAssertions;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Agents.AI.Hosting.AGUI.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;

namespace AgenticSystem.Tests;

public class HostingProtocolRegistrationTests
{
    [Fact]
    public async Task MapAGUIServer_ResolvesAgentAndSessionStoreWithRootScopeValidationEnabled()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Host.UseDefaultServiceProvider((_, options) =>
        {
            options.ValidateScopes = true;
            options.ValidateOnBuild = true;
        });
        builder.Services.AddAGUIServer();
        builder.Services.AddSingleton<AgentIsolationKeyProvider, TestIsolationKeyProvider>();
        builder.Services.AddKeyedSingleton<AIAgent>("AgenticSystem", (_, _) =>
        {
            var agent = Substitute.For<AIAgent>();
            agent.Name.Returns("AgenticSystem");
            return agent;
        });
        builder.Services.AddKeyedSingleton<AgentSessionStore>("AgenticSystem", (_, _) => Substitute.For<AgentSessionStore>());

        await using var app = builder.Build();
        var map = () => app.MapAGUIServer("AgenticSystem", "/agui");

        map.Should().NotThrow();
    }

    [Fact]
    public async Task MapA2AHttpJson_ResolvesServerWithRootScopeValidationEnabled()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Host.UseDefaultServiceProvider((_, options) =>
        {
            options.ValidateScopes = true;
            options.ValidateOnBuild = true;
        });
        builder.Services.AddSingleton<AgentIsolationKeyProvider, TestIsolationKeyProvider>();
        builder.Services.AddKeyedSingleton<AIAgent>("AgenticSystem", (_, _) =>
        {
            var agent = Substitute.For<AIAgent>();
            agent.Name.Returns("AgenticSystem");
            return agent;
        });
        builder.Services.AddKeyedSingleton<AgentSessionStore>("AgenticSystem", (_, _) => Substitute.For<AgentSessionStore>());
        builder.Services.AddA2AServer("AgenticSystem");

        await using var app = builder.Build();
        var map = () => app.MapA2AHttpJson("AgenticSystem", "/a2a");

        map.Should().NotThrow();
    }

    private sealed class TestIsolationKeyProvider : AgentIsolationKeyProvider
    {
        public override ValueTask<string?> GetIsolationKeyAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<string?>("tenant:user");
    }
}
