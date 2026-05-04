using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using AgenticSystem.Api.Controllers;
using AgenticSystem.Infrastructure.Configuration;

namespace AgenticSystem.Tests;

public class SettingsControllerTests
{
    private readonly SettingsController _sut;
    private readonly AgenticSystemSettings _settings;

    public SettingsControllerTests()
    {
        _settings = new AgenticSystemSettings
        {
            OpenAI = new OpenAISettings { ApiKey = "sk-test", Enabled = true, Priority = 1, DefaultModel = "gpt-4o" },
            Ollama = new OllamaSettings { Enabled = false, Priority = 10 },
            Gemini = new GeminiSettings { ApiKey = "", Enabled = false, Priority = 5 },
            Claude = new ClaudeSettings { ApiKey = "sk-ant-test", Enabled = true, Priority = 3 },
            Gateway = new GatewaySettings { DefaultDailyBudget = 50.0m, DefaultRequestsPerMinute = 60 },
            Memory = new MemorySettings { VectorStoreType = "InMemory" }
        };

        var options = Substitute.For<IOptions<AgenticSystemSettings>>();
        options.Value.Returns(_settings);
        var logger = Substitute.For<ILogger<SettingsController>>();

        _sut = new SettingsController(options, logger);
    }

    [Fact]
    public void GetSettings_ReturnsOk()
    {
        var result = _sut.GetSettings();

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public void GetGatewaySettings_ReturnsGatewayConfig()
    {
        var result = _sut.GetGatewaySettings();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeOfType<GatewaySettings>();
        var gw = (GatewaySettings)ok.Value!;
        gw.DefaultDailyBudget.Should().Be(50.0m);
    }

    [Fact]
    public void GetMemorySettings_ReturnsMemoryConfig()
    {
        var result = _sut.GetMemorySettings();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeOfType<MemorySettings>();
        var mem = (MemorySettings)ok.Value!;
        mem.VectorStoreType.Should().Be("InMemory");
    }

    [Fact]
    public void GetProviderSettings_ReturnsAllProviders()
    {
        var result = _sut.GetProviderSettings();

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public void GetSettings_MasksApiKeys()
    {
        var result = _sut.GetSettings();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var json = System.Text.Json.JsonSerializer.Serialize(ok.Value);

        // API keys should NOT appear in the response — only hasApiKey booleans
        json.Should().NotContain("sk-test");
        json.Should().NotContain("sk-ant-test");
        json.Should().Contain("hasApiKey");
    }

    [Fact]
    public void UpdateGatewaySettings_UpdatesInMemory()
    {
        var update = new GatewaySettings
        {
            DefaultDailyBudget = 100.0m,
            DefaultFailureThreshold = 10,
            DefaultBreakDurationSeconds = 60,
            DefaultRequestsPerMinute = 120
        };

        var result = _sut.UpdateGatewaySettings(update);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var gw = ok.Value.Should().BeOfType<GatewaySettings>().Subject;
        gw.DefaultDailyBudget.Should().Be(100.0m);
        gw.DefaultRequestsPerMinute.Should().Be(120);
        _settings.Gateway.DefaultDailyBudget.Should().Be(100.0m);
    }

    [Fact]
    public void UpdateMemorySettings_UpdatesInMemory()
    {
        var update = new MemorySettings
        {
            ObsidianVaultPath = "/vault",
            VectorStoreType = "Postgres",
            ConnectionString = "Host=localhost"
        };

        var result = _sut.UpdateMemorySettings(update);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var mem = ok.Value.Should().BeOfType<MemorySettings>().Subject;
        mem.VectorStoreType.Should().Be("Postgres");
        _settings.Memory.VectorStoreType.Should().Be("Postgres");
    }
}
