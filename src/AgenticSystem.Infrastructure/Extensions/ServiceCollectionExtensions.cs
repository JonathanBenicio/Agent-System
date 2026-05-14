using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Services;
using AgenticSystem.Core.Tools;
using AgenticSystem.Core.LLM.Interfaces;
using AgenticSystem.Infrastructure.Chunking;
using AgenticSystem.Infrastructure.Configuration;
using AgenticSystem.Infrastructure.Documents;
using AgenticSystem.Infrastructure.Gateway;
using AgenticSystem.Infrastructure.LLM;
using AgenticSystem.Infrastructure.MCP;
using AgenticSystem.Infrastructure.Memory;
using AgenticSystem.Infrastructure.Persistence;
using AgenticSystem.Infrastructure.RAG;
using AgenticSystem.Infrastructure.Skills;
using AgenticSystem.Infrastructure.AgentFramework;
using AgenticSystem.Infrastructure.AI;
using AgenticSystem.Infrastructure.Sync;

namespace AgenticSystem.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAgenticSystemInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDistributedMemoryCache();

        // Configuration
        services.Configure<AgenticSystemSettings>(configuration.GetSection("AgenticSystem"));
        services.Configure<OpenAISettings>(configuration.GetSection("AgenticSystem:OpenAI"));
        services.Configure<OllamaSettings>(configuration.GetSection("AgenticSystem:Ollama"));
        services.Configure<GeminiSettings>(configuration.GetSection("AgenticSystem:Gemini"));
        services.Configure<ClaudeSettings>(configuration.GetSection("AgenticSystem:Claude"));
        services.Configure<GatewaySettings>(configuration.GetSection("AgenticSystem:Gateway"));
        services.Configure<CollaborationWorkflowOptions>(configuration.GetSection("AgenticSystem:CollaborationWorkflow"));
        services.Configure<ChatClientMiddlewareOptions>(configuration.GetSection("AgenticSystem:ChatClientMiddleware"));
        services.Configure<ReRankingOptions>(configuration.GetSection("AgenticSystem:RAG:ReRanking"));
        services.Configure<DynamicSkillsOptions>(configuration.GetSection("AgenticSystem:Skills"));


        // ─── Microsoft.Extensions.AI — registry contextual de IChatClient e IEmbeddingGenerator ───
        var openAiSettings = configuration.GetSection("AgenticSystem:OpenAI");
        var openAiApiKey = openAiSettings["ApiKey"];
        var enableStreaming = bool.TryParse(openAiSettings["EnableStreaming"], out var streaming)
            ? streaming
            : false;

        var ollamaSettings = configuration.GetSection("AgenticSystem:Ollama");
        var ollamaEnabled = bool.TryParse(ollamaSettings["Enabled"], out var oe) ? oe : false;

        if (!string.IsNullOrWhiteSpace(openAiApiKey))
        {
            services.AddEmbeddingGenerator(_ =>
                new OpenAI.Embeddings.EmbeddingClient(
                    openAiSettings["EmbeddingModel"] ?? "text-embedding-3-small",
                    openAiApiKey).AsIEmbeddingGenerator())
                .UseDistributedCache()
                .UseOpenTelemetry(sourceName: "AgenticSystem.Embeddings");
        }
        else if (ollamaEnabled)
        {
            var ollamaBaseUrl = ollamaSettings["BaseUrl"] ?? "http://localhost:11434";
            var ollamaModel = ollamaSettings["EmbeddingModel"] ?? "nomic-embed-text";

            services.AddEmbeddingGenerator(_ =>
                new Microsoft.Extensions.AI.OllamaEmbeddingGenerator(new Uri(ollamaBaseUrl), ollamaModel))
                .UseDistributedCache()
                .UseOpenTelemetry(sourceName: "AgenticSystem.Embeddings");
        }

        services.AddSingleton<LLMManager>();
        services.AddSingleton<ILLMAdministrationService>(sp => sp.GetRequiredService<LLMManager>());
        services.AddSingleton<ContextAwareChatClient>(sp => new ContextAwareChatClient(sp.GetRequiredService<LLMManager>()));
        services.AddSingleton<IChatClient>(sp => 
        {
            var governedClient = new GovernedChatClient(
                sp.GetRequiredService<ContextAwareChatClient>(),
                sp.GetRequiredService<IQualityGateService>(),
                sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ChatClientMiddlewareOptions>>(),
                sp.GetRequiredService<ILogger<GovernedChatClient>>());

            var cacheService = sp.GetService<ISemanticCacheService>();
            if (cacheService != null)
            {
                return new SemanticCacheChatClient(
                    governedClient,
                    cacheService,
                    "AgenticSystem", // default agent name
                    0.95, // threshold
                    sp.GetRequiredService<ILogger<SemanticCacheChatClient>>());
            }

            return governedClient;
        });

        // Gateway
        services.AddSingleton<ICostTracker, CostTracker>();
        services.AddSingleton<IServiceGateway, ServiceGateway>();
        services.AddSingleton<ITokenAuditService, AgenticSystem.Infrastructure.Observability.TokenAuditService>();

        // Quality Gates
        services.AddSingleton<IQualityGate, InputValidationGate>();
        services.AddSingleton<IQualityGate, ResponseQualityGate>();
        services.AddSingleton<IQualityGateService, QualityGateService>();

        // MCP Plugin Manager
        services.AddSingleton<IMCPPluginManager, MCPPluginManager>();
        services.AddSingleton<McpToolsAIFunctionAdapter>();
        services.AddHostedService<DynamicSkillCatalogHostedService>();

        // Unified tool schema (internas + MCP)
        services.AddSingleton<UnifiedAIToolProvider>();

        // M.E.AI — ChatClient Planner + VectorStore Adapter
        services.AddSingleton<ChatClientPlanner>();
        services.AddSingleton<IAgentCollaborationWorkflow, AgentCollaborationWorkflow>();
        services.AddSingleton<AgenticVectorStoreAdapter>();
        services.AddSingleton<IAgentChannelService, FrameworkAgentChannelService>();

        // Microsoft Agent Framework — Factory + Session Bridge + Decorator
        // Conditional: requires IChatClient (registered when openAiApiKey exists or externally)
        var hasChatClient = !string.IsNullOrWhiteSpace(openAiApiKey)
            || services.Any(d => d.ServiceType == typeof(Microsoft.Extensions.AI.IChatClient));
        if (hasChatClient)
        {
            var orchestratorMetadata = OrchestratorMetadata.Default;

            services.AddSingleton(orchestratorMetadata);
            services.AddSingleton<AgentFrameworkFactory>();
            services.AddSingleton<SimpleSessionStoreAdapter>();
            services.AddSingleton<OrchestratorAuxiliaryToolService>();
            services.AddSingleton<OrchestratorInstructionService>();
            services.AddSingleton<OrchestratorToolBindingService>();
            
            // [PHASE 1] Novo builder nativo
            services.AddSingleton<RAGContextProvider>(sp =>
            {
                var ragService = sp.GetService<IRAGService>();
                if (ragService is null)
                    return null!;
                var budgetManager = sp.GetService<IContextBudgetManager>();
                var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
                return new RAGContextProvider(ragService, budgetManager, loggerFactory.CreateLogger<RAGContextProvider>());
            });
            
            services.AddSingleton<OrchestratorHostBuilder>(sp =>
                new OrchestratorHostBuilder(
                    sp.GetRequiredService<IChatClient>(),
                    sp.GetRequiredService<ILoggerFactory>(),
                    sp,
                    orchestratorMetadata,
                    sp.GetRequiredService<IAgentFactory>(),
                    sp.GetRequiredService<OrchestratorInstructionService>(),
                    sp.GetRequiredService<OrchestratorToolBindingService>(),
                    sp.GetRequiredService<OrchestratorAuxiliaryToolService>(),
                    sp.GetRequiredService<ILogger<OrchestratorHostBuilder>>(),
                    sp.GetService<RAGContextProvider>(),
                    sp.GetService<IQualityGateService>()));
            
            // [DEPRECATED] Thin wrapper for backward compatibility
            services.AddSingleton<OrchestratorContextFactory>();
            services.AddScoped(sp => sp.GetRequiredService<OrchestratorContextFactory>().Resolve());
            
            services.AddSingleton<IDirectAgentExecutionService>(sp =>
                new AgentFrameworkDirectExecutionService(
                    sp.GetRequiredService<AgentFrameworkFactory>(),
                    sp.GetRequiredService<SimpleSessionStoreAdapter>(),
                    sp.GetRequiredService<ISessionManager>(),
                    sp.GetRequiredService<ILogger<AgentFrameworkDirectExecutionService>>(),
                    sp,
                    sp.GetService<IAgentRuntimeCoordinator>(),
                    enableStreaming: enableStreaming));

            var hostedOrchestratorBuilder = services.AddAIAgent(
                orchestratorMetadata.Name,
                static (sp, _) => sp.GetRequiredService<OrchestratorContext>().OrchestratorAgent,
                ServiceLifetime.Scoped);

            hostedOrchestratorBuilder.WithSessionStore(
                static (sp, _) => sp.GetRequiredService<SimpleSessionStoreAdapter>(),
                ServiceLifetime.Singleton);

            services.AddSingleton<IFrameworkOrchestratorService, FrameworkOrchestratorService>();

        }

        // Memory / Vector Store — conditional based on VectorStoreType config
        var localExecutionStorageMode = configuration["AgenticSystem:LocalExecution:StorageMode"];
        var vectorStoreType = configuration["AgenticSystem:Memory:VectorStoreType"] ?? "InMemory";
        var memoryConnectionString = configuration["AgenticSystem:Memory:ConnectionString"];

        if (!string.Equals(localExecutionStorageMode, "InMemory", StringComparison.OrdinalIgnoreCase)
            && vectorStoreType.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(memoryConnectionString))
        {
            services.UsePostgresVectorStore(memoryConnectionString);
        }
        else
        {
            services.AddSingleton<IVectorStore, InMemoryVectorStore>();
        }

        // Document Parsers
        services.AddSingleton<IDocumentParser, MarkdownParser>();
        services.AddSingleton<IDocumentParser, PlainTextParser>();
        services.AddSingleton<IDocumentParser, HtmlParser>();
        services.AddSingleton<IDocumentParser, PdfDocumentParser>();
        services.AddSingleton<IDocumentParser, DocxDocumentParser>();

        // Chunking Strategy
        services.AddSingleton<IChunkingStrategy, HybridChunkingStrategy>();

        // Document Ingestion Pipeline
        services.AddSingleton<IMultimodalProcessor, LlmMultimodalProcessor>();
        services.AddSingleton<IDocumentIngestionPipeline, DocumentIngestionPipeline>();
        services.AddSingleton<IDataConnector, FileSystemDataConnector>();
        services.AddHostedService<DataSyncBackgroundService>();

        // RAG
        services.AddSingleton<IRerankingAssetStore, InMemoryRerankingAssetStore>();
        services.AddSingleton<IRerankingSettingsAccessor, RerankingSettingsAccessor>();
        services.AddSingleton<HeuristicReRanker>();
        services.AddSingleton<IDedicatedReRankerProvider, LocalOnnxCrossEncoderReRankerProvider>();
        services.AddHttpClient("DedicatedReRanker");
        services.AddSingleton<IDedicatedReRankerProvider>(sp => new JinaReRankerProvider(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient("DedicatedReRanker"),
            sp.GetRequiredService<IRerankingSettingsAccessor>(),
            sp.GetRequiredService<ILogger<JinaReRankerProvider>>()));
        services.AddSingleton<IReRanker>(sp => new LlmReRanker(
            sp.GetRequiredService<HeuristicReRanker>(),
            sp.GetRequiredService<IChatClient>(),
            sp.GetServices<IDedicatedReRankerProvider>(),
            sp.GetService<Microsoft.Extensions.AI.IEmbeddingGenerator<string, Microsoft.Extensions.AI.Embedding<float>>>(),
            sp.GetRequiredService<IRerankingSettingsAccessor>(),
            sp.GetRequiredService<ILogger<LlmReRanker>>()));
            
        services.AddSingleton<IEmbeddingProvider>(sp => new AgenticSystem.Infrastructure.Embeddings.EmbeddingProviderAdapter(
            sp.GetRequiredService<Microsoft.Extensions.AI.IEmbeddingGenerator<string, Microsoft.Extensions.AI.Embedding<float>>>(),
            sp.GetRequiredService<ILogger<AgenticSystem.Infrastructure.Embeddings.EmbeddingProviderAdapter>>()));
            
        services.AddSingleton<IAdvancedRetrievalService, PostgresAdvancedRetrievalService>();
        services.AddSingleton<IRAGService, RAGService>();

        // Obsidian Sync
        services.AddSingleton<IObsidianSync>(sp =>
        {
            var vectorStore = sp.GetRequiredService<IVectorStore>();
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<FileObsidianSync>>();
            var vaultPath = configuration["AgenticSystem:Memory:ObsidianVaultPath"];
            return new FileObsidianSync(vectorStore, logger, vaultPath);
        });

        // HttpClient for tools that need it
        services.AddHttpClient();

        return services;
    }

    /// <summary>
    /// Substitui o InMemorySessionStore pelo PostgresSessionStore (produção).
    /// Chamar após AddAgenticSystemCore().
    /// </summary>
    public static IServiceCollection UsePostgresSessionStore(this IServiceCollection services, string connectionString)
    {
        EnsureDbContextRegistrations(services, connectionString);
        ReplaceSingleton<Core.Interfaces.ISessionStore, PostgresSessionStore>(services);

        return services;
    }

    /// <summary>
    /// Substitui o InMemoryVectorStore pelo PostgresVectorStore (produção).
    /// Chamar após AddAgenticSystemInfrastructure().
    /// </summary>
    public static IServiceCollection UsePostgresVectorStore(this IServiceCollection services, string connectionString)
    {
        EnsureDbContextRegistrations(services, connectionString);
        ReplaceSingleton<IVectorStore, PostgresVectorStore>(services);

        return services;
    }

    /// <summary>
    /// Substitui o CostTracker in-memory pelo PostgresCostTracker (produção).
    /// Chamar após AddAgenticSystemInfrastructure().
    /// </summary>
    public static IServiceCollection UsePostgresCostTracker(this IServiceCollection services, string connectionString)
    {
        EnsureDbContextRegistrations(services, connectionString);
        ReplaceSingleton<ICostTracker, PostgresCostTracker>(services);

        return services;
    }

    /// <summary>
    /// Decora o SmartRouter existente com persistência PostgreSQL (write-through + warm-up).
    /// Chamar após AddAgenticSystemCore().
    /// </summary>
    public static IServiceCollection UsePostgresSmartRouter(this IServiceCollection services, string connectionString)
    {
        EnsureDbContextRegistrations(services, connectionString);

        var innerDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(ISmartRouter));
        if (innerDescriptor is not null)
            services.Remove(innerDescriptor);

        services.AddSingleton<ISmartRouter>(sp =>
        {
            // Resolve the original SmartRouter
            ISmartRouter inner = innerDescriptor?.ImplementationType is { } implType
                ? (ISmartRouter)ActivatorUtilities.CreateInstance(sp, implType)
                : innerDescriptor?.ImplementationFactory is { } factory
                    ? (ISmartRouter)factory(sp)
                    : ActivatorUtilities.CreateInstance<Core.Services.SmartRouter>(sp);

            var logger = sp.GetRequiredService<ILogger<PersistentSmartRouter>>();
            var dbContextFactory = sp.GetRequiredService<IDbContextFactory<AgenticDbContext>>();
            return new PersistentSmartRouter(inner, dbContextFactory, logger);
        });

        return services;
    }

    /// <summary>
    /// Registra o operational store PostgreSQL para artefatos, métricas, reflexões e avaliações.
    /// Chamar após UseEntityFramework().
    /// </summary>
    public static IServiceCollection UsePostgresOperationalStore(this IServiceCollection services)
    {
        ReplaceSingleton<IOperationalStore, PostgresOperationalStore>(services);
        ReplaceSingleton<IRuntimeEvaluator, RuntimeEvaluatorService>(services);
        return services;
    }

    /// <summary>
    /// Registra os serviços de segurança e auditoria suportados pelo PostgreSQL.
    /// Chamar após UseEntityFramework().
    /// </summary>
    public static IServiceCollection UsePostgresSecurityAndAudit(this IServiceCollection services, string connectionString)
    {
        EnsureDbContextRegistrations(services, connectionString);
        ReplaceSingleton<IAuditLog, PostgresAuditLog>(services);
        ReplaceSingleton<IPermissionService, PostgresPermissionService>(services);
        services.AddHostedService<Persistence.OutboxProcessorBackgroundService>(); // Register outbox
        services.AddSingleton<IEventBus, PostgresEventBus>();
        services.AddSingleton<IPolicyStore, PostgresPolicyStore>();
        return services;
    }

    /// <summary>
    /// Substitui InMemoryMigrationJobStore pelo PostgresMigrationJobStore.
    /// </summary>
    public static IServiceCollection UsePostgresMigrationJobStore(this IServiceCollection services, string connectionString)
    {
        EnsureDbContextRegistrations(services, connectionString);
        ReplaceSingleton<IMigrationJobStore, PostgresMigrationJobStore>(services);
        return services;
    }

    /// <summary>
    /// Substitui InMemoryEmbeddingModelStore pelo PostgresEmbeddingModelStore.
    /// </summary>
    public static IServiceCollection UsePostgresEmbeddingModelStore(this IServiceCollection services, string connectionString)
    {
        EnsureDbContextRegistrations(services, connectionString);
        ReplaceSingleton<IEmbeddingModelStore, PostgresEmbeddingModelStore>(services);
        return services;
    }

    /// <summary>
    /// Substitui os stores de qualidade e confiabilidade em-memória pelos baseados no PostgreSQL.
    /// </summary>
    public static IServiceCollection UsePostgresQualityStores(this IServiceCollection services, string connectionString)
    {
        EnsureDbContextRegistrations(services, connectionString);
        ReplaceSingleton<IAgentVersionStore, PostgresAgentVersionStore>(services);
        ReplaceSingleton<IPromptTemplateStore, PostgresPromptTemplateStore>(services);
        ReplaceSingleton<IEvalResultStore, PostgresEvalResultStore>(services);
        return services;
    }

    public static IServiceCollection UseLocalExecutionStorageMode(this IServiceCollection services, IConfiguration configuration)
    {
        var configuredMode = configuration["AgenticSystem:LocalExecution:StorageMode"];
        var connectionString = configuration.GetConnectionString("SessionStore");
        var storageMode = string.IsNullOrWhiteSpace(configuredMode)
            ? (!string.IsNullOrWhiteSpace(connectionString) ? "PostgreSQL" : "InMemory")
            : configuredMode;

        if (string.Equals(storageMode, "InMemory", StringComparison.OrdinalIgnoreCase))
        {
            ReplaceSingleton<IVectorStore, InMemoryVectorStore>(services);
            return services;
        }

        if (!string.Equals(storageMode, "PostgreSQL", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Unsupported local execution storage mode '{storageMode}'. Use InMemory or PostgreSQL.");
        }

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("ConnectionStrings:SessionStore must be configured when AgenticSystem:LocalExecution:StorageMode is PostgreSQL.");
        }

        EnsureDbContextRegistrations(services, connectionString);

        ReplaceSingleton<IAgentMemoryStore, EfAgentMemoryStore>(services);
        ReplaceSingleton<ITenantStore, EfTenantStore>(services);
        ReplaceSingleton<IScheduledTaskStore, PostgresScheduledTaskStore>(services);
        ReplaceSingleton<IConfigStore, PostgresConfigStore>(services);
        ReplaceSingleton<IRerankingAssetStore, PostgresRerankingAssetStore>(services);

        services.UsePostgresSessionStore(connectionString);
        services.UsePostgresVectorStore(connectionString);
        services.UsePostgresCostTracker(connectionString);
        services.UsePostgresSmartRouter(connectionString);
        services.UsePostgresSemanticCache(connectionString);
        services.UsePostgresOperationalStore();
        services.UsePostgresSecurityAndAudit(connectionString);
        services.UsePostgresMigrationJobStore(connectionString);
        services.UsePostgresEmbeddingModelStore(connectionString);
        services.UsePostgresQualityStores(connectionString);
        services.UsePostgresKnowledgeGraph(connectionString);
        services.UsePostgresWorkflowEngine(connectionString);
        services.UsePostgresAdvancedIntelligence(connectionString);
        services.UsePostgresPlatformStores(connectionString);

        return services;
    }

    /// <summary>
    /// Substitui os stores de plataforma (Data Connectors, Marketplace, Memory Lifecycle) pelo PostgreSQL.
    /// </summary>
    public static IServiceCollection UsePostgresPlatformStores(this IServiceCollection services, string connectionString)
    {
        EnsureDbContextRegistrations(services, connectionString);
        ReplaceSingleton<IDataConnectorStore, PostgresDataConnectorStore>(services);
        ReplaceSingleton<IAgentMarketplace, PostgresAgentMarketplace>(services);
        ReplaceSingleton<IMemoryLifecycleStore, PostgresMemoryLifecycleStore>(services);
        return services;
    }

    /// <summary>
    /// Substitui os stores de inteligência avançada pelo PostgreSQL.
    /// </summary>
    public static IServiceCollection UsePostgresAdvancedIntelligence(this IServiceCollection services, string connectionString)
    {
        EnsureDbContextRegistrations(services, connectionString);
        ReplaceSingleton<IModelPerformanceStore, PostgresModelPerformanceStore>(services);
        return services;
    }

    /// <summary>
    /// Substitui o Workflow Engine in-memory pelo PostgreSQL.
    /// </summary>
    public static IServiceCollection UsePostgresWorkflowEngine(this IServiceCollection services, string connectionString)
    {
        EnsureDbContextRegistrations(services, connectionString);
        ReplaceSingleton<IWorkflowStore, PostgresWorkflowStore>(services);
        // The core implementation is agnostic to the store
        ReplaceSingleton<IWorkflowEngine, DefaultWorkflowEngine>(services);
        return services;
    }

    /// <summary>
    /// Substitui o Knowledge Graph in-memory pelo PostgreSQL.
    /// </summary>
    public static IServiceCollection UsePostgresKnowledgeGraph(this IServiceCollection services, string connectionString)
    {
        EnsureDbContextRegistrations(services, connectionString);
        ReplaceSingleton<IKnowledgeGraphService, PostgresKnowledgeGraphService>(services);
        ReplaceSingleton<IKnowledgeGraphStore, PostgresKnowledgeGraphService>(services);
        return services;
    }

    /// <summary>
    /// Substitui a interface base de cache pelo PostgresSemanticCacheService.
    /// </summary>
    public static IServiceCollection UsePostgresSemanticCache(this IServiceCollection services, string connectionString)
    {
        EnsureDbContextRegistrations(services, connectionString);
        ReplaceSingleton<ISemanticCacheService, PostgresSemanticCacheService>(services);
        return services;
    }

    private static void ReplaceSingleton<TService, TImplementation>(IServiceCollection services)
        where TService : class
        where TImplementation : class, TService
    {
        var descriptors = services.Where(d => d.ServiceType == typeof(TService)).ToList();
        foreach (var descriptor in descriptors)
        {
            services.Remove(descriptor);
        }

        services.AddSingleton<TService, TImplementation>();
    }

    private static void ReplaceSingleton<TService>(IServiceCollection services, Func<IServiceProvider, TService> factory)
        where TService : class
    {
        var descriptors = services.Where(d => d.ServiceType == typeof(TService)).ToList();
        foreach (var descriptor in descriptors)
        {
            services.Remove(descriptor);
        }

        services.AddSingleton(factory);
    }

    private static void EnsureDbContextRegistrations(IServiceCollection services, string connectionString)
    {
        if (!services.Any(descriptor => descriptor.ServiceType == typeof(DbContextOptions<AgenticDbContext>)))
        {
            services.AddDbContext<AgenticDbContext>(options =>
                options.UseNpgsql(connectionString, npgsql =>
                {
                    npgsql.MigrationsHistoryTable("__ef_migrations_history");
                    npgsql.UseVector();
                }), contextLifetime: ServiceLifetime.Scoped, optionsLifetime: ServiceLifetime.Singleton);
        }

        if (!services.Any(descriptor => descriptor.ServiceType == typeof(IDbContextFactory<AgenticDbContext>)))
        {
            services.AddDbContextFactory<AgenticDbContext>(options =>
                options.UseNpgsql(connectionString, npgsql =>
                {
                    npgsql.MigrationsHistoryTable("__ef_migrations_history");
                    npgsql.UseVector();
                }));
        }
    }

    /// <summary>
    /// Registra HttpTool usando HttpClient do DI.
    /// Chamar após build do ServiceProvider, junto com SeedAgenticDefaults.
    /// </summary>
    public static IServiceProvider SeedInfrastructureTools(this IServiceProvider serviceProvider)
    {
        var toolManager = serviceProvider.GetRequiredService<IToolManager>();
        var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
        var logger = serviceProvider.GetRequiredService<ILogger<HttpTool>>();
        var httpClient = httpClientFactory.CreateClient("AgenticTools");
        toolManager.RegisterTool(new HttpTool(httpClient, logger));
        return serviceProvider;
    }
}
