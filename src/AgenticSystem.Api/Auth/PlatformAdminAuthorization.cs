using System.Security.Claims;
using AgenticSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Api.Auth;

internal static class PlatformAdminAuthorization
{
    public static async Task<bool> IsPlatformAdministratorAsync(
        AgenticDbContext dbContext,
        ISystemOperationContextAccessor systemOperations,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(userId))
            return false;

        using var systemScope = systemOperations.BeginScope(SystemOperationKind.PlatformAdminAuthorization);
        systemOperations.Require(SystemOperationKind.PlatformAdminAuthorization);
        return await dbContext.PlatformAdministrators.IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(admin => admin.UserId == userId, cancellationToken);
    }
}
