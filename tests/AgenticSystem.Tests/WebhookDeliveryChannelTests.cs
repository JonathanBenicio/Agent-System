using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;

namespace AgenticSystem.Tests;

public class WebhookDeliveryChannelTests
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<WebhookDeliveryChannel> _logger;
    private readonly WebhookDeliveryChannel _sut;

    public WebhookDeliveryChannelTests()
    {
        _httpClientFactory = Substitute.For<IHttpClientFactory>();
        _logger = Substitute.For<ILogger<WebhookDeliveryChannel>>();
        _sut = new WebhookDeliveryChannel(_httpClientFactory, _logger);
    }

    [Fact]
    public void ChannelName_ReturnsWebhook()
    {
        _sut.ChannelName.Should().Be("webhook");
    }

    [Fact]
    public async Task SendAsync_NoWebhookUrl_ReturnsFailed()
    {
        var payload = CreatePayload();
        var config = new Dictionary<string, string>();

        var result = await _sut.SendAsync(payload, config);

        result.Status.Should().Be(DeliveryStatus.Failed);
        result.ErrorMessage.Should().Contain("webhookUrl not configured");
        result.Attempts.Should().Be(0);
    }

    [Fact]
    public async Task SendAsync_EmptyWebhookUrl_ReturnsFailed()
    {
        var payload = CreatePayload();
        var config = new Dictionary<string, string> { ["webhookUrl"] = "" };

        var result = await _sut.SendAsync(payload, config);

        result.Status.Should().Be(DeliveryStatus.Failed);
        result.ErrorMessage.Should().Contain("webhookUrl not configured");
    }

    [Fact]
    public async Task SendAsync_SuccessfulPost_ReturnsSuccess()
    {
        var handler = new FakeHttpMessageHandler(System.Net.HttpStatusCode.OK);
        var client = new HttpClient(handler);
        _httpClientFactory.CreateClient("WebhookDelivery").Returns(client);

        var payload = CreatePayload();
        var config = new Dictionary<string, string> { ["webhookUrl"] = "https://hooks.example.com/alert" };

        var result = await _sut.SendAsync(payload, config);

        result.Status.Should().Be(DeliveryStatus.Success);
        result.Attempts.Should().Be(1);
        result.HttpStatusCode.Should().Be(200);
        result.DeliveredAt.Should().NotBeNull();
    }

    [Fact]
    public async Task SendAsync_WithSecurityHeaders_AddsSignatureAndIdempotency()
    {
        var handler = new FakeHttpMessageHandler(System.Net.HttpStatusCode.OK);
        var client = new HttpClient(handler);
        _httpClientFactory.CreateClient("WebhookDelivery").Returns(client);

        var payload = CreatePayload();
        var config = new Dictionary<string, string>
        {
            ["webhookUrl"] = "https://hooks.example.com/alert",
            ["hmacSecret"] = "top-secret",
            ["signatureHeaderName"] = "X-Test-Signature",
            ["idempotencyKey"] = "delivery-123",
            ["idempotencyHeaderName"] = "X-Idempotency-Key",
            ["header:Authorization"] = "Bearer abc"
        };

        var result = await _sut.SendAsync(payload, config);

        result.Status.Should().Be(DeliveryStatus.Success);
        handler.LastRequest.Should().NotBeNull();
        handler.LastRequest!.Headers.Should().Contain(header => header.Key == "X-Test-Signature");
        handler.LastRequest.Headers.Should().Contain(header => header.Key == "X-Idempotency-Key" && header.Value.Contains("delivery-123"));
        handler.LastRequest.Headers.Should().Contain(header => header.Key == "Authorization" && header.Value.Contains("Bearer abc"));
    }

    [Fact]
    public async Task SendAsync_ServerError_RetriesAndFails()
    {
        var handler = new FakeHttpMessageHandler(System.Net.HttpStatusCode.InternalServerError);
        var client = new HttpClient(handler);
        _httpClientFactory.CreateClient("WebhookDelivery").Returns(client);

        var payload = CreatePayload();
        var config = new Dictionary<string, string> { ["webhookUrl"] = "https://hooks.example.com/alert" };

        var result = await _sut.SendAsync(payload, config);

        result.Status.Should().Be(DeliveryStatus.Failed);
        result.Attempts.Should().Be(3); // max retries
        result.HttpStatusCode.Should().Be(500);
    }

    [Fact]
    public async Task IsHealthyAsync_ReturnsTrue()
    {
        var client = new HttpClient();
        _httpClientFactory.CreateClient("WebhookDelivery").Returns(client);

        var result = await _sut.IsHealthyAsync();

        result.Should().BeTrue();
    }

    private static TriggerNotificationPayload CreatePayload() => new()
    {
        TriggerName = "test-trigger",
        Timestamp = DateTime.UtcNow,
        ConditionResult = "StatusCode: 500 (expected: 200)",
        SuggestedAction = "Investigate API health",
        ActualValue = "500",
        ExpectedValue = "200"
    };

    /// <summary>
    /// Fake handler for unit testing HttpClient calls.
    /// </summary>
    private class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly System.Net.HttpStatusCode _statusCode;
        public HttpRequestMessage? LastRequest { get; private set; }

        public FakeHttpMessageHandler(System.Net.HttpStatusCode statusCode)
        {
            _statusCode = statusCode;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent("{}")
            });
        }
    }
}
