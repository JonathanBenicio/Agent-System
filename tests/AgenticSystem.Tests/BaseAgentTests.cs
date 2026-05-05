using AgenticSystem.Core.Agents;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.LLM.Interfaces;
using AgenticSystem.Core.LLM.Models;
using AgenticSystem.Core.Models;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Tests;

public class BaseAgentTests
{
    [Fact]
    public async Task ExecuteAsync_WhenRelevantMemoriesExist_IncludesAgentMemoryInPrompt()
    {
        var llmManager = Substitute.For<ILLMManager>();
        llmManager
            .GenerateWithProfileAsync("test-agent", "default", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(LLMResponse.Ok("ok", "test-model", "test-provider"));

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
        var agent = new TestAgent(llmManager, skillManager, logger, agentMemoryService);

        await agent.ExecuteAsync("pedido atual", new UserContext { UserId = "user-1" });

        await llmManager.Received().GenerateWithProfileAsync(
            "test-agent",
            "default",
            Arg.Is<string>(prompt =>
                prompt.Contains("base prompt") &&
                prompt.Contains("## Agent Memory") &&
                prompt.Contains("Responder em pt-BR e priorizar contexto do usuario")),
            Arg.Any<CancellationToken>());
    }

    private sealed class TestAgent : BaseAgent
    {
        public TestAgent(
            ILLMManager llmManager,
            ISkillManager skillManager,
            ILogger logger,
            IAgentMemoryService agentMemoryService)
            : base(llmManager, skillManager, logger, agentMemoryService)
        {
        }

        public override string Name => "test-agent";
        public override string Description => "agent for tests";
        public override AgentTier Tier => AgentTier.Master;
        public override string Domain => "test-domain";

        protected override string GetBaseSystemPrompt() => "base prompt";
    }
}