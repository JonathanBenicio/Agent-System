using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

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
            npgsql.MigrationsHistoryTable("__ef_migrations_history"));

        return new AgenticDbContext(optionsBuilder.Options);
    }
}