namespace AgenticSystem.Core.Models;

// ═══════════════════════════════════════════════════════════
// Gateway Models
// ═══════════════════════════════════════════════════════════

public class GatewayResponse<T>
{
    public T? Data { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public TimeSpan Latency { get; set; }
    public decimal EstimatedCost { get; set; }

    public static GatewayResponse<T> Ok(T data, string service, TimeSpan latency, decimal cost = 0)
        => new() { Data = data, Success = true, ServiceName = service, Latency = latency, EstimatedCost = cost };

    public static GatewayResponse<T> Fail(string error, string service)
        => new() { Success = false, ErrorMessage = error, ServiceName = service };
}

public class ServiceRegistration
{
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public CircuitBreakerConfig CircuitBreaker { get; set; } = new();
    public RateLimitConfig RateLimits { get; set; } = new();
    public decimal DailyBudget { get; set; } = 10.00m;
}

public class CircuitBreakerConfig
{
    public int FailureThreshold { get; set; } = 5;
    public TimeSpan SamplingDuration { get; set; } = TimeSpan.FromMinutes(1);
    public TimeSpan BreakDuration { get; set; } = TimeSpan.FromSeconds(30);
    public int MinimumThroughput { get; set; } = 10;
}

public class RateLimitConfig
{
    public int RequestsPerMinute { get; set; } = 60;
    public int RequestsPerHour { get; set; } = 1000;
    public int TokensPerDay { get; set; } = 100000;
}

public class ServiceStatus
{
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public bool IsHealthy { get; set; }
    public CircuitState CircuitState { get; set; } = CircuitState.Closed;
    public int RequestCount { get; set; }
    public int FailureCount { get; set; }
    public double SuccessRate { get; set; } = 100;
    public TimeSpan AverageLatency { get; set; }
    public decimal TotalCost { get; set; }
    public DateTime LastChecked { get; set; } = DateTime.UtcNow;
}

public enum CircuitState
{
    Closed,     // Normal operation
    Open,       // Blocking requests
    HalfOpen    // Testing recovery
}

public class CostReport
{
    public decimal TotalCost { get; set; }
    public decimal DailyBudget { get; set; }
    public double UsagePercent { get; set; }
    public Dictionary<string, decimal> CostByService { get; set; } = new();
    public Dictionary<string, decimal> CostByCategory { get; set; } = new();
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public bool BudgetAlert { get; set; }
}

public class HealthReport
{
    public bool OverallHealthy { get; set; }
    public int TotalServices { get; set; }
    public int HealthyServices { get; set; }
    public int UnhealthyServices { get; set; }
    public List<ServiceHealthEntry> Services { get; set; } = new();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class ServiceHealthEntry
{
    public string Name { get; set; } = string.Empty;
    public bool IsHealthy { get; set; }
    public string? LastError { get; set; }
    public DateTime LastCheck { get; set; }
    public int ConsecutiveFailures { get; set; }
}

public class GatewayDashboard
{
    public HealthReport Health { get; set; } = new();
    public CostReport Costs { get; set; } = new();
    public List<ServiceStatus> Services { get; set; } = new();
    public GatewayMetrics Metrics { get; set; } = new();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class GatewayMetrics
{
    public long TotalRequests { get; set; }
    public long TotalFailures { get; set; }
    public double OverallSuccessRate { get; set; }
    public TimeSpan AverageLatency { get; set; }
    public TimeSpan Uptime { get; set; }
}
