using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Interfaces;

/// <summary>
/// Gateway unificado para serviços externos com proteção e telemetria.
/// Circuit Breaker, Rate Limiter, Cost Tracker, Health Monitor.
/// </summary>
public interface IServiceGateway
{
    Task<GatewayResponse<T>> ExecuteAsync<T>(string serviceName, Func<CancellationToken, Task<T>> action, CancellationToken ct = default);
    /// <summary>Executes a provider stream while tracking its full enumeration, failures, and cancellation.</summary>
    IAsyncEnumerable<T> ExecuteStreamingAsync<T>(string serviceName, Func<CancellationToken, IAsyncEnumerable<T>> action, CancellationToken ct = default);
    Task<ServiceStatus> GetServiceStatusAsync(string serviceName);
    Task<IEnumerable<ServiceStatus>> GetAllServicesStatusAsync();
    Task<IEnumerable<ServiceStatus>> GetServicesByCategoryAsync(string category);
    Task EnableServiceAsync(string serviceName);
    Task DisableServiceAsync(string serviceName);
    Task<CostReport> GetCostReportAsync(TimeSpan? range = null);
    Task<HealthReport> GetHealthReportAsync();
    Task<GatewayDashboard> GetDashboardAsync();
    void RegisterService(ServiceRegistration registration);
    void UnregisterService(string serviceName);
}
