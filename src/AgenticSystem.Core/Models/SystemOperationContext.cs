namespace AgenticSystem.Core.Models;

/// <summary>
/// Identifies an approved, server-created operation that may access platform-wide data.
/// It deliberately contains no tenant identity and is never resolved from a request claim or header.
/// </summary>
public sealed class SystemOperationContext
{
    internal SystemOperationContext(SystemOperationKind operation, string operationId)
    {
        Operation = operation;
        OperationId = operationId;
    }

    /// <summary>Gets the narrowly-scoped system capability.</summary>
    public SystemOperationKind Operation { get; }

    /// <summary>Gets a correlation identifier for the operation.</summary>
    public string OperationId { get; }
}

/// <summary>Supported internal platform/background operation capabilities.</summary>
public enum SystemOperationKind
{
    TenantResolution,
    EnumerateTenantsForBackground,
    TenantRegistryWrite,
    ApiKeyAuthentication,
    Bootstrap,
    PlatformAdminAuthorization,
    PlatformAdministration,
    PlatformConfigRead,
    PlatformConfigWrite,
    PlatformCatalogRead,
    PlatformCatalogWrite,
    PlatformQuotaSync,
    ProcessOutbox,
    ProcessPlatformOutbox,
    PublishPlatformEvent,
    EnumerateTenantsForSecretRotation,
    ProcessOnnxJobs,
    PlatformGatewayOperation,
    ClaimWorkflowExecutions
}
