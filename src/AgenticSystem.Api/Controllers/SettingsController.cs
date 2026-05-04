using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using AgenticSystem.Infrastructure.Configuration;

namespace AgenticSystem.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/admin/settings")]
public class SettingsController : ControllerBase
{
    private readonly AgenticSystemSettings _settings;
    private readonly ILogger<SettingsController> _logger;

    public SettingsController(IOptions<AgenticSystemSettings> settings, ILogger<SettingsController> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult GetSettings()
    {
        return Ok(new
        {
            openAI = new { _settings.OpenAI.BaseUrl, _settings.OpenAI.DefaultModel, _settings.OpenAI.Enabled, _settings.OpenAI.Priority, hasApiKey = !string.IsNullOrWhiteSpace(_settings.OpenAI.ApiKey) },
            ollama = new { _settings.Ollama.BaseUrl, _settings.Ollama.DefaultModel, _settings.Ollama.Enabled, _settings.Ollama.Priority },
            gemini = new { _settings.Gemini.BaseUrl, _settings.Gemini.DefaultModel, _settings.Gemini.Enabled, _settings.Gemini.Priority, hasApiKey = !string.IsNullOrWhiteSpace(_settings.Gemini.ApiKey) },
            claude = new { _settings.Claude.BaseUrl, _settings.Claude.DefaultModel, _settings.Claude.Enabled, _settings.Claude.Priority, hasApiKey = !string.IsNullOrWhiteSpace(_settings.Claude.ApiKey) },
            gateway = _settings.Gateway,
            memory = _settings.Memory
        });
    }

    [HttpGet("gateway")]
    public IActionResult GetGatewaySettings()
    {
        return Ok(_settings.Gateway);
    }

    [HttpGet("memory")]
    public IActionResult GetMemorySettings()
    {
        return Ok(_settings.Memory);
    }

    [HttpGet("providers")]
    public IActionResult GetProviderSettings()
    {
        return Ok(new
        {
            openAI = new { _settings.OpenAI.BaseUrl, _settings.OpenAI.DefaultModel, _settings.OpenAI.Enabled, _settings.OpenAI.Priority, hasApiKey = !string.IsNullOrWhiteSpace(_settings.OpenAI.ApiKey) },
            ollama = new { _settings.Ollama.BaseUrl, _settings.Ollama.DefaultModel, _settings.Ollama.Enabled, _settings.Ollama.Priority },
            gemini = new { _settings.Gemini.BaseUrl, _settings.Gemini.DefaultModel, _settings.Gemini.Enabled, _settings.Gemini.Priority, hasApiKey = !string.IsNullOrWhiteSpace(_settings.Gemini.ApiKey) },
            claude = new { _settings.Claude.BaseUrl, _settings.Claude.DefaultModel, _settings.Claude.Enabled, _settings.Claude.Priority, hasApiKey = !string.IsNullOrWhiteSpace(_settings.Claude.ApiKey) }
        });
    }

    [HttpPut("gateway")]
    public IActionResult UpdateGatewaySettings([FromBody] GatewaySettings update)
    {
        _settings.Gateway.DefaultDailyBudget = update.DefaultDailyBudget;
        _settings.Gateway.DefaultFailureThreshold = update.DefaultFailureThreshold;
        _settings.Gateway.DefaultBreakDurationSeconds = update.DefaultBreakDurationSeconds;
        _settings.Gateway.DefaultRequestsPerMinute = update.DefaultRequestsPerMinute;
        _logger.LogInformation("Gateway settings updated");
        return Ok(_settings.Gateway);
    }

    [HttpPut("memory")]
    public IActionResult UpdateMemorySettings([FromBody] MemorySettings update)
    {
        _settings.Memory.ObsidianVaultPath = update.ObsidianVaultPath;
        _settings.Memory.VectorStoreType = update.VectorStoreType;
        _settings.Memory.ConnectionString = update.ConnectionString;
        _logger.LogInformation("Memory settings updated");
        return Ok(_settings.Memory);
    }
}
