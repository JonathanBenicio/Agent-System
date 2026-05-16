namespace AgenticSystem.Core.Interfaces;

/// <summary>
/// Interface para entidades que suportam multi-tenancy.
/// </summary>
public interface ITenantEntity
{
    string TenantId { get; set; }
}
