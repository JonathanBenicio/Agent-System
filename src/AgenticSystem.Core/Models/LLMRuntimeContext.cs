namespace AgenticSystem.Core.Models;

/// <summary>
/// Contexto runtime de escolha do LLM para a execução atual.
/// </summary>
public sealed class LLMRuntimeContext
{
    public string UserId { get; init; } = string.Empty;
    public string TenantId { get; init; } = Tenant.DefaultTenantId;
    public string? SessionId { get; init; }

    public string? RequestProvider { get; init; }
    public string? RequestModel { get; init; }
    public string? RequestApiKey { get; init; }

    public string? SessionProvider { get; init; }
    public string? SessionModel { get; init; }
    public string? SessionApiKey { get; init; }
}
