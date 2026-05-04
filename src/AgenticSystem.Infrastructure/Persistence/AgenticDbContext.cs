using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace AgenticSystem.Infrastructure.Persistence;

public class AgenticDbContext : DbContext
{
    public AgenticDbContext(DbContextOptions<AgenticDbContext> options) : base(options) { }

    public DbSet<SessionData> Sessions => Set<SessionData>();
    public DbSet<Tenant> Tenants => Set<Tenant>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AgenticDbContext).Assembly);
    }
}
