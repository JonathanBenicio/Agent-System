using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AgenticSystem.Core.LLM.Interfaces;

namespace AgenticSystem.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/admin/llm")]
public class LLMController : ControllerBase
{
    private readonly ILLMAdministrationService _llmAdministrationService;

    public LLMController(ILLMAdministrationService llmAdministrationService)
    {
        _llmAdministrationService = llmAdministrationService;
    }

    [HttpGet("configuration")]
    public async Task<IActionResult> GetConfiguration(CancellationToken ct)
    {
        var configuration = await _llmAdministrationService.GetConfigurationAsync(ct);
        return Ok(configuration);
    }

    [HttpGet("providers")]
    public async Task<IActionResult> GetProviders(CancellationToken ct)
    {
        var configuration = await _llmAdministrationService.GetConfigurationAsync(ct);
        return Ok(configuration.Providers);
    }

    [HttpGet("providers/{name}")]
    public async Task<IActionResult> GetProvider(string name, CancellationToken ct)
    {
        var provider = await _llmAdministrationService.GetProviderAsync(name, ct);
        if (provider is null)
            return NotFound(new { error = $"Provider '{name}' not found." });

        return Ok(provider);
    }

    [HttpGet("providers/enabled")]
    public async Task<IActionResult> GetEnabledProviders(CancellationToken ct)
    {
        var providers = await _llmAdministrationService.GetEnabledProvidersAsync(ct);
        return Ok(providers);
    }

    [HttpGet("providers/default")]
    public async Task<IActionResult> GetDefaultProvider(CancellationToken ct)
    {
        var provider = await _llmAdministrationService.GetDefaultProviderAsync(ct);
        if (provider is null)
        {
            return NotFound(new { error = "No default provider configured." });
        }

        return Ok(provider);
    }

    [HttpPost("providers/{name}/test")]
    public async Task<IActionResult> TestProvider(string name, CancellationToken ct)
    {
        var available = await _llmAdministrationService.TestProviderAsync(name, ct);
        return Ok(new { provider = name, available });
    }

    [HttpPost("providers/{name}/discover-models")]
    public async Task<IActionResult> DiscoverModels(string name, [FromBody] DiscoverModelsRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.ApiKey))
        {
            return BadRequest(new { error = "ApiKey is required for discovery." });
        }

        var response = await _llmAdministrationService.DiscoverModelsAsync(name, request, ct);
        if (!response.Success)
        {
            return BadRequest(new { error = response.ErrorMessage });
        }

        return Ok(response);
    }

    [HttpPut("default-selection")]
    public async Task<IActionResult> UpdateDefaultSelection([FromBody] UpdateDefaultLlmSelectionRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.ProviderName))
            return BadRequest(new { error = "ProviderName is required." });

        var configuration = await _llmAdministrationService.UpdateDefaultSelectionAsync(request, ct);
        return Ok(configuration);
    }

    [HttpPut("providers/{name}")]
    public async Task<IActionResult> UpdateProvider(string name, [FromBody] UpdateProviderRequest request, CancellationToken ct)
    {
        var info = await _llmAdministrationService.UpdateProviderAsync(name, request, ct);
        if (info is null)
            return NotFound(new { error = $"Provider '{name}' not found." });

        return Ok(info);
    }
}
