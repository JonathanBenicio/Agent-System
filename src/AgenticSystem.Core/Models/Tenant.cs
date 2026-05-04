namespace AgenticSystem.Core.Models;

/// <summary>
/// Entidade de tenant — representa uma organização/cliente no sistema SaaS.
/// </summary>
public class Tenant
{
    public const string DefaultTenantId = "default";

    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public TenantPlan Plan { get; set; } = TenantPlan.Free;
    public TenantLimits Limits { get; set; } = TenantLimits.FreeTier();
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// API keys do tenant para cada provider LLM (BYOK).
    /// Key: provider name, Value: API key.
    /// Se vazio, usa pool compartilhado do sistema.
    /// </summary>
    public Dictionary<string, string> ProviderApiKeys { get; set; } = new();

    /// <summary>
    /// Configurações customizadas por tenant (feature flags, preferences).
    /// </summary>
    public Dictionary<string, object> Settings { get; set; } = new();
}

public enum TenantPlan
{
    Free,
    Pro,
    Enterprise
}

/// <summary>
/// Limites de uso por plano de tenant.
/// </summary>
public class TenantLimits
{
    public int MaxRequestsPerMinute { get; set; }
    public int MaxTokensPerDay { get; set; }
    public decimal MaxDailyCostUsd { get; set; }
    public int MaxConcurrentSessions { get; set; }
    public int MaxAgents { get; set; }
    public int MaxDocumentsMb { get; set; }

    public static TenantLimits FreeTier() => new()
    {
        MaxRequestsPerMinute = 10,
        MaxTokensPerDay = 50_000,
        MaxDailyCostUsd = 1.00m,
        MaxConcurrentSessions = 3,
        MaxAgents = 5,
        MaxDocumentsMb = 100
    };

    public static TenantLimits ProTier() => new()
    {
        MaxRequestsPerMinute = 60,
        MaxTokensPerDay = 500_000,
        MaxDailyCostUsd = 25.00m,
        MaxConcurrentSessions = 20,
        MaxAgents = 50,
        MaxDocumentsMb = 5_000
    };

    public static TenantLimits EnterpriseTier() => new()
    {
        MaxRequestsPerMinute = 300,
        MaxTokensPerDay = 5_000_000,
        MaxDailyCostUsd = 500.00m,
        MaxConcurrentSessions = 100,
        MaxAgents = 500,
        MaxDocumentsMb = 50_000
    };
}
