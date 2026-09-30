using AgenticSystem.Core.Interfaces;

namespace AgenticSystem.Api.Helpers;

internal static class SessionAccessValidator
{
    public static async Task<bool> CanAccessAsync(
        ISessionStore sessionStore,
        string? sessionId,
        string userId,
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            return true;

        var session = await sessionStore.GetAsync(sessionId, cancellationToken);
        return session is not null &&
            string.Equals(session.UserId, userId, StringComparison.Ordinal) &&
            string.Equals(session.TenantId, tenantId, StringComparison.OrdinalIgnoreCase);
    }
}
