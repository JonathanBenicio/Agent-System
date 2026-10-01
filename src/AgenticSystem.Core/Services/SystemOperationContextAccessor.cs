using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Services;

/// <summary>
/// Stores an internal system capability in the current async flow without changing tenant context.
/// </summary>
public sealed class SystemOperationContextAccessor : ISystemOperationContextAccessor
{
    private static readonly AsyncLocal<SystemOperationContext?> AmbientContext = new();

    /// <inheritdoc />
    public SystemOperationContext? Current => AmbientContext.Value;

    /// <inheritdoc />
    public IDisposable BeginScope(SystemOperationKind operation)
    {
        if (!Enum.IsDefined(operation))
        {
            throw new ArgumentOutOfRangeException(nameof(operation), operation, "Unsupported system operation.");
        }

        var previous = AmbientContext.Value;
        AmbientContext.Value = new SystemOperationContext(operation, Guid.NewGuid().ToString("N"));
        return new Scope(() => AmbientContext.Value = previous);
    }

    /// <inheritdoc />
    public SystemOperationContext Require(SystemOperationKind operation)
    {
        var current = AmbientContext.Value;
        if (current?.Operation != operation)
        {
            throw new InvalidOperationException($"System operation '{operation}' is required for this platform-wide action.");
        }

        return current;
    }

    private sealed class Scope(Action restore) : IDisposable
    {
        private Action? _restore = restore;

        public void Dispose() => Interlocked.Exchange(ref _restore, null)?.Invoke();
    }
}
