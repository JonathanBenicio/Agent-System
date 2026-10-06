using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Interfaces;

public interface ITenantContextAccessor
{
    string CurrentTenantId { get; }
    TenantContext? CurrentContext => null;
    IDisposable BeginScope(TenantContext context);
}
