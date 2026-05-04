using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;

namespace AgenticSystem.Tests;

public class TriggerEngineTests
{
    private readonly IScheduledTaskStore _store;
    private readonly IDeliveryChannel _webhookChannel;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<TriggerEngine> _logger;
    private readonly TriggerEngine _sut;

    public TriggerEngineTests()
    {
        _store = Substitute.For<IScheduledTaskStore>();
        _webhookChannel = Substitute.For<IDeliveryChannel>();
        _webhookChannel.ChannelName.Returns("webhook");
        _webhookChannel.SendAsync(Arg.Any<TriggerNotificationPayload>(), Arg.Any<Dictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(new DeliveryResult { ChannelName = "webhook", Status = DeliveryStatus.Success });
        _httpClientFactory = Substitute.For<IHttpClientFactory>();
        _logger = Substitute.For<ILogger<TriggerEngine>>();

        _store.SaveRuleAsync(Arg.Any<TriggerRule>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<TriggerRule>());

        _sut = new TriggerEngine(_store, new[] { _webhookChannel }, _httpClientFactory, _logger);
    }

    [Fact]
    public async Task RegisterRuleAsync_SavesRuleToStore()
    {
        var rule = CreateRule();

        await _sut.RegisterRuleAsync(rule);

        await _store.Received(1).SaveRuleAsync(rule, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetRuleAsync_DelegatesToStore()
    {
        var rule = CreateRule();
        _store.GetRuleAsync("rule-1", Arg.Any<CancellationToken>()).Returns(rule);

        var result = await _sut.GetRuleAsync("rule-1");

        result.Should().Be(rule);
    }

    [Fact]
    public async Task RemoveRuleAsync_DeletesFromStore()
    {
        await _sut.RemoveRuleAsync("rule-1");

        await _store.Received(1).DeleteRuleAsync("rule-1", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnableRuleAsync_SetsEnabledTrue()
    {
        var rule = CreateRule();
        rule.Enabled = false;
        _store.GetRuleAsync("rule-1", Arg.Any<CancellationToken>()).Returns(rule);

        await _sut.EnableRuleAsync("rule-1");

        rule.Enabled.Should().BeTrue();
        await _store.Received().SaveRuleAsync(rule, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DisableRuleAsync_SetsEnabledFalse()
    {
        var rule = CreateRule();
        rule.Enabled = true;
        _store.GetRuleAsync("rule-1", Arg.Any<CancellationToken>()).Returns(rule);

        await _sut.DisableRuleAsync("rule-1");

        rule.Enabled.Should().BeFalse();
    }

    [Fact]
    public async Task EnableRuleAsync_NonExistentRule_ThrowsInvalidOperation()
    {
        _store.GetRuleAsync("missing", Arg.Any<CancellationToken>()).Returns((TriggerRule?)null);

        var act = () => _sut.EnableRuleAsync("missing");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task EvaluateAsync_DisabledRule_ReturnsFalse()
    {
        var rule = CreateRule();
        rule.Enabled = false;

        var result = await _sut.EvaluateAsync(rule);

        result.ConditionMet.Should().BeFalse();
        result.RuleId.Should().Be(rule.Id);
    }

    [Fact]
    public async Task EvaluateAllAsync_OnlyEvaluatesEnabledRules()
    {
        var enabledRule = CreateRule();
        enabledRule.Enabled = true;
        var disabledRule = CreateRule();
        disabledRule.Id = "rule-disabled";
        disabledRule.Enabled = false;

        _store.GetAllRulesAsync(Arg.Any<CancellationToken>())
            .Returns(new List<TriggerRule> { enabledRule, disabledRule });

        // Will fail on HTTP fetch since we didn't set up the client,
        // but it should only attempt enabled rules
        var results = await _sut.EvaluateAllAsync();

        results.Should().HaveCount(1);
        results[0].RuleId.Should().Be(enabledRule.Id);
    }

    private static TriggerRule CreateRule() => new()
    {
        Id = "rule-1",
        Name = "Test Rule",
        Schedule = "*/5 * * * *",
        Source = new TriggerSource(TriggerSourceType.HealthCheck, "https://api.example.com/health", new Dictionary<string, string>()),
        Condition = new TriggerCondition(ConditionType.StatusCode, "200"),
        Action = new TriggerAction("notify", "Alert team", new Dictionary<string, string> { ["webhookUrl"] = "https://hooks.example.com/alert" }),
        DeliveryChannels = new[] { "webhook" },
        Enabled = true
    };
}
