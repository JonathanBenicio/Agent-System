#pragma warning disable MAAI001 // Uses the production MAF session store adapter.
using System.Runtime.CompilerServices;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;
using AgenticSystem.Infrastructure.AgentFramework;
using AgenticSystem.Infrastructure.Configuration;
using AgenticSystem.Infrastructure.Security;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgenticSystem.Tests;

public sealed class FidesDirectDispatchTests
{
    private static readonly string Token = "sk-" + new string('a', 40);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DirectExecution_ProtectsActualProviderInputIncludingInstructions(bool streaming)
    {
        using var setup = new Setup();
        var agent = CreateAgent($"Instructions token={Token}");
        var service = setup.CreateDirectService(streaming);
        var result = await service.ExecuteDirectAsync(agent, "session", $"hello {Token} me@example.com",
            new UserContext { TenantId = "tenant-a", UserId = "user-a" });

        result.Success.Should().BeTrue(result.ErrorMessage);
        setup.Provider.Requests.Should().ContainSingle();
        var request = setup.Provider.Requests.Single();
        setup.Provider.Instructions.Single().Should().Contain("Instructions")
            .And.Contain("[TOKEN MASCARADO]").And.NotContain(Token);
        string.Join("\n", request.Select(message => message.Text)).Should().NotContain(Token);
        request.Last(message => message.Role == ChatRole.User).Text.Should().Contain("[TOKEN MASCARADO]")
            .And.Contain("[EMAIL MASCARADO]");
    }

    [Fact]
    public async Task TenantPoliciesAreAppliedAtDispatchAndMandatoryTokensStayEnabled()
    {
        using var setup = new Setup();
        await setup.Policy.SaveAsync(new Dictionary<string, bool> { [FidesDetectorCatalog.Email] = false }, "user-a");
        var agent = await setup.Factory.CreateFromSpecificationAsync(new AgentSpecification
            { Name = "dynamic", Instructions = "Keep instructions", Description = "dynamic" });
        await agent.RunAsync($"me@example.com {Token}");
        setup.Provider.Requests.Last().Last().Text.Should().Contain("me@example.com").And.NotContain(Token);

        using var tenantB = setup.Accessor.BeginScope(new TenantContext { TenantId = "tenant-b" });
        await agent.RunAsync($"me@example.com {Token}");
        setup.Provider.Requests.Last().Last().Text.Should().Contain("[EMAIL MASCARADO]").And.NotContain(Token);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PolicyErrorBlocksDirectExecutionWithoutCallingProvider(bool streaming)
    {
        using var setup = new Setup(policyFailure: true);
        var result = await setup.CreateDirectService(streaming).ExecuteDirectAsync(CreateAgent("instructions"),
            "session", Token, new UserContext { TenantId = "tenant-a", UserId = "user-a" });
        result.Success.Should().BeFalse();
        setup.Provider.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task MissingSecurityServicesFailClosed()
    {
        using var provider = new ServiceCollection().AddLogging().BuildServiceProvider();
        var client = new CapturingProvider();
        var factory = new AgentFrameworkFactory(client, provider.GetRequiredService<ILoggerFactory>(), provider);
        var agent = await factory.CreateFromAgentAsync(CreateAgent("instructions"));
        var act = async () => await agent.RunAsync(Token);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*FIDES*");
        client.Requests.Should().BeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MediaFailureAndTimeoutBlockProvider(bool timeout)
    {
        using var setup = new Setup();
        var scanner = Substitute.For<IFidesMediaScanner>();
        scanner.ScanAndRedactAsync(Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<string>(),
            Arg.Any<FidesTenantPolicy>(), Arg.Any<CancellationToken>())
            .Returns(timeout ? new TaskCompletionSource<FidesMediaScanResult>().Task
                : Task.FromException<FidesMediaScanResult>(new InvalidOperationException("scanner failed")));
        using var services = setup.BuildServices(scanner);
        var factory = new AgentFrameworkFactory(setup.Provider, services.GetRequiredService<ILoggerFactory>(), services);
        var agent = await factory.CreateFromAgentAsync(CreateAgent("instructions"));
        var message = new ChatMessage(ChatRole.User, "image");
        message.Contents.Add(new DataContent(new byte[] { 1, 2 }, "image/png"));
        var act = async () => await agent.RunAsync([message]);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*FIDES*");
        setup.Provider.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task ToolLoopRetainsCallIdsAndRedactsToolResultBeforeNextProviderRound()
    {
        using var setup = new Setup();
        setup.Provider.CallTool = true;
        var tool = AIFunctionFactory.Create(() => new { secret = Token, detail = "safe" }, "get_secret");
        var agent = await setup.Factory.CreateFromAgentAsync(CreateAgent("Use get_secret"), [tool]);
        var response = await agent.RunAsync("Run tool");
        response.Text.Should().Be("ok");
        setup.Provider.Requests.Should().HaveCount(2);
        var round = setup.Provider.Requests[1];
        var call = round.SelectMany(m => m.Contents).OfType<FunctionCallContent>().Single();
        call.CallId.Should().Be("call-1");
        var result = round.SelectMany(m => m.Contents).OfType<FunctionResultContent>().Single();
        result.CallId.Should().Be("call-1");
        System.Text.Json.JsonSerializer.Serialize(result.Result).Should().NotContain(Token)
            .And.Contain("TOKEN MASCARADO").And.Contain("safe");
    }

    [Fact]
    public async Task HostedSupervisorProtectsInstructionsAndStreamingAtProviderBoundary()
    {
        using var setup = new Setup();
        using var services = setup.BuildServices(new FailClosedFidesMediaScanner());
        var loggers = services.GetRequiredService<ILoggerFactory>();
        var skills = Substitute.For<ISkillManager>();
        skills.BuildEnrichedPromptAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns($"Supervisor instructions {Token}");
        var host = new OrchestratorHostBuilder(setup.Provider, loggers, services,
            new OrchestratorMetadata("supervisor", "supervisor description"),
            new OrchestratorInstructionService(loggers.CreateLogger<OrchestratorInstructionService>()),
            new OrchestratorToolBindingService(Substitute.For<IAgentFactory>(), setup.Factory,
                loggers.CreateLogger<OrchestratorToolBindingService>()),
            new OrchestratorAuxiliaryToolService(loggers.CreateLogger<OrchestratorAuxiliaryToolService>()),
            loggers.CreateLogger<OrchestratorHostBuilder>(), skills);
        var agent = await host.BuildAsync([]);
        await foreach (var update in agent.RunStreamingAsync($"hello {Token}")) { }
        setup.Provider.Requests.Should().ContainSingle();
        setup.Provider.Instructions.Single().Should().Contain("Supervisor instructions")
            .And.Contain("[TOKEN MASCARADO]").And.NotContain(Token);
        setup.Provider.Requests.Single().Last().Text.Should().NotContain(Token);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PolicyTimeoutOrWrongTenantBlocksProvider(bool wrongTenant)
    {
        using var setup = new Setup();
        var policy = Substitute.For<IFidesTenantPolicyStore>();
        policy.GetAsync(Arg.Any<CancellationToken>()).Returns(wrongTenant
            ? Task.FromResult(new FidesTenantPolicy { TenantId = "tenant-b" })
            : new TaskCompletionSource<FidesTenantPolicy>().Task);
        using var services = new ServiceCollection().AddLogging()
            .AddSingleton<ITenantContextAccessor>(setup.Accessor).AddSingleton(policy)
            .AddSingleton<IFidesMediaScanner>(new FailClosedFidesMediaScanner())
            .AddSingleton(Options.Create(new FidesSecuritySettings { MediaScanTimeoutSeconds = 1 }))
            .BuildServiceProvider();
        var agent = await new AgentFrameworkFactory(setup.Provider,
            services.GetRequiredService<ILoggerFactory>(), services).CreateFromAgentAsync(CreateAgent("instructions"));
        var act = async () => await agent.RunAsync(Token);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*FIDES*");
        setup.Provider.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task CorruptStoredPolicyCannotDisableMandatoryCredentialRedactionAtProvider()
    {
        using var setup = new Setup();
        var policy = Substitute.For<IFidesTenantPolicyStore>();
        policy.GetAsync(Arg.Any<CancellationToken>()).Returns(new FidesTenantPolicy
        {
            TenantId = "tenant-a",
            EnabledDetectors = new Dictionary<string, bool> { [FidesDetectorCatalog.CredentialToken] = false }
        });
        using var services = new ServiceCollection().AddLogging()
            .AddSingleton<ITenantContextAccessor>(setup.Accessor).AddSingleton(policy)
            .AddSingleton<IFidesMediaScanner>(new FailClosedFidesMediaScanner())
            .AddSingleton(Options.Create(new FidesSecuritySettings())).BuildServiceProvider();
        var agent = await new AgentFrameworkFactory(setup.Provider,
            services.GetRequiredService<ILoggerFactory>(), services).CreateFromAgentAsync(CreateAgent(Token));
        await agent.RunAsync(Token);
        setup.Provider.Requests.Single().Last().Text.Should().Be("[TOKEN MASCARADO]");
        setup.Provider.Instructions.Single().Should().Be("[TOKEN MASCARADO]");
    }

    private static IAgent CreateAgent(string instructions)
    {
        var agent = Substitute.For<IAgent>();
        agent.Name.Returns("test-agent");
        agent.Description.Returns("Test agent");
        agent.Domain.Returns("test");
        agent.Tier.Returns(AgentTier.Specialist);
        agent.Instructions.Returns(instructions);
        agent.AvailableTools.Returns(Array.Empty<string>());
        return agent;
    }

    private sealed class Setup : IDisposable
    {
        public TenantContextAccessor Accessor { get; } = new();
        public IFidesTenantPolicyStore Policy { get; }
        public CapturingProvider Provider { get; } = new();
        public AgentFrameworkFactory Factory { get; }
        private readonly IDisposable _scope;
        private readonly ServiceProvider _services;

        public Setup(bool policyFailure = false)
        {
            _scope = Accessor.BeginScope(new TenantContext { TenantId = "tenant-a" });
            Policy = new InMemoryFidesTenantPolicyStore(Accessor);
            if (policyFailure)
            {
                Policy = Substitute.For<IFidesTenantPolicyStore>();
                Policy.GetAsync(Arg.Any<CancellationToken>())
                    .Returns(Task.FromException<FidesTenantPolicy>(new InvalidOperationException("policy unavailable")));
            }
            _services = BuildServices(new FailClosedFidesMediaScanner());
            Factory = new AgentFrameworkFactory(Provider, _services.GetRequiredService<ILoggerFactory>(), _services);
        }

        public ServiceProvider BuildServices(IFidesMediaScanner scanner) => new ServiceCollection()
            .AddLogging().AddSingleton<ITenantContextAccessor>(Accessor).AddSingleton(Policy)
            .AddSingleton(scanner).AddSingleton(Options.Create(new FidesSecuritySettings { MediaScanTimeoutSeconds = 1 }))
            .BuildServiceProvider();

        public AgentFrameworkDirectExecutionService CreateDirectService(bool streaming)
        {
            var sessions = new InMemorySessionStore();
            sessions.SaveAsync(new SessionData { Id = "session", TenantId = "tenant-a", UserId = "user-a" })
                .GetAwaiter().GetResult();
            return new AgentFrameworkDirectExecutionService(Factory,
                new SimpleSessionStoreAdapter(sessions, _services.GetRequiredService<ILogger<SimpleSessionStoreAdapter>>()),
                Substitute.For<ISessionManager>(), _services.GetRequiredService<ILogger<AgentFrameworkDirectExecutionService>>(),
                _services, enableStreaming: streaming);
        }

        public void Dispose() { _services.Dispose(); _scope.Dispose(); }
    }

    private sealed class CapturingProvider : IChatClient
    {
        public List<List<ChatMessage>> Requests { get; } = [];
        public bool CallTool { get; set; }
        public List<string?> Instructions { get; } = [];
        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(messages.Select(message => message.Clone()).ToList());
            Instructions.Add(options?.Instructions);
            var message = new ChatMessage(ChatRole.Assistant, "ok");
            if (CallTool && Requests.Count == 1)
            {
                message.Contents = [new FunctionCallContent("call-1", "get_secret", new Dictionary<string, object?>())];
            }
            return Task.FromResult(new ChatResponse(message));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages,
            ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            Requests.Add(messages.Select(message => message.Clone()).ToList());
            Instructions.Add(options?.Instructions);
            await Task.CompletedTask;
            yield return new ChatResponseUpdate(ChatRole.Assistant, "ok");
        }
        public object? GetService(Type type, object? key = null) => type == typeof(IChatClient) ? this : null;
        public void Dispose() { }
    }
}
