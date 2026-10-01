using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Interfaces;

public interface IGoldenSetRepository
{
    Task<GoldenSet?> GetByIdAsync(string id, string tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<GoldenSet>> ListAsync(string tenantId, string? agentName, int page, int pageSize, CancellationToken ct = default);
    Task AddAsync(GoldenSet model, CancellationToken ct = default);
    Task UpdateAsync(GoldenSet model, CancellationToken ct = default);
    Task DeleteAsync(string id, string tenantId, CancellationToken ct = default);
}
