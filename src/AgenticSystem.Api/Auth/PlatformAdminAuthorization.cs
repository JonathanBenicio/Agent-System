using System.Security.Claims;
using AgenticSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgenticSystem.Api.Auth;

internal static class PlatformAdminAuthorization
{
    public static async Task<bool> IsPlatformAdministratorAsync(
        AgenticDbContext dbContext,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(userId))
            return false;

        return await dbContext.PlatformAdministrators.IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(admin => admin.UserId == userId, cancellationToken);
    }
}
