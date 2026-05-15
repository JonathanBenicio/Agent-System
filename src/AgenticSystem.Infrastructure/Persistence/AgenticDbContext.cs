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
    public DbSet<KnowledgeGraphNodeEntity> KnowledgeGraphNodes => Set<KnowledgeGraphNodeEntity>();
    public DbSet<KnowledgeGraphEdgeEntity> KnowledgeGraphEdges => Set<KnowledgeGraphEdgeEntity>();
    public DbSet<WorkflowDefinitionEntity> WorkflowDefinitions => Set<WorkflowDefinitionEntity>();
    public DbSet<WorkflowExecutionEntity> WorkflowExecutions => Set<WorkflowExecutionEntity>();
    public DbSet<WorkflowStepExecutionEntity> WorkflowStepExecutions => Set<WorkflowStepExecutionEntity>();
    public DbSet<ModelPerformanceEntity> ModelPerformanceRecords => Set<ModelPerformanceEntity>();
    public DbSet<DataConnectorEntity> DataConnectors => Set<DataConnectorEntity>();
    public DbSet<AgentMarketplaceEntryEntity> AgentMarketplaceEntries => Set<AgentMarketplaceEntryEntity>();
    public DbSet<EnhancedMemoryEntity> EnhancedMemories => Set<EnhancedMemoryEntity>();
    public DbSet<LlmPricingRuleEntity> LlmPricingRules => Set<LlmPricingRuleEntity>();
    public DbSet<ExternalProviderQuotaEntity> ExternalProviderQuotas => Set<ExternalProviderQuotaEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AgenticDbContext).Assembly);

        if (Database.ProviderName != "Npgsql.EntityFrameworkCore.PostgreSQL")
        {
            modelBuilder.Entity<VectorDocumentEntity>().Ignore(v => v.Embedding);
            modelBuilder.Entity<VectorDocumentEntity>().Ignore(v => v.SearchVector);
        }
        else
        {
            modelBuilder.Entity<VectorDocumentEntity>()
                .HasGeneratedTsVectorColumn(
                    p => p.SearchVector,
                    "english",
                    p => new { p.Content })
                .HasIndex(p => p.SearchVector)
                .HasMethod("GIN");
        }
    }
}
