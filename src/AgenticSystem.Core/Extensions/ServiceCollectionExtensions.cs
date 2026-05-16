using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;
using AgenticSystem.Core.Tools;
using AgenticSystem.Core.Skills;
using AgenticSystem.Core.Services.Triage;
using AgenticSystem.Core.Services.FastPath;
using AgenticSystem.Core.Services.Ml;
using Microsoft.Extensions.ML;

namespace AgenticSystem.Core.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAgenticSystemCore(this IServiceCollection services)
    {
        services.AddSingleton<IMetaAgent, MetaAgentOrchestrator>();
        services.AddSingleton<IContextAnalyzer, ContextAnalyzer>();
        services.AddSingleton<IAgentFactory, HierarchicalAgentFactory>();
        services.AddSingleton<IAgentMemoryStore, InMemoryAgentMemoryStore>();
        services.AddSingleton<IAgentMemoryService, AgentMemoryService>();
        services.AddSingleton<ISessionStore, InMemorySessionStore>();
        services.AddSingleton<ISessionManager, SessionManager>();
        services.AddSingleton<ILLMRuntimeContextAccessor, LLMRuntimeContextAccessor>();
        services.AddSingleton<IAgentRuntimeCoordinator, AgentRuntimeCoordinator>();
        services.AddSingleton<IFinalResponseApprovalService, FinalResponseApprovalService>();
        services.AddSingleton<IAgentExecutionPreProcessingPipeline, AgentExecutionPreProcessingPipeline>();
        services.AddSingleton<IAgentExecutionPostProcessingPipeline, AgentExecutionPostProcessingPipeline>();
        services.AddSingleton<IDirectAgentRequestExecutor, DirectAgentRequestExecutor>();
        services.AddSingleton<IQualityGateService, QualityGateService>();
        services.AddSingleton<ISessionConsolidator, SessionConsolidator>();
        services.AddSingleton<ISkillManager, InMemorySkillManager>();
        services.AddSingleton<IToolManager, InMemoryToolManager>();
        services.AddSingleton<IToolGovernanceService, ToolGovernanceService>();
        services.AddSingleton<IEventBus, InMemoryEventBus>();
        services.AddSingleton<IAuditLog, InMemoryAuditLog>();
        services.AddSingleton<IPermissionService, InMemoryPermissionService>();
        services.AddSingleton<IPolicyStore, InMemoryPolicyStore>();

        // Triage & FastPath (ML14 Expansion)
        services.AddSingleton<ITriageService, TriageService>();
        services.AddSingleton<IFastPathInterceptor, ConversationalFastPathInterceptor>();
        services.AddSingleton<IFastPathInterceptor, MlFastPathInterceptor>();

        // ML.NET Pool registration (Model file should be provided in the root or config)
        if (System.IO.File.Exists("fastpath_model.zip"))
        {
            services.AddPredictionEnginePool<FastPathModelInput, FastPathModelOutput>()
                .FromFile("fastpath_model.zip");
        }

        // Phase 1 — Enterprise Security & Runtime
        services.AddSingleton<IPolicyStore, InMemoryPolicyStore>();
        services.AddSingleton<IPolicyEngine, PolicyEngine>();
        services.AddSingleton<IPermissionService, InMemoryPermissionService>();
        services.AddSingleton<IAuditLog, InMemoryAuditLog>();
        services.AddSingleton<IToolGateway, ToolGateway>();

        // Multi-Tenant
        services.AddSingleton<ITenantStore, InMemoryTenantStore>();
        services.AddSingleton<ITenantResolver, TenantResolver>();
        services.AddSingleton<ITenantContextAccessor, TenantContextAccessor>();
        services.AddScoped<TenantContext>();

        // Maturity Level Services
        services.AddSingleton<IChunkLifecycleManager, ChunkLifecycleManager>();
        services.AddSingleton<IContextBudgetManager, ContextBudgetManager>();
        services.AddSingleton<ITaskPlanManager, TaskPlanManager>();
        services.AddSingleton<IReflectionEngine, ReflectionEngine>();
        services.AddSingleton<ICorrectionLoop, CorrectionLoopService>();
        services.AddSingleton<IKnowledgeFreshnessService, KnowledgeFreshnessService>();
        services.AddSingleton<IConfidenceScoreCalculator, ConfidenceScoreCalculator>();
        services.AddSingleton<ISemanticCompressor, SemanticCompressorService>();
        services.AddSingleton<IQueryCompressor, QueryCompressorService>();
        services.AddSingleton<IUserPreferenceEngine, UserPreferenceEngine>();

        // ML11-ML15 — Roadmap Services
        services.AddSingleton<IDynamicAgentService, DynamicAgentService>();
        services.AddSingleton<ISessionConsolidator, SessionConsolidator>();
        services.AddSingleton<ISmartRouter, SmartRouter>();
        services.AddSingleton<ISetupFlowManager, SetupFlowManager>();
        services.AddSingleton<IMemoryInjectionService, MemoryInjectionService>();

        // ML20 — Tool Availability Guard + Discovery
        services.AddSingleton<IToolDiscoveryService, ToolDiscoveryService>();
        services.AddSingleton<IToolAvailabilityGuard, ToolAvailabilityGuard>();

        // ML21 — Scheduled Tasks & Trigger Engine
        services.AddHttpClient();
        services.AddSingleton<IScheduledTaskStore, InMemoryScheduledTaskStore>();
        services.AddSingleton<IDeliveryChannel, WebhookDeliveryChannel>();
        services.AddSingleton<IDeliveryChannel, EmailDeliveryChannel>();
        services.AddSingleton<IDeliveryChannel, PushDeliveryChannel>();
        services.AddSingleton<ITriggerEngine, TriggerEngine>();
        services.AddSingleton<IScheduledTaskManager, ScheduledTaskManager>();
        services.AddHostedService<ScheduledTaskHostedService>();

        // GAP-13 — Agent Cleanup Background Service
        services.AddHostedService<AgentCleanupHostedService>();

        // ML15 — Session Auto-Consolidation Background Service
        services.AddHostedService<SessionAutoConsolidator>();

        // ML22 — Config Management (Credentials, Paths, Settings)
        services.AddSingleton<IConfigStore, InMemoryConfigStore>();
        services.AddSingleton<IConfigEncryptionService>(sp =>
        {
            var config = sp.GetService<Microsoft.Extensions.Configuration.IConfiguration>();
            var encryptionKey = config?["AgenticSystem:Encryption:Key"];
            var env = sp.GetService<IHostEnvironment>();
            if (string.IsNullOrEmpty(encryptionKey) && env?.EnvironmentName != "Development")
                throw new InvalidOperationException("AgenticSystem:Encryption:Key must be configured in non-Development environments.");
            return new AesConfigEncryptionService(encryptionKey);
        });
        services.AddSingleton<IConfigReloadNotifier, ConfigReloadNotifier>();
        services.AddSingleton<IConfigManager, ConfigManager>();
        services.AddHostedService<SecretRotationBackgroundService>();

        // ML23 — Embedding Migration (Re-indexação)
        services.AddSingleton<IEmbeddingModelStore, InMemoryEmbeddingModelStore>();
        services.AddSingleton<IMigrationJobStore, InMemoryMigrationJobStore>();
        services.AddSingleton<IEmbeddingMigrationManager, EmbeddingMigrationManager>();

        // ONNX Runtime Integration (Task 1.2 & 3.3)
        if (System.IO.File.Exists("fastpath_model.onnx"))
        {
            services.AddSingleton<IMlClassifier>(sp => 
                new OnnxMlClassifier("fastpath_model.onnx", sp.GetRequiredService<ILogger<OnnxMlClassifier>>()));
        }

        if (System.IO.File.Exists("reranker_model.onnx"))
        {
            services.AddSingleton<IReRanker>(sp => 
                new OnnxReRanker("reranker_model.onnx", sp.GetRequiredService<ILogger<OnnxReRanker>>()));
        }

        // Embedding Generator Strategy
        if (System.IO.File.Exists("embeddings_model.onnx"))
        {
            services.AddSingleton<OnnxEmbeddingGenerator>(sp => 
                new OnnxEmbeddingGenerator("embeddings_model.onnx", sp.GetRequiredService<ILogger<OnnxEmbeddingGenerator>>()));
            
            services.AddSingleton<IEmbeddingGenerator>(sp => sp.GetRequiredService<OnnxEmbeddingGenerator>());
        }
        else
        {
            services.AddSingleton<IEmbeddingGenerator, HttpEmbeddingGenerator>();
        }

        // Runtime Evaluator — InMemory fallback (overridden by UsePostgresOperationalStore when Postgres is configured)
        services.AddSingleton<IRuntimeEvaluator, InMemoryRuntimeEvaluator>();
        services.AddSingleton<IOperationalStore, InMemoryOperationalStore>();

        // Agent Versioning
        services.AddSingleton<IAgentVersionStore, InMemoryAgentVersionStore>();
        services.AddSingleton<IAgentVersioningService>(sp =>
        {
            var store = sp.GetRequiredService<IAgentVersionStore>();
            var auditLog = sp.GetRequiredService<IAuditLog>();
            var logger = sp.GetRequiredService<ILogger<AgentVersioningService>>();
            var agents = sp.GetServices<IAgent>().ToList();
            IAgent? resolver(string name) => agents.FirstOrDefault(a => a.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            return new AgentVersioningService(store, auditLog, resolver, logger);
        });

        // Agent Evaluation
        services.AddSingleton<IEvalResultStore, InMemoryEvalResultStore>();
        services.AddSingleton<IAgentEvaluationService, AgentEvaluationService>();

        // Structured Output Validation
        services.AddSingleton<IStructuredOutputValidator, StructuredOutputValidator>();

        // Prompt Management
        services.AddSingleton<IPromptTemplateStore, InMemoryPromptTemplateStore>();
        services.AddSingleton<IPromptManager>(sp =>
        {
            var store = sp.GetRequiredService<IPromptTemplateStore>();
            var logger = sp.GetRequiredService<ILogger<PromptManager>>();
            var agents = sp.GetServices<IAgent>().ToList();
            IAgent? resolver(string name) => agents.FirstOrDefault(a => a.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            return new PromptManager(store, resolver, logger);
        });

        // Fallback Executor
        services.AddSingleton<FallbackExecutor>();

        // Knowledge Graph (Graph RAG)
        services.AddSingleton<InMemoryKnowledgeGraphService>();
        services.AddSingleton<IKnowledgeGraphService>(sp => sp.GetRequiredService<InMemoryKnowledgeGraphService>());
        services.AddSingleton<IKnowledgeGraphStore>(sp => sp.GetRequiredService<InMemoryKnowledgeGraphService>());

        // Workflow Engine
        services.AddSingleton<IWorkflowStore, InMemoryWorkflowStore>();
        services.AddSingleton<IWorkflowEngine, DefaultWorkflowEngine>();

        // Phase 3 — Advanced Intelligence & Quality
        services.AddSingleton<IModelPerformanceStore, InMemoryModelPerformanceStore>();
        services.AddSingleton<IModelRouter, AdaptiveModelRouter>();
        services.AddSingleton<ICitationEngine, DefaultCitationEngine>();
        services.AddSingleton<IExplainabilityService, DefaultExplainabilityService>();
        services.AddSingleton<IAgentSimulationEngine, AgentSimulationService>();
        services.AddSingleton<ISelfImprovementEngine, SelfImprovementService>();

        // Phase 4 — Platform & Data Connectors
        services.AddSingleton<IDataConnectorStore, InMemoryDataConnectorStore>();
        services.AddSingleton<IDataConnectorManager, DataConnectorManager>();
        services.AddSingleton<ITenantIsolationEnforcer, TenantIsolationService>();
        services.AddSingleton<IAgentMarketplace, InMemoryAgentMarketplace>();
        services.AddSingleton<IAdminConsole, AdminConsoleService>();
        services.AddSingleton<IComplianceService, ComplianceService>();
        services.AddSingleton<IMemoryLifecycleStore, InMemoryMemoryLifecycleStore>();

        // Phase 5 — Enterprise Scoping & Sandboxing
        services.AddSingleton<IQuotaEnforcer, QuotaEnforcer>();
        services.AddSingleton<IAgentSandbox, AgentSandbox>();

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(ServiceCollectionExtensions).Assembly));

        return services;
    }

    /// <summary>
    /// Registra tools e skills built-in no sistema.
    /// Chamar após o build do ServiceProvider.
    /// </summary>
    public static IServiceProvider SeedAgenticDefaults(this IServiceProvider serviceProvider)
    {
        // Register built-in tools
        var toolManager = serviceProvider.GetRequiredService<IToolManager>();
        toolManager.RegisterTool(new DateTimeTool());
        toolManager.RegisterTool(new CalculatorTool());
        toolManager.RegisterTool(new FileSearchTool());

        // Register built-in skills
        var skillManager = serviceProvider.GetRequiredService<ISkillManager>();
        skillManager.RegisterSkill(new CodingAssistantSkill());
        skillManager.RegisterSkill(new ProductivitySkill());
        skillManager.RegisterSkill(new CreativeWritingSkill());
        skillManager.RegisterSkill(new DataAnalysisSkill());

        return serviceProvider;
    }
}