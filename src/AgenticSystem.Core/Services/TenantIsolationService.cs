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
    private readonly IDynamicAgentRepository _dynamicAgentRepository;
    private readonly ILogger<TenantIsolationService> _logger;

    public TenantIsolationService(
        ITenantStore tenantStore,
        ISessionStore sessionStore,
        IVectorStore vectorStore,
        ICostTracker costTracker,
        IDynamicAgentRepository dynamicAgentRepository,
        ILogger<TenantIsolationService> logger)
    {
        _tenantStore = tenantStore;
        _sessionStore = sessionStore;
        _vectorStore = vectorStore;
        _costTracker = costTracker;
        _dynamicAgentRepository = dynamicAgentRepository;
        _logger = logger;
    }

    public async Task<bool> CanStartSessionAsync(string tenantId, CancellationToken ct = default)
    {
        var tenant = await _tenantStore.GetByIdAsync(tenantId, ct);
        if (tenant == null || !tenant.IsActive) return false;

        var limit = tenant.Limits.MaxConcurrentSessions;
        if (limit <= 0) return true;

        var activeCount = await _sessionStore.CountActiveAsync(tenantId, ct);

        if (activeCount >= limit)
        {
            _logger.LogWarning("Tenant {TenantId} reached concurrent session limit ({Limit})", tenantId, limit);
            return false;
        }

        return true;
    }

    public async Task<bool> CanCreateAgentAsync(string tenantId, string agentName, CancellationToken ct = default)
    {
        var tenant = await _tenantStore.GetByIdAsync(tenantId, ct);
        if (tenant is null || !tenant.IsActive) return false;

        if (await _dynamicAgentRepository.GetByNameAsync(agentName, ct) is not null)
            return true;

        var limit = tenant.Limits.MaxAgents;
        if (limit <= 0) return true;

        var agents = await _dynamicAgentRepository.GetAllAsync(ct);
        var count = agents.Count();
        if (count >= limit)
        {
            _logger.LogWarning("Tenant {TenantId} reached agent limit ({Limit})", tenantId, limit);
            return false;
        }

        return true;
    }

    public async Task<bool> CanIngestDocumentAsync(string tenantId, long newDocumentSizeCount = 1, long newBytesCount = 0, CancellationToken ct = default)
    {
        var tenant = await _tenantStore.GetByIdAsync(tenantId, ct);
        if (tenant == null || !tenant.IsActive) return false;

        var stats = await _vectorStore.GetStatsAsync(tenantId, ct);

        var maxDocs = tenant.Limits.MaxDocuments;
        var maxStorage = (long)tenant.Limits.MaxDocumentsMb * 1024L * 1024L;

        if (maxDocs > 0 && stats.DocumentCount + newDocumentSizeCount > maxDocs)
        {
            _logger.LogWarning("Tenant {TenantId} reached document limit ({Current} + {New} > {Limit})",
                tenantId, stats.DocumentCount, newDocumentSizeCount, maxDocs);
            return false;
        }

        if (maxStorage > 0 && stats.TotalBytes + newBytesCount > maxStorage)
        {
            _logger.LogWarning("Tenant {TenantId} reached storage limit ({Current} + {New} > {Limit})",
                tenantId, stats.TotalBytes, newBytesCount, maxStorage);
            return false;
        }

        return true;
    }

    public async Task<TenantUsageSummary> GetUsageAsync(string tenantId, CancellationToken ct = default)
    {
        var tenant = await _tenantStore.GetByIdAsync(tenantId, ct)
            ?? throw new KeyNotFoundException($"Tenant '{tenantId}' was not found.");
        var activeCount = await _sessionStore.CountActiveAsync(tenantId, ct);
        var agents = await _dynamicAgentRepository.GetAllAsync(ct);
        var stats = await _vectorStore.GetStatsAsync(tenantId, ct);

        return new TenantUsageSummary
        {
            TenantId = tenantId,
            ActiveSessions = activeCount,
            ActiveAgents = agents.Count(),
            TotalDocuments = (int)stats.DocumentCount,
            StorageUsageBytes = stats.TotalBytes,
            Limits = TenantResourceLimits.From(tenant.Limits)
        };
    }
}
