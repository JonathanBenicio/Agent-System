using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;

namespace AgenticSystem.Tests;

public class EmailDeliveryChannelTests
{
    private readonly ILogger<EmailDeliveryChannel> _logger = Substitute.For<ILogger<EmailDeliveryChannel>>();

    private EmailDeliveryChannel CreateChannel() => new(_logger);

    private static TriggerNotificationPayload CreatePayload() => new()
    {
        TriggerName = "test-trigger",
        Timestamp = DateTime.UtcNow,
        ConditionResult = "Threshold exceeded",
        SuggestedAction = "Check system",
        ActualValue = "95",
        ExpectedValue = "< 90"
    };

    [Fact]
    public void ChannelName_ShouldBeEmail()
    {
        var channel = CreateChannel();
        channel.ChannelName.Should().Be("email");
    }

    [Fact]
    public async Task SendAsync_MissingSmtpHost_ReturnsFailedWithError()
    {
        var channel = CreateChannel();
        var config = new Dictionary<string, string>
        {
            ["fromAddress"] = "from@test.com",
            ["toAddress"] = "to@test.com"
        };

        var result = await channel.SendAsync(CreatePayload(), config);

        result.Status.Should().Be(DeliveryStatus.Failed);
        result.ErrorMessage.Should().Contain("smtpHost");
        result.Attempts.Should().Be(0);
    }

    [Fact]
    public async Task SendAsync_MissingFromAddress_ReturnsFailedWithError()
    {
        var channel = CreateChannel();
        var config = new Dictionary<string, string>
        {
            ["smtpHost"] = "smtp.test.com",
            ["toAddress"] = "to@test.com"
        };

        var result = await channel.SendAsync(CreatePayload(), config);

        result.Status.Should().Be(DeliveryStatus.Failed);
        result.ErrorMessage.Should().Contain("fromAddress");
    }

    [Fact]
    public async Task SendAsync_MissingToAddress_ReturnsFailedWithError()
    {
        var channel = CreateChannel();
        var config = new Dictionary<string, string>
        {
            ["smtpHost"] = "smtp.test.com",
            ["fromAddress"] = "from@test.com"
        };

        var result = await channel.SendAsync(CreatePayload(), config);

        result.Status.Should().Be(DeliveryStatus.Failed);
        result.ErrorMessage.Should().Contain("toAddress");
    }

    [Fact]
    public async Task SendAsync_InvalidSmtpHost_ReturnsFailedAfterRetries()
    {
        var channel = CreateChannel();
        var config = new Dictionary<string, string>
        {
            ["smtpHost"] = "invalid.host.that.does.not.exist.example",
            ["smtpPort"] = "587",
            ["fromAddress"] = "from@test.com",
            ["toAddress"] = "to@test.com",
            ["useSsl"] = "false"
        };

        var result = await channel.SendAsync(CreatePayload(), config);

        result.Status.Should().Be(DeliveryStatus.Failed);
        result.Attempts.Should().BeGreaterThan(0);
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task SendAsync_RespectsCustomPort()
    {
        var channel = CreateChannel();
        var config = new Dictionary<string, string>
        {
            ["smtpHost"] = "localhost",
            ["smtpPort"] = "2525",
            ["fromAddress"] = "from@test.com",
            ["toAddress"] = "to@test.com",
            ["useSsl"] = "false"
        };

        // Will fail because no SMTP server, but should attempt
        var result = await channel.SendAsync(CreatePayload(), config);

        result.ChannelName.Should().Be("email");
        result.Attempts.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task IsHealthyAsync_ReturnsTrue()
    {
        var channel = CreateChannel();
        var healthy = await channel.IsHealthyAsync();
        healthy.Should().BeTrue();
    }
}
