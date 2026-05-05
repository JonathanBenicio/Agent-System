using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace AgenticSystem.Infrastructure.Persistence;

public class EfSessionStore : ISessionStore
{
    private readonly AgenticDbContext _db;

    public EfSessionStore(AgenticDbContext db)
    {
        _db = db;
    }

    public async Task SaveAsync(SessionData session, CancellationToken ct = default)
    {
        try
        {
            _db.Sessions.Update(session);
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Detach the failed entity and retry as insert
            _db.Entry(session).State = EntityState.Detached;
            _db.Sessions.Add(session);
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task<SessionData?> GetAsync(string sessionId, CancellationToken ct = default)
    {
        return await _db.Sessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct);
    }

    public async Task<IReadOnlyList<SessionData>> GetByUserAsync(string userId, int maxResults = 10, CancellationToken ct = default)
    {
        return await _db.Sessions
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.StartedAt)
            .Take(maxResults)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<SessionData>> GetByTenantAsync(string tenantId, string? userId = null, int maxResults = 10, CancellationToken ct = default)
    {
        var query = _db.Sessions
            .AsNoTracking()
            .Where(s => s.TenantId == tenantId);

        if (!string.IsNullOrEmpty(userId))
            query = query.Where(s => s.UserId == userId);

        return await query
            .OrderByDescending(s => s.StartedAt)
            .Take(maxResults)
            .ToListAsync(ct);
    }

    public async Task DeleteAsync(string sessionId, CancellationToken ct = default)
    {
        var entity = await _db.Sessions.FindAsync(new object[] { sessionId }, ct);
        if (entity is not null)
        {
            _db.Sessions.Remove(entity);
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task<bool> ExistsAsync(string sessionId, CancellationToken ct = default)
    {
        return await _db.Sessions
            .AnyAsync(s => s.Id == sessionId, ct);
    }
}
