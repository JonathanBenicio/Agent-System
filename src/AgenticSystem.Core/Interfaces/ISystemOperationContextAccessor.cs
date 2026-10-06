using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Interfaces;

/// <summary>
/// Provides an internal, async-flow-scoped system capability separate from tenant identity.
/// Implementations must never populate this context from request headers or claims.
/// </summary>
public interface ISystemOperationContextAccessor
{
    /// <summary>Gets the system operation active in the current async flow, if any.</summary>
    SystemOperationContext? Current { get; }

    /// <summary>Begins a server-selected system operation scope.</summary>
    IDisposable BeginScope(SystemOperationKind operation);

    /// <summary>Requires the specified system capability to be active.</summary>
    SystemOperationContext Require(SystemOperationKind operation);
}
