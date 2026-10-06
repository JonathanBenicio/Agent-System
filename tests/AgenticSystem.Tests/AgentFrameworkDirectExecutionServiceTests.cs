#pragma warning disable MAAI001 // These tests exercise the experimental MAF session-store contract used by the backend.

using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.AgentFramework;
using FluentAssertions;
using AIChatResponse = Microsoft.Extensions.AI.ChatResponse;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using AgenticSystem.Core.Skills;
using AgenticSystem.Core.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;
using AgenticSystem.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace AgenticSystem.Tests;

public class AgentFrameworkDirectExecutionServiceTests
{
    [Fact]
    public async Task WorkflowBannerStep_UsesImageModelAndAuthorizedToolsToRenderFinalFile()
    {
        var root = Path.Combine("tests", "TestResults", "banner-workflow", Guid.NewGuid().ToString("N"));
        var tenantDirectory = Path.GetFullPath(Path.Combine(root, "wwwroot", "uploads", "tenant-a"));
        Directory.CreateDirectory(tenantDirectory);
        var source = Path.Combine(tenantDirectory, "source.png");
        using (var image = new Image<Rgba32>(1000, 800))
            await image.SaveAsPngAsync(source);

        try
        {
            var calls = 0;
            var sawImage = false;
            var sawModel = false;
            var client = Substitute.For<IChatClient>();
            client.GetResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions?>(), Arg.Any<CancellationToken>())
                .Returns(call =>
                {
                    var messages = call.ArgAt<IEnumerable<ChatMessage>>(0).ToList();
                    var options = call.ArgAt<ChatOptions?>(1);
                    sawModel |= options?.ModelId == "banner-model";
                    sawImage |= messages.Any(message => message.Contents.OfType<DataContent>().Any());
                    var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, string.Empty));
                    switch (Interlocked.Increment(ref calls))
                    {
                        case 1:
                            response.Messages[0].Contents.Add(new FunctionCallContent("clean-1", "CleanImageAsync",
                                new Dictionary<string, object?> { ["path"] = source, ["items"] = "fios" }));
                            break;
                        case 2:
                            response.Messages[0].Contents.Add(new FunctionCallContent("render-1", "RenderBannerAsync",
                                new Dictionary<string, object?>
                                {
                                    ["path"] = Path.Combine(tenantDirectory, "clean_source.png"),
                                    ["price"] = 650000m, ["beds"] = 3, ["location"] = "Centro", ["phone"] = "0000"
                                }));
                            break;
                        default:
                            response.Messages[0].Contents.Add(new TextContent("Banner pronto"));
                            break;
                    }
                    return Task.FromResult(response);
                });

            var permissions = Substitute.For<IPermissionService>();
            permissions.HasPermissionAsync("user-a", Arg.Any<string>(), Permission.Execute, Arg.Any<CancellationToken>())
                .Returns(true);
            var host = Substitute.For<IHostEnvironment>();
            host.ContentRootPath.Returns(Path.GetFullPath(root));
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton(permissions);
            services.AddSingleton(host);
            services.AddSingleton<BannerProductionSkills>();
            AddFidesServices(services);
            using var provider = services.BuildServiceProvider();
            var sessions = new InMemorySessionStore();
            await sessions.SaveAsync(new SessionData { Id = "banner-session", UserId = "user-a", TenantId = "tenant-a" });
            var adapter = CreateSessionStoreAdapter(sessions);
            var factory = new AgentFrameworkFactory(client, provider.GetRequiredService<ILoggerFactory>(), provider);
            var sut = new AgentFrameworkDirectExecutionService(factory, adapter,
                Substitute.For<ISessionManager>(), provider.GetRequiredService<ILogger<AgentFrameworkDirectExecutionService>>(), provider);
            var agent = CreateAgent("EditorChefe");
            agent.AvailableTools.Returns(["CleanImageAsync", "RenderBannerAsync"]);
            var result = await sut.ExecuteDirectAsync(agent, "banner-session", "Renderize o banner",
                new UserContext
                {
                    UserId = "user-a", TenantId = "tenant-a",
                    WorkflowOptions = new WorkflowAgentOptions("banner-session", "banner-model",
                        ["CleanImageAsync", "RenderBannerAsync"], source)
                });

            result.Success.Should().BeTrue(result.ErrorMessage);
            sawImage.Should().BeTrue();
            sawModel.Should().BeTrue();
            calls.Should().BeGreaterThanOrEqualTo(3);
            File.Exists(Path.Combine(tenantDirectory, "banner_clean_source.png")).Should().BeTrue();

            calls = 0;
            permissions.HasPermissionAsync("user-a", Arg.Any<string>(), Permission.Execute, Arg.Any<CancellationToken>())
                .Returns(false);
            var denied = await sut.ExecuteDirectAsync(agent, "banner-session", "Tente novamente",
                new UserContext
                {
                    UserId = "user-a", TenantId = "tenant-a",
                    WorkflowOptions = new WorkflowAgentOptions("banner-session", "banner-model",
                        ["CleanImageAsync", "RenderBannerAsync"], source)
                });
            denied.Success.Should().BeFalse();
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
    private static SimpleSessionStoreAdapter CreateSessionStoreAdapter(ISessionStore? sessionStore = null)
        => new(
            sessionStore ?? Substitute.For<ISessionStore>(),
            Substitute.For<ILogger<SimpleSessionStoreAdapter>>());

    private static AgentFrameworkFactory CreateFrameworkFactory(IChatClient? chatClient = null)
    {
        chatClient ??= Substitute.For<IChatClient>();
        var loggerFactory = Substitute.For<ILoggerFactory>();
        loggerFactory.CreateLogger(Arg.Any<string>()).Returns(Substitute.For<ILogger>());
        var services = new ServiceCollection();
        AddFidesServices(services);
        var serviceProvider = services.BuildServiceProvider();
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
        var sessionData = new SessionData { Id = "session-1", UserId = "u1", TenantId = "tenant-a" };
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

        var result = await sut.ExecuteDirectAsync(agent, "session-1", "hello", new UserContext { UserId = "u1", TenantId = "tenant-a" });

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
        sessionStore.GetAsync("session-1", Arg.Any<CancellationToken>()).Returns(new SessionData { Id = "session-1", UserId = "u1", TenantId = "tenant-a" });
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

        var result = await sut.ExecuteDirectAsync(agent, "session-1", "hello", new UserContext { UserId = "u1", TenantId = "tenant-a" });

        result.Success.Should().BeFalse();
        result.Content.Should().Contain("Framework error: boom");
        result.AgentName.Should().Be("TestAgent");
    }

    private static void AddFidesServices(ServiceCollection services)
    {
        var tenant = Substitute.For<ITenantContextAccessor>();
        tenant.CurrentTenantId.Returns("tenant-a");
        services.AddSingleton(tenant);
        services.AddSingleton<IFidesTenantPolicyStore>(new InMemoryFidesTenantPolicyStore(tenant));
        var scanner = Substitute.For<IFidesMediaScanner>();
        scanner.ScanAndRedactAsync(Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<string>(),
            Arg.Any<FidesTenantPolicy>(), Arg.Any<CancellationToken>())
            .Returns(new FidesMediaScanResult { Status = FidesMediaScanStatus.NoSensitiveContent });
        services.AddSingleton(scanner);
        services.AddSingleton(Options.Create(new FidesSecuritySettings()));
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
