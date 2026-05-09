using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Core.Services;

public class AdminConsoleService : IAdminConsole
{
    private readonly IMetaAgent _metaAgent;
    private readonly ISessionManager _sessionManager;
    private readonly ICostTracker _costTracker;
    private readonly IAuditLog _auditLog;
    private readonly ILogger<AdminConsoleService> _logger;
    private readonly List<AdminAlert> _alerts = new();

    public AdminConsoleService(
        IMetaAgent metaAgent,
        ISessionManager sessionManager,
        ICostTracker costTracker,
        IAuditLog auditLog,
        ILogger<AdminConsoleService> logger)
    {
        _metaAgent = metaAgent;
        _sessionManager = sessionManager;
        _costTracker = costTracker;
        _auditLog = auditLog;
        _logger = logger;
    }

    public async Task<AdminDashboard> GetDashboardAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("📊 Gathering admin dashboard metrics.");

        var activeAgents = await _metaAgent.GetActiveAgentsAsync();
        var costs = _costTracker.GetReport(TimeSpan.FromDays(1));
        
        // Mocking some values for the dashboard based on available data
        var dashboard = new AdminDashboard
        {
            TotalAgents = activeAgents.Count(),
            ActiveSessions = 5, // Should come from session store/manager
            PendingApprovals = 0, // Should come from ToolGovernance
            DeadLetterCount = 0,
            TotalCostToday = (double)costs.TotalCost,
            RequestsToday = 0,
            ActiveAlerts = _alerts.ToList(),
            SystemHealth = new Dictionary<string, DependencyHealth>
            {
                ["Core API"] = DependencyHealth.Healthy,
                ["PostgreSQL"] = DependencyHealth.Healthy,
                ["Vector DB"] = DependencyHealth.Healthy,
                ["LLM Gateway"] = DependencyHealth.Healthy
            }
        };

        return dashboard;
    }

    public Task BroadcastAlertAsync(AdminAlert alert, CancellationToken ct = default)
    {
        _logger.LogWarning("🔔 Admin Alert: {Message} (Severity: {Severity})", alert.Message, alert.Severity);
        lock (_alerts)
        {
            _alerts.Insert(0, alert);
            if (_alerts.Count > 50) _alerts.RemoveAt(_alerts.Count - 1);
        }
        return Task.CompletedTask;
    }
}
