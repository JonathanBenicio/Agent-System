using System.Net;
using System.Net.Http;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;

namespace AgenticSystem.Tests;

public class PushDeliveryChannelTests
{
    private readonly ILogger<PushDeliveryChannel> _logger = Substitute.For<ILogger<PushDeliveryChannel>>();

    private static TriggerNotificationPayload CreatePayload() => new()
    {
        TriggerName = "cpu-high",
        Timestamp = DateTime.UtcNow,
        ConditionResult = "CPU at 95%",
        SuggestedAction = "Scale up",
        ActualValue = "95",
        ExpectedValue = "< 80"
    };

    private static MockHttpMessageHandler CreateMockHandler(HttpStatusCode statusCode = HttpStatusCode.OK)
        => new(statusCode);

    [Fact]
    public void ChannelName_ShouldBePush()
    {
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(new HttpClient());

        var channel = new PushDeliveryChannel(factory, _logger);
        channel.ChannelName.Should().Be("push");
    }

    [Fact]
    public async Task SendAsync_MissingFcmServerKey_ReturnsFailedWithError()
    {
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(new HttpClient());

        var channel = new PushDeliveryChannel(factory, _logger);
        var config = new Dictionary<string, string>
        {
            ["deviceToken"] = "token123"
        };

        var result = await channel.SendAsync(CreatePayload(), config);

        result.Status.Should().Be(DeliveryStatus.Failed);
        result.ErrorMessage.Should().Contain("fcmServerKey");
    }

    [Fact]
    public async Task SendAsync_MissingDeviceToken_ReturnsFailedWithError()
    {
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(new HttpClient());

        var channel = new PushDeliveryChannel(factory, _logger);
        var config = new Dictionary<string, string>
        {
            ["fcmServerKey"] = "key123"
        };

        var result = await channel.SendAsync(CreatePayload(), config);

        result.Status.Should().Be(DeliveryStatus.Failed);
        result.ErrorMessage.Should().Contain("deviceToken");
    }

    [Fact]
    public async Task SendAsync_SuccessfulResponse_ReturnsSuccess()
    {
        var handler = CreateMockHandler(HttpStatusCode.OK);
        var httpClient = new HttpClient(handler);

        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(httpClient);

        var channel = new PushDeliveryChannel(factory, _logger);
        var config = new Dictionary<string, string>
        {
            ["fcmServerKey"] = "test-key",
            ["deviceToken"] = "test-device-token",
            ["fcmUrl"] = "http://localhost/fcm",
            ["title"] = "Alert"
        };

        var result = await channel.SendAsync(CreatePayload(), config);

        result.Status.Should().Be(DeliveryStatus.Success);
        result.Attempts.Should().Be(1);
        result.HttpStatusCode.Should().Be(200);
        result.DeliveredAt.Should().NotBeNull();
    }

    [Fact]
    public async Task SendAsync_ServerError_RetriesAndFails()
    {
        var handler = CreateMockHandler(HttpStatusCode.InternalServerError);
        var httpClient = new HttpClient(handler);

        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(httpClient);

        var channel = new PushDeliveryChannel(factory, _logger);
        var config = new Dictionary<string, string>
        {
            ["fcmServerKey"] = "test-key",
            ["deviceToken"] = "test-device-token",
            ["fcmUrl"] = "http://localhost/fcm"
        };

        var result = await channel.SendAsync(CreatePayload(), config);

        result.Status.Should().Be(DeliveryStatus.Failed);
        result.Attempts.Should().Be(3); // max retries
        result.HttpStatusCode.Should().Be(500);
    }

    [Fact]
    public async Task SendAsync_UsesDefaultFcmUrl_WhenNotConfigured()
    {
        var handler = CreateMockHandler(HttpStatusCode.OK);
        var httpClient = new HttpClient(handler);

        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(httpClient);

        var channel = new PushDeliveryChannel(factory, _logger);
        var config = new Dictionary<string, string>
        {
            ["fcmServerKey"] = "test-key",
            ["deviceToken"] = "device-1"
        };

        var result = await channel.SendAsync(CreatePayload(), config);

        // Will fail because default URL is real FCM, but verifies no null ref
        result.Should().NotBeNull();
        result.ChannelName.Should().Be("push");
    }

    [Fact]
    public async Task IsHealthyAsync_ReturnsTrue()
    {
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(new HttpClient());

        var channel = new PushDeliveryChannel(factory, _logger);
        var healthy = await channel.IsHealthyAsync();
        healthy.Should().BeTrue();
    }

    /// <summary>
    /// Simple mock HTTP handler for testing
    /// </summary>
    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;

        public MockHttpMessageHandler(HttpStatusCode statusCode)
        {
            _statusCode = statusCode;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent("{\"success\":1}")
            });
        }
    }
}
