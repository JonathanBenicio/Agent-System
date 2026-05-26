using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Api.Middleware;

/// <summary>
/// Middleware que extrai o tenantId do request (JWT claim ou header) e popula o TenantContext scoped.
/// Se nenhum tenant é encontrado, usa o tenant "default" para backward compatibility.
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

    public async Task InvokeAsync(HttpContext context, ITenantResolver tenantResolver, ITenantContextAccessor tenantContextAccessor)
    {
        var endpoint = context.GetEndpoint();
        var hasAuthorize = endpoint?.Metadata.GetMetadata<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>() is not null;
        var allowAnonymous = endpoint?.Metadata.GetMetadata<Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute>() is not null;

        var tenantId = ResolveTenantId(context);
        var jwtTenantId = GetJwtTenantId(context);
        var isAdmin = context.User?.IsInRole("Admin") ?? false;

        if (!string.IsNullOrEmpty(tenantId) && !string.IsNullOrEmpty(jwtTenantId) && tenantId != jwtTenantId && !isAdmin)
        {
            _logger.LogWarning("Tenant spoofing attempt detected. Header tenant '{TenantId}' does not match JWT tenant '{JwtTenantId}'.", tenantId, jwtTenantId);
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { error = "Unauthorized tenant access." });
            return;
        }

        TenantContext? tenantContext = null;

        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            var resolved = await tenantResolver.ResolveAsync(tenantId);
            if (resolved is not null)
            {
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
            else if (hasAuthorize && !allowAnonymous)
            {
                _logger.LogWarning("Tenant '{TenantId}' not found in store for authorized request.", tenantId);
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new { error = "Tenant not found or inactive." });
                return;
            }
            else
            {
                // Fallback de desenvolvimento para rotas não protegidas
                tenantContext = new TenantContext
                {
                    TenantId = tenantId,
                    TenantName = tenantId,
                    IsAuthenticated = true
                };
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
            await _next(context);
        }
        else
        {
            await _next(context);
        }
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
