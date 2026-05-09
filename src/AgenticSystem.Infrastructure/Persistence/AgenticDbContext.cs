using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgenticSystem.Infrastructure.Persistence;

public class AgenticDbContext : DbContext
{
    public AgenticDbContext(DbContextOptions<AgenticDbContext> options) : base(options) { }

    public DbSet<SessionRecordEntity> SessionRecords => Set<SessionRecordEntity>();
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<VectorDocumentEntity> VectorDocuments => Set<VectorDocumentEntity>();
    public DbSet<CostEntryEntity> CostEntries => Set<CostEntryEntity>();
    public DbSet<CostBudgetEntity> CostBudgets => Set<CostBudgetEntity>();
    public DbSet<AgentPerformanceMetricEntity> AgentPerformanceMetrics => Set<AgentPerformanceMetricEntity>();
    public DbSet<RuntimeArtifactEntity> RuntimeArtifacts => Set<RuntimeArtifactEntity>();
    public DbSet<RuntimeMetricsSnapshotEntity> RuntimeMetricsSnapshots => Set<RuntimeMetricsSnapshotEntity>();
    public DbSet<ReflectionEntity> Reflections => Set<ReflectionEntity>();
    public DbSet<EvaluationScoreEntity> EvaluationScores => Set<EvaluationScoreEntity>();
    public DbSet<AgentMemoryEntity> AgentMemories => Set<AgentMemoryEntity>();
    public DbSet<ConfigEntryEntity> ConfigEntries => Set<ConfigEntryEntity>();
    public DbSet<ConfigChangeLogEntity> ConfigChangeLogs => Set<ConfigChangeLogEntity>();
    public DbSet<ScheduledTaskEntity> ScheduledTasks => Set<ScheduledTaskEntity>();
    public DbSet<TriggerRuleEntity> TriggerRules => Set<TriggerRuleEntity>();
    public DbSet<ScheduledTaskExecutionEntity> ScheduledTaskExecutions => Set<ScheduledTaskExecutionEntity>();
    public DbSet<EmbeddingModelEntity> EmbeddingModels => Set<EmbeddingModelEntity>();
    public DbSet<MigrationJobEntity> MigrationJobs => Set<MigrationJobEntity>();
    public DbSet<RerankingAssetEntity> RerankingAssets => Set<RerankingAssetEntity>();
    public DbSet<AuditEntryEntity> AuditEntries => Set<AuditEntryEntity>();
    public DbSet<RoleAssignmentEntity> RoleAssignments => Set<RoleAssignmentEntity>();
    public DbSet<OutboxMessageEntity> OutboxMessages => Set<OutboxMessageEntity>();
    public DbSet<AgentPolicyEntity> AgentPolicies => Set<AgentPolicyEntity>();
    public DbSet<AgentVersionEntity> AgentVersions => Set<AgentVersionEntity>();
    public DbSet<PromptTemplateEntity> PromptTemplates => Set<PromptTemplateEntity>();
    public DbSet<EvalSuiteResultEntity> EvalSuiteResults => Set<EvalSuiteResultEntity>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AgenticDbContext).Assembly);
    }
}
