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

    public async Task InvokeAsync(HttpContext context, TenantContext tenantContext, ITenantResolver tenantResolver, ITenantContextAccessor tenantContextAccessor)
    {
        using var tenantScope = tenantContextAccessor.BeginScope(tenantContext);

        var endpoint = context.GetEndpoint();
        var hasAuthorize = endpoint?.Metadata.GetMetadata<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>() is not null;
        var allowAnonymous = endpoint?.Metadata.GetMetadata<Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute>() is not null;

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

                _logger.LogInformation("Tenant resolved: {TenantId} ({TenantName})", tenantContext.TenantId, tenantContext.TenantName);
            }
            else
            {
                // Fallback: use the provided tenantId even if not in the store (dev/test scenario)
                _logger.LogInformation("Tenant not in store, using provided ID: {TenantId}", tenantId);
                tenantContext.TenantId = tenantId;
                tenantContext.TenantName = tenantId;
                tenantContext.IsAuthenticated = true;
            }
        }
        else if (hasAuthorize && !allowAnonymous && context.User?.Identity?.IsAuthenticated == true)
        {
            _logger.LogWarning("Authenticated request without tenant context.");
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { error = "Tenant identification required." });
            return;
        }

        await _next(context);
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

        // 2. JWT claim
        var claimValue = context.User?.FindFirst(TenantIdClaimType)?.Value;
        if (!string.IsNullOrWhiteSpace(claimValue))
            return claimValue;

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
