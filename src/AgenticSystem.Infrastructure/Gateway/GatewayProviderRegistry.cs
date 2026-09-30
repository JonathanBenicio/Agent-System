using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace AgenticSystem.Infrastructure.Gateway;

/// <summary>Keeps global platform-provider registrations synchronized with runtime configuration.</summary>
public sealed class GatewayProviderRegistry
{
    private readonly IServiceGateway _gateway;
    private readonly IOptions<AgenticSystemSettings> _settings;

    public GatewayProviderRegistry(IServiceGateway gateway, IOptions<AgenticSystemSettings> settings)
    {
        _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    public void Synchronize(string providerName, bool enabled)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);
        if (!enabled)
        {
            _gateway.UnregisterService(providerName);
            return;
        }

        var gatewaySettings = _settings.Value.Gateway;
        _gateway.RegisterService(new ServiceRegistration
        {
            Name = providerName,
            Category = "LLM",
            DailyBudget = gatewaySettings.DefaultDailyBudget,
            CircuitBreaker = new CircuitBreakerConfig
            {
                FailureThreshold = gatewaySettings.DefaultFailureThreshold,
                BreakDuration = TimeSpan.FromSeconds(gatewaySettings.DefaultBreakDurationSeconds)
            },
            RateLimits = new RateLimitConfig
            {
                RequestsPerMinute = gatewaySettings.DefaultRequestsPerMinute
            }
        });
    }
}
