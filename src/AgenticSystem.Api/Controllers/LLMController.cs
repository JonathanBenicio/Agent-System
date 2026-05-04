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

    [HttpGet("providers")]
    public IActionResult GetProviders()
    {
        var providers = _llmManager.GetAllProviderInfo();
        return Ok(providers);
    }

    [HttpGet("providers/{name}")]
    public IActionResult GetProvider(string name)
    {
        var providers = _llmManager.GetAllProviderInfo();
        var provider = providers.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
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
    public IActionResult GetDefaultProvider()
    {
        try
        {
            var provider = _llmManager.GetDefaultProvider();
            return Ok(new
            {
                provider.Name,
                provider.DefaultModel,
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

    [HttpPut("providers/{name}")]
    public IActionResult UpdateProvider(string name, [FromBody] UpdateProviderRequest request)
    {
        var updated = _llmManager.UpdateProvider(name, request);
        if (!updated)
            return NotFound(new { error = $"Provider '{name}' not found." });

        var info = _llmManager.GetAllProviderInfo()
            .FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        return Ok(info);
    }
}
