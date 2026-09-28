using AgenticSystem.Core.Interfaces;
using AgenticSystem.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgenticSystem.Infrastructure.Persistence;

public class PostgresAgentKnowledgeRoomStore : IAgentKnowledgeRoomStore
{
    private readonly AgenticDbContext _dbContext;

    public PostgresAgentKnowledgeRoomStore(AgenticDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IEnumerable<string>> GetRoomIdsForAgentAsync(string agentName, string tenantId, CancellationToken ct = default)
    {
        return await _dbContext.Set<AgentKnowledgeRoomAssignmentEntity>()
            .Where(a => a.AgentName == agentName && a.TenantId == tenantId)
            .Select(a => a.RoomId)
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<string>> GetAgentNamesForRoomAsync(string roomId, string tenantId, CancellationToken ct = default)
    {
        return await _dbContext.Set<AgentKnowledgeRoomAssignmentEntity>()
            .Where(a => a.RoomId == roomId && a.TenantId == tenantId)
            .Select(a => a.AgentName)
            .ToListAsync(ct);
    }

    public async Task AssignRoomAsync(string agentName, string roomId, string tenantId, CancellationToken ct = default)
    {
        var exists = await _dbContext.Set<AgentKnowledgeRoomAssignmentEntity>()
            .AnyAsync(a => a.AgentName == agentName && a.RoomId == roomId && a.TenantId == tenantId, ct);

        if (exists) return;

        _dbContext.Set<AgentKnowledgeRoomAssignmentEntity>().Add(new AgentKnowledgeRoomAssignmentEntity
        {
            Id = Guid.NewGuid().ToString(),
            AgentName = agentName,
            RoomId = roomId,
            TenantId = tenantId,
            CreatedAt = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync(ct);
    }

    public async Task UnassignRoomAsync(string agentName, string roomId, string tenantId, CancellationToken ct = default)
    {
        var assignment = await _dbContext.Set<AgentKnowledgeRoomAssignmentEntity>()
            .FirstOrDefaultAsync(a => a.AgentName == agentName && a.RoomId == roomId && a.TenantId == tenantId, ct);

        if (assignment == null) return;

        _dbContext.Set<AgentKnowledgeRoomAssignmentEntity>().Remove(assignment);
        await _dbContext.SaveChangesAsync(ct);
    }

    public async Task SetRoomsForAgentAsync(string agentName, IEnumerable<string> roomIds, string tenantId, CancellationToken ct = default)
    {
        var current = await _dbContext.Set<AgentKnowledgeRoomAssignmentEntity>()
            .Where(a => a.AgentName == agentName && a.TenantId == tenantId)
            .ToListAsync(ct);

        _dbContext.Set<AgentKnowledgeRoomAssignmentEntity>().RemoveRange(current);

        var roomList = roomIds.ToList();
        foreach (var roomId in roomList)
        {
            _dbContext.Set<AgentKnowledgeRoomAssignmentEntity>().Add(new AgentKnowledgeRoomAssignmentEntity
            {
                Id = Guid.NewGuid().ToString(),
                AgentName = agentName,
                RoomId = roomId,
                TenantId = tenantId,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _dbContext.SaveChangesAsync(ct);
    }

    public async Task<bool> IsRoomAssignedAsync(string agentName, string roomId, string tenantId, CancellationToken ct = default)
    {
        return await _dbContext.Set<AgentKnowledgeRoomAssignmentEntity>()
            .AnyAsync(a => a.AgentName == agentName && a.RoomId == roomId && a.TenantId == tenantId, ct);
    }
}
