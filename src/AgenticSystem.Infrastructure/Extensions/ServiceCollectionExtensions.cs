using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.LLM.Interfaces;
using AgenticSystem.Core.Services;
using AgenticSystem.Core.Tools;
using AgenticSystem.Infrastructure.AgentFramework;
using AgenticSystem.Infrastructure.AI;
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
using AgenticSystem.Infrastructure.Sync;
using AgenticSystem.Infrastructure.BackgroundServices;
using AgenticSystem.Infrastructure.LLM.BackgroundServices;
using AgenticSystem.Infrastructure.LLM.Services;
using Microsoft.Agents.AI.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pgvector.EntityFrameworkCore;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAgenticSystemInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDistributedMemoryCache();
        services.AddHttpClient();

        services
            .AddAgenticConfiguration(configuration)
            .AddAgenticMultiTenancy()
            .AddAgenticLlmServices(configuration)
            .AddAgenticGateway()
            .AddAgenticQualityGates()
            .AddAgenticMcpAndSkills()
            .AddAgenticAgentFramework(configuration)
            .AddAgenticRagAndMemory(configuration)
            .AddAgenticDocumentServices()
            .AddAgenticBackgroundServices(configuration);

        return services;
    }

    private static IServiceCollection AddAgenticConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
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
        services.Configure<SemanticCacheOptions>(configuration.GetSection("AgenticSystem:SemanticCache"));
        services.Configure<SelfImprovementSettings>(configuration.GetSection("AgenticSystem:SelfImprovement"));

        return services;
    }

    private static IServiceCollection AddAgenticLlmServices(this IServiceCollection services, IConfiguration configuration)
    {
        var ollamaSettings = configuration.GetSection("AgenticSystem:Ollama").Get<OllamaSettings>() ?? new();

        services.AddTransient<AgenticSystem.Infrastructure.LLM.Handlers.ExternalQuotaHeaderHandler>();

        services.AddHttpClient<GeminiProvider>()
            .AddHttpMessageHandler<AgenticSystem.Infrastructure.LLM.Handlers.ExternalQuotaHeaderHandler>()
            .AddStandardResilienceHandler();

        services.AddHttpClient<OllamaProvider>(client =>
        {
            client.Timeout = TimeSpan.FromMinutes(5); // Ollama can be slow for complex models
        })
        .AddStandardResilienceHandler();

        services.AddHttpClient<OpenAIProvider>()
            .AddHttpMessageHandler<AgenticSystem.Infrastructure.LLM.Handlers.ExternalQuotaHeaderHandler>()
            .AddStandardResilienceHandler();

        services.AddHttpClient<ClaudeProvider>()
            .AddHttpMessageHandler<AgenticSystem.Infrastructure.LLM.Handlers.ExternalQuotaHeaderHandler>()
            .AddStandardResilienceHandler();

        services.AddHttpClient<OpenRouterProvider>()
            .AddHttpMessageHandler<AgenticSystem.Infrastructure.LLM.Handlers.ExternalQuotaHeaderHandler>()
            .AddStandardResilienceHandler();

        services.AddEmbeddingGenerator(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<OllamaSettings>>().Value;
            return new Microsoft.Extensions.AI.OllamaEmbeddingGenerator(new Uri(settings.BaseUrl), settings.EmbeddingModel);
        })
        .UseDistributedCache()
        .UseOpenTelemetry(sourceName: "AgenticSystem.Embeddings");

        services.AddSingleton<ILLMProviderFactory, LLMProviderFactory>();
        services.AddSingleton<ILLMProvider, HotSwappableLLMProvider>();

        services.AddSingleton<LLMManager>();
        services.AddSingleton<IExternalQuotaSyncService, ExternalQuotaSyncService>();
        services.AddSingleton<ILLMAdministrationService>(sp => sp.GetRequiredService<LLMManager>());
        services.AddSingleton<ContextAwareChatClient>(sp => new ContextAwareChatClient(sp.GetRequiredService<LLMManager>(), sp.GetRequiredService<ILogger<ContextAwareChatClient>>()));

        services.AddSingleton<IChatClient>(sp =>
        {
            var governedClient = new GovernedChatClient(
                sp.GetRequiredService<ContextAwareChatClient>(),
                sp.GetRequiredService<IQualityGateService>(),
                sp.GetRequiredService<IOptions<ChatClientMiddlewareOptions>>(),
                sp.GetRequiredService<ILogger<GovernedChatClient>>());

            var cacheService = sp.GetService<ISemanticCacheService>();
            if (cacheService != null)
            {
                var cacheOptions = sp.GetRequiredService<IOptions<SemanticCacheOptions>>().Value;
                if (cacheOptions.Enabled)
                {
                    return new SemanticCacheChatClient(
                        governedClient,
                        cacheService,
                        cacheOptions.AgentName,
                        cacheOptions.SimilarityThreshold,
                        sp.GetRequiredService<ILogger<SemanticCacheChatClient>>());
                }
            }

            return governedClient;
        });

        return services;
    }

    private static IServiceCollection AddAgenticGateway(this IServiceCollection services)
    {
        services.AddSingleton<ICostTracker, CostTracker>();
        services.AddSingleton<IServiceGateway, ServiceGateway>();
        services.AddSingleton<ITokenAuditService, AgenticSystem.Infrastructure.Observability.TokenAuditService>();
        return services;
    }

    private static IServiceCollection AddAgenticQualityGates(this IServiceCollection services)
    {
        services.AddSingleton<IQualityGate, InputValidationGate>();
        services.AddSingleton<IQualityGate, ResponseQualityGate>();
        services.AddSingleton<IQualityGateService, QualityGateService>();
        return services;
    }

    private static IServiceCollection AddAgenticMcpAndSkills(this IServiceCollection services)
    {
        services.AddSingleton<IMCPPluginManager, MCPPluginManager>();
        services.AddSingleton<McpToolsAIFunctionAdapter>();
        services.AddHostedService<DynamicSkillCatalogHostedService>();
        services.AddSingleton<UnifiedAIToolProvider>();
        return services;
    }

    private static IServiceCollection AddAgenticAgentFramework(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<ChatClientPlanner>();
        services.AddSingleton<IAgentCollaborationWorkflow, AgentCollaborationWorkflow>();
        services.AddSingleton<AgenticVectorStoreAdapter>();
        services.AddSingleton<IAgentChannelService, FrameworkAgentChannelService>();

        var ollamaEnabled = configuration.GetValue<bool>("AgenticSystem:Ollama:Enabled");
        var enableStreaming = configuration.GetValue<bool>("AgenticSystem:Ollama:EnableStreaming");

        var hasChatClient = ollamaEnabled || services.Any(d => d.ServiceType == typeof(IChatClient));

        if (hasChatClient)
        {
            var orchestratorMetadata = OrchestratorMetadata.Default;

            services.AddSingleton(orchestratorMetadata);
            services.AddSingleton<AgentFrameworkFactory>();
            services.AddSingleton<SimpleSessionStoreAdapter>();
            services.AddSingleton<OrchestratorAuxiliaryToolService>();
            services.AddSingleton<OrchestratorInstructionService>();
            services.AddSingleton<OrchestratorToolBindingService>();

            services.AddSingleton<RAGContextProvider>(sp =>
            {
                var ragService = sp.GetService<IRAGService>();
                if (ragService is null) return null!;

                return new RAGContextProvider(
                    ragService,
                    sp.GetService<IContextBudgetManager>(),
                    sp.GetRequiredService<ILogger<RAGContextProvider>>());
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

        return services;
    }

    private static IServiceCollection AddAgenticRagAndMemory(this IServiceCollection services, IConfiguration configuration)
    {
        // Memory / Vector Store — Refactored for Hot-Swapping (Phase 0)
        services.Configure<MemorySettings>(configuration.GetSection("AgenticSystem:Memory"));
        
        services.AddSingleton<IVectorStoreFactory, VectorStoreFactory>();
        services.AddSingleton<IVectorStore, HotSwappableVectorStore>();

        // RAG Services
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
            sp.GetService<IEmbeddingGenerator<string, Embedding<float>>>(),
            sp.GetRequiredService<IRerankingSettingsAccessor>(),
            sp.GetRequiredService<ILogger<LlmReRanker>>()));

        // Embedding Provider — Refactored for Hot-Swapping (Phase 0)
        services.AddSingleton<IEmbeddingProviderFactory, AgenticSystem.Infrastructure.Embeddings.EmbeddingProviderFactory>();
        services.AddSingleton<IEmbeddingProvider, AgenticSystem.Infrastructure.Embeddings.HotSwappableEmbeddingProvider>();

        var storageMode = configuration["AgenticSystem:LocalExecution:StorageMode"];
        if (string.Equals(storageMode, "PostgreSQL", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IAdvancedRetrievalService, PostgresAdvancedRetrievalService>();
            services.AddScoped<IKnowledgeRoomService, PostgresKnowledgeRoomStore>();
            services.AddScoped<IAgentKnowledgeRoomStore, PostgresAgentKnowledgeRoomStore>();
        }
        else
        {
            services.AddSingleton<IAdvancedRetrievalService, InMemoryAdvancedRetrievalService>();
            services.AddSingleton<IKnowledgeRoomService, InMemoryKnowledgeRoomStore>();
            services.AddSingleton<IAgentKnowledgeRoomStore, PostgresAgentKnowledgeRoomStore>();
        }
        services.AddSingleton<IRAGService, RAGService>();

        // Obsidian Sync
        services.AddSingleton<IObsidianSync>(sp =>
        {
            var vectorStore = sp.GetRequiredService<IVectorStore>();
            var logger = sp.GetRequiredService<ILogger<FileObsidianSync>>();
            var vaultPath = configuration["AgenticSystem:Memory:ObsidianVaultPath"];
            return new FileObsidianSync(vectorStore, logger, vaultPath);
        });

        return services;
    }

    private static IServiceCollection AddAgenticDocumentServices(this IServiceCollection services)
    {
        // Parsers
        services.AddSingleton<IDocumentParser, MarkdownParser>();
        services.AddSingleton<IDocumentParser, PlainTextParser>();
        services.AddSingleton<IDocumentParser, HtmlParser>();
        services.AddSingleton<IDocumentParser, PdfDocumentParser>();
        services.AddSingleton<IDocumentParser, DocxDocumentParser>();

        // Pipeline
        services.AddSingleton<IChunkingStrategy, HybridChunkingStrategy>();
        services.AddSingleton<IMultimodalProcessor, LlmMultimodalProcessor>();
        services.AddSingleton<IDocumentIngestionPipeline, DocumentIngestionPipeline>();
        services.AddSingleton<IDataConnector, FileSystemDataConnector>();
        services.AddHostedService<DataSyncBackgroundService>();

        return services;
    }

    private static IServiceCollection AddAgenticBackgroundServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHostedService<SelfImprovementBackgroundJob>();
        services.AddHostedService<ExternalQuotaSyncHostedService>();
        
        var storageMode = configuration["AgenticSystem:LocalExecution:StorageMode"];
        if (!string.Equals(storageMode, "SQLite", StringComparison.OrdinalIgnoreCase) && 
            !string.Equals(storageMode, "InMemory", StringComparison.OrdinalIgnoreCase))
        {
            services.AddHostedService<RealTimeConfigReloadBackgroundService>();
        }
        
        return services;
    }

    private static IServiceCollection AddAgenticMultiTenancy(this IServiceCollection services)
    {
        services.AddScoped<TenantContext>();
        services.AddSingleton<ITenantContextAccessor, TenantContextAccessor>();
        services.AddScoped<ITenantResolver, TenantResolver>();
        // ITenantStore implementation should be registered by the storage mode
        return services;
    }

    public static IServiceCollection UsePostgresSessionStore(this IServiceCollection services, string connectionString)
    {
        EnsureDbContextRegistrations(services, connectionString);
        ReplaceSingleton<Core.Interfaces.ISessionStore, PostgresSessionStore>(services);
        return services;
    }

    public static IServiceCollection UsePostgresSessionSummaryStore(this IServiceCollection services, string connectionString)
    {
        EnsureDbContextRegistrations(services, connectionString);
        ReplaceSingleton<Core.Interfaces.ISessionSummaryStore, PostgresSessionSummaryStore>(services);
        return services;
    }

    public static IServiceCollection UsePostgresVectorStore(this IServiceCollection services, string connectionString)
    {
        EnsureDbContextRegistrations(services, connectionString);
        ReplaceSingleton<IVectorStore, PostgresVectorStore>(services);
        return services;
    }

    public static IServiceCollection UsePostgresCostTracker(this IServiceCollection services, string connectionString)
    {
        EnsureDbContextRegistrations(services, connectionString);
        ReplaceSingleton<ICostTracker, PostgresCostTracker>(services);
        return services;
    }

    public static IServiceCollection UsePostgresSmartRouter(this IServiceCollection services, string connectionString)
    {
        EnsureDbContextRegistrations(services, connectionString);

        var innerDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(ISmartRouter));
        if (innerDescriptor is not null)
            services.Remove(innerDescriptor);

        services.AddSingleton<ISmartRouter>(sp =>
        {
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

    public static IServiceCollection UsePostgresOperationalStore(this IServiceCollection services)
    {
        ReplaceSingleton<IOperationalStore, PostgresOperationalStore>(services);
        ReplaceSingleton<IRuntimeEvaluator, RuntimeEvaluatorService>(services);
        return services;
    }

    public static IServiceCollection UsePostgresSecurityAndAudit(this IServiceCollection services, string connectionString, bool useInMemoryEventBus = false)
    {
        EnsureDbContextRegistrations(services, connectionString);
        ReplaceSingleton<IAuditLog, PostgresAuditLog>(services);
        ReplaceSingleton<IPermissionService, PostgresPermissionService>(services);
        
        if (useInMemoryEventBus)
        {
            ReplaceSingleton<IEventBus, InMemoryEventBus>(services);
            // No outbox processor needed for in-memory event bus
        }
        else
        {
            services.AddHostedService<Persistence.OutboxProcessorBackgroundService>();
            ReplaceSingleton<IEventBus, PostgresEventBus>(services);
        }
        
        services.AddSingleton<IPolicyStore, PostgresPolicyStore>();
        return services;
    }

    public static IServiceCollection UsePostgresMigrationJobStore(this IServiceCollection services, string connectionString)
    {
        EnsureDbContextRegistrations(services, connectionString);
        ReplaceSingleton<IMigrationJobStore, PostgresMigrationJobStore>(services);
        return services;
    }

    public static IServiceCollection UsePostgresEmbeddingModelStore(this IServiceCollection services, string connectionString)
    {
        EnsureDbContextRegistrations(services, connectionString);
        ReplaceSingleton<IEmbeddingModelStore, PostgresEmbeddingModelStore>(services);
        return services;
    }

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
            ReplaceSingleton<IExternalQuotaSyncService, InMemoryExternalQuotaSyncService>(services);
            return services;
        }

        if (string.Equals(storageMode, "SQLite", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("ConnectionStrings:SessionStore must be configured when AgenticSystem:LocalExecution:StorageMode is SQLite.");
            }

            EnsureDbContextRegistrations(services, connectionString);

            ReplaceSingleton<IVectorStore, SqliteVectorStore>(services);
            ReplaceSingleton<IExternalQuotaSyncService, InMemoryExternalQuotaSyncService>(services);
            
            // Register MockEmbeddingGenerator for load testing
            services.AddSingleton<Microsoft.Extensions.AI.IEmbeddingGenerator<string, Microsoft.Extensions.AI.Embedding<float>>>(new AgenticSystem.Infrastructure.Memory.MockEmbeddingGenerator());
            
            return services;
        }

        if (!string.Equals(storageMode, "PostgreSQL", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Unsupported local execution storage mode '{storageMode}'. Use InMemory, SQLite or PostgreSQL.");
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

        var useInMemoryEventBus = configuration.GetValue<bool>("AgenticSystem:EventBus:UseInMemory");

        services.UsePostgresSessionStore(connectionString);
        services.UsePostgresSessionSummaryStore(connectionString);
        services.UsePostgresVectorStore(connectionString);
        services.UsePostgresCostTracker(connectionString);
        services.UsePostgresSmartRouter(connectionString);
        services.UsePostgresSemanticCache(connectionString);
        services.UsePostgresOperationalStore();
        services.UsePostgresSecurityAndAudit(connectionString, useInMemoryEventBus);
        services.UsePostgresMigrationJobStore(connectionString);
        services.UsePostgresEmbeddingModelStore(connectionString);
        services.UsePostgresQualityStores(connectionString);
        services.UsePostgresKnowledgeGraph(connectionString);
        services.UsePostgresWorkflowEngine(connectionString);
        services.UsePostgresAdvancedIntelligence(connectionString);
        services.UsePostgresPlatformStores(connectionString);

        return services;
    }

    public static IServiceCollection UsePostgresPlatformStores(this IServiceCollection services, string connectionString)
    {
        EnsureDbContextRegistrations(services, connectionString);
        ReplaceSingleton<IDataConnectorStore, PostgresDataConnectorStore>(services);
        ReplaceSingleton<IAgentMarketplace, PostgresAgentMarketplace>(services);
        ReplaceSingleton<IMemoryLifecycleStore, PostgresMemoryLifecycleStore>(services);
        return services;
    }

    public static IServiceCollection UsePostgresAdvancedIntelligence(this IServiceCollection services, string connectionString)
    {
        EnsureDbContextRegistrations(services, connectionString);
        ReplaceSingleton<IModelPerformanceStore, PostgresModelPerformanceStore>(services);
        return services;
    }

    public static IServiceCollection UsePostgresWorkflowEngine(this IServiceCollection services, string connectionString)
    {
        EnsureDbContextRegistrations(services, connectionString);
        ReplaceSingleton<IWorkflowStore, PostgresWorkflowStore>(services);
        ReplaceSingleton<IWorkflowEngine, DefaultWorkflowEngine>(services);
        return services;
    }

    public static IServiceCollection UsePostgresKnowledgeGraph(this IServiceCollection services, string connectionString)
    {
        EnsureDbContextRegistrations(services, connectionString);
        ReplaceSingleton<IKnowledgeGraphService, PostgresKnowledgeGraphService>(services);
        ReplaceSingleton<IKnowledgeGraphStore, PostgresKnowledgeGraphService>(services);
        return services;
    }

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
        if (services.Any(descriptor => descriptor.ServiceType == typeof(DbContextOptions<AgenticDbContext>)))
        {
            return;
        }

        var isSqlite = connectionString.Contains(".db") || connectionString.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase);

        services.AddDbContext<AgenticDbContext>(options =>
        {
            if (isSqlite)
            {
                options.UseSqlite(connectionString);
            }
            else
            {
                options.UseNpgsql(connectionString, npgsql =>
                {
                    npgsql.MigrationsHistoryTable("__ef_migrations_history");
                    npgsql.UseVector();
                });
            }
        }, contextLifetime: ServiceLifetime.Scoped, optionsLifetime: ServiceLifetime.Singleton);

        services.AddDbContextFactory<AgenticDbContext>(options =>
        {
            if (isSqlite)
            {
                options.UseSqlite(connectionString);
            }
            else
            {
                options.UseNpgsql(connectionString, npgsql =>
                {
                    npgsql.MigrationsHistoryTable("__ef_migrations_history");
                    npgsql.UseVector();
                });
            }
        });
    }

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
