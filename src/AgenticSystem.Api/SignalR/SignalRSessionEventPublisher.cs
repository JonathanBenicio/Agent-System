using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Api.Hubs;

namespace AgenticSystem.Api.SignalR;

public class SignalRSessionEventPublisher : IEventPublisher
{
    private readonly IHubContext<ChatHub> _hubContext;
    private readonly ILogger<SignalRSessionEventPublisher> _logger;

    public SignalRSessionEventPublisher(IHubContext<ChatHub> hubContext, ILogger<SignalRSessionEventPublisher> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default) where TEvent : class
    {
        switch (@event)
        {
            case SessionCreatedEvent e:
                _logger.LogDebug("📡 Broadcasting SessionCreated for {SessionId} to user {UserId}", e.SessionId, e.UserId);
                await _hubContext.Clients.User(e.UserId).SendAsync("SessionCreated", e.SessionId, ct);
                break;

            case SessionEndedEvent e:
                _logger.LogDebug("📡 Broadcasting SessionEnded for {SessionId} to user {UserId}", e.SessionId, e.UserId);
                await _hubContext.Clients.User(e.UserId).SendAsync("SessionEnded", e.SessionId, ct);
                break;
        }
    }
}
