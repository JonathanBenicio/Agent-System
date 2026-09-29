using System.Text.Json;
using AgenticSystem.Api.Controllers;
using AgenticSystem.Core.LLM.Interfaces;
using AgenticSystem.Core.LLM.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;

namespace AgenticSystem.Tests;

public sealed class LLMProviderApiKeyControllerTests
{
    [Fact]
    public async Task TestKey_ValidatesSavedCredentialWithProviderAndRedactsEchoedSecret()
    {
        const string secret = "synthetic-secret-123";
        var keys = Substitute.For<ILLMProviderApiKeyService>();
        keys.GetDecryptedKeyAsync("OpenAI", "key-1", Arg.Any<CancellationToken>()).Returns(secret);
        var administration = Substitute.For<ILLMAdministrationService>();
        administration.DiscoverModelsAsync("OpenAI",
                Arg.Is<DiscoverModelsRequest>(request => request.ApiKey == secret), Arg.Any<CancellationToken>())
            .Returns(new DiscoverModelsResponse { Success = false, ErrorMessage = $"Provider rejected {secret}" });
        var controller = new LLMProviderApiKeyController(keys, administration);

        var result = await controller.TestKey("OpenAI", "key-1", CancellationToken.None);

        var json = JsonSerializer.Serialize(((OkObjectResult)result).Value);
        json.Should().Contain("redacted").And.NotContain(secret);
        await keys.Received(1).GetDecryptedKeyAsync("OpenAI", "key-1", Arg.Any<CancellationToken>());
        await administration.Received(1).DiscoverModelsAsync("OpenAI",
            Arg.Is<DiscoverModelsRequest>(request => request.ApiKey == secret), Arg.Any<CancellationToken>());
    }
}
