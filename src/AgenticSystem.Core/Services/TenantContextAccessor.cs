using System.Threading;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Services;

public sealed class TenantContextAccessor : ITenantContextAccessor
{
    private static readonly AsyncLocal<TenantContext?> AmbientContext = new();

    public TenantContext? CurrentContext => AmbientContext.Value;

    public string CurrentTenantId => 
        AmbientContext.Value?.TenantId
        ?? throw new InvalidOperationException("Strict Multi-Tenancy Violation: No active Tenant Context resolved in the current async flow.");

    public IDisposable BeginScope(TenantContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (string.IsNullOrWhiteSpace(context.TenantId))
        {
            throw new ArgumentException("A real TenantId is required for a tenant scope.", nameof(context));
        }

        if (TenantIdPolicy.IsReservedSystemId(context.TenantId))
        {
            throw new ArgumentException("System operation identifiers are not valid tenant IDs.", nameof(context));
        }

        var previous = AmbientContext.Value;
        AmbientContext.Value = context;
        return new Scope(() => AmbientContext.Value = previous);
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
