using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using AgenticSystem.Api.Auth;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.Persistence;

namespace AgenticSystem.Api.Hubs;

/// <summary>
/// SignalR Hub para eventos de Gateway em tempo real.
/// Eventos: ServiceStatusChanged, CostAlertTriggered, CircuitStateChanged, RateLimitWarning
/// </summary>
[Authorize]
public class GatewayHub : Hub
{
    private readonly IServiceGateway _gateway;
    private readonly ILogger<GatewayHub> _logger;
    private readonly ITenantContextAccessor _tenantContextAccessor;
    private readonly ISystemOperationContextAccessor _systemOperations;
    private readonly AgenticDbContext _dbContext;

    public GatewayHub(
        IServiceGateway gateway,
        ILogger<GatewayHub> logger,
        ITenantContextAccessor tenantContextAccessor,
        ISystemOperationContextAccessor systemOperations,
        AgenticDbContext dbContext)
    {
        _gateway = gateway;
        _logger = logger;
        _tenantContextAccessor = tenantContextAccessor;
        _systemOperations = systemOperations;
        _dbContext = dbContext;
    }

    public async Task GetDashboard()
    {
        if (!await RequirePlatformAdministratorAsync())
            return;

        using var systemScope = _systemOperations.BeginScope(SystemOperationKind.PlatformGatewayOperation);
        var dashboard = await _gateway.GetDashboardAsync();
        await Clients.Caller.SendAsync("DashboardUpdate", dashboard);
    }

    public async Task GetServiceStatus(string serviceName)
    {
        if (!await RequirePlatformAdministratorAsync())
            return;

        using var systemScope = _systemOperations.BeginScope(SystemOperationKind.PlatformGatewayOperation);
        try
        {
            var status = await _gateway.GetServiceStatusAsync(serviceName);
            await Clients.Caller.SendAsync("ServiceStatusChanged", status);
        }
        catch (KeyNotFoundException)
        {
            await Clients.Caller.SendAsync("Error", $"Service '{serviceName}' not found");
        }
    }

    public async Task SubscribeToService(string serviceName)
    {
        if (!await RequirePlatformAdministratorAsync())
            return;

        using var systemScope = _systemOperations.BeginScope(SystemOperationKind.PlatformGatewayOperation);
        await Groups.AddToGroupAsync(Context.ConnectionId, $"tenant:{_tenantContextAccessor.CurrentTenantId}:service:{serviceName}");
        _logger.LogDebug("Client {ConnectionId} subscribed to service {Service}",
            Context.ConnectionId, serviceName);
    }

    public async Task UnsubscribeFromService(string serviceName)
    {
        if (!await RequirePlatformAdministratorAsync())
            return;

        using var systemScope = _systemOperations.BeginScope(SystemOperationKind.PlatformGatewayOperation);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"tenant:{_tenantContextAccessor.CurrentTenantId}:service:{serviceName}");
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("🔌 GatewayHub client connected: {ConnectionId}", Context.ConnectionId);
        if (!await PlatformAdminAuthorization.IsPlatformAdministratorAsync(
                _dbContext, _systemOperations, Context.User!, Context.ConnectionAborted))
        {
            await Clients.Caller.SendAsync("Error", "Platform administrator access is required.", Context.ConnectionAborted);
            await base.OnConnectedAsync();
            return;
        }

        using var systemScope = _systemOperations.BeginScope(SystemOperationKind.PlatformGatewayOperation);
        await Groups.AddToGroupAsync(Context.ConnectionId, $"tenant:{_tenantContextAccessor.CurrentTenantId}:gateway");
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("🔌 GatewayHub client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    private async Task<bool> RequirePlatformAdministratorAsync()
    {
        if (await PlatformAdminAuthorization.IsPlatformAdministratorAsync(
                _dbContext, _systemOperations, Context.User!, Context.ConnectionAborted))
            return true;

        await Clients.Caller.SendAsync("Error", "Platform administrator access is required.", Context.ConnectionAborted);
        return false;
    }
}
