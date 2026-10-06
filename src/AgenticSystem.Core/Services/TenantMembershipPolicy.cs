using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Services;

/// <summary>Canonical tenant roles shared by authentication and tenant authorization.</summary>
public static class TenantMembershipPolicy
{
    /// <summary>Returns the supported canonical role, including legacy Member normalization.</summary>
    public static string? NormalizeRole(string role)
    {
        if (role.Equals("Member", StringComparison.OrdinalIgnoreCase))
            return "Viewer";
        if (role.Equals("ServiceAccount", StringComparison.OrdinalIgnoreCase))
            return "ServiceAccount";
        return BuiltInRoles.All.FirstOrDefault(known =>
            known.Name.Equals(role, StringComparison.OrdinalIgnoreCase))?.Name;
    }

    /// <summary>Returns only supported memberships in the requested tenant.</summary>
    public static async Task<string[]> GetRolesAsync(
        IPermissionService permissions, string subjectId, string tenantId, CancellationToken ct = default)
    {
        var assignments = await permissions.GetRolesAsync(subjectId, ct);
        return assignments
            .Where(role => role.IsActive && string.Equals(role.TenantId, tenantId, StringComparison.OrdinalIgnoreCase))
            .Select(role => NormalizeRole(role.RoleName))
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
