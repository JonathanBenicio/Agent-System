namespace AgenticSystem.Core.Models;

/// <summary>
/// Contexto do tenant atual — injetado como Scoped no DI.
/// Populado pelo TenantMiddleware a partir do JWT token.
/// </summary>
public class TenantContext
{
    /// <summary>
    /// Identificador único do tenant.
    /// </summary>
    public required string TenantId { get; init; }

    /// <summary>
    /// Nome do tenant para exibição.
    /// </summary>
    public string TenantName { get; set; } = string.Empty;

    /// <summary>
    /// Plano do tenant (Free, Pro, Enterprise).
    /// </summary>
    public TenantPlan Plan { get; set; } = TenantPlan.Free;

    /// <summary>
    /// Limites de uso do tenant baseados no plano.
    /// </summary>
    public TenantLimits Limits { get; set; } = TenantLimits.FreeTier();

    /// <summary>
    /// Se o contexto foi resolvido a partir de um token válido.
    /// </summary>
    public bool IsAuthenticated { get; set; }
}
