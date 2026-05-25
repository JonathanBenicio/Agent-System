using System.Threading;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Services;

public sealed class TenantContextAccessor : ITenantContextAccessor
{
    private static readonly AsyncLocal<TenantContext?> CurrentContext = new();

    public string CurrentTenantId => 
        CurrentContext.Value?.TenantId 
        ?? throw new InvalidOperationException("Strict Multi-Tenancy Violation: No active Tenant Context resolved in the current async flow.");

    public IDisposable BeginScope(TenantContext context)
    {
        var previous = CurrentContext.Value;
        CurrentContext.Value = context;
        return new Scope(() => CurrentContext.Value = previous);
    }

    private sealed class Scope : IDisposable
    {
        private readonly Action _restore;
        private int _disposed;

        public Scope(Action restore)
        {
            _restore = restore;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                _restore();
            }
        }
    }
}