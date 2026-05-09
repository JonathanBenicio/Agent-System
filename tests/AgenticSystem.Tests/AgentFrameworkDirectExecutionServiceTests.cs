using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.AgentFramework;
using FluentAssertions;
using AIChatResponse = Microsoft.Extensions.AI.ChatResponse;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace AgenticSystem.Tests;

public class AgentFrameworkDirectExecutionServiceTests
{
    private static SimpleSessionStoreAdapter CreateSessionStoreAdapter(ISessionStore? sessionStore = null)
        => new(
            sessionStore ?? Substitute.For<ISessionStore>(),
            Substitute.For<ILogger<SimpleSessionStoreAdapter>>());

    private static AgentFrameworkFactory CreateFrameworkFactory(IChatClient? chatClient = null)
    {
        chatClient ??= Substitute.For<IChatClient>();
        var loggerFactory = Substitute.For<ILoggerFactory>();
        loggerFactory.CreateLogger(Arg.Any<string>()).Returns(Substitute.For<ILogger>());
        var serviceProvider = Substitute.For<IServiceProvider>();
        return new AgentFrameworkFactory(chatClient, loggerFactory, serviceProvider);
    }

    [Fact]
    public void Constructor_ThrowsOnNullFrameworkFactory()
    {
        var act = () => new AgentFrameworkDirectExecutionService(
            null!,
            CreateSessionStoreAdapter(),
            Substitute.For<ISessionManager>(),
            Substitute.For<ILogger<AgentFrameworkDirectExecutionService>>(),
            Substitute.For<IServiceProvider>());

        act.Should().Throw<ArgumentNullException>().WithParameterName("frameworkFactory");
    }

    [Fact]
    public void Constructor_ThrowsOnNullSessionStore()
    {
        var act = () => new AgentFrameworkDirectExecutionService(
            CreateFrameworkFactory(),
            null!,
            Substitute.For<ISessionManager>(),
            Substitute.For<ILogger<AgentFrameworkDirectExecutionService>>(),
            Substitute.For<IServiceProvider>());

        act.Should().Throw<ArgumentNullException>().WithParameterName("sessionStore");
    }

    [Fact]
    public void Constructor_ThrowsOnNullSessionManager()
    {
        var act = () => new AgentFrameworkDirectExecutionService(
            CreateFrameworkFactory(),
            CreateSessionStoreAdapter(),
            null!,
            Substitute.For<ILogger<AgentFrameworkDirectExecutionService>>(),
            Substitute.For<IServiceProvider>());

        act.Should().Throw<ArgumentNullException>().WithParameterName("sessionManager");
    }

    [Fact]
    public void Constructor_ThrowsOnNullLogger()
    {
        var act = () => new AgentFrameworkDirectExecutionService(
            CreateFrameworkFactory(),
            CreateSessionStoreAdapter(),
            Substitute.For<ISessionManager>(),
            null!,
            Substitute.For<IServiceProvider>());

        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    [Fact]
    public async Task ExecuteDirectAsync_ReturnsFrameworkResponse_WhenFrameworkSucceeds()
    {
        var chatClient = Substitute.For<IChatClient>();
        chatClient.GetResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions?>(), Arg.Any<CancellationToken>())
            .Returns(new AIChatResponse(new ChatMessage(ChatRole.Assistant, "framework success")));

        var sessionStore = Substitute.For<ISessionStore>();
        var sessionData = new SessionData { Id = "session-1" };
        sessionStore.GetAsync("session-1", Arg.Any<CancellationToken>()).Returns(sessionData);
        sessionStore.SaveAsync(sessionData, Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var sessionManager = Substitute.For<ISessionManager>();
        var sut = new AgentFrameworkDirectExecutionService(
            CreateFrameworkFactory(chatClient),
            CreateSessionStoreAdapter(sessionStore),
            sessionManager,
            Substitute.For<ILogger<AgentFrameworkDirectExecutionService>>(),
            Substitute.For<IServiceProvider>());

        var agent = CreateAgent("TestAgent");

        var result = await sut.ExecuteDirectAsync(agent, "session-1", "hello", new UserContext { UserId = "u1" });

        result.Success.Should().BeTrue();
        result.Content.Should().Contain("framework success");
        result.AgentName.Should().Be("TestAgent");
        result.Metadata["frameworkStreaming"].Should().Be(false);
        await sessionManager.Received(1).AddEventAsync(
            "session-1",
            Arg.Is<AgentEvent>(e =>
                e.AgentName == "TestAgent"
                && e.UserInput == "hello"
                && e.AgentResponse.Contains("framework success")));
    }

    [Fact]
    public async Task ExecuteDirectAsync_ReturnsError_WhenFrameworkThrows()
    {
        var chatClient = Substitute.For<IChatClient>();
        chatClient.GetResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<AIChatResponse>(new InvalidOperationException("boom")));

        var sessionStore = Substitute.For<ISessionStore>();
        sessionStore.GetAsync("session-1", Arg.Any<CancellationToken>()).Returns(new SessionData { Id = "session-1" });
        var sessionManager = Substitute.For<ISessionManager>();
        var runtimeCoordinator = Substitute.For<IAgentRuntimeCoordinator>();
        var sut = new AgentFrameworkDirectExecutionService(
            CreateFrameworkFactory(chatClient),
            CreateSessionStoreAdapter(sessionStore),
            sessionManager,
            Substitute.For<ILogger<AgentFrameworkDirectExecutionService>>(),
            Substitute.For<IServiceProvider>(),
            runtimeCoordinator);

        var agent = CreateAgent("TestAgent");

        var result = await sut.ExecuteDirectAsync(agent, "session-1", "hello", new UserContext { UserId = "u1" });

        result.Success.Should().BeFalse();
        result.Content.Should().Contain("Framework error: boom");
        result.AgentName.Should().Be("TestAgent");
    }

    private static IAgent CreateAgent(string name)
    {
        var agent = Substitute.For<IAgent>();
        agent.Name.Returns(name);
        agent.Description.Returns("Test agent");
        agent.Domain.Returns("test");
        agent.Tier.Returns(AgentTier.Specialist);
        agent.Instructions.Returns("You are a test agent.");
        agent.AvailableTools.Returns(Array.Empty<string>());
        return agent;
    }
}