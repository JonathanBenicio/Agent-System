using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.AI;
using AgenticSystem.Infrastructure.AgentFramework;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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

        var stepAgent = CreateAgent(
            "WorkAgent",
            "work",
            AgentTier.Specialist,
            new AgentResponse
            {
                Content = "Step completed",
                AgentName = "WorkAgent",
                Success = true,
                ToolsUsed = []
            });

        var reviewerAgent = CreateAgent(
            "AnalysisAgent",
            "analysis",
            AgentTier.Specialist,
            new AgentResponse
            {
                Content = "Review completed",
                AgentName = "AnalysisAgent",
                Success = true,
                Metadata = new Dictionary<string, object>()
            });

        var agentFactory = Substitute.For<IAgentFactory>();
        agentFactory.GetOrCreateAgentAsync(Arg.Any<AnalysisResult>())
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

        var sut = new AgentCollaborationWorkflow(
            planner,
            agentFactory,
            taskPlanManager,
            runtimeCoordinator,
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

    private static IAgent CreateAgent(string name, string domain, AgentTier tier, AgentResponse response)
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
        agent.ExecuteAsync(Arg.Any<string>(), Arg.Any<UserContext>()).Returns(response);
        agent.CanHandleAsync(Arg.Any<AnalysisResult>()).Returns(Task.FromResult(true));
        return agent;
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