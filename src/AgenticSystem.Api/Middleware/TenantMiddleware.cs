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

    public async Task InvokeAsync(HttpContext context, TenantContext tenantContext, ITenantResolver tenantResolver)
    {
        var tenantId = ResolveTenantId(context);

        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            var resolved = await tenantResolver.ResolveAsync(tenantId);
            if (resolved is not null)
            {
                tenantContext.TenantId = resolved.TenantId;
                tenantContext.TenantName = resolved.TenantName;
                tenantContext.Plan = resolved.Plan;
                tenantContext.Limits = resolved.Limits;
                tenantContext.IsAuthenticated = resolved.IsAuthenticated;

                _logger.LogDebug("Tenant resolved: {TenantId} ({TenantName})", tenantContext.TenantId, tenantContext.TenantName);
            }
            else
            {
                _logger.LogWarning("Tenant not found for id: {TenantId}. Using default.", tenantId);
            }
        }

        await _next(context);
    }

    private static string? ResolveTenantId(HttpContext context)
    {
        // 1. JWT claim
        var claimValue = context.User?.FindFirst(TenantIdClaimType)?.Value;
        if (!string.IsNullOrWhiteSpace(claimValue))
            return claimValue;

        // 2. Header X-Tenant-Id
        if (context.Request.Headers.TryGetValue(TenantIdHeaderName, out var headerValue))
        {
            var val = headerValue.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(val))
                return val;
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
