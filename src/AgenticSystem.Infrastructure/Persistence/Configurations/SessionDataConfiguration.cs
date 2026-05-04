using System.Text.Json;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgenticSystem.Infrastructure.Persistence.Configurations;

public class SessionDataConfiguration : IEntityTypeConfiguration<SessionData>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public void Configure(EntityTypeBuilder<SessionData> builder)
    {
        builder.ToTable("sessions");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id").HasMaxLength(128);
        builder.Property(s => s.UserId).HasColumnName("user_id").HasMaxLength(128).IsRequired();
        builder.Property(s => s.TenantId).HasColumnName("tenant_id").HasMaxLength(128).IsRequired();
        builder.Property(s => s.StartedAt).HasColumnName("started_at");
        builder.Property(s => s.EndedAt).HasColumnName("ended_at");
        builder.Property(s => s.IsConsolidated).HasColumnName("is_consolidated");

        // Complex types stored as JSONB columns with explicit conversion
        builder.Property(s => s.Events)
            .HasColumnName("events")
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => JsonSerializer.Deserialize<List<AgentEvent>>(v, JsonOptions) ?? new List<AgentEvent>(),
                new ValueComparer<List<AgentEvent>>(
                    (c1, c2) => JsonSerializer.Serialize(c1, JsonOptions) == JsonSerializer.Serialize(c2, JsonOptions),
                    c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                    c => JsonSerializer.Deserialize<List<AgentEvent>>(JsonSerializer.Serialize(c, JsonOptions), JsonOptions)!));

        builder.Property(s => s.Summary)
            .HasColumnName("summary")
            .HasColumnType("jsonb")
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, JsonOptions),
                v => v == null ? null : JsonSerializer.Deserialize<SessionSummary>(v, JsonOptions));

        builder.Property(s => s.Insights)
            .HasColumnName("insights")
            .HasColumnType("jsonb")
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, JsonOptions),
                v => v == null ? null : JsonSerializer.Deserialize<SessionInsights>(v, JsonOptions));

        // Indexes
        builder.HasIndex(s => s.UserId).HasDatabaseName("ix_sessions_user_id");
        builder.HasIndex(s => s.TenantId).HasDatabaseName("ix_sessions_tenant_id");
        builder.HasIndex(s => new { s.TenantId, s.UserId }).HasDatabaseName("ix_sessions_tenant_user");
    }
}
