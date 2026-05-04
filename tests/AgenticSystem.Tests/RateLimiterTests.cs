using FluentAssertions;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.Gateway;

namespace AgenticSystem.Tests;

public class RateLimiterTests
{
    private RateLimitConfig CreateConfig(int perMinute = 5, int perHour = 100, int tokensPerDay = 10000)
        => new()
        {
            RequestsPerMinute = perMinute,
            RequestsPerHour = perHour,
            TokensPerDay = tokensPerDay
        };

    [Fact]
    public void AllowRequest_WhenUnderLimit_ReturnsTrue()
    {
        var rl = new RateLimiter(CreateConfig());
        rl.AllowRequest().Should().BeTrue();
    }

    [Fact]
    public void AllowRequest_WhenMinuteLimitReached_ReturnsFalse()
    {
        var rl = new RateLimiter(CreateConfig(perMinute: 2));

        rl.RecordRequest();
        rl.RecordRequest();

        rl.AllowRequest().Should().BeFalse();
    }

    [Fact]
    public void AllowTokens_WhenUnderLimit_ReturnsTrue()
    {
        var rl = new RateLimiter(CreateConfig(tokensPerDay: 1000));
        rl.AllowTokens(500).Should().BeTrue();
    }

    [Fact]
    public void AllowTokens_WhenOverLimit_ReturnsFalse()
    {
        var rl = new RateLimiter(CreateConfig(tokensPerDay: 100));

        rl.RecordRequest(80);
        rl.AllowTokens(30).Should().BeFalse();
    }

    [Fact]
    public void RecordRequest_IncreasesTokenCount()
    {
        var rl = new RateLimiter(CreateConfig(tokensPerDay: 1000));

        rl.RecordRequest(100);
        rl.RecordRequest(200);

        rl.AllowTokens(701).Should().BeFalse();
        rl.AllowTokens(700).Should().BeTrue();
    }

    [Fact]
    public void GetStatus_ReturnsAccurateStatus()
    {
        var rl = new RateLimiter(CreateConfig(perMinute: 10, perHour: 100, tokensPerDay: 5000));

        rl.RecordRequest(100);
        rl.RecordRequest(200);

        var status = rl.GetStatus();
        status.MinuteUsed.Should().Be(2);
        status.MinuteLimit.Should().Be(10);
        status.HourUsed.Should().Be(2);
        status.HourLimit.Should().Be(100);
        status.DailyTokensUsed.Should().Be(300);
        status.DailyTokensLimit.Should().Be(5000);
    }
}
