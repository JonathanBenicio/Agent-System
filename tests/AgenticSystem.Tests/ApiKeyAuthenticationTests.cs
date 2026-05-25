using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Text.Encodings.Web;
using Microsoft.EntityFrameworkCore;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.Persistence;
using AgenticSystem.Api.Auth;

namespace AgenticSystem.Tests;

public class ApiKeyAuthenticationTests
{
    private static ApiKeyAuthenticationHandler CreateHandler(
        string? configuredKey,
        string? providedKey)
    {
        var dbName = $"apikey-auth-tests-{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<AgenticDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        var tenantAccessor = Substitute.For<ITenantContextAccessor>();
        tenantAccessor.CurrentTenantId.Returns("admin");

        var dbContext = new AgenticDbContext(options, tenantAccessor);
        dbContext.Database.EnsureCreated();

        if (configuredKey is not null)
        {
            var keyBytes = System.Text.Encoding.UTF8.GetBytes(configuredKey.Trim());
            var hashBytes = System.Security.Cryptography.SHA256.HashData(keyBytes);
            var keyHash = Convert.ToHexString(hashBytes).ToLowerInvariant();

            dbContext.AccessApiKeys.Add(new AgenticSystem.Infrastructure.Persistence.Entities.AccessApiKeyEntity
            {
                Id = Guid.NewGuid(),
                Name = "Admin Key",
                TenantId = "admin",
                KeyHash = keyHash,
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow
            });
            dbContext.SaveChanges();
        }

        var optionsMonitor = Substitute.For<IOptionsMonitor<AuthenticationSchemeOptions>>();
        var schemeOptions = new AuthenticationSchemeOptions();
        optionsMonitor.Get(ApiKeyAuthenticationHandler.SchemeName).Returns(schemeOptions);
        optionsMonitor.CurrentValue.Returns(schemeOptions);

        var loggerFactory = Substitute.For<ILoggerFactory>();
        loggerFactory.CreateLogger(Arg.Any<string>()).Returns(Substitute.For<ILogger>());

        var handler = new ApiKeyAuthenticationHandler(
            optionsMonitor,
            loggerFactory,
            UrlEncoder.Default,
            dbContext);

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
        result.Principal!.Identity!.Name.Should().Be("Admin Key");
    }

    [Fact]
    public async Task Authenticate_WithInvalidKey_ReturnsFailure()
    {
        var handler = CreateHandler("my-secret-key", "wrong-key");

        var result = await handler.AuthenticateAsync();

        result.Succeeded.Should().BeFalse();
        result.Failure!.Message.Should().Contain("Invalid API key");
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
        result.Failure!.Message.Should().Contain("Invalid API key");
    }
}
