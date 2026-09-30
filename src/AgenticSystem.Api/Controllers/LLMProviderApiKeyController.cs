using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AgenticSystem.Core.LLM.Interfaces;
using AgenticSystem.Core.LLM.Models;

namespace AgenticSystem.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/admin/llm/providers/{providerName}/keys")]
public class LLMProviderApiKeyController : ControllerBase
{
    private readonly ILLMProviderApiKeyService _apiKeyService;
    private readonly ILLMAdministrationService _llmAdminService;

    public LLMProviderApiKeyController(
        ILLMProviderApiKeyService apiKeyService,
        ILLMAdministrationService llmAdminService)
    {
        _apiKeyService = apiKeyService;
        _llmAdminService = llmAdminService;
    }

    [HttpGet]
    public async Task<IActionResult> GetKeys(string providerName, CancellationToken ct)
    {
        var keys = await _apiKeyService.GetKeysByProviderAsync(providerName, ct);
        return Ok(keys);
    }

    [HttpPost]
    [Authorize(Roles = "Owner,Admin")]
    public async Task<IActionResult> RegisterKey(string providerName, [FromBody] RegisterApiKeyRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.ApiKey))
        {
            return BadRequest(new { error = "Name and ApiKey are required." });
        }

        try
        {
            var key = await _apiKeyService.RegisterKeyAsync(providerName, request, ct);
            return Ok(key);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Owner,Admin")]
    public async Task<IActionResult> UpdateKey(string providerName, string id, [FromBody] UpdateApiKeyRequest request, CancellationToken ct)
    {
        try
        {
            var key = await _apiKeyService.UpdateKeyAsync(providerName, id, request, ct);
            return Ok(key);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Owner,Admin")]
    public async Task<IActionResult> DeleteKey(string providerName, string id, CancellationToken ct)
    {
        try
        {
            await _apiKeyService.DeleteKeyAsync(providerName, id, ct);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpPost("{id}/default")]
    [Authorize(Roles = "Owner,Admin")]
    public async Task<IActionResult> SetDefaultKey(string providerName, string id, CancellationToken ct)
    {
        try
        {
            await _apiKeyService.SetDefaultKeyAsync(providerName, id, ct);
            return Ok(new { message = "Default key updated successfully." });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpPost("{id}/test")]
    [Authorize(Roles = "Owner,Admin")]
    public async Task<IActionResult> TestKey(string providerName, string id, CancellationToken ct)
    {
        try
        {
            var decryptedKey = await _apiKeyService.GetDecryptedKeyAsync(providerName, id, ct);
            var response = await _llmAdminService.DiscoverModelsAsync(
                providerName, new DiscoverModelsRequest { ApiKey = decryptedKey }, ct);
            return Ok(new
            {
                success = response.Success,
                errorMessage = RedactSecret(response.ErrorMessage, decryptedKey)
            });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpPost("{id}/discover-models")]
    [Authorize(Roles = "Owner,Admin")]
    public async Task<IActionResult> DiscoverModels(string providerName, string id, CancellationToken ct)
    {
        try
        {
            var decryptedKey = await _apiKeyService.GetDecryptedKeyAsync(providerName, id, ct);
            var req = new DiscoverModelsRequest { ApiKey = decryptedKey };
            var response = await _llmAdminService.DiscoverModelsAsync(providerName, req, ct);
            
            if (!response.Success)
            {
                return BadRequest(new { error = RedactSecret(response.ErrorMessage, decryptedKey) });
            }

            // Update the entity with discovered models
            if (response.DiscoveredModels != null && response.DiscoveredModels.Count > 0)
            {
                await _apiKeyService.UpdateKeyAsync(providerName, id, new UpdateApiKeyRequest { Models = response.DiscoveredModels }, ct);
            }

            return Ok(new
            {
                response.Success,
                errorMessage = RedactSecret(response.ErrorMessage, decryptedKey),
                response.DiscoveredModels
            });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    private static string? RedactSecret(string? message, string secret) =>
        string.IsNullOrEmpty(message) || string.IsNullOrEmpty(secret)
            ? message
            : message.Replace(secret, "[redacted]", StringComparison.Ordinal);
}
