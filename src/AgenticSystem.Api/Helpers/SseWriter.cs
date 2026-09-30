using System.Text.Json;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Api.Helpers;

/// <summary>
/// Utility for writing Server-Sent Events (SSE) to the HTTP response stream.
/// </summary>
public static class SseWriter
{
    public static async Task WriteSseEventAsync(HttpContext httpContext, AgentStreamEvent streamEvent, CancellationToken ct)
    {
        var eventName = ToSseEventName(streamEvent.Type);
        var json = JsonSerializer.Serialize(streamEvent);
        await httpContext.Response.WriteAsync($"event: {eventName}\n", ct);
        await httpContext.Response.WriteAsync($"data: {json}\n\n", ct);
        await httpContext.Response.Body.FlushAsync(ct);
    }

    private static string ToSseEventName(AgentStreamEventType type)
        => type.ToString().ToLowerInvariant();
}
