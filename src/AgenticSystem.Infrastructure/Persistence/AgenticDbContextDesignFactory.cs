using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Pgvector.EntityFrameworkCore;

namespace AgenticSystem.Infrastructure.Persistence;

public sealed class AgenticDbContextDesignFactory : IDesignTimeDbContextFactory<AgenticDbContext>
{
    public AgenticDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("AGENTIC_EF_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=agentic;Username=postgres;Password=postgres";

        var optionsBuilder = new DbContextOptionsBuilder<AgenticDbContext>();
        optionsBuilder.UseNpgsql(connectionString, npgsql =>
        {
            npgsql.MigrationsHistoryTable("__ef_migrations_history");
            npgsql.UseVector();
        });

        return new AgenticDbContext(optionsBuilder.Options, new DummyTenantAccessor());
    }

    private class DummyTenantAccessor : AgenticSystem.Core.Interfaces.ITenantContextAccessor
    {
        public AgenticSystem.Core.Models.TenantContext Current => new() { TenantId = "design-time" };
        public IDisposable BeginScope(AgenticSystem.Core.Models.TenantContext context) => null!;
    }
}