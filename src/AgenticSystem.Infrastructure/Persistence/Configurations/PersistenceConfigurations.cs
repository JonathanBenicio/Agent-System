using AgenticSystem.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgenticSystem.Infrastructure.Persistence.Configurations;

public class VectorDocumentConfiguration : IEntityTypeConfiguration<VectorDocumentEntity>
{
    public void Configure(EntityTypeBuilder<VectorDocumentEntity> builder)
    {
        builder.ToTable("vector_documents");

        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).HasColumnName("id").HasMaxLength(256);
        builder.Property(v => v.Content).HasColumnName("content").IsRequired();
        builder.Property(v => v.Type).HasColumnName("type").HasMaxLength(64).IsRequired();
        builder.Property(v => v.Collection).HasColumnName("collection").HasMaxLength(128).IsRequired();
        builder.Property(v => v.MetadataJson).HasColumnName("metadata").HasColumnType("jsonb");
        builder.Property(v => v.IndexedAt).HasColumnName("indexed_at");

        // Utilizando a extensão nativa pgvector com dimensão dinâmica (ideal para modelos locais Ollama)
        builder.Property(v => v.Embedding).HasColumnName("embedding").HasColumnType("vector");

        builder.HasIndex(v => v.Collection).HasDatabaseName("ix_vector_documents_collection");
        builder.HasIndex(v => v.Type).HasDatabaseName("ix_vector_documents_type");
        builder.HasIndex(v => v.IndexedAt).HasDatabaseName("ix_vector_documents_indexed_at");
    }
}

public class CostEntryConfiguration : IEntityTypeConfiguration<CostEntryEntity>
{
    public void Configure(EntityTypeBuilder<CostEntryEntity> builder)
    {
        builder.ToTable("cost_entries");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(c => c.ServiceName).HasColumnName("service_name").HasMaxLength(256).IsRequired();
        builder.Property(c => c.Category).HasColumnName("category").HasMaxLength(128).IsRequired();
        builder.Property(c => c.TenantId).HasColumnName("tenant_id").HasMaxLength(128).IsRequired();
        builder.Property(c => c.Cost).HasColumnName("cost").HasColumnType("numeric(18,6)");
        builder.Property(c => c.RecordedAt).HasColumnName("recorded_at");

        builder.HasIndex(c => c.ServiceName).HasDatabaseName("ix_cost_entries_service_name");
        builder.HasIndex(c => c.TenantId).HasDatabaseName("ix_cost_entries_tenant_id");
        builder.HasIndex(c => c.RecordedAt).HasDatabaseName("ix_cost_entries_recorded_at");
        builder.HasIndex(c => new { c.TenantId, c.ServiceName, c.RecordedAt })
            .HasDatabaseName("ix_cost_entries_tenant_service_date");
    }
}

public class CostBudgetConfiguration : IEntityTypeConfiguration<CostBudgetEntity>
{
    public void Configure(EntityTypeBuilder<CostBudgetEntity> builder)
    {
        builder.ToTable("cost_budgets");

        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).HasColumnName("id").HasMaxLength(384);
        builder.Property(b => b.ServiceName).HasColumnName("service_name").HasMaxLength(256).IsRequired();
        builder.Property(b => b.TenantId).HasColumnName("tenant_id").HasMaxLength(128).IsRequired();
        builder.Property(b => b.DailyBudget).HasColumnName("daily_budget").HasColumnType("numeric(18,6)");
        builder.Property(b => b.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(b => new { b.TenantId, b.ServiceName })
            .IsUnique()
            .HasDatabaseName("ix_cost_budgets_tenant_service");
    }
}

public class AgentPerformanceMetricConfiguration : IEntityTypeConfiguration<AgentPerformanceMetricEntity>
{
    public void Configure(EntityTypeBuilder<AgentPerformanceMetricEntity> builder)
    {
        builder.ToTable("agent_performance_metrics");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(m => m.AgentName).HasColumnName("agent_name").HasMaxLength(256).IsRequired();
        builder.Property(m => m.Domain).HasColumnName("domain").HasMaxLength(128).IsRequired();
        builder.Property(m => m.LatencyMs).HasColumnName("latency_ms");
        builder.Property(m => m.Success).HasColumnName("success");
        builder.Property(m => m.UserSatisfaction).HasColumnName("user_satisfaction");
        builder.Property(m => m.RecordedAt).HasColumnName("recorded_at");

        builder.HasIndex(m => m.AgentName).HasDatabaseName("ix_agent_metrics_agent_name");
        builder.HasIndex(m => m.Domain).HasDatabaseName("ix_agent_metrics_domain");
        builder.HasIndex(m => new { m.AgentName, m.Domain })
            .HasDatabaseName("ix_agent_metrics_agent_domain");
    }
}

public class RuntimeArtifactConfiguration : IEntityTypeConfiguration<RuntimeArtifactEntity>
{
    public void Configure(EntityTypeBuilder<RuntimeArtifactEntity> builder)
    {
        builder.ToTable("runtime_artifacts");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("id").HasMaxLength(64);
        builder.Property(a => a.SessionId).HasColumnName("session_id").HasMaxLength(128).IsRequired();
        builder.Property(a => a.Type).HasColumnName("type").HasMaxLength(64).IsRequired();
        builder.Property(a => a.Name).HasColumnName("name").HasMaxLength(256).IsRequired();
        builder.Property(a => a.AgentName).HasColumnName("agent_name").HasMaxLength(256);
        builder.Property(a => a.Status).HasColumnName("status").HasMaxLength(64).IsRequired();
        builder.Property(a => a.Summary).HasColumnName("summary");
        builder.Property(a => a.DataJson).HasColumnName("data").HasColumnType("jsonb");
        builder.Property(a => a.RelatedIdsJson).HasColumnName("related_ids").HasColumnType("jsonb");
        builder.Property(a => a.CreatedAt).HasColumnName("created_at");

        builder.HasIndex(a => a.SessionId).HasDatabaseName("ix_runtime_artifacts_session_id");
        builder.HasIndex(a => a.Type).HasDatabaseName("ix_runtime_artifacts_type");
        builder.HasIndex(a => a.CreatedAt).HasDatabaseName("ix_runtime_artifacts_created_at");
        builder.HasIndex(a => new { a.SessionId, a.Type }).HasDatabaseName("ix_runtime_artifacts_session_type");
    }
}

public class RuntimeMetricsSnapshotConfiguration : IEntityTypeConfiguration<RuntimeMetricsSnapshotEntity>
{
    public void Configure(EntityTypeBuilder<RuntimeMetricsSnapshotEntity> builder)
    {
        builder.ToTable("runtime_metrics_snapshots");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(m => m.SessionId).HasColumnName("session_id").HasMaxLength(128);
        builder.Property(m => m.StreamCount).HasColumnName("stream_count");
        builder.Property(m => m.AgentExecutions).HasColumnName("agent_executions");
        builder.Property(m => m.AgentFallbacks).HasColumnName("agent_fallbacks");
        builder.Property(m => m.ToolExecutions).HasColumnName("tool_executions");
        builder.Property(m => m.ToolApprovalsRequested).HasColumnName("tool_approvals_requested");
        builder.Property(m => m.ToolApprovalsResolved).HasColumnName("tool_approvals_resolved");
        builder.Property(m => m.FinalApprovalsRequested).HasColumnName("final_approvals_requested");
        builder.Property(m => m.FinalApprovalsResolved).HasColumnName("final_approvals_resolved");
        builder.Property(m => m.Handoffs).HasColumnName("handoffs");
        builder.Property(m => m.RagQueries).HasColumnName("rag_queries");
        builder.Property(m => m.Reviews).HasColumnName("reviews");
        builder.Property(m => m.AverageAgentLatencyMs).HasColumnName("avg_agent_latency_ms");
        builder.Property(m => m.AverageToolLatencyMs).HasColumnName("avg_tool_latency_ms");
        builder.Property(m => m.EventsByTypeJson).HasColumnName("events_by_type").HasColumnType("jsonb");
        builder.Property(m => m.AgentExecutionCountsJson).HasColumnName("agent_execution_counts").HasColumnType("jsonb");
        builder.Property(m => m.SnapshotAt).HasColumnName("snapshot_at");

        builder.HasIndex(m => m.SessionId).HasDatabaseName("ix_runtime_metrics_session_id");
        builder.HasIndex(m => m.SnapshotAt).HasDatabaseName("ix_runtime_metrics_snapshot_at");
    }
}

public class ReflectionEntityConfiguration : IEntityTypeConfiguration<ReflectionEntity>
{
    public void Configure(EntityTypeBuilder<ReflectionEntity> builder)
    {
        builder.ToTable("runtime_reflections");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id").HasMaxLength(64);
        builder.Property(r => r.SessionId).HasColumnName("session_id").HasMaxLength(128).IsRequired();
        builder.Property(r => r.AgentName).HasColumnName("agent_name").HasMaxLength(256).IsRequired();
        builder.Property(r => r.ActionTaken).HasColumnName("action_taken").IsRequired();
        builder.Property(r => r.Outcome).HasColumnName("outcome").IsRequired();
        builder.Property(r => r.ConfidenceInOutcome).HasColumnName("confidence");
        builder.Property(r => r.DeviationsJson).HasColumnName("deviations").HasColumnType("jsonb");
        builder.Property(r => r.LessonsLearnedJson).HasColumnName("lessons_learned").HasColumnType("jsonb");
        builder.Property(r => r.ImprovementSuggestion).HasColumnName("improvement_suggestion");
        builder.Property(r => r.Severity).HasColumnName("severity").HasMaxLength(32).IsRequired();
        builder.Property(r => r.CreatedAt).HasColumnName("created_at");

        builder.HasIndex(r => r.SessionId).HasDatabaseName("ix_runtime_reflections_session_id");
        builder.HasIndex(r => r.AgentName).HasDatabaseName("ix_runtime_reflections_agent_name");
        builder.HasIndex(r => r.CreatedAt).HasDatabaseName("ix_runtime_reflections_created_at");
    }
}

public class EvaluationScoreConfiguration : IEntityTypeConfiguration<EvaluationScoreEntity>
{
    public void Configure(EntityTypeBuilder<EvaluationScoreEntity> builder)
    {
        builder.ToTable("runtime_evaluations");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").HasMaxLength(64);
        builder.Property(e => e.SessionId).HasColumnName("session_id").HasMaxLength(128);
        builder.Property(e => e.AgentName).HasColumnName("agent_name").HasMaxLength(256);
        builder.Property(e => e.OverallScore).HasColumnName("overall_score");
        builder.Property(e => e.BaselineScore).HasColumnName("baseline_score");
        builder.Property(e => e.Threshold).HasColumnName("threshold");
        builder.Property(e => e.RegressionDetected).HasColumnName("regression_detected");
        builder.Property(e => e.FactorsJson).HasColumnName("factors").HasColumnType("jsonb");
        builder.Property(e => e.AlertsJson).HasColumnName("alerts").HasColumnType("jsonb");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");

        builder.HasIndex(e => e.AgentName).HasDatabaseName("ix_runtime_evaluations_agent_name");
        builder.HasIndex(e => e.CreatedAt).HasDatabaseName("ix_runtime_evaluations_created_at");
        builder.HasIndex(e => e.RegressionDetected).HasDatabaseName("ix_runtime_evaluations_regression");
    }
}

public class AgentMemoryConfiguration : IEntityTypeConfiguration<AgentMemoryEntity>
{
    public void Configure(EntityTypeBuilder<AgentMemoryEntity> builder)
    {
        builder.ToTable("agent_memories");

        builder.HasKey(memory => memory.Id);
        builder.Property(memory => memory.Id).HasColumnName("id").HasMaxLength(64);
        builder.Property(memory => memory.UserId).HasColumnName("user_id").HasMaxLength(256).IsRequired();
        builder.Property(memory => memory.AgentName).HasColumnName("agent_name").HasMaxLength(256).IsRequired();
        builder.Property(memory => memory.SessionId).HasColumnName("session_id").HasMaxLength(128);
        builder.Property(memory => memory.MemoryType).HasColumnName("memory_type").HasMaxLength(64).IsRequired();
        builder.Property(memory => memory.Content).HasColumnName("content").IsRequired();
        builder.Property(memory => memory.Confidence).HasColumnName("confidence");
        builder.Property(memory => memory.Source).HasColumnName("source").HasMaxLength(128).IsRequired();
        builder.Property(memory => memory.KeywordsJson).HasColumnName("keywords").HasColumnType("jsonb");
        builder.Property(memory => memory.MetadataJson).HasColumnName("metadata").HasColumnType("jsonb");
        builder.Property(memory => memory.CreatedAt).HasColumnName("created_at");
        builder.Property(memory => memory.LastUsedAt).HasColumnName("last_used_at");
        builder.Property(memory => memory.UsageCount).HasColumnName("usage_count");
        builder.Property(memory => memory.IsActive).HasColumnName("is_active");

        builder.HasIndex(memory => memory.UserId).HasDatabaseName("ix_agent_memories_user_id");
        builder.HasIndex(memory => memory.AgentName).HasDatabaseName("ix_agent_memories_agent_name");
        builder.HasIndex(memory => new { memory.UserId, memory.AgentName })
            .HasDatabaseName("ix_agent_memories_user_agent");
        builder.HasIndex(memory => memory.LastUsedAt).HasDatabaseName("ix_agent_memories_last_used_at");
    }
}

public class SessionRecordConfiguration : IEntityTypeConfiguration<SessionRecordEntity>
{
    public void Configure(EntityTypeBuilder<SessionRecordEntity> builder)
    {
        builder.ToTable("sessions");

        builder.HasKey(session => session.Id);
        builder.Property(session => session.Id).HasColumnName("id").HasMaxLength(128);
        builder.Property(session => session.UserId).HasColumnName("user_id").HasMaxLength(128).IsRequired();
        builder.Property(session => session.TenantId).HasColumnName("tenant_id").HasMaxLength(128).IsRequired();
        builder.Property(session => session.DataJson).HasColumnName("data").HasColumnType("jsonb").IsRequired();
        builder.Property(session => session.StartedAt).HasColumnName("started_at");
        builder.Property(session => session.EndedAt).HasColumnName("ended_at");
        builder.Property(session => session.IsConsolidated).HasColumnName("is_consolidated");

        builder.HasIndex(session => session.UserId).HasDatabaseName("ix_sessions_user_id");
        builder.HasIndex(session => session.TenantId).HasDatabaseName("ix_sessions_tenant_id");
        builder.HasIndex(session => session.StartedAt).HasDatabaseName("idx_sessions_started");
    }
}

public class ConfigEntryConfiguration : IEntityTypeConfiguration<ConfigEntryEntity>
{
    public void Configure(EntityTypeBuilder<ConfigEntryEntity> builder)
    {
        builder.ToTable("config_entries");

        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.Id).HasColumnName("id").HasMaxLength(128);
        builder.Property(entry => entry.TenantId).HasColumnName("tenant_id").HasMaxLength(128).IsRequired();
        builder.Property(entry => entry.Key).HasColumnName("key").HasMaxLength(256).IsRequired();
        builder.Property(entry => entry.Value).HasColumnName("value").IsRequired();
        builder.Property(entry => entry.EncryptedValue).HasColumnName("encrypted_value");
        builder.Property(entry => entry.IsSecret).HasColumnName("is_secret");
        builder.Property(entry => entry.Category).HasColumnName("category").HasMaxLength(64).IsRequired();
        builder.Property(entry => entry.Status).HasColumnName("status").HasMaxLength(64).IsRequired();
        builder.Property(entry => entry.Description).HasColumnName("description");
        builder.Property(entry => entry.Provider).HasColumnName("provider");
        builder.Property(entry => entry.CreatedAt).HasColumnName("created_at");
        builder.Property(entry => entry.UpdatedAt).HasColumnName("updated_at");
        builder.Property(entry => entry.ExpiresAt).HasColumnName("expires_at");
        builder.Property(entry => entry.MetadataJson).HasColumnName("metadata").HasColumnType("jsonb").IsRequired();

        // Make the Key unique per Tenant
        builder.HasIndex(entry => new { entry.TenantId, entry.Key }).IsUnique().HasDatabaseName("ix_config_entries_tenant_key");
        builder.HasIndex(entry => entry.Category).HasDatabaseName("ix_config_entries_category");
    }
}

public class ConfigChangeLogConfiguration : IEntityTypeConfiguration<ConfigChangeLogEntity>
{
    public void Configure(EntityTypeBuilder<ConfigChangeLogEntity> builder)
    {
        builder.ToTable("config_change_logs");

        builder.HasKey(log => log.Id);
        builder.Property(log => log.Id).HasColumnName("id").HasMaxLength(128);
        builder.Property(log => log.ConfigKey).HasColumnName("config_key").HasMaxLength(256).IsRequired();
        builder.Property(log => log.Action).HasColumnName("action").HasMaxLength(64).IsRequired();
        builder.Property(log => log.ChangedBy).HasColumnName("changed_by");
        builder.Property(log => log.ChangedAt).HasColumnName("changed_at");
        builder.Property(log => log.PreviousValueHash).HasColumnName("previous_value_hash");
        builder.Property(log => log.NewValueHash).HasColumnName("new_value_hash");

        builder.HasIndex(log => new { log.ConfigKey, log.ChangedAt })
            .HasDatabaseName("ix_config_change_logs_key_changed_at");
    }
}

public class ScheduledTaskConfiguration : IEntityTypeConfiguration<ScheduledTaskEntity>
{
    public void Configure(EntityTypeBuilder<ScheduledTaskEntity> builder)
    {
        builder.ToTable("scheduled_tasks");

        builder.HasKey(task => task.Id);
        builder.Property(task => task.Id).HasColumnName("id").HasMaxLength(128);
        builder.Property(task => task.Name).HasColumnName("name").IsRequired();
        builder.Property(task => task.Status).HasColumnName("status").HasMaxLength(64).IsRequired();
        builder.Property(task => task.NextRunAt).HasColumnName("next_run_at");
        builder.Property(task => task.PayloadJson).HasColumnName("payload").HasColumnType("jsonb").IsRequired();
        builder.Property(task => task.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(task => task.Status).HasDatabaseName("ix_scheduled_tasks_status");
    }
}

public class TriggerRuleConfiguration : IEntityTypeConfiguration<TriggerRuleEntity>
{
    public void Configure(EntityTypeBuilder<TriggerRuleEntity> builder)
    {
        builder.ToTable("trigger_rules");

        builder.HasKey(rule => rule.Id);
        builder.Property(rule => rule.Id).HasColumnName("id").HasMaxLength(128);
        builder.Property(rule => rule.Name).HasColumnName("name").IsRequired();
        builder.Property(rule => rule.Enabled).HasColumnName("enabled");
        builder.Property(rule => rule.PayloadJson).HasColumnName("payload").HasColumnType("jsonb").IsRequired();
        builder.Property(rule => rule.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(rule => rule.Enabled).HasDatabaseName("ix_trigger_rules_enabled");
    }
}

public class ScheduledTaskExecutionConfiguration : IEntityTypeConfiguration<ScheduledTaskExecutionEntity>
{
    public void Configure(EntityTypeBuilder<ScheduledTaskExecutionEntity> builder)
    {
        builder.ToTable("scheduled_task_executions");

        builder.HasKey(execution => execution.ExecutionId);
        builder.Property(execution => execution.ExecutionId).HasColumnName("execution_id").HasMaxLength(128);
        builder.Property(execution => execution.TaskId).HasColumnName("task_id").HasMaxLength(128).IsRequired();
        builder.Property(execution => execution.StartedAt).HasColumnName("started_at");
        builder.Property(execution => execution.CompletedAt).HasColumnName("completed_at");
        builder.Property(execution => execution.Success).HasColumnName("success");
        builder.Property(execution => execution.PayloadJson).HasColumnName("payload").HasColumnType("jsonb").IsRequired();

        builder.HasIndex(execution => execution.TaskId).HasDatabaseName("ix_scheduled_task_executions_task_id");
    }
}

public class EmbeddingModelConfiguration : IEntityTypeConfiguration<EmbeddingModelEntity>
{
    public void Configure(EntityTypeBuilder<EmbeddingModelEntity> builder)
    {
        builder.ToTable("embedding_models");

        builder.HasKey(model => model.Id);
        builder.Property(model => model.Id).HasColumnName("id").HasMaxLength(128);
        builder.Property(model => model.Name).HasColumnName("name").IsRequired();
        builder.Property(model => model.IsActive).HasColumnName("is_active");
        builder.Property(model => model.DataJson).HasColumnName("data").HasColumnType("jsonb").IsRequired();
        builder.Property(model => model.CreatedAt).HasColumnName("created_at");

        builder.HasIndex(model => model.Name).HasDatabaseName("idx_embedding_models_name");
        builder.HasIndex(model => model.IsActive).HasDatabaseName("idx_embedding_models_active");
    }
}

public class MigrationJobConfiguration : IEntityTypeConfiguration<MigrationJobEntity>
{
    public void Configure(EntityTypeBuilder<MigrationJobEntity> builder)
    {
        builder.ToTable("migration_jobs");

        builder.HasKey(job => job.Id);
        builder.Property(job => job.Id).HasColumnName("id").HasMaxLength(128);
        builder.Property(job => job.Status).HasColumnName("status").HasMaxLength(64).IsRequired();
        builder.Property(job => job.DataJson).HasColumnName("data").HasColumnType("jsonb").IsRequired();
        builder.Property(job => job.CreatedAt).HasColumnName("created_at");

        builder.HasIndex(job => job.Status).HasDatabaseName("idx_migration_jobs_status");
        builder.HasIndex(job => job.CreatedAt).HasDatabaseName("idx_migration_jobs_created");
    }
}

public class RerankingAssetConfiguration : IEntityTypeConfiguration<RerankingAssetEntity>
{
    public void Configure(EntityTypeBuilder<RerankingAssetEntity> builder)
    {
        builder.ToTable("reranking_assets");

        builder.HasKey(asset => new { asset.TenantId, asset.AssetType });
        builder.Property(asset => asset.TenantId).HasColumnName("tenant_id").HasMaxLength(128);
        builder.Property(asset => asset.AssetType).HasColumnName("asset_type").HasMaxLength(64);
        builder.Property(asset => asset.FileName).HasColumnName("file_name").IsRequired();
        builder.Property(asset => asset.ContentType).HasColumnName("content_type").IsRequired();
        builder.Property(asset => asset.Content).HasColumnName("content").IsRequired();
        builder.Property(asset => asset.ContentHash).HasColumnName("content_hash").HasMaxLength(128).IsRequired();
        builder.Property(asset => asset.UpdatedAt).HasColumnName("updated_at");
    }
}

public class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntryEntity>
{
    public void Configure(EntityTypeBuilder<AuditEntryEntity> builder)
    {
        builder.ToTable("audit_entries");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("id").HasMaxLength(64);
        builder.Property(a => a.Timestamp).HasColumnName("timestamp");
        builder.Property(a => a.Category).HasColumnName("category").HasMaxLength(64).IsRequired();
        builder.Property(a => a.Action).HasColumnName("action").HasMaxLength(256).IsRequired();
        builder.Property(a => a.UserId).HasColumnName("user_id").HasMaxLength(128);
        builder.Property(a => a.TenantId).HasColumnName("tenant_id").HasMaxLength(128);
        builder.Property(a => a.SessionId).HasColumnName("session_id").HasMaxLength(128);
        builder.Property(a => a.AgentName).HasColumnName("agent_name").HasMaxLength(128);
        builder.Property(a => a.ToolName).HasColumnName("tool_name").HasMaxLength(128);
        builder.Property(a => a.ModelUsed).HasColumnName("model_used").HasMaxLength(128);
        builder.Property(a => a.Cost).HasColumnName("cost");
        builder.Property(a => a.TraceId).HasColumnName("trace_id").HasMaxLength(128);
        builder.Property(a => a.Description).HasColumnName("description");
        builder.Property(a => a.Success).HasColumnName("success").IsRequired();
        builder.Property(a => a.ErrorMessage).HasColumnName("error_message");
        builder.Property(a => a.DetailsJson).HasColumnName("details").HasColumnType("jsonb").IsRequired();

        builder.HasIndex(a => a.TenantId).HasDatabaseName("ix_audit_entries_tenant_id");
        builder.HasIndex(a => a.UserId).HasDatabaseName("ix_audit_entries_user_id");
        builder.HasIndex(a => a.Timestamp).HasDatabaseName("ix_audit_entries_timestamp");
    }
}

public class RoleAssignmentConfiguration : IEntityTypeConfiguration<RoleAssignmentEntity>
{
    public void Configure(EntityTypeBuilder<RoleAssignmentEntity> builder)
    {
        builder.ToTable("role_assignments");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id").HasMaxLength(64);
        builder.Property(r => r.UserId).HasColumnName("user_id").HasMaxLength(128).IsRequired();
        builder.Property(r => r.RoleId).HasColumnName("role_id").HasMaxLength(64).IsRequired();
        builder.Property(r => r.TenantId).HasColumnName("tenant_id").HasMaxLength(128);
        builder.Property(r => r.GrantedBy).HasColumnName("granted_by").HasMaxLength(128);
        builder.Property(r => r.GrantedAt).HasColumnName("granted_at");

        builder.HasIndex(r => new { r.UserId, r.RoleId, r.TenantId }).IsUnique().HasDatabaseName("ix_role_assignments_unique");
        builder.HasIndex(r => r.TenantId).HasDatabaseName("ix_role_assignments_tenant_id");
    }
}

public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessageEntity>
{
    public void Configure(EntityTypeBuilder<OutboxMessageEntity> builder)
    {
        builder.ToTable("outbox_messages");

        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).HasColumnName("id");
        builder.Property(o => o.EventType).HasColumnName("event_type").HasMaxLength(256).IsRequired();
        builder.Property(o => o.PayloadJson).HasColumnName("payload").HasColumnType("jsonb").IsRequired();
        builder.Property(o => o.CreatedAt).HasColumnName("created_at");
        builder.Property(o => o.ProcessedAt).HasColumnName("processed_at");
        builder.Property(o => o.Error).HasColumnName("error");

        builder.HasIndex(o => o.ProcessedAt).HasDatabaseName("ix_outbox_messages_processed_at");
        builder.HasIndex(o => o.CreatedAt).HasDatabaseName("ix_outbox_messages_created_at");
    }
}

public class AgentVersionConfiguration : IEntityTypeConfiguration<AgentVersionEntity>
{
    public void Configure(EntityTypeBuilder<AgentVersionEntity> builder)
    {
        builder.ToTable("agent_versions");

        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).HasColumnName("id").HasMaxLength(64);
        builder.Property(v => v.AgentName).HasColumnName("agent_name").HasMaxLength(256).IsRequired();
        builder.Property(v => v.VersionNumber).HasColumnName("version_number").IsRequired();
        builder.Property(v => v.Label).HasColumnName("label").HasMaxLength(128);
        builder.Property(v => v.Status).HasColumnName("status").IsRequired();
        builder.Property(v => v.Environment).HasColumnName("environment").IsRequired();
        builder.Property(v => v.SystemPrompt).HasColumnName("system_prompt").IsRequired();
        builder.Property(v => v.ModelProvider).HasColumnName("model_provider").HasMaxLength(128);
        builder.Property(v => v.ModelId).HasColumnName("model_id").HasMaxLength(128);
        builder.Property(v => v.Tools).HasColumnName("tools").HasColumnType("text[]").IsRequired();
        builder.Property(v => v.PolicySnapshotJson).HasColumnName("policy_snapshot").HasColumnType("jsonb");
        builder.Property(v => v.ParametersJson).HasColumnName("parameters").HasColumnType("jsonb").IsRequired();
        builder.Property(v => v.Description).HasColumnName("description");
        builder.Property(v => v.ChangeLog).HasColumnName("change_log");
        builder.Property(v => v.CreatedBy).HasColumnName("created_by").HasMaxLength(128);
        builder.Property(v => v.CreatedAt).HasColumnName("created_at");
        builder.Property(v => v.PromotedAt).HasColumnName("promoted_at");
        builder.Property(v => v.PromotedBy).HasColumnName("promoted_by").HasMaxLength(128);
        builder.Property(v => v.ConfigHash).HasColumnName("config_hash").HasMaxLength(128);
        builder.Property(v => v.ParentVersionId).HasColumnName("parent_version_id").HasMaxLength(64);

        builder.HasIndex(v => v.AgentName).HasDatabaseName("ix_agent_versions_agent_name");
        builder.HasIndex(v => new { v.AgentName, v.VersionNumber }).IsUnique().HasDatabaseName("ix_agent_versions_agent_version_unique");
    }
}

public class PromptTemplateConfiguration : IEntityTypeConfiguration<PromptTemplateEntity>
{
    public void Configure(EntityTypeBuilder<PromptTemplateEntity> builder)
    {
        builder.ToTable("prompt_templates");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id").HasMaxLength(64);
        builder.Property(p => p.Name).HasColumnName("name").HasMaxLength(256).IsRequired();
        builder.Property(p => p.AgentName).HasColumnName("agent_name").HasMaxLength(256).IsRequired();
        builder.Property(p => p.TemplateBody).HasColumnName("template_body").IsRequired();
        builder.Property(p => p.Version).HasColumnName("version").IsRequired();
        builder.Property(p => p.Locale).HasColumnName("locale").HasMaxLength(16).IsRequired();
        builder.Property(p => p.Variables).HasColumnName("variables").HasColumnType("text[]").IsRequired();
        builder.Property(p => p.Description).HasColumnName("description");
        builder.Property(p => p.IsActive).HasColumnName("is_active");
        builder.Property(p => p.CreatedAt).HasColumnName("created_at");
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at");
        builder.Property(p => p.CreatedBy).HasColumnName("created_by").HasMaxLength(128);

        builder.HasIndex(p => p.AgentName).HasDatabaseName("ix_prompt_templates_agent_name");
        builder.HasIndex(p => new { p.AgentName, p.Locale, p.IsActive }).HasDatabaseName("ix_prompt_templates_agent_locale_active");
    }
}

public class EvalSuiteResultConfiguration : IEntityTypeConfiguration<EvalSuiteResultEntity>
{
    public void Configure(EntityTypeBuilder<EvalSuiteResultEntity> builder)
    {
        builder.ToTable("eval_suite_results");

        builder.HasKey(e => e.SuiteId);
        builder.Property(e => e.SuiteId).HasColumnName("suite_id").HasMaxLength(64);
        builder.Property(e => e.AgentName).HasColumnName("agent_name").HasMaxLength(256).IsRequired();
        builder.Property(e => e.AgentVersionId).HasColumnName("agent_version_id").HasMaxLength(64);
        builder.Property(e => e.TotalTests).HasColumnName("total_tests").IsRequired();
        builder.Property(e => e.Passed).HasColumnName("passed").IsRequired();
        builder.Property(e => e.Failed).HasColumnName("failed").IsRequired();
        builder.Property(e => e.OverallScore).HasColumnName("overall_score").IsRequired();
        builder.Property(e => e.AccuracyScore).HasColumnName("accuracy_score").IsRequired();
        builder.Property(e => e.SafetyScore).HasColumnName("safety_score").IsRequired();
        builder.Property(e => e.LatencyP50Ms).HasColumnName("latency_p50_ms").IsRequired();
        builder.Property(e => e.LatencyP95Ms).HasColumnName("latency_p95_ms").IsRequired();
        builder.Property(e => e.TotalTokensUsed).HasColumnName("total_tokens_used").IsRequired();
        builder.Property(e => e.ResultsJson).HasColumnName("results").HasColumnType("jsonb").IsRequired();
        builder.Property(e => e.RegressionsJson).HasColumnName("regressions").HasColumnType("jsonb").IsRequired();
        builder.Property(e => e.StartedAt).HasColumnName("started_at");
        builder.Property(e => e.CompletedAt).HasColumnName("completed_at");

        builder.HasIndex(e => e.AgentName).HasDatabaseName("ix_eval_suite_results_agent_name");
        builder.HasIndex(e => e.StartedAt).HasDatabaseName("ix_eval_suite_results_started_at");
    }
}

public class KnowledgeGraphNodeConfiguration : IEntityTypeConfiguration<KnowledgeGraphNodeEntity>
{
    public void Configure(EntityTypeBuilder<KnowledgeGraphNodeEntity> builder)
    {
        builder.ToTable("knowledge_graph_nodes");

        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id).HasColumnName("id").HasMaxLength(64);
        builder.Property(n => n.Label).HasColumnName("label").HasMaxLength(256).IsRequired();
        builder.Property(n => n.EntityType).HasColumnName("entity_type").HasMaxLength(128).IsRequired();
        builder.Property(n => n.Description).HasColumnName("description");
        builder.Property(n => n.PropertiesJson).HasColumnName("properties").HasColumnType("jsonb").IsRequired();
        builder.Property(n => n.SourceDocumentId).HasColumnName("source_document_id").HasMaxLength(256);
        builder.Property(n => n.CreatedAt).HasColumnName("created_at");

        builder.HasIndex(n => n.Label).HasDatabaseName("ix_knowledge_graph_nodes_label");
        builder.HasIndex(n => n.EntityType).HasDatabaseName("ix_knowledge_graph_nodes_entity_type");
        builder.HasIndex(n => n.SourceDocumentId).HasDatabaseName("ix_knowledge_graph_nodes_source_document");
    }
}

public class KnowledgeGraphEdgeConfiguration : IEntityTypeConfiguration<KnowledgeGraphEdgeEntity>
{
    public void Configure(EntityTypeBuilder<KnowledgeGraphEdgeEntity> builder)
    {
        builder.ToTable("knowledge_graph_edges");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").HasMaxLength(64);
        builder.Property(e => e.SourceNodeId).HasColumnName("source_node_id").HasMaxLength(64).IsRequired();
        builder.Property(e => e.TargetNodeId).HasColumnName("target_node_id").HasMaxLength(64).IsRequired();
        builder.Property(e => e.RelationType).HasColumnName("relation_type").HasMaxLength(128).IsRequired();
        builder.Property(e => e.Weight).HasColumnName("weight");
        builder.Property(e => e.PropertiesJson).HasColumnName("properties").HasColumnType("jsonb").IsRequired();
        builder.Property(e => e.SourceDocumentId).HasColumnName("source_document_id").HasMaxLength(256);
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");

        builder.HasIndex(e => e.SourceNodeId).HasDatabaseName("ix_knowledge_graph_edges_source_node");
        builder.HasIndex(e => e.TargetNodeId).HasDatabaseName("ix_knowledge_graph_edges_target_node");
        builder.HasIndex(e => e.RelationType).HasDatabaseName("ix_knowledge_graph_edges_relation_type");
        builder.HasIndex(e => new { e.SourceNodeId, e.TargetNodeId, e.RelationType }).HasDatabaseName("ix_knowledge_graph_edges_unique_relation").IsUnique();
    }
}

public class WorkflowDefinitionConfiguration : IEntityTypeConfiguration<WorkflowDefinitionEntity>
{
    public void Configure(EntityTypeBuilder<WorkflowDefinitionEntity> builder)
    {
        builder.ToTable("workflow_definitions");
        builder.HasKey(w => w.Id);
        builder.Property(w => w.Id).HasColumnName("id").HasMaxLength(64);
        builder.Property(w => w.Name).HasColumnName("name").HasMaxLength(256).IsRequired();
        builder.Property(w => w.Version).HasColumnName("version");
        builder.Property(w => w.DefinitionJson).HasColumnName("definition").HasColumnType("jsonb").IsRequired();
        builder.Property(w => w.CreatedAt).HasColumnName("created_at");

        builder.HasIndex(w => w.Name).HasDatabaseName("ix_workflow_definitions_name");
    }
}

public class WorkflowExecutionConfiguration : IEntityTypeConfiguration<WorkflowExecutionEntity>
{
    public void Configure(EntityTypeBuilder<WorkflowExecutionEntity> builder)
    {
        builder.ToTable("workflow_executions");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").HasMaxLength(64);
        builder.Property(e => e.WorkflowId).HasColumnName("workflow_id").HasMaxLength(64).IsRequired();
        builder.Property(e => e.WorkflowName).HasColumnName("workflow_name").HasMaxLength(256).IsRequired();
        builder.Property(e => e.Status).HasColumnName("status").HasMaxLength(64).IsRequired();
        builder.Property(e => e.VariablesJson).HasColumnName("variables").HasColumnType("jsonb").IsRequired();
        builder.Property(e => e.InitiatedBy).HasColumnName("initiated_by").HasMaxLength(128);
        builder.Property(e => e.ErrorMessage).HasColumnName("error_message");
        builder.Property(e => e.StartedAt).HasColumnName("started_at");
        builder.Property(e => e.CompletedAt).HasColumnName("completed_at");

        builder.HasIndex(e => e.WorkflowId).HasDatabaseName("ix_workflow_executions_workflow_id");
        builder.HasIndex(e => e.Status).HasDatabaseName("ix_workflow_executions_status");
        builder.HasIndex(e => e.StartedAt).HasDatabaseName("ix_workflow_executions_started_at");
    }
}

public class WorkflowStepExecutionConfiguration : IEntityTypeConfiguration<WorkflowStepExecutionEntity>
{
    public void Configure(EntityTypeBuilder<WorkflowStepExecutionEntity> builder)
    {
        builder.ToTable("workflow_step_executions");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id").HasMaxLength(64);
        builder.Property(s => s.ExecutionId).HasColumnName("execution_id").HasMaxLength(64).IsRequired();
        builder.Property(s => s.StepId).HasColumnName("step_id").HasMaxLength(64).IsRequired();
        builder.Property(s => s.StepName).HasColumnName("step_name").HasMaxLength(256).IsRequired();
        builder.Property(s => s.Status).HasColumnName("status").HasMaxLength(64).IsRequired();
        builder.Property(s => s.OutputJson).HasColumnName("output").HasColumnType("jsonb").IsRequired();
        builder.Property(s => s.ErrorMessage).HasColumnName("error_message");
        builder.Property(s => s.RetryCount).HasColumnName("retry_count");
        builder.Property(s => s.CompensationExecuted).HasColumnName("compensation_executed");
        builder.Property(s => s.StartedAt).HasColumnName("started_at");
        builder.Property(s => s.CompletedAt).HasColumnName("completed_at");

        builder.HasIndex(s => s.ExecutionId).HasDatabaseName("ix_workflow_step_executions_execution_id");
        builder.HasIndex(s => new { s.ExecutionId, s.StepId }).IsUnique().HasDatabaseName("ix_workflow_step_executions_unique_step");
    }
}

public class ModelPerformanceConfiguration : IEntityTypeConfiguration<ModelPerformanceEntity>
{
    public void Configure(EntityTypeBuilder<ModelPerformanceEntity> builder)
    {
        builder.ToTable("model_performance_records");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").HasMaxLength(64);
        builder.Property(e => e.ModelId).HasColumnName("model_id").HasMaxLength(128).IsRequired();
        builder.Property(e => e.LatencyMs).HasColumnName("latency_ms");
        builder.Property(e => e.Success).HasColumnName("success");
        builder.Property(e => e.InputTokens).HasColumnName("input_tokens");
        builder.Property(e => e.OutputTokens).HasColumnName("output_tokens");
        builder.Property(e => e.ActualCostUsd).HasColumnName("actual_cost_usd");
        builder.Property(e => e.RecordedAt).HasColumnName("recorded_at");

        builder.HasIndex(e => e.ModelId).HasDatabaseName("ix_model_performance_model_id");
        builder.HasIndex(e => e.RecordedAt).HasDatabaseName("ix_model_performance_recorded_at");
    }
}

public class DataConnectorConfiguration : IEntityTypeConfiguration<DataConnectorEntity>
{
    public void Configure(EntityTypeBuilder<DataConnectorEntity> builder)
    {
        builder.ToTable("data_connectors");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id").HasMaxLength(64);
        builder.Property(c => c.Name).HasColumnName("name").HasMaxLength(256).IsRequired();
        builder.Property(c => c.ConnectorType).HasColumnName("connector_type").HasMaxLength(64).IsRequired();
        builder.Property(c => c.ConnectionString).HasColumnName("connection_string").HasMaxLength(512);
        builder.Property(c => c.SettingsJson).HasColumnName("settings").HasColumnType("jsonb").IsRequired();
        builder.Property(c => c.TenantId).HasColumnName("tenant_id").HasMaxLength(64);
        builder.Property(c => c.SyncScheduleJson).HasColumnName("sync_schedule").HasColumnType("jsonb").IsRequired();
        builder.Property(c => c.IsActive).HasColumnName("is_active");
        builder.Property(c => c.LastSyncAt).HasColumnName("last_sync_at");
        builder.Property(c => c.Status).HasColumnName("status").HasMaxLength(64).IsRequired();

        builder.HasIndex(c => c.TenantId).HasDatabaseName("ix_data_connectors_tenant_id");
    }
}

public class AgentMarketplaceEntryConfiguration : IEntityTypeConfiguration<AgentMarketplaceEntryEntity>
{
    public void Configure(EntityTypeBuilder<AgentMarketplaceEntryEntity> builder)
    {
        builder.ToTable("agent_marketplace_entries");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").HasMaxLength(64);
        builder.Property(e => e.Name).HasColumnName("name").HasMaxLength(256).IsRequired();
        builder.Property(e => e.Domain).HasColumnName("domain").HasMaxLength(128).IsRequired();
        builder.Property(e => e.Description).HasColumnName("description").IsRequired();
        builder.Property(e => e.Author).HasColumnName("author").HasMaxLength(128).IsRequired();
        builder.Property(e => e.TagsJson).HasColumnName("tags").HasColumnType("jsonb").IsRequired();
        builder.Property(e => e.AverageRating).HasColumnName("average_rating");
        builder.Property(e => e.InstallCount).HasColumnName("install_count");
        builder.Property(e => e.SpecificationJson).HasColumnName("specification").HasColumnType("jsonb").IsRequired();
        builder.Property(e => e.PublishedAt).HasColumnName("published_at");

        builder.HasIndex(e => e.Domain).HasDatabaseName("ix_marketplace_domain");
        builder.HasIndex(e => e.Author).HasDatabaseName("ix_marketplace_author");
    }
}

public class EnhancedMemoryConfiguration : IEntityTypeConfiguration<EnhancedMemoryEntity>
{
    public void Configure(EntityTypeBuilder<EnhancedMemoryEntity> builder)
    {
        builder.ToTable("enhanced_memories");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").HasMaxLength(64);
        builder.Property(e => e.AgentName).HasColumnName("agent_name").HasMaxLength(128).IsRequired();
        builder.Property(e => e.SessionId).HasColumnName("session_id").HasMaxLength(64).IsRequired();
        builder.Property(e => e.Content).HasColumnName("content").IsRequired();
        builder.Property(e => e.MemoryType).HasColumnName("memory_type").HasMaxLength(64).IsRequired();
        builder.Property(e => e.Sensitivity).HasColumnName("sensitivity").HasMaxLength(64).IsRequired();
        builder.Property(e => e.Confidence).HasColumnName("confidence");
        builder.Property(e => e.Freshness).HasColumnName("freshness");
        builder.Property(e => e.DecayRate).HasColumnName("decay_rate");
        builder.Property(e => e.AccessCount).HasColumnName("access_count");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.LastAccessedAt).HasColumnName("last_accessed_at");
        builder.Property(e => e.ExpiresAt).HasColumnName("expires_at");
        builder.Property(e => e.TagsJson).HasColumnName("tags").HasColumnType("jsonb").IsRequired();

        builder.HasIndex(e => e.AgentName).HasDatabaseName("ix_enhanced_memory_agent");
        builder.HasIndex(e => e.SessionId).HasDatabaseName("ix_enhanced_memory_session");
        builder.HasIndex(e => e.MemoryType).HasDatabaseName("ix_enhanced_memory_type");
    }
}

public class KnowledgeRoomConfiguration : IEntityTypeConfiguration<KnowledgeRoomEntity>
{
    public void Configure(EntityTypeBuilder<KnowledgeRoomEntity> builder)
    {
        builder.ToTable("knowledge_rooms");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").HasMaxLength(64);
        builder.Property(e => e.TenantId).HasColumnName("tenant_id").HasMaxLength(64).IsRequired();
        builder.Property(e => e.Name).HasColumnName("name").HasMaxLength(256).IsRequired();
        builder.Property(e => e.Description).HasColumnName("description").HasMaxLength(1024);
        builder.Property(e => e.Color).HasColumnName("color").HasMaxLength(32);
        builder.Property(e => e.Icon).HasColumnName("icon").HasMaxLength(32);
        builder.Property(e => e.DocumentCount).HasColumnName("document_count");
        builder.Property(e => e.Tags).HasColumnName("tags").HasColumnType("text[]");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(e => e.TenantId).HasDatabaseName("ix_knowledge_rooms_tenant_id");
    }
}

public class KnowledgeRoomPermissionConfiguration : IEntityTypeConfiguration<KnowledgeRoomPermissionEntity>
{
    public void Configure(EntityTypeBuilder<KnowledgeRoomPermissionEntity> builder)
    {
        builder.ToTable("knowledge_room_permissions");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").HasMaxLength(64);
        builder.Property(e => e.TenantId).HasColumnName("tenant_id").HasMaxLength(64).IsRequired();
        builder.Property(e => e.RoomId).HasColumnName("room_id").HasMaxLength(64).IsRequired();
        builder.Property(e => e.UserId).HasColumnName("user_id").HasMaxLength(64).IsRequired();
        builder.Property(e => e.Role).HasColumnName("role").HasMaxLength(32).IsRequired();
        builder.Property(e => e.GrantedAt).HasColumnName("granted_at");

        builder.HasIndex(e => new { e.RoomId, e.UserId }).IsUnique().HasDatabaseName("ix_knowledge_room_permissions_room_user");
        builder.HasIndex(e => e.TenantId).HasDatabaseName("ix_knowledge_room_permissions_tenant_id");
    }
}

public class AgentKnowledgeRoomAssignmentConfiguration : IEntityTypeConfiguration<AgentKnowledgeRoomAssignmentEntity>
{
    public void Configure(EntityTypeBuilder<AgentKnowledgeRoomAssignmentEntity> builder)
    {
        builder.ToTable("agent_knowledge_room_assignments");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").HasMaxLength(64);
        builder.Property(e => e.TenantId).HasColumnName("tenant_id").HasMaxLength(64).IsRequired();
        builder.Property(e => e.AgentName).HasColumnName("agent_name").HasMaxLength(128).IsRequired();
        builder.Property(e => e.RoomId).HasColumnName("room_id").HasMaxLength(64).IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");

        builder.HasIndex(e => new { e.AgentName, e.RoomId, e.TenantId })
            .IsUnique()
            .HasDatabaseName("ux_agent_room_assignment");
        builder.HasIndex(e => e.AgentName).HasDatabaseName("ix_agent_room_assignments_agent");
        builder.HasIndex(e => e.TenantId).HasDatabaseName("ix_agent_room_assignments_tenant");
    }
}
