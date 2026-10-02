using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;
using System.Security.Claims;

namespace AgenticSystem.Api.Middleware;

/// <summary>
/// Middleware que extrai o tenantId do request (JWT claim ou header) e popula o TenantContext scoped.
/// Requests that access tenant data must identify an existing tenant explicitly.
/// </summary>
public class TenantMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantMiddleware> _logger;

    public const string TenantIdClaimType = "tenant_id";
    public const string TenantIdHeaderName = "X-Tenant-Id";

    public TenantMiddleware(RequestDelegate next, ILogger<TenantMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext context,
        ITenantResolver tenantResolver,
        ITenantContextAccessor tenantContextAccessor,
        IPermissionService? permissionService = null,
        IQuotaEnforcer? quotaEnforcer = null)
    {
        // Login exchanges a credential; logout must clear even a revoked session cookie.
        // These actions do not access tenant-owned product data.
        if (context.Request.Path.Equals("/api/auth/login", StringComparison.OrdinalIgnoreCase) ||
            context.Request.Path.Equals("/api/auth/logout", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        // The platform administration API is authenticated and authorized by the explicit
        // platform_administrators registry. It has no tenant context.
        if (context.Request.Path.StartsWithSegments("/api/platform", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var endpoint = context.GetEndpoint();
        var hasAuthorize = endpoint?.Metadata.GetMetadata<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>() is not null;
        var allowAnonymous = endpoint?.Metadata.GetMetadata<Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute>() is not null;

        var tenantId = ResolveTenantId(context);
        var jwtTenantId = GetJwtTenantId(context);
        var isAuthenticated = context.User?.Identity?.IsAuthenticated == true;

        if (isAuthenticated && !string.IsNullOrWhiteSpace(tenantId))
        {
            if (string.IsNullOrWhiteSpace(jwtTenantId) ||
                !string.Equals(tenantId.Trim(), jwtTenantId.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Tenant spoofing attempt detected. Header tenant '{TenantId}' does not match JWT tenant '{JwtTenantId}'.", tenantId, jwtTenantId);
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new { error = "Unauthorized tenant access." });
                return;
            }
        }

        TenantContext? tenantContext = null;

        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            var resolved = await tenantResolver.ResolveAsync(tenantId);
            if (resolved is not null)
            {
                if (isAuthenticated && string.IsNullOrWhiteSpace(context.User?.FindFirst(TenantIdClaimType)?.Value))
                    (context.User?.Identity as ClaimsIdentity)?.AddClaim(new Claim(TenantIdClaimType, resolved.TenantId));

                tenantContext = new TenantContext
                {
                    TenantId = resolved.TenantId,
                    TenantName = resolved.TenantName,
                    Plan = resolved.Plan,
                    Limits = resolved.Limits,
                    IsAuthenticated = resolved.IsAuthenticated
                };
                _logger.LogInformation("Tenant resolved: {TenantId} ({TenantName})", tenantContext.TenantId, tenantContext.TenantName);
            }
            else
            {
                _logger.LogWarning("Tenant '{TenantId}' not found in store for request.", tenantId);
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new { error = "Tenant not found or inactive." });
                return;
            }
        }
        else if (hasAuthorize && !allowAnonymous)
        {
            _logger.LogWarning("Authenticated request without tenant context.");
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { error = "Tenant identification required." });
            return;
        }

        if (tenantContext is not null)
        {
            using var tenantScope = tenantContextAccessor.BeginScope(tenantContext);
            if (isAuthenticated && !await HasTenantMembershipAsync(context, tenantContext.TenantId, permissionService))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new { error = "No active membership for this tenant." });
                return;
            }

            var planLimits = tenantContext.Plan switch
            {
                TenantPlan.Pro => TenantLimits.ProTier(),
                TenantPlan.Enterprise => TenantLimits.EnterpriseTier(),
                _ => TenantLimits.FreeTier()
            };
            var rpm = Math.Min(planLimits.MaxRequestsPerMinute,
                tenantContext.Limits.MaxRequestsPerMinute > 0 ? tenantContext.Limits.MaxRequestsPerMinute : planLimits.MaxRequestsPerMinute);
            var quotaConfig = quotaEnforcer is null
                ? null
                : await quotaEnforcer.GetQuotaConfigAsync(tenantContext.TenantId);
            if (quotaConfig?.RequestsPerMinute > 0)
                rpm = Math.Min(rpm, quotaConfig.RequestsPerMinute);
            context.Items["tenant-rate-limit"] = rpm;
            context.Items["tenant-context"] = tenantContext;

            await _next(context);
        }
        else
        {
            await _next(context);
        }
    }

    private static async Task<bool> HasTenantMembershipAsync(
        HttpContext context,
        string tenantId,
        IPermissionService? permissionService)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? context.User.FindFirst("sub")?.Value;
        if (string.IsNullOrWhiteSpace(userId) || permissionService is null)
            return false;

        var tenantRoles = await TenantMembershipPolicy.GetRolesAsync(
            permissionService, userId, tenantId, context.RequestAborted);
        if (tenantRoles.Length == 0)
            return false;

        foreach (var claimsIdentity in context.User.Identities)
        {
            foreach (var roleClaim in claimsIdentity.Claims
                         .Where(claim => claim.Type is ClaimTypes.Role or "role" or "roles")
                         .ToArray())
                claimsIdentity.RemoveClaim(roleClaim);
        }

        var authenticatedIdentity = context.User.Identities.FirstOrDefault(item => item.IsAuthenticated);
        if (authenticatedIdentity is null)
            return false;
        foreach (var role in tenantRoles)
            authenticatedIdentity.AddClaim(new Claim(ClaimTypes.Role, role));
        return true;
    }

    private static string? ResolveTenantId(HttpContext context)
    {
        // 1. Header X-Tenant-Id (prioridade máxima para permitir override explícito)
        if (context.Request.Headers.TryGetValue(TenantIdHeaderName, out var headerValue))
        {
            var val = headerValue.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(val))
                return val;
        }

        // SignalR WebSocket clients may be unable to send custom headers. Accept the
        // tenant selector from the query string only on hub routes; the authenticated
        // tenant claim is still checked below and must match it.
        if (context.Request.Path.StartsWithSegments("/hubs", StringComparison.OrdinalIgnoreCase))
        {
            var queryValue = context.Request.Query
                .FirstOrDefault(item => string.Equals(item.Key, TenantIdHeaderName, StringComparison.OrdinalIgnoreCase)).Value;
            var requestedTenantId = queryValue.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(requestedTenantId))
                return requestedTenantId;
        }

        // 2 & 3. JWT claim or Supabase Metadata
        return GetJwtTenantId(context);
    }

    private static string? GetJwtTenantId(HttpContext context)
    {
        // 2. JWT claim (Standard claim or Supabase claim)
        var claimValue = context.User?.FindFirst(TenantIdClaimType)?.Value;
        if (!string.IsNullOrWhiteSpace(claimValue))
            return claimValue;

        // 3. Supabase Metadata (app_metadata.tenant_id)
        // Note: Supabase puts custom claims inside app_metadata or user_metadata
        // This requires the JWT to be parsed correctly by the handler
        var metadataClaim = context.User?.FindFirst("app_metadata")?.Value;
        if (!string.IsNullOrWhiteSpace(metadataClaim))
        {
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(metadataClaim);
                if (doc.RootElement.TryGetProperty("tenant_id", out var tenantIdProp))
                {
                    return tenantIdProp.GetString();
                }
            }
            catch { /* Ignore parse errors */ }
        }

        return null;
    }

}

public static class TenantMiddlewareExtensions
{
    public static IApplicationBuilder UseTenantMiddleware(this IApplicationBuilder app)
    {
        return app.UseMiddleware<TenantMiddleware>();
    }
}
