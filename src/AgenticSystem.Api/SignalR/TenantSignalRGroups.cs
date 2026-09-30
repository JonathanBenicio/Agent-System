using System.Security.Cryptography;
using System.Text;

namespace AgenticSystem.Api.SignalR;

public static class TenantSignalRGroups
{
    public static string User(string tenantId, string userId)
    {
        var userHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(userId))).ToLowerInvariant();
        return $"tenant:{tenantId}:user:{userHash}";
    }
}
