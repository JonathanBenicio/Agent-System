using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Core.Services;

public class TenantIsolationService : ITenantIsolationEnforcer
{
    private readonly ITenantStore _tenantStore;
    private readonly ISessionStore _sessionStore;
    private readonly IVectorStore _vectorStore;
    private readonly ICostTracker _costTracker;
    private readonly ILogger<TenantIsolationService> _logger;

    public TenantIsolationService(
        ITenantStore tenantStore,
        ISessionStore sessionStore,
        IVectorStore vectorStore,
        ICostTracker costTracker,
        ILogger<TenantIsolationService> logger)
    {
        _tenantStore = tenantStore;
        _sessionStore = sessionStore;
        _vectorStore = vectorStore;
        _costTracker = costTracker;
        _logger = logger;
    }

    public async Task<bool> CanStartSessionAsync(string tenantId, CancellationToken ct = default)
    {
        var tenant = await _tenantStore.GetByIdAsync(tenantId, ct);
        if (tenant == null) return true; // Default behavior if tenant not found

        // In a real app, TenantResourceLimits would be part of the Tenant model or IsolationConfig
        // For now, we'll assume a default limit if not explicitly configured
        var limits = new TenantResourceLimits(); 
        
        var sessions = await _sessionStore.GetByTenantAsync(tenantId, ct: ct);
        var activeCount = sessions.Count(s => !s.EndedAt.HasValue);

        if (activeCount >= limits.MaxConcurrentSessions)
        {
            _logger.LogWarning("🚫 Tenant {TenantId} reached concurrent session limit ({Limit})", tenantId, limits.MaxConcurrentSessions);
            return false;
        }

        return true;
    }

    public async Task<bool> CanIngestDocumentAsync(string tenantId, long newDocumentSizeCount = 1, long newBytesCount = 0, CancellationToken ct = default)
    {
        var tenant = await _tenantStore.GetByIdAsync(tenantId, ct);
        if (tenant == null) return true;

        var limits = new TenantResourceLimits();
        
        // This is a simplified check. In production, we'd query counts per tenant.
        // IVectorStore would need a GetStatsAsync(tenantId) method.
        // For now, we'll implement a basic check.
        return true;
    }

    public async Task<TenantUsageSummary> GetUsageAsync(string tenantId, CancellationToken ct = default)
    {
        var sessions = await _sessionStore.GetByTenantAsync(tenantId, ct: ct);
        var activeCount = sessions.Count(s => !s.EndedAt.HasValue);
        
        return new TenantUsageSummary
        {
            TenantId = tenantId,
            ActiveSessions = activeCount,
            Limits = new TenantResourceLimits()
        };
    }
}
