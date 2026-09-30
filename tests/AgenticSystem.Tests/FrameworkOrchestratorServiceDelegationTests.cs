#pragma warning disable MAAI001 // The test exercises the experimental MAF session/function-call APIs used by the backend.

using System.Collections.Generic;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;
using AgenticSystem.Infrastructure.AgentFramework;
using FluentAssertions;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Tests;

public class FrameworkOrchestratorServiceDelegationTests
{
    [Fact]
    public async Task ExecuteAsync_WhenNoSpecialistIsAvailable_ReturnsSupervisorAnswer()
    {
        var failProvider = false;
        var chatClient = Substitute.For<IChatClient>();
        chatClient.GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var token = call.Arg<CancellationToken>();
                if (token.IsCancellationRequested)
                    return Task.FromCanceled<ChatResponse>(token);
                if (failProvider)
                    return Task.FromException<ChatResponse>(new HttpRequestException("provider unavailable"));
                return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "Direct supervisor answer")));
            });

        var agentFactory = Substitute.For<IAgentFactory>();
        agentFactory.GetAllAgentsAsync().Returns(Task.FromResult<IEnumerable<AgentInfo>>([]));
        var sessionStore = new InMemorySessionStore();
        var conversation = new SessionData
        {
            Id = "orchestrator-no-specialist",
            TenantId = "tenant-a",
            UserId = "user-a",
            StartedAt = DateTime.UtcNow
        };
        await sessionStore.SaveAsync(conversation);

        var skillManager = Substitute.For<ISkillManager>();
        skillManager.BuildEnrichedPromptAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(call => Task.FromResult(call.ArgAt<string>(2)));
        var preprocessing = Substitute.For<IAgentExecutionPreProcessingPipeline>();
        preprocessing.ProcessAsync(Arg.Any<AgentExecutionPreProcessingContext>(), Arg.Any<CancellationToken>())
            .Returns(call => new AgentExecutionPreProcessingResult { EffectiveInput = call.Arg<AgentExecutionPreProcessingContext>().Input });
        var postprocessing = Substitute.For<IAgentExecutionPostProcessingPipeline>();
        postprocessing.ProcessAsync(Arg.Any<AgentExecutionPostProcessingContext>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<AgentExecutionPostProcessingContext>().Response);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IChatClient>(chatClient);
        services.AddSingleton<IAgentFactory>(agentFactory);
        services.AddSingleton<ISessionStore>(sessionStore);
        services.AddSingleton<ISkillManager>(skillManager);
        services.AddSingleton<AgentSessionStore, SimpleSessionStoreAdapter>();
        services.AddSingleton<AgentIsolationKeyProvider, TestIsolationKeyProvider>();
        services.AddSingleton(OrchestratorMetadata.Default);
        services.AddSingleton<OrchestratorInstructionService>();
        services.AddSingleton<OrchestratorAuxiliaryToolService>();
        services.AddScoped<OrchestratorContextState>();
        services.AddScoped<AgentFrameworkFactory>(sp => new AgentFrameworkFactory(
            sp.GetRequiredService<IChatClient>(),
            sp.GetRequiredService<ILoggerFactory>(),
            sp,
            skillManager: sp.GetRequiredService<ISkillManager>(),
            sessionStore: sp.GetRequiredService<AgentSessionStore>()));
        services.AddScoped<OrchestratorToolBindingService>();
        services.AddScoped<OrchestratorHostBuilder>();
        services.AddScoped<OrchestratorContext>(sp =>
        {
            var state = sp.GetRequiredService<OrchestratorContextState>();
            return new OrchestratorContext(state.OrchestratorAgent!, state.SpecialistBindings?.ToList() ?? []);
        });
        services.AddKeyedScoped<AIAgent>(OrchestratorMetadata.Default.Name,
            (sp, _) => sp.GetRequiredService<OrchestratorContext>().OrchestratorAgent);

        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        var sut = new FrameworkOrchestratorService(
            OrchestratorMetadata.Default,
            provider.GetRequiredService<IServiceScopeFactory>(),
            preprocessing,
            Substitute.For<IAgentRuntimeCoordinator>(),
            Substitute.For<ILLMRuntimeContextAccessor>(),
            postprocessing,
            Substitute.For<ILogger<FrameworkOrchestratorService>>());

        var result = await sut.ExecuteAsync(
            conversation.Id,
            "Explain the platform",
            new UserContext { UserId = "user-a", TenantId = "tenant-a" });

        result.Success.Should().BeTrue();
        result.Content.Should().Be("Direct supervisor answer");
        result.AgentName.Should().Be(OrchestratorMetadata.Default.Name);
        result.Metadata.Should().NotContainKey("delegatedTo");
        conversation.RuntimeSettings.Keys.Should().Contain(key => key.StartsWith("frameworkSessionState:orchestrator:scope:", StringComparison.Ordinal));

        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        var cancelledResult = await sut.ExecuteAsync(
            conversation.Id,
            "This request is cancelled",
            new UserContext { UserId = "user-a", TenantId = "tenant-a" },
            cancelled.Token);
        cancelledResult.Success.Should().BeFalse();
        cancelledResult.ErrorMessage.Should().NotBeNullOrWhiteSpace();

        failProvider = true;
        var providerFailure = await sut.ExecuteAsync(
            conversation.Id,
            "This request fails at provider",
            new UserContext { UserId = "user-a", TenantId = "tenant-a" });
        providerFailure.Success.Should().BeFalse();
        providerFailure.ErrorMessage.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ExecuteAsync_InvokesBoundSpecialistAndPersistsSupervisorAndSpecialistSessions()
    {
        var callCount = 0;
        var observedToolNames = new System.Collections.Concurrent.ConcurrentQueue<IReadOnlyList<string>>();
        var chatClient = Substitute.For<IChatClient>();
        chatClient.GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var options = callInfo.Arg<ChatOptions?>();
                observedToolNames.Enqueue(options?.Tools?.Select(tool => tool.Name).ToList() ?? []);
                var call = Interlocked.Increment(ref callCount);
                if (call == 1)
                {
                    var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, string.Empty));
                    response.Messages[0].Contents.Add(new FunctionCallContent(
                        "call-finance",
                        "financeagent",
                        new Dictionary<string, object?> { ["query"] = "Summarize the revenue" }));
                    response.Messages[0].Contents.Add(new FunctionCallContent(
                        "call-support",
                        "supportagent",
                        new Dictionary<string, object?> { ["query"] = "Check account support history" }));
                    return Task.FromResult(response);
                }

                return Task.FromResult(new ChatResponse(new ChatMessage(
                    ChatRole.Assistant,
                    call < 4 ? "Specialist result" : "Consolidated finance answer")));
            });

        var specialist = Substitute.For<IAgent>();
        specialist.Name.Returns("FinanceAgent");
        specialist.Description.Returns("Specialist for finance requests.");
        specialist.Domain.Returns("finance");
        specialist.Tier.Returns(AgentTier.Specialist);
        specialist.Instructions.Returns("Answer finance questions accurately.");
        specialist.IsActive.Returns(true);
        specialist.AvailableTools.Returns(["finance.read"]);

        var supportSpecialist = Substitute.For<IAgent>();
        supportSpecialist.Name.Returns("SupportAgent");
        supportSpecialist.Description.Returns("Specialist for support history.");
        supportSpecialist.Domain.Returns("support");
        supportSpecialist.Tier.Returns(AgentTier.Specialist);
        supportSpecialist.Instructions.Returns("Review support history accurately.");
        supportSpecialist.IsActive.Returns(true);
        supportSpecialist.AvailableTools.Returns(["support.read"]);

        var unusedSpecialist = Substitute.For<IAgent>();
        unusedSpecialist.Name.Returns("UnusedAgent");
        unusedSpecialist.Description.Returns("A bound specialist the supervisor does not call.");
        unusedSpecialist.Domain.Returns("unused");
        unusedSpecialist.Tier.Returns(AgentTier.Specialist);
        unusedSpecialist.Instructions.Returns("Do not call unless needed.");
        unusedSpecialist.IsActive.Returns(true);
        unusedSpecialist.AvailableTools.Returns(["unused.read"]);

        var specialistInfo = new AgentInfo
        {
            Name = specialist.Name,
            Description = specialist.Description,
            Domain = specialist.Domain,
            Tier = specialist.Tier,
            IsActive = true,
            AvailableTools = ["finance.read"]
        };
        var unavailableInfo = new AgentInfo
        {
            Name = "UnavailableAgent",
            Description = "This agent fails to materialize.",
            Domain = "unavailable",
            Tier = AgentTier.Specialist,
            IsActive = true
        };
        var supportInfo = new AgentInfo
        {
            Name = "SupportAgent",
            Description = supportSpecialist.Description,
            Domain = supportSpecialist.Domain,
            Tier = supportSpecialist.Tier,
            IsActive = true,
            AvailableTools = ["support.read"]
        };
        var unusedInfo = new AgentInfo
        {
            Name = "UnusedAgent",
            Description = unusedSpecialist.Description,
            Domain = unusedSpecialist.Domain,
            Tier = unusedSpecialist.Tier,
            IsActive = true,
            AvailableTools = ["unused.read"]
        };
        var agentFactory = Substitute.For<IAgentFactory>();
        agentFactory.GetAllAgentsAsync().Returns(Task.FromResult<IEnumerable<AgentInfo>>([specialistInfo, supportInfo, unusedInfo, unavailableInfo]));
        agentFactory.ResolveAgentAsync(Arg.Is<AgentInfo>(info => info.Name == "FinanceAgent")).Returns(specialist);
        agentFactory.ResolveAgentAsync(Arg.Is<AgentInfo>(info => info.Name == "SupportAgent")).Returns(supportSpecialist);
        agentFactory.ResolveAgentAsync(Arg.Is<AgentInfo>(info => info.Name == "UnusedAgent")).Returns(unusedSpecialist);
        agentFactory.ResolveAgentAsync(Arg.Is<AgentInfo>(info => info.Name == "UnavailableAgent"))
            .Returns(_ => Task.FromException<IAgent>(new InvalidOperationException("agent unavailable")));

        var sessionStore = new InMemorySessionStore();
        var conversation = new SessionData
        {
            Id = "orchestrator-session-1",
            TenantId = "tenant-a",
            UserId = "user-a",
            StartedAt = DateTime.UtcNow
        };
        await sessionStore.SaveAsync(conversation);

        var preProcessing = Substitute.For<IAgentExecutionPreProcessingPipeline>();
        preProcessing.ProcessAsync(Arg.Any<AgentExecutionPreProcessingContext>(), Arg.Any<CancellationToken>())
            .Returns(call => new AgentExecutionPreProcessingResult
            {
                EffectiveInput = call.Arg<AgentExecutionPreProcessingContext>().Input
            });

        var postProcessing = Substitute.For<IAgentExecutionPostProcessingPipeline>();
        postProcessing.ProcessAsync(Arg.Any<AgentExecutionPostProcessingContext>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<AgentExecutionPostProcessingContext>().Response);

        var skillManager = Substitute.For<ISkillManager>();
        skillManager.BuildEnrichedPromptAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(call => Task.FromResult(call.ArgAt<string>(2)));

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IChatClient>(chatClient);
        services.AddSingleton<IAgentFactory>(agentFactory);
        services.AddSingleton<ISessionStore>(sessionStore);
        services.AddSingleton<ISkillManager>(skillManager);
        services.AddSingleton<AgentSessionStore, SimpleSessionStoreAdapter>();
        services.AddSingleton<AgentIsolationKeyProvider, TestIsolationKeyProvider>();
        services.AddSingleton(OrchestratorMetadata.Default);
        services.AddSingleton<OrchestratorInstructionService>();
        services.AddSingleton<OrchestratorAuxiliaryToolService>();
        services.AddScoped<OrchestratorContextState>();
        services.AddScoped<AgentFrameworkFactory>(sp => new AgentFrameworkFactory(
            sp.GetRequiredService<IChatClient>(),
            sp.GetRequiredService<ILoggerFactory>(),
            sp,
            skillManager: sp.GetRequiredService<ISkillManager>(),
            sessionStore: sp.GetRequiredService<AgentSessionStore>()));
        services.AddScoped<OrchestratorToolBindingService>();
        services.AddScoped<OrchestratorHostBuilder>();
        services.AddScoped<OrchestratorContext>(sp =>
        {
            var state = sp.GetRequiredService<OrchestratorContextState>();
            return new OrchestratorContext(
                state.OrchestratorAgent ?? throw new InvalidOperationException("Supervisor was not built."),
                state.SpecialistBindings?.ToList() ?? []);
        });
        services.AddKeyedScoped<AIAgent>(OrchestratorMetadata.Default.Name,
            (sp, _) => sp.GetRequiredService<OrchestratorContext>().OrchestratorAgent);

        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });

        var sut = new FrameworkOrchestratorService(
            OrchestratorMetadata.Default,
            provider.GetRequiredService<IServiceScopeFactory>(),
            preProcessing,
            Substitute.For<IAgentRuntimeCoordinator>(),
            Substitute.For<ILLMRuntimeContextAccessor>(),
            postProcessing,
            Substitute.For<ILogger<FrameworkOrchestratorService>>());

        var result = await sut.ExecuteAsync(
            conversation.Id,
            "What happened to revenue?",
            new UserContext { UserId = "user-a", TenantId = "tenant-a" });

        result.Success.Should().BeTrue();
        result.Content.Should().Be("Consolidated finance answer");
        result.AgentName.Should().Be("FinanceAgent");
        result.Metadata["delegatedTo"].Should().Be("FinanceAgent");
        callCount.Should().Be(4);
        observedToolNames.First().Should().Contain("financeagent").And.Contain("supportagent").And.Contain("unusedagent").And.NotContain("unavailableagent");
        await agentFactory.Received(1).ResolveAgentAsync(Arg.Is<AgentInfo>(info => info.Name == "FinanceAgent"));
        await agentFactory.Received(1).ResolveAgentAsync(Arg.Is<AgentInfo>(info => info.Name == "SupportAgent"));
        await agentFactory.Received(1).ResolveAgentAsync(Arg.Is<AgentInfo>(info => info.Name == "UnusedAgent"));
        await agentFactory.Received(1).ResolveAgentAsync(Arg.Is<AgentInfo>(info => info.Name == "UnavailableAgent"));

        conversation.RuntimeSettings.Keys.Should().Contain(key => key.StartsWith("frameworkSessionState:orchestrator:scope:", StringComparison.Ordinal));
        conversation.RuntimeSettings.Keys.Should().Contain(key => key.StartsWith("frameworkSessionState:financeagent:scope:", StringComparison.Ordinal));
        conversation.RuntimeSettings.Keys.Should().Contain(key => key.StartsWith("frameworkSessionState:supportagent:scope:", StringComparison.Ordinal));
        conversation.RuntimeSettings.Keys.Should().NotContain(key => key.StartsWith("frameworkSessionState:unusedagent:scope:", StringComparison.Ordinal));
    }

    private sealed class TestIsolationKeyProvider : AgentIsolationKeyProvider
    {
        public override ValueTask<string?> GetIsolationKeyAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<string?>("tenant-a:user-a");
    }
}
