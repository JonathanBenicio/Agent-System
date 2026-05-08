namespace AgenticSystem.Core.Models;

// ═══════════════════════════════════════════════════════════
// RBAC Permission Model
// ═══════════════════════════════════════════════════════════

/// <summary>
/// Granular permissions for resources.
/// </summary>
[Flags]
public enum Permission
{
    None = 0,
    Read = 1,
    Write = 2,
    Execute = 4,
    Delete = 8,
    ManageAgents = 16,
    ManageTools = 32,
    ManageKnowledge = 64,
    ManageUsers = 128,
    ManagePolicies = 256,
    Admin = Read | Write | Execute | Delete | ManageAgents | ManageTools | ManageKnowledge | ManageUsers | ManagePolicies
}

/// <summary>
/// A role definition with a set of permissions and optional resource scopes.
/// </summary>
public class RoleDefinition
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Permission Permissions { get; set; }

    /// <summary>
    /// Resource scopes this role applies to. Empty = all resources.
    /// Examples: "agents/*", "tools/search", "knowledge/public".
    /// </summary>
    public List<string> ResourceScopes { get; set; } = new();

    public bool IsBuiltIn { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Assignment of a role to a user, optionally scoped to a tenant.
/// </summary>
public class RoleAssignment
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string UserId { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public string? TenantId { get; set; }
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }
    public string? AssignedBy { get; set; }
    public bool IsActive => ExpiresAt == null || ExpiresAt > DateTime.UtcNow;
}

/// <summary>
/// Built-in roles for the system.
/// </summary>
public static class BuiltInRoles
{
    public static readonly RoleDefinition Owner = new()
    {
        Name = "Owner",
        Description = "Full system access with all permissions.",
        Permissions = Permission.Admin,
        IsBuiltIn = true
    };

    public static readonly RoleDefinition Admin = new()
    {
        Name = "Admin",
        Description = "Administrative access for agents, tools, and knowledge management.",
        Permissions = Permission.Read | Permission.Write | Permission.Execute | Permission.ManageAgents | Permission.ManageTools | Permission.ManageKnowledge | Permission.ManagePolicies,
        IsBuiltIn = true
    };

    public static readonly RoleDefinition Operator = new()
    {
        Name = "Operator",
        Description = "Can execute agents and tools, manage knowledge.",
        Permissions = Permission.Read | Permission.Write | Permission.Execute | Permission.ManageKnowledge,
        IsBuiltIn = true
    };

    public static readonly RoleDefinition Viewer = new()
    {
        Name = "Viewer",
        Description = "Read-only access to the system.",
        Permissions = Permission.Read,
        IsBuiltIn = true
    };

    public static IReadOnlyList<RoleDefinition> All => [Owner, Admin, Operator, Viewer];
}
