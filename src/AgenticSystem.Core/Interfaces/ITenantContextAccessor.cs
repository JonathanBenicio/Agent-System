using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Interfaces;

public interface ITenantContextAccessor
{
    string CurrentTenantId { get; }
    IDisposable BeginScope(TenantContext context);
}