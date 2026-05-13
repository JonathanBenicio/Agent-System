using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using AgenticSystem.Core.Interfaces;

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

    public GatewayHub(IServiceGateway gateway, ILogger<GatewayHub> logger)
    {
        _gateway = gateway;
        _logger = logger;
    }

    public async Task GetDashboard()
    {
        var dashboard = await _gateway.GetDashboardAsync();
        await Clients.Caller.SendAsync("DashboardUpdate", dashboard);
    }

    public async Task GetServiceStatus(string serviceName)
    {
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
        await Groups.AddToGroupAsync(Context.ConnectionId, $"service:{serviceName}");
        _logger.LogDebug("Client {ConnectionId} subscribed to service {Service}",
            Context.ConnectionId, serviceName);
    }

    public async Task UnsubscribeFromService(string serviceName)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"service:{serviceName}");
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("🔌 GatewayHub client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("🔌 GatewayHub client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
