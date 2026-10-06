using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;
using AgenticSystem.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AgenticSystem.Api.Auth;

public class ApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "ApiKey";
    public const string HeaderName = "X-Api-Key";
    private readonly AgenticDbContext _dbContext;
    private readonly ISystemOperationContextAccessor _systemOperations;
    private readonly ITenantContextAccessor _tenantContextAccessor;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        AgenticDbContext dbContext,
        ISystemOperationContextAccessor systemOperations,
        ITenantContextAccessor tenantContextAccessor)
        : base(options, logger, encoder)
    {
        _dbContext = dbContext;
        _systemOperations = systemOperations;
        _tenantContextAccessor = tenantContextAccessor;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string? providedKey = null;

        if (Request.Headers.TryGetValue(HeaderName, out var apiKeyValues))
        {
            providedKey = apiKeyValues.FirstOrDefault()?.Trim();
        }

        if (string.IsNullOrWhiteSpace(providedKey) &&
            Request.Headers.TryGetValue("Authorization", out var authorizationValues))
        {
            var authorization = authorizationValues.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(authorization) &&
                authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                providedKey = authorization["Bearer ".Length..].Trim();
            }
            else if (!string.IsNullOrWhiteSpace(authorization) &&
                     Request.Path.StartsWithSegments("/v1"))
            {
                // Raw keys from legacy OpenAI clients still pass through the shared
                // authentication scheme and tenant/membership middleware.
                providedKey = authorization.Trim();
            }
        }

        if (string.IsNullOrWhiteSpace(providedKey) && Request.Cookies.TryGetValue("agentic_api_key", out var cookieKey))
        {
            providedKey = cookieKey?.Trim();
        }

        if (string.IsNullOrWhiteSpace(providedKey) && Request.Path.StartsWithSegments("/hubs"))
        {
            if (Request.Query.TryGetValue("api_key", out var queryKey) || Request.Query.TryGetValue("X-Api-Key", out queryKey))
            {
                providedKey = queryKey.FirstOrDefault()?.Trim();
            }
        }

        if (string.IsNullOrWhiteSpace(providedKey))
        {
            if (Context.Request.Path.StartsWithSegments("/health"))
            {
                return AuthenticateResult.NoResult();
            }
            return AuthenticateResult.Fail("Missing X-Api-Key header or query parameter.");
        }

        // 1. Calcula o Hash SHA-256 da chave fornecida
        var keyBytes = Encoding.UTF8.GetBytes(providedKey.Trim());
        var hashBytes = SHA256.HashData(keyBytes);
        var keyHash = Convert.ToHexString(hashBytes).ToLowerInvariant();

        // 2. Consulta no banco de dados se o hash corresponde a uma chave ativa
        using var systemScope = _systemOperations.BeginScope(SystemOperationKind.ApiKeyAuthentication);
        _systemOperations.Require(SystemOperationKind.ApiKeyAuthentication);
        var accessKey = await _dbContext.AccessApiKeys
            .IgnoreQueryFilters() // Ignora o filtro de tenant para permitir autenticação global cruzada
            .FirstOrDefaultAsync(k => k.KeyHash == keyHash && k.IsEnabled);

        if (accessKey is null)
        {
            return AuthenticateResult.Fail("Invalid API key.");
        }

        var role = TenantMembershipPolicy.NormalizeRole(accessKey.Role);
        if (role is null)
        {
            return AuthenticateResult.Fail("API key has an unsupported role.");
        }

        // 3. Atualiza o timestamp de último uso de forma assíncrona
        try
        {
            using var tenantScope = _tenantContextAccessor.BeginScope(new TenantContext { TenantId = accessKey.TenantId });
            accessKey.LastUsedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();
        }
        catch
        {
            // Abafa erros de gravação de estatística para não interromper a autenticação principal
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, accessKey.Name),
            new Claim(ClaimTypes.NameIdentifier, accessKey.Id.ToString()),
            new Claim(ClaimTypes.Role, role),
            new Claim("tenant_id", accessKey.TenantId)
        };
        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return AuthenticateResult.Success(ticket);
    }
}

