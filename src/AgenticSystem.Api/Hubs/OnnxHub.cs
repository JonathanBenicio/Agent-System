using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using AgenticSystem.Core.Interfaces;

namespace AgenticSystem.Api.Hubs;

[Authorize]
public class OnnxHub : Hub
{
    private readonly ILogger<OnnxHub> _logger;
    private readonly ITenantContextAccessor _tenantContextAccessor;

    public OnnxHub(ILogger<OnnxHub> logger, ITenantContextAccessor tenantContextAccessor)
    {
        _logger = logger;
        _tenantContextAccessor = tenantContextAccessor;
    }

    public async Task SubscribeToTenant(string tenantId)
    {
        if (string.IsNullOrWhiteSpace(tenantId)) return;

        EnsureAuthorizedForTenant(tenantId, "subscribe to");
        var authorizedTenantId = _tenantContextAccessor.CurrentTenantId;

        await Groups.AddToGroupAsync(Context.ConnectionId, $"tenant:{authorizedTenantId}");
        _logger.LogInformation("🔌 Client {ConnectionId} subscribed to ONNX updates for tenant {TenantId}",
            Context.ConnectionId, tenantId);
    }

    public async Task UnsubscribeFromTenant(string tenantId)
    {
        if (string.IsNullOrWhiteSpace(tenantId)) return;

        EnsureAuthorizedForTenant(tenantId, "unsubscribe from");
        var authorizedTenantId = _tenantContextAccessor.CurrentTenantId;

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"tenant:{authorizedTenantId}");
        _logger.LogInformation("🔌 Client {ConnectionId} unsubscribed from ONNX updates for tenant {TenantId}",
            Context.ConnectionId, tenantId);
    }

    private void EnsureAuthorizedForTenant(string tenantId, string action)
    {
        var userTenantId = _tenantContextAccessor.CurrentTenantId;
        if (!string.Equals(tenantId?.Trim(), userTenantId?.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw new HubException($"Unauthorized to {action} this tenant.");
        }
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("🔌 OnnxHub client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("🔌 OnnxHub client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}

public class SignalROnnxEventBroadcaster : IOnnxEventBroadcaster
{
    private readonly IHubContext<OnnxHub> _hubContext;
    private readonly ILogger<SignalROnnxEventBroadcaster> _logger;

    public SignalROnnxEventBroadcaster(IHubContext<OnnxHub> hubContext, ILogger<SignalROnnxEventBroadcaster> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task BroadcastJobStatusAsync(
        string tenantId,
        string jobId,
        string status,
        string? error = null,
        long? latencyMs = null,
        string? outputImagePath = null)
    {
        try
        {
            var payload = new
            {
                jobId,
                tenantId,
                status,
                error,
                latencyMs,
                outputImagePath,
                timestamp = DateTime.UtcNow
            };

            await _hubContext.Clients.Group($"tenant:{tenantId}").SendAsync("JobStatusChanged", payload);
            _logger.LogDebug("📢 Broadcasted ONNX status to tenant:{TenantId} - Job: {JobId}, Status: {Status}", tenantId, jobId, status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to broadcast ONNX job status change for job {JobId}", jobId);
        }
    }
}
