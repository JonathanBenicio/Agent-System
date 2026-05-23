using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using AgenticSystem.Core.Interfaces;

namespace AgenticSystem.Core.Services;

/// <summary>
/// ML15 — Background service que consolida automaticamente sessões encerradas.
/// </summary>
public class SessionAutoConsolidator : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SessionAutoConsolidator> _logger;
    private readonly TimeSpan _interval;
    private readonly ISemanticCompressor? _semanticCompressor;
    private readonly ITenantStore? _tenantStore;
    private readonly ITenantContextAccessor _tenantContextAccessor;

    public SessionAutoConsolidator(
        IServiceProvider serviceProvider,
        ILogger<SessionAutoConsolidator> logger,
        ITenantContextAccessor tenantContextAccessor,
        IOptions<SessionConsolidationOptions>? options = null,
        ISemanticCompressor? semanticCompressor = null,
        ITenantStore? tenantStore = null)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _tenantContextAccessor = tenantContextAccessor;
        _interval = options?.Value?.ConsolidationInterval ?? TimeSpan.FromMinutes(5);
        _semanticCompressor = semanticCompressor;
        _tenantStore = tenantStore;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("🔄 SessionAutoConsolidator started (interval: {Interval})", _interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingSessionsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SessionAutoConsolidator cycle");
            }

            await Task.Delay(_interval, stoppingToken);
        }

        _logger.LogInformation("🛑 SessionAutoConsolidator stopped");
    }

    private async Task ProcessPendingSessionsAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var sessionStore = scope.ServiceProvider.GetRequiredService<ISessionStore>();
        var consolidator = scope.ServiceProvider.GetRequiredService<ISessionConsolidator>();
        var memoryInjection = scope.ServiceProvider.GetService<IMemoryInjectionService>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<SessionAutoConsolidator>>();

        var tenants = new List<string> { "admin" };
        if (_tenantStore != null)
        {
            try
            {
                var allTenants = await _tenantStore.GetAllAsync(ct);
                if (allTenants != null && allTenants.Count > 0)
                {
                    tenants = allTenants.Select(t => t.Id).ToList();
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to load tenants from TenantStore, falling back to admin tenant.");
            }
        }

        foreach (var tenantId in tenants)
        {
            using var tenantScope = _tenantContextAccessor.BeginScope(new Core.Models.TenantContext { TenantId = tenantId });

            var sessions = await sessionStore.GetByTenantAsync(tenantId, maxResults: 50, ct: ct);
            var pending = sessions.Where(s => s.EndedAt.HasValue && !s.IsConsolidated).ToList();

            if (pending.Count == 0) continue;

            logger.LogInformation("📋 Found {Count} pending sessions for tenant {TenantId}", pending.Count, tenantId);

            foreach (var session in pending)
            {
                try
                {
                    logger.LogInformation("🔒 Consolidating session {SessionId} (user: {UserId})", session.Id, session.UserId);

                    var summary = await consolidator.SummarizeSessionAsync(session.Id, session.Events, session.UserId, session.TenantId);
                    var insights = await consolidator.ExtractInsightsAsync(session.Id, session.Events, session.UserId, session.TenantId);

                    if (memoryInjection != null)
                    {
                        await memoryInjection.VectorizeInsightsAsync(insights, session.UserId, session.TenantId, session.Id, ct);
                    }

                    session.IsConsolidated = true;
                    session.Summary = summary;
                    session.Insights = insights;

                    await sessionStore.SaveAsync(session, ct);

                    logger.LogInformation("✅ Session {SessionId} consolidated successfully", session.Id);

                    if (_semanticCompressor != null)
                    {
                        try
                        {
                            await _semanticCompressor.CompressSessionAsync(session.Id);
                            logger.LogInformation("🗜️ Session {SessionId} semantically compressed in background", session.Id);
                        }
                        catch (Exception ex)
                        {
                            logger.LogWarning(ex, "Semantic compression failed for session {SessionId} in background", session.Id);
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "❌ Failed to consolidate session {SessionId}", session.Id);
                }
            }
        }
    }
}

public class SessionConsolidationOptions
{
    public TimeSpan ConsolidationInterval { get; set; } = TimeSpan.FromMinutes(5);
}
