using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Interfaces;

public interface ITenantContextAccessor
{
    TenantContext Current { get; }
    IDisposable BeginScope(TenantContext context);
}