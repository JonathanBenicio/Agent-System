using Microsoft.AspNetCore.SignalR;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Api.SignalR;

/// <summary>
/// Filtro global do SignalR que intercepta todas as invocações de métodos e conexões,
/// estabelecendo automaticamente o escopo multi-tenant na thread atual via ITenantContextAccessor.
/// </summary>
public sealed class TenantHubFilter : IHubFilter
{
    private readonly ITenantResolver _tenantResolver;
    private readonly ITenantContextAccessor _tenantContextAccessor;
    private readonly ILogger<TenantHubFilter> _logger;

    public TenantHubFilter(
        ITenantResolver tenantResolver,
        ITenantContextAccessor tenantContextAccessor,
        ILogger<TenantHubFilter> logger)
    {
        _tenantResolver = tenantResolver;
        _tenantContextAccessor = tenantContextAccessor;
        _logger = logger;
    }

    public async Task<object?> InvokeMethodAsync(
        HubInvocationContext invocationContext,
        Func<HubInvocationContext, Task<object?>> next)
    {
        var tenantContext = await ResolveTenantContextAsync(invocationContext.Context.GetHttpContext());
        using var scope = _tenantContextAccessor.BeginScope(tenantContext);
        return await next(invocationContext);
    }

    public async Task OnConnectedAsync(
        HubConnectionContext connectionContext,
        Func<HubConnectionContext, Task> next)
    {
        var tenantContext = await ResolveTenantContextAsync(connectionContext.GetHttpContext());
        using var scope = _tenantContextAccessor.BeginScope(tenantContext);
        await next(connectionContext);
    }

    public async Task OnDisconnectedAsync(
        HubConnectionContext connectionContext,
        Exception? exception,
        Func<HubConnectionContext, Exception?, Task> next)
    {
        var tenantContext = await ResolveTenantContextAsync(connectionContext.GetHttpContext());
        using var scope = _tenantContextAccessor.BeginScope(tenantContext);
        await next(connectionContext, exception);
    }

    private async Task<TenantContext> ResolveTenantContextAsync(Microsoft.AspNetCore.Http.HttpContext? httpContext)
    {
        if (httpContext is null)
        {
            return new TenantContext();
        }

        var tenantId = ResolveTenantId(httpContext);
        var tenantContext = new TenantContext();

        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            var resolved = await _tenantResolver.ResolveAsync(tenantId);
            if (resolved is not null)
            {
                tenantContext.TenantId = resolved.TenantId;
                tenantContext.TenantName = resolved.TenantName;
                tenantContext.Plan = resolved.Plan;
                tenantContext.Limits = resolved.Limits;
                tenantContext.IsAuthenticated = resolved.IsAuthenticated;

                _logger.LogDebug("Tenant resolved in Hub pipeline: {TenantId} ({TenantName})", tenantContext.TenantId, tenantContext.TenantName);
            }
            else
            {
                // Fallback para cenários dev/test
                _logger.LogDebug("Tenant resolved in Hub pipeline (not in store, using provided ID): {TenantId}", tenantId);
                tenantContext.TenantId = tenantId;
                tenantContext.TenantName = tenantId;
                tenantContext.IsAuthenticated = true;
            }
        }

        return tenantContext;
    }

    private static string? ResolveTenantId(Microsoft.AspNetCore.Http.HttpContext httpContext)
    {
        // 1. Header X-Tenant-Id (prioridade máxima)
        if (httpContext.Request.Headers.TryGetValue("X-Tenant-Id", out var headerValue))
        {
            var val = headerValue.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(val))
                return val;
        }

        // Query string fallback (SignalR WebSockets às vezes usam query params se headers não forem suportados)
        if (httpContext.Request.Query.TryGetValue("X-Tenant-Id", out var queryValue))
        {
            var val = queryValue.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(val))
                return val;
        }

        // 2. JWT claim (Standard claim ou Supabase claim)
        var claimValue = httpContext.User?.FindFirst("tenant_id")?.Value;
        if (!string.IsNullOrWhiteSpace(claimValue))
            return claimValue;

        // 3. Supabase Metadata (app_metadata.tenant_id)
        var metadataClaim = httpContext.User?.FindFirst("app_metadata")?.Value;
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
