using System.Collections.Concurrent;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Services;

public class InMemoryAgentMarketplace : IAgentMarketplace
{
    private readonly ConcurrentDictionary<string, AgentMarketplaceEntry> _entries = new();

    public Task<AgentMarketplaceEntry> PublishAsync(AgentSpecification spec, string author, CancellationToken ct = default)
    {
        var entry = new AgentMarketplaceEntry
        {
            Name = spec.Name,
            Domain = spec.Domain,
            Description = spec.Description,
            Tier = spec.Tier,
            AuthorName = author,
            Tags = spec.Capabilities,
            Tools = spec.AllowedTools,
            SystemPrompt = spec.Instructions,
            Status = MarketplaceEntryStatus.Published
        };
        _entries[entry.Id] = entry;
        return Task.FromResult(entry);
    }

    public Task<IReadOnlyList<AgentMarketplaceEntry>> SearchAsync(string? query = null, string? domain = null, List<string>? tags = null, CancellationToken ct = default)
    {
        var dbQuery = _entries.Values.AsQueryable();
        if (!string.IsNullOrEmpty(query)) dbQuery = dbQuery.Where(e => e.Name.Contains(query, StringComparison.OrdinalIgnoreCase) || e.Description.Contains(query, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrEmpty(domain)) dbQuery = dbQuery.Where(e => e.Domain == domain);
        return Task.FromResult<IReadOnlyList<AgentMarketplaceEntry>>(dbQuery.OrderByDescending(e => e.PublishedAt).ToList());
    }

    public Task<AgentMarketplaceEntry?> GetAsync(string entryId, CancellationToken ct = default)
    {
        _entries.TryGetValue(entryId, out var entry);
        return Task.FromResult(entry);
    }

    public Task RateAgentAsync(string entryId, int rating, string? comment = null, CancellationToken ct = default)
    {
        if (_entries.TryGetValue(entryId, out var entry))
        {
            entry.Rating = (entry.Rating * entry.InstallCount + rating) / (entry.InstallCount + 1);
            entry.InstallCount++;
        }
        return Task.CompletedTask;
    }
}
