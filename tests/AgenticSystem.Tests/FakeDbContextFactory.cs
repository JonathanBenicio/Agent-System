using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using AgenticSystem.Infrastructure.Persistence;

namespace AgenticSystem.Tests;

public class FakeDbContextFactory : IDbContextFactory<AgenticDbContext>
{
    public AgenticDbContext DbContext { get; set; } = null!;
    
    public Func<AgenticDbContext>? ContextCreator { get; set; }

    public AgenticDbContext CreateDbContext() => ContextCreator != null ? ContextCreator() : DbContext;

    public Task<AgenticDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(CreateDbContext());
}
