using AgenticSystem.Core.Agents;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace AgenticSystem.Tests;

public class BaseAgentTests
{
    [Fact]
    public void BaseAgent_Provides_Correct_Properties()
    {
        var skillManager = Substitute.For<ISkillManager>();
        var agentMemoryService = Substitute.For<IAgentMemoryService>();
        var logger = Substitute.For<ILogger>();
        var agent = new TestAgent(skillManager, logger, agentMemoryService);

        agent.Name.Should().Be("test-agent");
        agent.Description.Should().Be("agent for tests");
        agent.Tier.Should().Be(AgentTier.Master);
        agent.Domain.Should().Be("test-domain");
        agent.Instructions.Should().Be("base prompt");
        agent.AvailableTools.Should().BeEmpty();
    }

    private sealed class TestAgent : BaseAgent
    {
        public TestAgent(
            ISkillManager skillManager,
            ILogger logger,
            IAgentMemoryService agentMemoryService)
            : base(skillManager, logger, agentMemoryService)
        {
        }

        public override string Name => "test-agent";
        public override string Description => "agent for tests";
        public override AgentTier Tier => AgentTier.Master;
        public override string Domain => "test-domain";

        protected override string GetBaseSystemPrompt() => "base prompt";
    }
}