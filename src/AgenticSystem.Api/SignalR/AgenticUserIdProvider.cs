using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace AgenticSystem.Api.SignalR;

/// <summary>
/// Provedor de ID de usuário personalizado para o SignalR.
/// Garante que o ID da conexão SignalR do usuário seja mapeado exatamente
/// da mesma forma que no ChatHub e no restante do sistema (NameIdentifier -> sub -> Name).
/// </summary>
public class AgenticUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection)
    {
        return connection.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? connection.User?.FindFirst("sub")?.Value
            ?? connection.User?.Identity?.Name
            ?? "authenticated-user";
    }
}
