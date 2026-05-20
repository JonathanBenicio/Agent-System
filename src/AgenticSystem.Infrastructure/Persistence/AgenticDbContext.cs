using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using System.Reflection;
using System.Linq.Expressions;

namespace AgenticSystem.Infrastructure.Persistence;

public class AgenticDbContext : DbContext
{
    private readonly ITenantContextAccessor _tenantContext;

    public AgenticDbContext(
        DbContextOptions<AgenticDbContext> options,
        ITenantContextAccessor tenantContext) : base(options)
    {
        _tenantContext = tenantContext;
    }

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
    public DbSet<SystemAlertEntity> SystemAlerts => Set<SystemAlertEntity>();
    public DbSet<InboundWebhookEntity> InboundWebhooks => Set<InboundWebhookEntity>();
    public DbSet<KnowledgeRoomEntity> KnowledgeRooms => Set<KnowledgeRoomEntity>();
    public DbSet<KnowledgeRoomPermissionEntity> KnowledgeRoomPermissions => Set<KnowledgeRoomPermissionEntity>();
    public DbSet<AgentKnowledgeRoomAssignmentEntity> AgentKnowledgeRoomAssignments => Set<AgentKnowledgeRoomAssignmentEntity>();
    public DbSet<McpPluginEntity> McpPlugins => Set<McpPluginEntity>();
    public DbSet<SessionSummaryEntity> SessionSummaries => Set<SessionSummaryEntity>();
    public DbSet<SessionInsightEntity> SessionInsights => Set<SessionInsightEntity>();
    public DbSet<LLMProviderApiKeyEntity> ProviderApiKeys => Set<LLMProviderApiKeyEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AgenticDbContext).Assembly);

        // Global Query Filters for Multi-tenancy
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ITenantEntity).IsAssignableFrom(entityType.ClrType))
            {
                var method = typeof(AgenticDbContext)
                    .GetMethod(nameof(ApplyTenantFilter), BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.MakeGenericMethod(entityType.ClrType);
                
                method?.Invoke(this, new object[] { modelBuilder });
            }
        }

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

    private void ApplyTenantFilter<T>(ModelBuilder modelBuilder) where T : class, ITenantEntity
    {
        modelBuilder.Entity<T>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
    }

    public string CurrentTenantId => _tenantContext.Current.TenantId;

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        OnBeforeSaving();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        OnBeforeSaving();
        return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void OnBeforeSaving()
    {
        var tenantId = _tenantContext.Current.TenantId;

        foreach (var entry in ChangeTracker.Entries<ITenantEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    if (string.IsNullOrEmpty(entry.Entity.TenantId) || entry.Entity.TenantId == "default")
                    {
                        entry.Entity.TenantId = tenantId;
                    }
                    break;
            }
        }
    }
}
