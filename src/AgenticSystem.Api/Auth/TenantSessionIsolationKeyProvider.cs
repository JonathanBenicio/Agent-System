using AgenticSystem.Core.Interfaces;
using Microsoft.Agents.AI.Hosting;

namespace AgenticSystem.Api.Auth;

/// <summary>Scopes hosted framework sessions to the authenticated principal inside a tenant.</summary>
public sealed class TenantSessionIsolationKeyProvider : AgentIsolationKeyProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ITenantContextAccessor _tenantContextAccessor;
    private readonly ILLMRuntimeContextAccessor _runtimeContextAccessor;

    public TenantSessionIsolationKeyProvider(
        IHttpContextAccessor httpContextAccessor,
        ITenantContextAccessor tenantContextAccessor,
        ILLMRuntimeContextAccessor runtimeContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
        _tenantContextAccessor = tenantContextAccessor;
        _runtimeContextAccessor = runtimeContextAccessor;
    }

    public override ValueTask<string?> GetIsolationKeyAsync(CancellationToken cancellationToken = default)
    {
        var runtime = _runtimeContextAccessor.Current;
        if (!string.IsNullOrWhiteSpace(runtime?.TenantId) && !string.IsNullOrWhiteSpace(runtime.UserId))
            return ValueTask.FromResult<string?>($"{runtime.TenantId}:{runtime.UserId}");

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
