using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Infrastructure.Gateway;

/// <summary>
/// Gateway unificado para serviços externos.
/// Integra CircuitBreaker, RateLimiter, CostTracker e HealthMonitor.
/// </summary>
public class ServiceGateway : IServiceGateway
{
    private readonly ConcurrentDictionary<string, ServiceEntry> _services = new();
    private readonly CostTracker _costTracker;
    private readonly ILogger<ServiceGateway> _logger;
    private readonly DateTime _startTime = DateTime.UtcNow;
    private long _totalRequests;
    private long _totalFailures;

    public ServiceGateway(CostTracker costTracker, ILogger<ServiceGateway> logger)
    {
        _costTracker = costTracker;
        _logger = logger;
    }

    public void RegisterService(ServiceRegistration registration)
    {
        var entry = new ServiceEntry
        {
            Registration = registration,
            CircuitBreaker = new CircuitBreaker(registration.CircuitBreaker),
            RateLimiter = new RateLimiter(registration.RateLimits),
            IsEnabled = true
        };

        _services[registration.Name] = entry;
        _costTracker.SetBudget(registration.Name, registration.DailyBudget);
        _logger.LogInformation("🔌 Service registered: {Service} [{Category}]", registration.Name, registration.Category);
    }

    public async Task<GatewayResponse<T>> ExecuteAsync<T>(string serviceName, Func<CancellationToken, Task<T>> action, CancellationToken ct = default)
    {
        if (!_services.TryGetValue(serviceName, out var entry))
            return GatewayResponse<T>.Fail($"Service '{serviceName}' not registered", serviceName);

        if (!entry.IsEnabled)
            return GatewayResponse<T>.Fail($"Service '{serviceName}' is disabled", serviceName);

        if (!entry.CircuitBreaker.AllowRequest())
            return GatewayResponse<T>.Fail($"Circuit breaker OPEN for '{serviceName}'", serviceName);

        if (!entry.RateLimiter.AllowRequest())
            return GatewayResponse<T>.Fail($"Rate limit exceeded for '{serviceName}'", serviceName);

        var sw = Stopwatch.StartNew();
        Interlocked.Increment(ref _totalRequests);
        entry.RequestCount++;

        try
        {
            var result = await action(ct);
            sw.Stop();

            entry.CircuitBreaker.RecordSuccess();
            entry.RateLimiter.RecordRequest();
            entry.RecordLatency(sw.Elapsed);
            entry.IsHealthy = true;
            entry.LastChecked = DateTime.UtcNow;

            // Estimate cost (pode ser sobrescrito por serviço)
            var cost = 0.001m; // Base cost per request
            _costTracker.RecordCost(serviceName, entry.Registration.Category, cost);

            return GatewayResponse<T>.Ok(result, serviceName, sw.Elapsed, cost);
        }
        catch (Exception ex)
        {
            sw.Stop();
            Interlocked.Increment(ref _totalFailures);
            entry.FailureCount++;
            entry.CircuitBreaker.RecordFailure();
            entry.IsHealthy = false;
            entry.LastError = ex.Message;
            entry.LastChecked = DateTime.UtcNow;

            _logger.LogWarning(ex, "⚠️ Gateway request failed for '{Service}': {Error}", serviceName, ex.Message);
            return GatewayResponse<T>.Fail(ex.Message, serviceName);
        }
    }

    public Task<ServiceStatus> GetServiceStatusAsync(string serviceName)
    {
        if (!_services.TryGetValue(serviceName, out var entry))
            throw new KeyNotFoundException($"Service '{serviceName}' not found");

        return Task.FromResult(entry.ToServiceStatus());
    }

    public Task<IEnumerable<ServiceStatus>> GetAllServicesStatusAsync()
    {
        var statuses = _services.Values.Select(e => e.ToServiceStatus());
        return Task.FromResult(statuses);
    }

    public Task<IEnumerable<ServiceStatus>> GetServicesByCategoryAsync(string category)
    {
        var statuses = _services.Values
            .Where(e => e.Registration.Category.Equals(category, StringComparison.OrdinalIgnoreCase))
            .Select(e => e.ToServiceStatus());
        return Task.FromResult(statuses);
    }

    public Task EnableServiceAsync(string serviceName)
    {
        if (_services.TryGetValue(serviceName, out var entry))
        {
            entry.IsEnabled = true;
            entry.CircuitBreaker.Reset();
        }
        return Task.CompletedTask;
    }

    public Task DisableServiceAsync(string serviceName)
    {
        if (_services.TryGetValue(serviceName, out var entry))
            entry.IsEnabled = false;
        return Task.CompletedTask;
    }

    public Task<CostReport> GetCostReportAsync(TimeSpan? range = null)
        => Task.FromResult(_costTracker.GetReport(range));

    public Task<HealthReport> GetHealthReportAsync()
    {
        var entries = _services.Values.ToList();
        var report = new HealthReport
        {
            TotalServices = entries.Count,
            HealthyServices = entries.Count(e => e.IsHealthy),
            UnhealthyServices = entries.Count(e => !e.IsHealthy),
            OverallHealthy = entries.All(e => e.IsHealthy || !e.IsEnabled),
            Services = entries.Select(e => new ServiceHealthEntry
            {
                Name = e.Registration.Name,
                IsHealthy = e.IsHealthy,
                LastError = e.LastError,
                LastCheck = e.LastChecked,
                ConsecutiveFailures = e.FailureCount
            }).ToList()
        };
        return Task.FromResult(report);
    }

    public Task<GatewayDashboard> GetDashboardAsync()
    {
        var totalReqs = Interlocked.Read(ref _totalRequests);
        var totalFails = Interlocked.Read(ref _totalFailures);

        var dashboard = new GatewayDashboard
        {
            Health = GetHealthReportAsync().Result,
            Costs = _costTracker.GetReport(),
            Services = _services.Values.Select(e => e.ToServiceStatus()).ToList(),
            Metrics = new GatewayMetrics
            {
                TotalRequests = totalReqs,
                TotalFailures = totalFails,
                OverallSuccessRate = totalReqs > 0 ? (double)(totalReqs - totalFails) / totalReqs * 100 : 100,
                AverageLatency = CalculateAverageLatency(),
                Uptime = DateTime.UtcNow - _startTime
            }
        };

        return Task.FromResult(dashboard);
    }

    private TimeSpan CalculateAverageLatency()
    {
        var allLatencies = _services.Values
            .SelectMany(e => e.RecentLatencies)
            .ToList();

        return allLatencies.Count > 0
            ? TimeSpan.FromMilliseconds(allLatencies.Average(l => l.TotalMilliseconds))
            : TimeSpan.Zero;
    }

    private class ServiceEntry
    {
        public ServiceRegistration Registration { get; set; } = new();
        public CircuitBreaker CircuitBreaker { get; set; } = null!;
        public RateLimiter RateLimiter { get; set; } = null!;
        public bool IsEnabled { get; set; }
        public bool IsHealthy { get; set; } = true;
        public int RequestCount { get; set; }
        public int FailureCount { get; set; }
        public string? LastError { get; set; }
        public DateTime LastChecked { get; set; } = DateTime.UtcNow;
        public List<TimeSpan> RecentLatencies { get; } = new();

        public void RecordLatency(TimeSpan latency)
        {
            RecentLatencies.Add(latency);
            if (RecentLatencies.Count > 100)
                RecentLatencies.RemoveAt(0);
        }

        public ServiceStatus ToServiceStatus() => new()
        {
            Name = Registration.Name,
            Category = Registration.Category,
            IsEnabled = IsEnabled,
            IsHealthy = IsHealthy,
            CircuitState = CircuitBreaker.State,
            RequestCount = RequestCount,
            FailureCount = FailureCount,
            SuccessRate = RequestCount > 0 ? (double)(RequestCount - FailureCount) / RequestCount * 100 : 100,
            AverageLatency = RecentLatencies.Count > 0
                ? TimeSpan.FromMilliseconds(RecentLatencies.Average(l => l.TotalMilliseconds))
                : TimeSpan.Zero,
            TotalCost = 0, // Filled from CostTracker
            LastChecked = LastChecked
        };
    }
}
