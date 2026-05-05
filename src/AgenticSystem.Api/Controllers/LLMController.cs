using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AgenticSystem.Core.LLM.Interfaces;

namespace AgenticSystem.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/admin/llm")]
public class LLMController : ControllerBase
{
    private readonly ILLMManager _llmManager;
    private readonly ILogger<LLMController> _logger;

    public LLMController(ILLMManager llmManager, ILogger<LLMController> logger)
    {
        _llmManager = llmManager;
        _logger = logger;
    }

    [HttpGet("configuration")]
    public async Task<IActionResult> GetConfiguration(CancellationToken ct)
    {
        var configuration = await _llmManager.GetConfigurationAsync(ct);
        return Ok(configuration);
    }

    [HttpGet("providers")]
    public async Task<IActionResult> GetProviders(CancellationToken ct)
    {
        var configuration = await _llmManager.GetConfigurationAsync(ct);
        return Ok(configuration.Providers);
    }

    [HttpGet("providers/{name}")]
    public async Task<IActionResult> GetProvider(string name, CancellationToken ct)
    {
        var configuration = await _llmManager.GetConfigurationAsync(ct);
        var provider = configuration.Providers.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (provider is null)
            return NotFound(new { error = $"Provider '{name}' not found." });

        return Ok(provider);
    }

    [HttpGet("providers/enabled")]
    public IActionResult GetEnabledProviders()
    {
        var providers = _llmManager.GetEnabledProviders()
            .Select(p => new
            {
                p.Name,
                p.DefaultModel,
                p.IsEnabled,
                p.Priority
            });

        return Ok(providers);
    }

    [HttpGet("providers/default")]
    public async Task<IActionResult> GetDefaultProvider(CancellationToken ct)
    {
        try
        {
            var configuration = await _llmManager.GetConfigurationAsync(ct);
            var provider = configuration.Providers.FirstOrDefault(p => p.Name.Equals(configuration.DefaultProvider, StringComparison.OrdinalIgnoreCase));
            if (provider is null)
                return NotFound(new { error = "No default provider configured." });

            return Ok(new
            {
                provider.Name,
                DefaultModel = configuration.DefaultModel,
                provider.IsEnabled,
                provider.Priority
            });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpPost("providers/{name}/test")]
    public async Task<IActionResult> TestProvider(string name, CancellationToken ct)
    {
        var available = await _llmManager.TestProviderAsync(name, ct);
        return Ok(new { provider = name, available });
    }

    [HttpPut("default-selection")]
    public async Task<IActionResult> UpdateDefaultSelection([FromBody] UpdateDefaultLlmSelectionRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.ProviderName))
            return BadRequest(new { error = "ProviderName is required." });

        var configuration = await _llmManager.UpdateDefaultSelectionAsync(request, ct);
        return Ok(configuration);
    }

    [HttpPut("providers/{name}")]
    public async Task<IActionResult> UpdateProvider(string name, [FromBody] UpdateProviderRequest request, CancellationToken ct)
    {
        var updated = await _llmManager.UpdateProviderAsync(name, request, ct);
        if (!updated)
            return NotFound(new { error = $"Provider '{name}' not found." });

        var configuration = await _llmManager.GetConfigurationAsync(ct);
        var info = configuration.Providers
            .FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        return Ok(info);
    }
}
