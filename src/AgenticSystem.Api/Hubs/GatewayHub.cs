using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using AgenticSystem.Api.Auth;
using AgenticSystem.Core.Interfaces;
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
    private readonly AgenticDbContext _dbContext;

    public GatewayHub(
        IServiceGateway gateway,
        ILogger<GatewayHub> logger,
        ITenantContextAccessor tenantContextAccessor,
        AgenticDbContext dbContext)
    {
        _gateway = gateway;
        _logger = logger;
        _tenantContextAccessor = tenantContextAccessor;
        _dbContext = dbContext;
    }

    public async Task GetDashboard()
    {
        if (!await RequirePlatformAdministratorAsync())
            return;

        var dashboard = await _gateway.GetDashboardAsync();
        await Clients.Caller.SendAsync("DashboardUpdate", dashboard);
    }

    public async Task GetServiceStatus(string serviceName)
    {
        if (!await RequirePlatformAdministratorAsync())
            return;

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

        var tenantId = _tenantContextAccessor.CurrentTenantId;
        await Groups.AddToGroupAsync(Context.ConnectionId, $"tenant:{tenantId}:service:{serviceName}");
        _logger.LogDebug("Client {ConnectionId} subscribed to service {Service}",
            Context.ConnectionId, serviceName);
    }

    public async Task UnsubscribeFromService(string serviceName)
    {
        var tenantId = _tenantContextAccessor.CurrentTenantId;
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"tenant:{tenantId}:service:{serviceName}");
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("🔌 GatewayHub client connected: {ConnectionId}", Context.ConnectionId);
        if (!await PlatformAdminAuthorization.IsPlatformAdministratorAsync(
                _dbContext, Context.User!, Context.ConnectionAborted))
        {
            await Clients.Caller.SendAsync("Error", "Platform administrator access is required.", Context.ConnectionAborted);
            await base.OnConnectedAsync();
            return;
        }

        var tenantId = _tenantContextAccessor.CurrentTenantId;
        await Groups.AddToGroupAsync(Context.ConnectionId, $"tenant:{tenantId}:gateway");
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
                _dbContext, Context.User!, Context.ConnectionAborted))
            return true;

        await Clients.Caller.SendAsync("Error", "Platform administrator access is required.", Context.ConnectionAborted);
        return false;
    }
}
