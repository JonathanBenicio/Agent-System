using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.AI;
using AgenticSystem.Infrastructure.Configuration;
using AgenticSystem.Infrastructure.AgentFramework;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Runtime.CompilerServices;
using AIChatResponse = Microsoft.Extensions.AI.ChatResponse;

namespace AgenticSystem.Tests;

public class AgentCollaborationWorkflowTests
{
    [Fact]
    public async Task ExecuteAsync_UsesAgentWorkflowBuilder_WhenFrameworkFactoryIsAvailable()
    {
        var taskPlanManager = Substitute.For<ITaskPlanManager>();
        var runtimeCoordinator = Substitute.For<IAgentRuntimeCoordinator>();
        runtimeCoordinator.BeginAgentScope(Arg.Any<string>(), Arg.Any<IEnumerable<string>>())
            .Returns(new DisposableScope());

        var createdPlan = new TaskPlan
        {
            Id = "plan-1",
            Title = "Plan title",
            Description = "Plan description",
            Steps =
            [
                new TaskStep
                {
                    Index = 0,
                    Description = "Draft implementation",
                    AssignedAgent = "WorkAgent",
                    Status = TaskStepStatus.Pending
                }
            ]
        };

        taskPlanManager.CreatePlanAsync("user-1", "Implement migration", Arg.Any<List<TaskStep>>())
            .Returns(createdPlan);

        var planner = new ChatClientPlanner(
            CreateStaticChatClient("[{\"description\":\"Draft implementation\",\"agent\":\"Work\"}]"),
            taskPlanManager,
            LoggerFactory.Create(_ => { }));

        var stepAgent = CreateAgent("WorkAgent", "work", AgentTier.Specialist);

        var reviewerAgent = CreateAgent("AnalysisAgent", "analysis", AgentTier.Specialist);

        var agentFactory = Substitute.For<IAgentFactory>();
        agentFactory.ResolveAgentAsync(Arg.Any<AnalysisResult>())
            .Returns(callInfo =>
            {
                var analysis = callInfo.Arg<AnalysisResult>();
                return analysis.EstimatedAgent == "AnalysisAgent"
                    ? reviewerAgent
                    : stepAgent;
            });

        var frameworkFactory = new AgentFrameworkFactory(
            CreateStaticChatClient("unused"),
            LoggerFactory.Create(_ => { }),
            new ServiceCollection().BuildServiceProvider());

        var directExecService = CreateDirectExecutionService(
            ("WorkAgent", new AgentResponse { Content = "Step completed", AgentName = "WorkAgent", Success = true, ToolsUsed = [] }),
            ("AnalysisAgent", new AgentResponse { Content = "Review completed", AgentName = "AnalysisAgent", Success = true, Metadata = new Dictionary<string, object>() }));

        var sut = new AgentCollaborationWorkflow(
            planner,
            agentFactory,
            taskPlanManager,
            runtimeCoordinator,
            directExecService,
            Substitute.For<ILogger<AgentCollaborationWorkflow>>(),
            agentFrameworkFactory: frameworkFactory);

        var context = new UserContext
        {
            UserId = "user-1",
            TenantId = Tenant.DefaultTenantId
        };

        var analysis = new AnalysisResult
        {
            PrimaryDomain = "work",
            EstimatedAgent = "WorkAgent",
            Intent = IntentType.Chat,
            Complexity = ComplexityLevel.RequiresPlanning,
            RecommendedTier = AgentTier.Specialist,
            RequiresDelegation = true
        };

        var response = await sut.ExecuteAsync("session-1", "Implement migration", context, analysis, CancellationToken.None);

        response.Success.Should().BeTrue();
        response.Metadata["executionMode"].Should().Be("planner-executor-reviewer-workflow");
        response.Metadata["workflowEngine"].Should().Be("AgentWorkflowBuilder");
        response.Content.Should().Contain("Review completed");
        await taskPlanManager.Received(1).AdvanceStepAsync("plan-1", "Step completed");
    }

    [Fact]
    public async Task ExecuteAsync_AddsConcurrentContextAndCheckpointMetadata_WhenAdvancedWorkflowIsEnabled()
    {
        var taskPlanManager = Substitute.For<ITaskPlanManager>();
        var runtimeCoordinator = Substitute.For<IAgentRuntimeCoordinator>();
        runtimeCoordinator.BeginAgentScope(Arg.Any<string>(), Arg.Any<IEnumerable<string>>())
            .Returns(new DisposableScope());

        var createdPlan = new TaskPlan
        {
            Id = "plan-advanced-1",
            Title = "Plan title",
            Description = "Plan description",
            Steps =
            [
                new TaskStep
                {
                    Index = 0,
                    Description = "Draft implementation",
                    AssignedAgent = "WorkAgent",
                    Status = TaskStepStatus.Pending
                }
            ]
        };

        taskPlanManager.CreatePlanAsync("user-1", "Implement migration", Arg.Any<List<TaskStep>>())
            .Returns(createdPlan);

        var planner = new ChatClientPlanner(
            CreateStaticChatClient("[{\"description\":\"Draft implementation\",\"agent\":\"Work\"}]"),
            taskPlanManager,
            LoggerFactory.Create(_ => { }));

        var ragService = Substitute.For<IRAGService>();
        ragService.RetrieveContextAsync(Arg.Any<RAGQuery>(), Arg.Any<CancellationToken>())
            .Returns(new RAGContext { BuiltContext = "Relevant migration context" });

        var stepAgent = CreateAgent("WorkAgent", "work", AgentTier.Specialist);

        var reviewerAgent = CreateAgent("AnalysisAgent", "analysis", AgentTier.Specialist);

        var agentFactory = Substitute.For<IAgentFactory>();
        agentFactory.ResolveAgentAsync(Arg.Any<AnalysisResult>())
            .Returns(callInfo =>
            {
                var analysis = callInfo.Arg<AnalysisResult>();
                return analysis.EstimatedAgent == "AnalysisAgent"
                    ? reviewerAgent
                    : stepAgent;
            });

        var frameworkFactory = new AgentFrameworkFactory(
            CreateStaticChatClient("unused"),
            LoggerFactory.Create(_ => { }),
            new ServiceCollection().BuildServiceProvider());

        var directExecService = CreateDirectExecutionService(
            ("WorkAgent", new AgentResponse { Content = "Step completed", AgentName = "WorkAgent", Success = true, ToolsUsed = [] }),
            ("AnalysisAgent", new AgentResponse { Content = "Review completed", AgentName = "AnalysisAgent", Success = true, Metadata = new Dictionary<string, object>() }));

        var sut = new AgentCollaborationWorkflow(
            planner,
            agentFactory,
            taskPlanManager,
            runtimeCoordinator,
            directExecService,
            Substitute.For<ILogger<AgentCollaborationWorkflow>>(),
            ragService: ragService,
            agentFrameworkFactory: frameworkFactory,
            workflowOptions: Options.Create(new CollaborationWorkflowOptions
            {
                EnableAdvancedWorkflow = true,
                EnableConcurrentContextStage = true,
                EnableCheckpointing = true
            }));

        var context = new UserContext
        {
            UserId = "user-1",
            TenantId = Tenant.DefaultTenantId
        };

        var analysis = new AnalysisResult
        {
            PrimaryDomain = "work",
            EstimatedAgent = "WorkAgent",
            Intent = IntentType.Chat,
            Complexity = ComplexityLevel.RequiresPlanning,
            RecommendedTier = AgentTier.Specialist,
            RequiresDelegation = true
        };

        var response = await sut.ExecuteAsync("session-advanced-1", "Implement migration", context, analysis, CancellationToken.None);

        response.Success.Should().BeTrue();
        response.Metadata["executionMode"].Should().Be("context-parallel-planner-executor-reviewer-workflow");
        response.Metadata["advancedWorkflow"].Should().Be(true);
        response.Metadata["workflowCheckpointingEnabled"].Should().Be(true);
        response.Metadata["contextCheckpointingEnabled"].Should().Be(true);
        response.Metadata["workflowContextEnriched"].Should().Be(true);
        response.Metadata["concurrentContextSourcesCount"].Should().Be(1);
        response.Content.Should().Contain("Review completed");
    }

    [Fact]
    public async Task ExecuteAsync_UsesNativeHandoffReview_WhenEnabled()
    {
        var taskPlanManager = Substitute.For<ITaskPlanManager>();
        var runtimeCoordinator = Substitute.For<IAgentRuntimeCoordinator>();
        runtimeCoordinator.BeginAgentScope(Arg.Any<string>(), Arg.Any<IEnumerable<string>>())
            .Returns(new DisposableScope());

        var createdPlan = new TaskPlan
        {
            Id = "plan-handoff-1",
            Title = "Plan title",
            Description = "Plan description",
            Steps =
            [
                new TaskStep
                {
                    Index = 0,
                    Description = "Draft implementation",
                    AssignedAgent = "WorkAgent",
                    Status = TaskStepStatus.Pending
                }
            ]
        };

        taskPlanManager.CreatePlanAsync("user-1", "Implement migration", Arg.Any<List<TaskStep>>())
            .Returns(createdPlan);

        var planner = new ChatClientPlanner(
            CreateStaticChatClient("[{\"description\":\"Draft implementation\",\"agent\":\"Work\"}]"),
            taskPlanManager,
            LoggerFactory.Create(_ => { }));

        var stepAgent = CreateAgent("WorkAgent", "work", AgentTier.Specialist);

        var reviewerAgent = CreateAgent("AnalysisAgent", "analysis", AgentTier.Specialist);

        var agentFactory = Substitute.For<IAgentFactory>();
        agentFactory.ResolveAgentAsync(Arg.Any<AnalysisResult>())
            .Returns(callInfo =>
            {
                var analysis = callInfo.Arg<AnalysisResult>();
                return analysis.EstimatedAgent == "AnalysisAgent"
                    ? reviewerAgent
                    : stepAgent;
            });

        var frameworkFactory = new AgentFrameworkFactory(
            CreateStaticChatClient("Review completed via handoff workflow"),
            LoggerFactory.Create(_ => { }),
            new ServiceCollection().BuildServiceProvider());

        var directExecService = CreateDirectExecutionService(
            ("WorkAgent", new AgentResponse { Content = "Step completed", AgentName = "WorkAgent", Success = true, ToolsUsed = [] }),
            ("AnalysisAgent", new AgentResponse { Content = "Direct review fallback", AgentName = "AnalysisAgent", Success = true, Metadata = new Dictionary<string, object>() }));

        var sut = new AgentCollaborationWorkflow(
            planner,
            agentFactory,
            taskPlanManager,
            runtimeCoordinator,
            directExecService,
            Substitute.For<ILogger<AgentCollaborationWorkflow>>(),
            agentFrameworkFactory: frameworkFactory,
            workflowOptions: Options.Create(new CollaborationWorkflowOptions
            {
                EnableAdvancedWorkflow = true,
                EnableConcurrentContextStage = false,
                EnableCheckpointing = true,
                EnableNativeHandoffReview = true
            }));

        var context = new UserContext
        {
            UserId = "user-1",
            TenantId = Tenant.DefaultTenantId
        };

        var analysis = new AnalysisResult
        {
            PrimaryDomain = "work",
            EstimatedAgent = "WorkAgent",
            Intent = IntentType.Chat,
            Complexity = ComplexityLevel.RequiresPlanning,
            RecommendedTier = AgentTier.Specialist,
            RequiresDelegation = true
        };

        var response = await sut.ExecuteAsync("session-handoff-1", "Implement migration", context, analysis, CancellationToken.None);

        response.Success.Should().BeTrue();
        response.Metadata["nativeHandoffWorkflow"].Should().Be(true);
        response.Metadata["nativeReviewMode"].Should().Be("HandoffWorkflowBuilder");
        response.Metadata["handoffCheckpointingEnabled"].Should().Be(true);
        response.Content.Should().Contain("Review completed via handoff workflow");
    }

    [Fact]
    public async Task ExecuteAsync_UsesNativeGroupChatTermination_WhenEnabled()
    {
        var taskPlanManager = Substitute.For<ITaskPlanManager>();
        var runtimeCoordinator = Substitute.For<IAgentRuntimeCoordinator>();
        runtimeCoordinator.BeginAgentScope(Arg.Any<string>(), Arg.Any<IEnumerable<string>>())
            .Returns(new DisposableScope());
        runtimeCoordinator.CurrentSessionId.Returns("session-group-chat-1");

        var createdPlan = new TaskPlan
        {
            Id = "plan-group-chat-1",
            Title = "Plan title",
            Description = "Plan description",
            Steps =
            [
                new TaskStep
                {
                    Index = 0,
                    Description = "Draft implementation",
                    AssignedAgent = "WorkAgent",
                    Status = TaskStepStatus.Pending
                }
            ]
        };

        taskPlanManager.CreatePlanAsync("user-1", "Implement migration", Arg.Any<List<TaskStep>>())
            .Returns(createdPlan);

        var planner = new ChatClientPlanner(
            CreateStaticChatClient("[{\"description\":\"Draft implementation\",\"agent\":\"Work\"}]"),
            taskPlanManager,
            LoggerFactory.Create(_ => { }));

        var stepAgent = CreateAgent("WorkAgent", "work", AgentTier.Specialist);

        var reviewerAgent = CreateAgent("AnalysisAgent", "analysis", AgentTier.Specialist);

        var agentFactory = Substitute.For<IAgentFactory>();
        agentFactory.ResolveAgentAsync(Arg.Any<AnalysisResult>())
            .Returns(callInfo =>
            {
                var analysis = callInfo.Arg<AnalysisResult>();
                return analysis.EstimatedAgent == "AnalysisAgent"
                    ? reviewerAgent
                    : stepAgent;
            });

        var frameworkFactory = new AgentFrameworkFactory(
            CreateStaticChatClient("Review completed via group chat"),
            LoggerFactory.Create(_ => { }),
            new ServiceCollection().BuildServiceProvider());

        var directExecService = CreateDirectExecutionService(
            ("WorkAgent", new AgentResponse { Content = "Step completed", AgentName = "WorkAgent", Success = true, ToolsUsed = [] }),
            ("AnalysisAgent", new AgentResponse { Content = "Direct review fallback", AgentName = "AnalysisAgent", Success = true, Metadata = new Dictionary<string, object>() }));

        var sut = new AgentCollaborationWorkflow(
            planner,
            agentFactory,
            taskPlanManager,
            runtimeCoordinator,
            directExecService,
            Substitute.For<ILogger<AgentCollaborationWorkflow>>(),
            agentFrameworkFactory: frameworkFactory,
            workflowOptions: Options.Create(new CollaborationWorkflowOptions
            {
                EnableAdvancedWorkflow = true,
                EnableConcurrentContextStage = false,
                EnableCheckpointing = true,
                EnableNativeGroupChatTermination = true,
                GroupChatMaximumIterations = 3,
                GroupChatTerminationPhrases = ["review completed"]
            }));

        var context = new UserContext
        {
            UserId = "user-1",
            TenantId = Tenant.DefaultTenantId
        };

        var analysis = new AnalysisResult
        {
            PrimaryDomain = "work",
            EstimatedAgent = "WorkAgent",
            Intent = IntentType.Chat,
            Complexity = ComplexityLevel.RequiresPlanning,
            RecommendedTier = AgentTier.Specialist,
            RequiresDelegation = true
        };

        var response = await sut.ExecuteAsync("session-group-chat-1", "Implement migration", context, analysis, CancellationToken.None);

        response.Success.Should().BeTrue();
        response.Metadata["nativeGroupChatTermination"].Should().Be(true);
        response.Metadata["nativeReviewMode"].Should().Be("GroupChatWorkflowBuilder");
        response.Metadata["groupChatTerminationReason"].Should().Be("phrase:review completed");
        response.Metadata["groupChatCheckpointingEnabled"].Should().Be(true);
        response.Content.Should().Contain("Review completed via group chat");
    }

    private static IAgent CreateAgent(string name, string domain, AgentTier tier)
    {
        var agent = Substitute.For<IAgent>();
        agent.Name.Returns(name);
        agent.Description.Returns($"{name} description");
        agent.Domain.Returns(domain);
        agent.Tier.Returns(tier);
        agent.CreatedAt.Returns(DateTime.UtcNow);
        agent.LastUsedAt.Returns(DateTime.UtcNow);
        agent.IsActive.Returns(true);
        agent.AvailableTools.Returns(Array.Empty<string>());
        agent.Instructions.Returns($"You are {name}.");
        agent.CanHandleAsync(Arg.Any<AnalysisResult>()).Returns(Task.FromResult(true));
        return agent;
    }

    private static IDirectAgentExecutionService CreateDirectExecutionService(
        params (string AgentName, AgentResponse Response)[] mappings)
    {
        var service = Substitute.For<IDirectAgentExecutionService>();
        service.ExecuteDirectAsync(
                Arg.Any<IAgent>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<UserContext>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var agent = callInfo.Arg<IAgent>();
                var mapping = mappings.FirstOrDefault(m => m.AgentName == agent.Name);
                return mapping.Response ?? new AgentResponse
                {
                    Content = $"Default response from {agent.Name}",
                    AgentName = agent.Name,
                    Success = true,
                    Metadata = new Dictionary<string, object>()
                };
            });
        return service;
    }

    private sealed class DisposableScope : IDisposable
    {
        public void Dispose()
        {
        }
    }

    private static IChatClient CreateStaticChatClient(string responseText)
    {
        var chatClient = Substitute.For<IChatClient>();
        chatClient.GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new AIChatResponse(new ChatMessage(ChatRole.Assistant, responseText))));
        chatClient.GetStreamingResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(CreateStreamingResponse(responseText));
        chatClient.GetService(Arg.Any<Type>(), Arg.Any<object?>())
            .Returns(callInfo => callInfo.Arg<Type>() == typeof(IChatClient) ? chatClient : null);
        return chatClient;
    }

    private static async IAsyncEnumerable<ChatResponseUpdate> CreateStreamingResponse(
        string responseText,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
        yield return new ChatResponseUpdate(ChatRole.Assistant, responseText);
    }
}