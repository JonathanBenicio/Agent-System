using AgenticSystem.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgenticSystem.Infrastructure.Persistence.Configurations;

public sealed class ChatSettingsConfiguration : IEntityTypeConfiguration<ChatSettingsEntity>
{
    public void Configure(EntityTypeBuilder<ChatSettingsEntity> builder)
    {
        builder.ToTable("chat_settings");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasColumnName("id").HasMaxLength(32);
        builder.Property(item => item.TenantId).HasColumnName("tenant_id").HasMaxLength(128).IsRequired();
        builder.Property(item => item.UserId).HasColumnName("user_id").HasMaxLength(256).IsRequired();
        builder.Property(item => item.Provider).HasColumnName("provider").HasMaxLength(100).IsRequired();
        builder.Property(item => item.Model).HasColumnName("model").HasMaxLength(256).IsRequired();
        builder.Property(item => item.UpdatedAt).HasColumnName("updated_at");
        builder.HasIndex(item => new { item.TenantId, item.UserId }).IsUnique();
    }
}
