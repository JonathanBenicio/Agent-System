using AgenticSystem.Core.Services;
using AgenticSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pgvector.EntityFrameworkCore;

namespace AgenticSystem.Tests;

public sealed class DurableTaskCompletionPostgresRegressionTests
{
    [ReviewSessionPostgresFact]
    public async Task CompetingCompletionPublishesExactlyOneResultAndRollsBackTheLoser()
    {
        var target = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("AGENTIC_REVIEW_POSTGRES"));
        if (target.Host != "127.0.0.1" || target.Port != 55432 || target.Username != "validation" ||
            !target.Database!.StartsWith("review_pr152_", StringComparison.Ordinal))
            throw new InvalidOperationException("Requires isolated review database.");
        var options = new DbContextOptionsBuilder<AgenticDbContext>()
            .UseNpgsql(target.ConnectionString, postgres =>
            {
                postgres.UseVector();
                postgres.MigrationsHistoryTable("__ef_migrations_history");
            }).Options;
        await using (var db = new AgenticDbContext(options, new TenantContextAccessor()))
            await db.Database.MigrateAsync();
        var instance = $"completion-race-{Guid.NewGuid():N}";
        var execution = Guid.NewGuid().ToString("N");
        await using var setup = new NpgsqlConnection(target.ConnectionString);
        await setup.OpenAsync();
        await using (var seed = new NpgsqlCommand("""
            INSERT INTO dt.instances(task_hub,instance_id,execution_id,name,runtime_status)
                VALUES(dt.current_task_hub(),@instance,@execution,'ReviewRace','Running');
            """, setup))
        {
            seed.Parameters.AddWithValue("instance", instance);
            seed.Parameters.AddWithValue("execution", execution);
            await seed.ExecuteNonQueryAsync();
        }
        long sequence;
        await using (var task = new NpgsqlCommand("""
            INSERT INTO dt.new_tasks(task_hub,instance_id,execution_id,task_id)
                VALUES(dt.current_task_hub(),@instance,@execution,1) RETURNING sequence_number;
            """, setup))
        {
            task.Parameters.AddWithValue("instance", instance);
            task.Parameters.AddWithValue("execution", execution);
            sequence = (long)(await task.ExecuteScalarAsync())!;
        }
        try
        {
            var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var arrived = 0;
            async Task<bool> Complete()
            {
                await using var connection = new NpgsqlConnection(target.ConnectionString);
                await connection.OpenAsync();
                if (Interlocked.Increment(ref arrived) == 2) start.TrySetResult();
                await start.Task.WaitAsync(TimeSpan.FromSeconds(5));
                await using var complete = new NpgsqlCommand("""
                    SELECT dt.complete_tasks(ARRAY[@sequence]::BIGINT[],
                        ARRAY[ROW(@instance,@execution,'ReviewRace','TaskCompleted',1,
                            NULL::TIMESTAMPTZ,'{}'::JSONB,@payload,NULL,NULL)::dt.task_result]);
                    """, connection);
                complete.Parameters.AddWithValue("sequence", sequence);
                complete.Parameters.AddWithValue("instance", instance);
                complete.Parameters.AddWithValue("execution", execution);
                complete.Parameters.AddWithValue("payload", Guid.NewGuid());
                try
                {
                    ((long[])(await complete.ExecuteScalarAsync())!).Should().Equal(sequence);
                    return true;
                }
                catch (PostgresException error) when (error.SqlState == "40001") { return false; }
            }
            var results = await Task.WhenAll(Complete(), Complete());
            results.Count(success => success).Should().Be(1);
            foreach (var table in new[] { "new_events", "payloads" })
            {
                await using var count = new NpgsqlCommand(
                    $"SELECT count(*) FROM dt.{table} WHERE task_hub=dt.current_task_hub() AND instance_id=@instance", setup);
                count.Parameters.AddWithValue("instance", instance);
                ((long)(await count.ExecuteScalarAsync())!).Should().Be(1);
            }
        }
        finally
        {
            await using var cleanup = new NpgsqlCommand("""
                DELETE FROM dt.new_events WHERE task_hub=dt.current_task_hub() AND instance_id=@instance;
                DELETE FROM dt.new_tasks WHERE task_hub=dt.current_task_hub() AND instance_id=@instance;
                DELETE FROM dt.payloads WHERE task_hub=dt.current_task_hub() AND instance_id=@instance;
                DELETE FROM dt.instances WHERE task_hub=dt.current_task_hub() AND instance_id=@instance;
                """, setup);
            cleanup.Parameters.AddWithValue("instance", instance);
            await cleanup.ExecuteNonQueryAsync();
        }
    }

    [ReviewSessionPostgresFact]
    public async Task FreshSchemaMatchesEfAndCompletesAtomicTaskBatches()
    {
        var target = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("AGENTIC_REVIEW_POSTGRES"));
        if (target.Host != "127.0.0.1" || target.Port != 55432 || target.Username != "validation" ||
            !target.Database!.StartsWith("review_pr152_", StringComparison.Ordinal))
            throw new InvalidOperationException("Requires exclusive review database on Compose port55432.");
        var options = new DbContextOptionsBuilder<AgenticDbContext>()
            .UseNpgsql(target.ConnectionString, postgres =>
            {
                postgres.UseVector();
                postgres.MigrationsHistoryTable("__ef_migrations_history");
            }).Options;
        await using var db = new AgenticDbContext(options, new TenantContextAccessor());
        await db.Database.MigrateAsync();
        (await db.Database.GetPendingMigrationsAsync()).Should().BeEmpty();
        var tables = db.Model.GetRelationalModel().Tables;
        await using var connection = new NpgsqlConnection(target.ConnectionString);
        await connection.OpenAsync();
        foreach (var table in tables)
        {
            await using var columns = new NpgsqlCommand("""
                SELECT column_name FROM information_schema.columns
                WHERE table_schema=@schema AND table_name=@table
                """, connection);
            columns.Parameters.AddWithValue("schema", table.Schema ?? "public");
            columns.Parameters.AddWithValue("table", table.Name);
            var actual = new List<string>();
            await using (var reader = await columns.ExecuteReaderAsync())
                while (await reader.ReadAsync()) actual.Add(reader.GetString(0));
            if (table.Columns.Any(column => column.Name == "xmin"))
            {
                // PostgreSQL system columns are real but excluded by information_schema.
                await using var systemColumn = new NpgsqlCommand(
                    "SELECT attname FROM pg_attribute WHERE attrelid=to_regclass(@relation) AND attname='xmin'", connection);
                systemColumn.Parameters.AddWithValue("relation", $"\"{table.Schema ?? "public"}\".\"{table.Name}\"");
                actual.Add((string)(await systemColumn.ExecuteScalarAsync())!);
            }
            actual.Should().BeEquivalentTo(table.Columns.Select(column => column.Name),
                $"table {table.Name} must match every EF column");
        }
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "tests", "backend-validation", "durabletask-completion.sql")))
            root = root.Parent;
        root.Should().NotBeNull();
        var sql = await File.ReadAllTextAsync(Path.Combine(root!.FullName, "tests", "backend-validation", "durabletask-completion.sql"));
        await using var probe = new NpgsqlCommand(sql, connection);
        await probe.ExecuteNonQueryAsync();
    }
}
