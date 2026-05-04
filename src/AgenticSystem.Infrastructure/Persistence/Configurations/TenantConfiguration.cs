using System.Text.Json;
using AgenticSystem.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgenticSystem.Infrastructure.Persistence.Configurations;

public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("tenants");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("id").HasMaxLength(128);
        builder.Property(t => t.Name).HasColumnName("name").HasMaxLength(256).IsRequired();
        builder.Property(t => t.Slug).HasColumnName("slug").HasMaxLength(128).IsRequired();
        builder.Property(t => t.Plan).HasColumnName("plan").HasConversion<string>().HasMaxLength(32);
        builder.Property(t => t.IsActive).HasColumnName("is_active");
        builder.Property(t => t.CreatedAt).HasColumnName("created_at");
        builder.Property(t => t.UpdatedAt).HasColumnName("updated_at");

        // Complex types stored as JSONB with explicit conversion
        builder.Property(t => t.Limits)
            .HasColumnName("limits")
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => JsonSerializer.Deserialize<TenantLimits>(v, JsonOptions) ?? new TenantLimits());

        builder.Property(t => t.ProviderApiKeys)
            .HasColumnName("provider_api_keys")
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, JsonOptions) ?? new Dictionary<string, string>(),
                new ValueComparer<Dictionary<string, string>>(
                    (c1, c2) => JsonSerializer.Serialize(c1, JsonOptions) == JsonSerializer.Serialize(c2, JsonOptions),
                    c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                    c => JsonSerializer.Deserialize<Dictionary<string, string>>(JsonSerializer.Serialize(c, JsonOptions), JsonOptions)!));

        builder.Property(t => t.Settings)
            .HasColumnName("settings")
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => JsonSerializer.Deserialize<Dictionary<string, object>>(v, JsonOptions) ?? new Dictionary<string, object>(),
                new ValueComparer<Dictionary<string, object>>(
                    (c1, c2) => JsonSerializer.Serialize(c1, JsonOptions) == JsonSerializer.Serialize(c2, JsonOptions),
                    c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                    c => JsonSerializer.Deserialize<Dictionary<string, object>>(JsonSerializer.Serialize(c, JsonOptions), JsonOptions)!));

        // Indexes
        builder.HasIndex(t => t.Slug).IsUnique().HasDatabaseName("ix_tenants_slug");
        builder.HasIndex(t => t.IsActive).HasDatabaseName("ix_tenants_is_active");
    }
}
