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
    private readonly IPermissionService _permissionService;
    private readonly ILogger<TenantHubFilter> _logger;

    public TenantHubFilter(
        ITenantResolver tenantResolver,
        ITenantContextAccessor tenantContextAccessor,
        IPermissionService permissionService,
        ILogger<TenantHubFilter> logger)
    {
        _tenantResolver = tenantResolver;
        _tenantContextAccessor = tenantContextAccessor;
        _permissionService = permissionService;
        _logger = logger;
    }

    public async Task<object?> InvokeMethodAsync(
        HubInvocationContext invocationContext,
        Func<HubInvocationContext, Task<object?>> next)
    {
        var tenantContext = await ResolveTenantContextAsync(invocationContext.Context.GetHttpContext());
        if (tenantContext is null)
        {
            _logger.LogWarning("Hub invocation rejected: No active tenant context. Method: {MethodName}", invocationContext.HubMethodName);
            throw new HubException("Strict Multi-Tenancy Violation: No active Tenant Context resolved for this hub invocation.");
        }

        using var scope = _tenantContextAccessor.BeginScope(tenantContext);
        return await next(invocationContext);
    }

    public async Task OnConnectedAsync(
        HubConnectionContext connectionContext,
        Func<HubConnectionContext, Task> next)
    {
        var tenantContext = await ResolveTenantContextAsync(connectionContext.GetHttpContext());
        if (tenantContext is null)
        {
            _logger.LogWarning("WebSocket connection attempt rejected: No valid tenant context resolved. Connection ID: {ConnectionId}", connectionContext.ConnectionId);
            connectionContext.Abort();
            throw new HubException("Strict Multi-Tenancy Violation: A valid Tenant Context is required to connect to this Hub.");
        }

        using var scope = _tenantContextAccessor.BeginScope(tenantContext);
        await next(connectionContext);
    }

    public async Task OnDisconnectedAsync(
        HubConnectionContext connectionContext,
        Exception? exception,
        Func<HubConnectionContext, Exception?, Task> next)
    {
        var tenantContext = await ResolveTenantContextAsync(connectionContext.GetHttpContext());
        if (tenantContext is null)
        {
            _logger.LogWarning("OnDisconnectedAsync called without resolved tenant context. Connection ID: {ConnectionId}", connectionContext.ConnectionId);
            await next(connectionContext, exception);
            return;
        }

        using var scope = _tenantContextAccessor.BeginScope(tenantContext);
        await next(connectionContext, exception);
    }

    private async Task<TenantContext?> ResolveTenantContextAsync(Microsoft.AspNetCore.Http.HttpContext? httpContext)
    {
        if (httpContext is null)
        {
            return null;
        }

        var claimTenantId = ResolveClaimTenantId(httpContext);
        var requestedTenantId = ResolveExplicitTenantId(httpContext);
        var tenantId = requestedTenantId ?? claimTenantId;

        _logger.LogInformation(
            "Hub tenant validation: path={Path}, requested={RequestedTenantId}, claim={ClaimTenantId}, authenticated={Authenticated}",
            httpContext.Request.Path,
            requestedTenantId,
            claimTenantId,
            httpContext.User?.Identity?.IsAuthenticated == true);

        if (httpContext.User?.Identity?.IsAuthenticated == true &&
            (string.IsNullOrWhiteSpace(claimTenantId) ||
             requestedTenantId is not null &&
             !string.Equals(requestedTenantId.Trim(), claimTenantId.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            _logger.LogWarning("Hub tenant mismatch rejected. Claim tenant '{ClaimTenantId}', requested tenant '{RequestedTenantId}'.", claimTenantId, tenantId);
            return null;
        }

        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            var userId = httpContext.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                ?? httpContext.User?.FindFirst("sub")?.Value;
            if (httpContext.User?.Identity?.IsAuthenticated == true)
            {
                if (string.IsNullOrWhiteSpace(userId))
                    return null;

                var roles = await _permissionService.GetRolesAsync(userId);
                if (!roles.Any(role => string.Equals(role.TenantId, tenantId, StringComparison.OrdinalIgnoreCase)))
                {
                    _logger.LogWarning("Hub access rejected: user {UserId} has no membership in tenant {TenantId}.", userId, tenantId);
                    return null;
                }
            }

            var resolved = await _tenantResolver.ResolveAsync(tenantId);
            if (resolved is not null)
            {
                var tenantContext = new TenantContext
                {
                    TenantId = resolved.TenantId,
                    TenantName = resolved.TenantName,
                    Plan = resolved.Plan,
                    Limits = resolved.Limits,
                    IsAuthenticated = resolved.IsAuthenticated
                };
                _logger.LogDebug("Tenant resolved in Hub pipeline: {TenantId} ({TenantName})", tenantContext.TenantId, tenantContext.TenantName);
                return tenantContext;
            }
        }

        return null;
    }

    private static string? ResolveExplicitTenantId(Microsoft.AspNetCore.Http.HttpContext httpContext)
    {
        // 1. Header X-Tenant-Id (prioridade máxima)
        if (httpContext.Request.Headers.TryGetValue("X-Tenant-Id", out var headerValue))
        {
            var val = headerValue.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(val))
                return val;
        }

        // Query string fallback (SignalR WebSockets às vezes usam query params se headers não forem suportados)
        var queryValue = httpContext.Request.Query
            .FirstOrDefault(item => string.Equals(item.Key, "X-Tenant-Id", StringComparison.OrdinalIgnoreCase)).Value;
        if (!Microsoft.Extensions.Primitives.StringValues.IsNullOrEmpty(queryValue))
        {
            var val = queryValue.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(val))
                return val;
        }

        // 2. JWT claim (Standard claim ou Supabase claim)
        return null;
    }

    private static string? ResolveClaimTenantId(Microsoft.AspNetCore.Http.HttpContext httpContext)
    {
        var claimValue = httpContext.User?.FindFirst("tenant_id")?.Value;
        if (!string.IsNullOrWhiteSpace(claimValue))
            return claimValue;

        var metadataClaim = httpContext.User?.FindFirst("app_metadata")?.Value;
        if (!string.IsNullOrWhiteSpace(metadataClaim))
        {
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(metadataClaim);
                if (doc.RootElement.TryGetProperty("tenant_id", out var tenantId))
                    return tenantId.GetString();
            }
            catch (System.Text.Json.JsonException)
            {
                return null;
            }
        }
        return null;
    }
}
