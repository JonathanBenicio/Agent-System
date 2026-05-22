using System.Security.Cryptography;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace AgenticSystem.Api.Extensions;

/// <summary>
/// Encapsulates all rate limiting configuration: protocol-level (A2A/AG-UI) and
/// tenant-scoped chat rate limiting using native ASP.NET Core middleware.
/// Replaces the ad-hoc ConcurrentDictionary-based rate limiter that was in Program.cs.
/// </summary>
public static class RateLimitingServiceCollectionExtensions
{
    /// <summary>
    /// Name of the rate limiting policy applied to protocol endpoints (A2A, AG-UI).
    /// </summary>
    public const string ProtocolPolicyName = "ProtocolEndpoints";

    /// <summary>
    /// Name of the rate limiting policy applied per-tenant to chat endpoints.
    /// </summary>
    public const string TenantChatPolicyName = "TenantChatLimit";

    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var protocolHosting = configuration.GetSection("ProtocolHosting");
        var protocolRateLimitingEnabled = protocolHosting.GetValue("RateLimiting:Enabled", true);
        var protocolRateLimitPermitLimit = protocolHosting.GetValue("RateLimiting:PermitLimit", 60);
        var protocolRateLimitWindowSeconds = protocolHosting.GetValue("RateLimiting:WindowSeconds", 60);
        var protocolRateLimitQueueLimit = protocolHosting.GetValue("RateLimiting:QueueLimit", 0);

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, ct) =>
            {
                context.HttpContext.Response.Headers["Retry-After"] = protocolRateLimitWindowSeconds.ToString();
                if (!context.HttpContext.Response.HasStarted)
                {
                    context.HttpContext.Response.ContentType = "application/json";
                    await context.HttpContext.Response.WriteAsJsonAsync(new
                    {
                        error = "Rate limit exceeded. Try again later."
                    }, cancellationToken: ct);
                }
            };

            // Policy for protocol endpoints (A2A, AG-UI)
            options.AddPolicy(ProtocolPolicyName, httpContext =>
            {
                if (!protocolRateLimitingEnabled)
                {
                    return RateLimitPartition.GetNoLimiter("protocol-rate-limiting-disabled");
                }

                var partitionKey = BuildProtocolPartitionKey(httpContext);
                return RateLimitPartition.GetSlidingWindowLimiter(
                    partitionKey,
                    _ => new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = protocolRateLimitPermitLimit,
                        Window = TimeSpan.FromSeconds(protocolRateLimitWindowSeconds),
                        SegmentsPerWindow = 4,
                        QueueLimit = protocolRateLimitQueueLimit,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        AutoReplenishment = true
                    });
            });

            // Policy for chat endpoints — per-tenant sliding window
            options.AddPolicy(TenantChatPolicyName, httpContext =>
            {
                var tenantId = httpContext.User.FindFirst("tenant_id")?.Value
                    ?? httpContext.Request.Headers["X-Tenant-Id"].FirstOrDefault()
                    ?? "default";

                return RateLimitPartition.GetSlidingWindowLimiter(
                    $"chat:tenant:{tenantId}",
                    _ => new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = 30,
                        Window = TimeSpan.FromSeconds(60),
                        SegmentsPerWindow = 4,
                        QueueLimit = 0,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        AutoReplenishment = true
                    });
            });
        });

        return services;
    }

    private static string BuildProtocolPartitionKey(HttpContext httpContext)
    {
        var tenantId = httpContext.User.FindFirst("tenant_id")?.Value;
        var userId = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? httpContext.User.FindFirst("sub")?.Value
            ?? httpContext.User.Identity?.Name;

        if (!string.IsNullOrWhiteSpace(tenantId) && !string.IsNullOrWhiteSpace(userId))
        {
            return $"tenant:{tenantId}:user:{userId}";
        }

        if (!string.IsNullOrWhiteSpace(userId))
        {
            return $"user:{userId}";
        }

        if (httpContext.Request.Headers.TryGetValue("Authorization", out var authorization)
            && !string.IsNullOrWhiteSpace(authorization))
        {
            return $"auth:{Hash(authorization.ToString())}";
        }

        if (httpContext.Request.Headers.TryGetValue("X-Api-Key", out var apiKey)
            && !string.IsNullOrWhiteSpace(apiKey))
        {
            return $"api-key:{Hash(apiKey.ToString())}";
        }

        var remoteIp = httpContext.Connection.RemoteIpAddress?.ToString();
        return string.IsNullOrWhiteSpace(remoteIp) ? "anonymous" : $"ip:{remoteIp}";
    }

    private static string Hash(string value)
    {
        var bytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes[..8]);
    }
}
