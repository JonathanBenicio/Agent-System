using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace AgenticSystem.Tests;

public class HandoffManagerTests
{
    private readonly IAgentFactory _agentFactory;
    private readonly HandoffManager _sut;

    public HandoffManagerTests()
    {
        _agentFactory = Substitute.For<IAgentFactory>();
        var logger = Substitute.For<ILogger<HandoffManager>>();
        _sut = new HandoffManager(_agentFactory, logger);
    }

    [Fact]
    public async Task EvaluateHandoffAsync_SimpleRequest_ReturnsNoHandoff()
    {
        var analysis = new AnalysisResult
        {
            PrimaryDomain = "code",
            RequiresDelegation = false,
            SecondaryDomains = []
        };
        var agent = Substitute.For<IAgent>();
        agent.Domain.Returns("code");

        var decision = await _sut.EvaluateHandoffAsync(analysis, agent);

        decision.ShouldHandoff.Should().BeFalse();
        decision.Strategy.Should().Be(HandoffStrategy.None);
    }

    [Fact]
    public async Task EvaluateHandoffAsync_DomainMismatch_ReturnsSingleDelegate()
    {
        var analysis = new AnalysisResult
        {
            PrimaryDomain = "finance",
            RequiresDelegation = false,
            SecondaryDomains = [],
            EstimatedAgent = "FinanceAgent"
        };
        var agent = Substitute.For<IAgent>();
        agent.Name.Returns("WorkAgent");
        agent.Domain.Returns("code");

        var decision = await _sut.EvaluateHandoffAsync(analysis, agent);

        decision.ShouldHandoff.Should().BeTrue();
        decision.Strategy.Should().Be(HandoffStrategy.SingleDelegate);
        decision.Targets.Should().HaveCount(1);
        decision.Targets[0].Domain.Should().Be("finance");
    }

    [Fact]
    public async Task EvaluateHandoffAsync_MultipleDomains_ReturnsFanOut()
    {
        var analysis = new AnalysisResult
        {
            PrimaryDomain = "code",
            RequiresDelegation = true,
            SecondaryDomains = ["data", "devops"]
        };
        var agent = Substitute.For<IAgent>();
        agent.Domain.Returns("code");

        var decision = await _sut.EvaluateHandoffAsync(analysis, agent);

        decision.ShouldHandoff.Should().BeTrue();
        decision.Strategy.Should().Be(HandoffStrategy.FanOut);
        decision.Targets.Count.Should().BeGreaterThan(1);
    }

    [Fact]
    public async Task EvaluateHandoffAsync_RequiresPlanning_ReturnsChain()
    {
        var analysis = new AnalysisResult
        {
            PrimaryDomain = "code",
            RequiresDelegation = true,
            SecondaryDomains = [],
            Complexity = ComplexityLevel.RequiresPlanning
        };
        var agent = Substitute.For<IAgent>();
        agent.Domain.Returns("code");

        var decision = await _sut.EvaluateHandoffAsync(analysis, agent);

        decision.ShouldHandoff.Should().BeTrue();
        decision.Strategy.Should().Be(HandoffStrategy.Chain);
    }

    [Fact]
    public async Task RecordHandoffAsync_StoresRecord()
    {
        var record = new HandoffRecord
        {
            SessionId = "session-1",
            SourceAgent = "WorkAgent",
            TargetAgent = "FinanceAgent",
            Reason = "Domain mismatch",
            Strategy = HandoffStrategy.SingleDelegate,
            Success = true
        };

        await _sut.RecordHandoffAsync("session-1", record);
        var history = await _sut.GetHandoffHistoryAsync("session-1");

        history.Should().HaveCount(1);
        history.First().SourceAgent.Should().Be("WorkAgent");
    }

    [Fact]
    public async Task GetHandoffHistoryAsync_NoRecords_ReturnsEmpty()
    {
        var history = await _sut.GetHandoffHistoryAsync("nonexistent");
        history.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteHandoffAsync_SingleDelegate_ExecutesTargetAgent()
    {
        var decision = new HandoffDecision
        {
            ShouldHandoff = true,
            Strategy = HandoffStrategy.SingleDelegate,
            Targets =
            [
                new HandoffTarget { Domain = "finance", AgentName = "FinanceAgent", SubTask = "Analyze costs" }
            ]
        };
        var context = new UserContext { UserId = "user1" };

        var targetAgent = Substitute.For<IAgent>();
        targetAgent.Name.Returns("FinanceAgent");
        targetAgent.Tier.Returns(AgentTier.Specialist);
        targetAgent.ExecuteAsync(Arg.Any<string>(), context)
            .Returns(AgentResponse.Ok("Cost analysis result", "FinanceAgent", AgentTier.Specialist));

        _agentFactory.GetOrCreateAgentAsync(Arg.Is<AnalysisResult>(a => a.PrimaryDomain == "finance"))
            .Returns(targetAgent);

        var result = await _sut.ExecuteHandoffAsync("analyze costs", context, decision);

        result.Success.Should().BeTrue();
    }
}
