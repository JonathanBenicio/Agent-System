using AgenticSystem.Core.Interfaces;
using Microsoft.Agents.AI.Hosting;

namespace AgenticSystem.Api.Auth;

/// <summary>Scopes hosted framework sessions to the authenticated principal inside a tenant.</summary>
public sealed class TenantSessionIsolationKeyProvider : SessionIsolationKeyProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ITenantContextAccessor _tenantContextAccessor;

    public TenantSessionIsolationKeyProvider(
        IHttpContextAccessor httpContextAccessor,
        ITenantContextAccessor tenantContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
        _tenantContextAccessor = tenantContextAccessor;
    }

    public override ValueTask<string?> GetSessionIsolationKeyAsync(CancellationToken cancellationToken)
    {
        var principal = _httpContextAccessor.HttpContext?.User;
        var identity = principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? principal?.FindFirst("sub")?.Value;

        if (principal?.Identity?.IsAuthenticated != true || string.IsNullOrWhiteSpace(identity))
            return ValueTask.FromResult<string?>(null);

        try
        {
            var tenantId = _tenantContextAccessor.CurrentTenantId;
            return ValueTask.FromResult<string?>($"{tenantId}:{identity}");
        }
        catch (InvalidOperationException)
        {
            return ValueTask.FromResult<string?>(null);
        }
    }
}
