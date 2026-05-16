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
        
        var stats = await _vectorStore.GetStatsAsync(tenantId, ct);

        // Define generic limits if none exist on TenantResourceLimits right now
        long maxDocs = limits.MaxDocuments > 0 ? limits.MaxDocuments : 50000;
        long maxStorage = limits.MaxStorageMb > 0 ? limits.MaxStorageMb * 1024L * 1024L : 1024L * 1024 * 1024; // 1 GB default

        if (stats.DocumentCount + newDocumentSizeCount > maxDocs)
        {
            _logger.LogWarning("🚫 Tenant {TenantId} reached document limit ({Current} + {New} > {Limit})", 
                tenantId, stats.DocumentCount, newDocumentSizeCount, maxDocs);
            return false;
        }

        if (stats.TotalBytes + newBytesCount > maxStorage)
        {
             _logger.LogWarning("🚫 Tenant {TenantId} reached storage limit ({Current} + {New} > {Limit})", 
                tenantId, stats.TotalBytes, newBytesCount, maxStorage);
            return false;
        }

        return true;
    }

    public async Task<TenantUsageSummary> GetUsageAsync(string tenantId, CancellationToken ct = default)
    {
        var sessions = await _sessionStore.GetByTenantAsync(tenantId, ct: ct);
        var activeCount = sessions.Count(s => !s.EndedAt.HasValue);
        
        var stats = await _vectorStore.GetStatsAsync(tenantId, ct);

        return new TenantUsageSummary
        {
            TenantId = tenantId,
            ActiveSessions = activeCount,
            TotalDocuments = (int)stats.DocumentCount,
            StorageUsageBytes = stats.TotalBytes,
            Limits = new TenantResourceLimits()
        };
    }
}
