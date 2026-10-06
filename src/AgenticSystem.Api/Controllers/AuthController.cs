using System.Security.Cryptography;
using System.Text;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;
using AgenticSystem.Infrastructure.Persistence;

namespace AgenticSystem.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AgenticDbContext _dbContext;
    private readonly ILogger<AuthController> _logger;
    private readonly ISystemOperationContextAccessor _systemOperations;
    private readonly ITenantContextAccessor _tenantContextAccessor;
    private readonly ITenantResolver _tenantResolver;
    private readonly IPermissionService _permissions;

    public AuthController(
        AgenticDbContext dbContext,
        ILogger<AuthController> logger,
        ISystemOperationContextAccessor systemOperations,
        ITenantContextAccessor tenantContextAccessor,
        ITenantResolver tenantResolver,
        IPermissionService permissions)
    {
        _dbContext = dbContext;
        _logger = logger;
        _systemOperations = systemOperations;
        _tenantContextAccessor = tenantContextAccessor;
        _tenantResolver = tenantResolver;
        _permissions = permissions;
    }

    [HttpPost("login")]
    [AllowAnonymous]
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

        if (accessKey is null || TenantMembershipPolicy.NormalizeRole(accessKey.Role) is null)
        {
            return Unauthorized(new { error = "Chave de API inválida." });
        }

        var tenant = await _tenantResolver.ResolveAsync(accessKey.TenantId);
        if (tenant is null)
            return Unauthorized(new { error = "Tenant inexistente ou inativo." });

        using var tenantScope = _tenantContextAccessor.BeginScope(tenant);
        var roles = await TenantMembershipPolicy.GetRolesAsync(
            _permissions, accessKey.Id.ToString(), tenant.TenantId, HttpContext.RequestAborted);
        if (roles.Length == 0)
            return Unauthorized(new { error = "Chave sem vínculo ativo com o tenant." });
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
        return Ok(new { success = true, role = roles[0], roles, tenantId = tenant.TenantId, userId = accessKey.Id.ToString() });
    }

    /// <summary>Restores browser identity from an authenticated cookie or bearer, without returning the credential.</summary>
    [HttpGet("session")]
    [Authorize]
    public IActionResult GetSession() => Ok(new
    {
        userId = User.FindFirstValue(ClaimTypes.NameIdentifier),
        tenantId = _tenantContextAccessor.CurrentTenantId,
        roles = User.FindAll(ClaimTypes.Role).Select(claim => claim.Value).ToArray()
    });

    [HttpPost("logout")]
    [AllowAnonymous]
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
