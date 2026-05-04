using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Text.Encodings.Web;
using AgenticSystem.Api.Auth;

namespace AgenticSystem.Tests;

public class ApiKeyAuthenticationTests
{
    private static ApiKeyAuthenticationHandler CreateHandler(
        string? configuredKey,
        string? providedKey)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configuredKey is not null
                ? new[] { new KeyValuePair<string, string?>("AgenticSystem:AdminApiKey", configuredKey) }
                : Array.Empty<KeyValuePair<string, string?>>())
            .Build();

        var options = new AuthenticationSchemeOptions();
        var optionsMonitor = Substitute.For<IOptionsMonitor<AuthenticationSchemeOptions>>();
        optionsMonitor.Get(ApiKeyAuthenticationHandler.SchemeName).Returns(options);
        optionsMonitor.CurrentValue.Returns(options);

        var loggerFactory = Substitute.For<ILoggerFactory>();
        loggerFactory.CreateLogger(Arg.Any<string>()).Returns(Substitute.For<ILogger>());

        var handler = new ApiKeyAuthenticationHandler(
            optionsMonitor,
            loggerFactory,
            UrlEncoder.Default,
            config);

        var scheme = new AuthenticationScheme(ApiKeyAuthenticationHandler.SchemeName, null, typeof(ApiKeyAuthenticationHandler));
        var context = new DefaultHttpContext();

        if (providedKey is not null)
            context.Request.Headers["X-Api-Key"] = providedKey;

        handler.InitializeAsync(scheme, context).GetAwaiter().GetResult();

        return handler;
    }

    [Fact]
    public async Task Authenticate_WithValidKey_ReturnsSuccess()
    {
        var handler = CreateHandler("my-secret-key", "my-secret-key");

        var result = await handler.AuthenticateAsync();

        result.Succeeded.Should().BeTrue();
        result.Principal!.Identity!.Name.Should().Be("admin");
    }

    [Fact]
    public async Task Authenticate_WithInvalidKey_ReturnsFailure()
    {
        var handler = CreateHandler("my-secret-key", "wrong-key");

        var result = await handler.AuthenticateAsync();

        result.Succeeded.Should().BeFalse();
        result.Failure!.Message.Should().Contain("Invalid");
    }

    [Fact]
    public async Task Authenticate_WithMissingHeader_ReturnsFailure()
    {
        var handler = CreateHandler("my-secret-key", null);

        var result = await handler.AuthenticateAsync();

        result.Succeeded.Should().BeFalse();
        result.Failure!.Message.Should().Contain("Missing");
    }

    [Fact]
    public async Task Authenticate_WithNoConfiguredKey_ReturnsFailure()
    {
        var handler = CreateHandler(null, "some-key");

        var result = await handler.AuthenticateAsync();

        result.Succeeded.Should().BeFalse();
        result.Failure!.Message.Should().Contain("not configured");
    }
}
