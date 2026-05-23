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

    public SessionAutoConsolidator(
        IServiceProvider serviceProvider,
        ILogger<SessionAutoConsolidator> logger,
        IOptions<SessionConsolidationOptions>? options = null)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _interval = options?.Value?.ConsolidationInterval ?? TimeSpan.FromMinutes(5);
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
        var tenantContextAccessor = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
        var semanticCompressor = scope.ServiceProvider.GetService<ISemanticCompressor>();
        var tenantStore = scope.ServiceProvider.GetService<ITenantStore>();

        var tenants = new List<string> { "admin" };
        if (tenantStore != null)
        {
            try
            {
                var allTenants = await tenantStore.GetAllAsync(ct);
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
            using var tenantScope = tenantContextAccessor.BeginScope(new Core.Models.TenantContext { TenantId = tenantId });

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

                    if (semanticCompressor != null)
                    {
                        try
                        {
                            await semanticCompressor.CompressSessionAsync(session.Id);
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
