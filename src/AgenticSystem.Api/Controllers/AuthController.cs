using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.Persistence;

namespace AgenticSystem.Api.Controllers;

[ApiController]
[Route("api/auth")]
[AllowAnonymous]
public class AuthController : ControllerBase
{
    private readonly AgenticDbContext _dbContext;
    private readonly ILogger<AuthController> _logger;
    private readonly ISystemOperationContextAccessor _systemOperations;
    private readonly ITenantContextAccessor _tenantContextAccessor;

    public AuthController(
        AgenticDbContext dbContext,
        ILogger<AuthController> logger,
        ISystemOperationContextAccessor systemOperations,
        ITenantContextAccessor tenantContextAccessor)
    {
        _dbContext = dbContext;
        _logger = logger;
        _systemOperations = systemOperations;
        _tenantContextAccessor = tenantContextAccessor;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ApiKey))
        {
            return BadRequest(new { error = "A chave de API é obrigatória." });
        }

        // 1. Calcula o Hash SHA-256 da chave fornecida
        var keyBytes = Encoding.UTF8.GetBytes(request.ApiKey.Trim());
        var hashBytes = SHA256.HashData(keyBytes);
        var keyHash = Convert.ToHexString(hashBytes).ToLowerInvariant();

        // 2. Consulta no banco de dados se o hash corresponde a uma chave ativa
        using var systemScope = _systemOperations.BeginScope(SystemOperationKind.ApiKeyAuthentication);
        _systemOperations.Require(SystemOperationKind.ApiKeyAuthentication);
        var accessKey = await _dbContext.AccessApiKeys
            .IgnoreQueryFilters() // Ignora o filtro de tenant no login
            .FirstOrDefaultAsync(k => k.KeyHash == keyHash && k.IsEnabled);

        if (accessKey is null)
        {
            return Unauthorized(new { error = "Chave de API inválida." });
        }

        using var tenantScope = _tenantContextAccessor.BeginScope(new TenantContext { TenantId = accessKey.TenantId });
        // 3. Atualiza o timestamp de último uso
        try
        {
            accessKey.LastUsedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();
        }
        catch { /* Abafa erros de gravação de estatística */ }

        Response.Cookies.Append("agentic_api_key", request.ApiKey.Trim(), new CookieOptions
        {
            HttpOnly = true,
            Secure = true, // Em dev ou prod, garante envio seguro
            SameSite = SameSiteMode.Strict,
            Path = "/"
        });

        _logger.LogInformation("API key login succeeded for key {KeyId} in tenant {TenantId}.", accessKey.Id, accessKey.TenantId);
        return Ok(new { success = true, role = accessKey.Role, tenantId = accessKey.TenantId, userId = accessKey.Id.ToString() });
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        Response.Cookies.Delete("agentic_api_key", new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/"
        });

        return Ok(new { success = true });
    }
}

public record LoginRequest(string ApiKey);
