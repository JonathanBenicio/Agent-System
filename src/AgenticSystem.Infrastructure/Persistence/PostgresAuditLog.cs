using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace AgenticSystem.Infrastructure.Persistence;

public class PostgresAuditLog : IAuditLog
{
    private readonly AgenticDbContext _dbContext;

    public PostgresAuditLog(AgenticDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task RecordAsync(AuditEntry entry, CancellationToken ct = default)
    {
        var entity = new AuditEntryEntity
        {
            Id = Guid.NewGuid().ToString("N"),
            Timestamp = entry.Timestamp,
            Category = entry.Category.ToString(),
            Action = entry.Action,
            UserId = entry.UserId,
            TenantId = entry.TenantId,
            SessionId = entry.SessionId,
            AgentName = entry.AgentName,
            ToolName = entry.ToolName,
            ModelUsed = entry.ModelUsed,
            Cost = entry.Cost,
            TraceId = entry.TraceId,
            Description = entry.Description,
            Success = entry.Success,
            ErrorMessage = entry.ErrorMessage,
            IpAddress = entry.IpAddress,
            UserAgent = entry.UserAgent,
            DetailsJson = entry.Metadata != null ? JsonSerializer.Serialize(entry.Metadata) : "{}"
        };

        _dbContext.AuditEntries.Add(entity);
        await _dbContext.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<AuditEntry>> QueryAsync(AuditQuery queryParams, CancellationToken ct = default)
    {
        var query = _dbContext.AuditEntries.AsNoTracking();
        
        if (queryParams.TenantId != null)
            query = query.Where(a => a.TenantId == queryParams.TenantId);
        if (queryParams.UserId != null)
            query = query.Where(a => a.UserId == queryParams.UserId);
        if (queryParams.SessionId != null)
            query = query.Where(a => a.SessionId == queryParams.SessionId);
        if (queryParams.AgentName != null)
            query = query.Where(a => a.AgentName == queryParams.AgentName);
        if (queryParams.Category.HasValue)
            query = query.Where(a => a.Category == queryParams.Category.Value.ToString());
        if (queryParams.From.HasValue)
            query = query.Where(a => a.Timestamp >= queryParams.From.Value);
        if (queryParams.To.HasValue)
            query = query.Where(a => a.Timestamp <= queryParams.To.Value);
        if (queryParams.SuccessOnly.HasValue)
            query = query.Where(a => a.Success == queryParams.SuccessOnly.Value);

        var entities = await query
            .OrderByDescending(a => a.Timestamp)
            .Skip(queryParams.Offset)
            .Take(queryParams.Limit)
            .ToListAsync(ct);

        return entities.Select(e => new AuditEntry
        {
            Id = e.Id,
            Category = Enum.TryParse<AuditCategory>(e.Category, out var cat) ? cat : AuditCategory.SystemEvent,
            Action = e.Action,
            UserId = e.UserId,
            TenantId = e.TenantId,
            SessionId = e.SessionId,
            AgentName = e.AgentName,
            ToolName = e.ToolName,
            ModelUsed = e.ModelUsed,
            Cost = e.Cost,
            TraceId = e.TraceId,
            Description = e.Description,
            Success = e.Success,
            ErrorMessage = e.ErrorMessage,
            IpAddress = e.IpAddress,
            UserAgent = e.UserAgent,
            Timestamp = e.Timestamp,
            Metadata = string.IsNullOrWhiteSpace(e.DetailsJson) || e.DetailsJson == "{}" 
                ? new Dictionary<string, object>()
                : JsonSerializer.Deserialize<Dictionary<string, object>>(e.DetailsJson) ?? new Dictionary<string, object>()
        }).ToList();
    }

    public async Task<long> CountAsync(AuditQuery queryParams, CancellationToken ct = default)
    {
        var query = _dbContext.AuditEntries.AsNoTracking();
        
        if (queryParams.TenantId != null)
            query = query.Where(a => a.TenantId == queryParams.TenantId);
        if (queryParams.UserId != null)
            query = query.Where(a => a.UserId == queryParams.UserId);
        if (queryParams.SessionId != null)
            query = query.Where(a => a.SessionId == queryParams.SessionId);
        if (queryParams.AgentName != null)
            query = query.Where(a => a.AgentName == queryParams.AgentName);
        if (queryParams.Category.HasValue)
            query = query.Where(a => a.Category == queryParams.Category.Value.ToString());
        if (queryParams.From.HasValue)
            query = query.Where(a => a.Timestamp >= queryParams.From.Value);
        if (queryParams.To.HasValue)
            query = query.Where(a => a.Timestamp <= queryParams.To.Value);
        if (queryParams.SuccessOnly.HasValue)
            query = query.Where(a => a.Success == queryParams.SuccessOnly.Value);

        return await query.LongCountAsync(ct);
    }
}
