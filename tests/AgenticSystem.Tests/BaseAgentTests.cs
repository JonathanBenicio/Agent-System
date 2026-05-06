using AIChatResponse = Microsoft.Extensions.AI.ChatResponse;
using Microsoft.Extensions.AI;
using AgenticSystem.Core.Agents;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Tests;

public class BaseAgentTests
{
    [Fact]
    public async Task ExecuteAsync_WhenRelevantMemoriesExist_IncludesAgentMemoryInPrompt()
    {
        var chatClient = Substitute.For<IChatClient>();
        chatClient
            .GetResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions?>(), Arg.Any<CancellationToken>())
            .Returns(new AIChatResponse(new ChatMessage(ChatRole.Assistant, "ok")));

        var skillManager = Substitute.For<ISkillManager>();
        skillManager
            .BuildEnrichedPromptAsync("test-agent", "test-domain", "base prompt")
            .Returns("base prompt");

        var agentMemoryService = Substitute.For<IAgentMemoryService>();
        agentMemoryService
            .GetRelevantMemoriesAsync("test-agent", "user-1", "pedido atual", Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<AgentMemoryEntry>
            {
                new()
                {
                    AgentName = "test-agent",
                    UserId = "user-1",
                    MemoryType = AgentMemoryType.LearnedRule,
                    Content = "Responder em pt-BR e priorizar contexto do usuario"
                }
            });

        var logger = Substitute.For<ILogger>();
        var agent = new TestAgent(chatClient, skillManager, logger, agentMemoryService);

        await agent.ExecuteAsync("pedido atual", new UserContext { UserId = "user-1" });

        await chatClient.Received().GetResponseAsync(
            Arg.Is<IEnumerable<ChatMessage>>(messages => MatchesExpectedPrompt(messages)),
            Arg.Any<ChatOptions?>(),
            Arg.Any<CancellationToken>());
    }

    private static bool MatchesExpectedPrompt(IEnumerable<ChatMessage> messages)
    {
        var list = messages.ToList();
        if (list.Count != 2)
        {
            return false;
        }

        var systemMessage = list[0];
        var userMessage = list[1];
        var systemText = systemMessage.Text;

        return systemMessage.Role == ChatRole.System
            && systemText is not null
            && systemText.Contains("base prompt")
            && systemText.Contains("## Agent Memory")
            && systemText.Contains("Responder em pt-BR e priorizar contexto do usuario")
            && userMessage.Role == ChatRole.User
            && userMessage.Text == "pedido atual";
    }

    private sealed class TestAgent : BaseAgent
    {
        public TestAgent(
            IChatClient chatClient,
            ISkillManager skillManager,
            ILogger logger,
            IAgentMemoryService agentMemoryService)
            : base(chatClient, skillManager, logger, agentMemoryService)
        {
        }

        public override string Name => "test-agent";
        public override string Description => "agent for tests";
        public override AgentTier Tier => AgentTier.Master;
        public override string Domain => "test-domain";

        protected override string GetBaseSystemPrompt() => "base prompt";
    }
}