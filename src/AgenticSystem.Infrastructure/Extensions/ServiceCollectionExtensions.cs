using Microsoft.EntityFrameworkCore;
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
        services.Configure<ChatClientMiddlewareOptions>(configuration.GetSection("AgenticSystem:ChatClientMiddleware"));
        services.Configure<ReRankingOptions>(configuration.GetSection("AgenticSystem:RAG:ReRanking"));
        services.Configure<DynamicSkillsOptions>(configuration.GetSection("AgenticSystem:Skills"));


        // ─── Microsoft.Extensions.AI — registry contextual de IChatClient ───
        var openAiSettings = configuration.GetSection("AgenticSystem:OpenAI");
        var openAiApiKey = openAiSettings["ApiKey"];
        var enableStreaming = bool.TryParse(openAiSettings["EnableStreaming"], out var streaming)
            ? streaming
            : false;

        if (!string.IsNullOrWhiteSpace(openAiApiKey))
        {
            services.AddEmbeddingGenerator(_ =>
                new OpenAI.Embeddings.EmbeddingClient(
                    openAiSettings["EmbeddingModel"] ?? "text-embedding-3-small",
                    openAiApiKey).AsIEmbeddingGenerator())
                .UseDistributedCache()
                .UseOpenTelemetry(sourceName: "AgenticSystem.Embeddings");
        }

        services.AddSingleton<LLMManager>();
        services.AddSingleton<ILLMManager>(sp => sp.GetRequiredService<LLMManager>());
        services.AddSingleton<ContextAwareChatClient>(sp => new ContextAwareChatClient(sp.GetRequiredService<LLMManager>()));
        services.AddSingleton<IChatClient>(sp => new GovernedChatClient(
            sp.GetRequiredService<ContextAwareChatClient>(),
            sp.GetRequiredService<IQualityGateService>(),
            sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ChatClientMiddlewareOptions>>(),
            sp.GetRequiredService<ILogger<GovernedChatClient>>()));

        // Gateway
        services.AddSingleton<ICostTracker, CostTracker>();
        services.AddSingleton<IServiceGateway, ServiceGateway>();

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
            services.AddSingleton<AgentFrameworkFactory>();
            services.AddSingleton<AgentSessionBridge>();
            services.AddSingleton<OrchestratorAgentBuilder>();
            services.AddSingleton<IFrameworkOrchestratorService, FrameworkOrchestratorService>();

            // Protocol-facing IChatClient: delega ao pipeline completo do orquestrador
            // Usado pelo AddAIAgent para A2A/AG-UI (Finding 11 — protocol agent integration)
            services.AddKeyedSingleton<IChatClient>("protocol-orchestrator", (sp, _) =>
                new ProtocolOrchestratorChatClient(
                    sp.GetRequiredService<IFrameworkOrchestratorService>(),
                    sp.GetRequiredService<ILogger<ProtocolOrchestratorChatClient>>()));

            // Decorator: wraps IAgentFactory (HierarchicalAgentFactory) with Agent Framework pipeline
            var innerFactoryDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IAgentFactory));
            if (innerFactoryDescriptor is not null)
            {
                services.Remove(innerFactoryDescriptor);
                services.AddSingleton<IAgentFactory>(sp =>
                {
                    // Resolve the original HierarchicalAgentFactory
                    IAgentFactory innerFactory = innerFactoryDescriptor.ImplementationType is { } implType
                        ? (IAgentFactory)ActivatorUtilities.CreateInstance(sp, implType)
                        : innerFactoryDescriptor.ImplementationFactory is { } factory
                            ? (IAgentFactory)factory(sp)
                            : throw new InvalidOperationException(
                                "IAgentFactory registration has no ImplementationType or ImplementationFactory");
                    var frameworkFactory = sp.GetRequiredService<AgentFrameworkFactory>();
                    var sessionBridge = sp.GetRequiredService<AgentSessionBridge>();
                    var adapterLogger = sp.GetRequiredService<ILogger<AgentFrameworkAdapter>>();
                    return new AgentFrameworkAgentFactory(
                        innerFactory,
                        frameworkFactory,
                        sessionBridge,
                        adapterLogger,
                        sp.GetService<IAgentRuntimeCoordinator>(),
                        enableStreaming: enableStreaming);
                });
            }
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

        // Chunking Strategy
        services.AddSingleton<IChunkingStrategy, HybridChunkingStrategy>();

        // Document Ingestion Pipeline
        services.AddSingleton<IDocumentIngestionPipeline, DocumentIngestionPipeline>();

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
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(Core.Interfaces.ISessionStore));
        if (descriptor is not null)
            services.Remove(descriptor);

        services.AddSingleton<Core.Interfaces.ISessionStore>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<PostgresSessionStore>>();
            return new PostgresSessionStore(connectionString, logger);
        });

        return services;
    }

    /// <summary>
    /// Substitui o InMemoryVectorStore pelo PostgresVectorStore (produção).
    /// Chamar após AddAgenticSystemInfrastructure().
    /// </summary>
    public static IServiceCollection UsePostgresVectorStore(this IServiceCollection services, string connectionString)
    {
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IVectorStore));
        if (descriptor is not null)
            services.Remove(descriptor);

        services.AddSingleton<IVectorStore>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<PostgresVectorStore>>();
            return new PostgresVectorStore(connectionString, logger);
        });

        return services;
    }

    /// <summary>
    /// Substitui o CostTracker in-memory pelo PostgresCostTracker (produção).
    /// Chamar após AddAgenticSystemInfrastructure().
    /// </summary>
    public static IServiceCollection UsePostgresCostTracker(this IServiceCollection services, string connectionString)
    {
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(ICostTracker));
        if (descriptor is not null)
            services.Remove(descriptor);

        services.AddSingleton<ICostTracker>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<PostgresCostTracker>>();
            return new PostgresCostTracker(connectionString, logger);
        });

        return services;
    }

    /// <summary>
    /// Decora o SmartRouter existente com persistência PostgreSQL (write-through + warm-up).
    /// Chamar após AddAgenticSystemCore().
    /// </summary>
    public static IServiceCollection UsePostgresSmartRouter(this IServiceCollection services, string connectionString)
    {
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
            return new PersistentSmartRouter(inner, connectionString, logger);
        });

        return services;
    }

    /// <summary>
    /// Registra o operational store PostgreSQL para artefatos, métricas, reflexões e avaliações.
    /// Chamar após UseEntityFramework().
    /// </summary>
    public static IServiceCollection UsePostgresOperationalStore(this IServiceCollection services)
    {
        services.AddScoped<IOperationalStore, PostgresOperationalStore>();
        services.AddScoped<IRuntimeEvaluator, RuntimeEvaluatorService>();
        return services;
    }

    /// <summary>
    /// Substitui InMemoryMigrationJobStore pelo PostgresMigrationJobStore.
    /// </summary>
    public static IServiceCollection UsePostgresMigrationJobStore(this IServiceCollection services, string connectionString)
    {
        ReplaceSingleton<IMigrationJobStore>(services, sp =>
            new PostgresMigrationJobStore(connectionString, sp.GetRequiredService<ILogger<PostgresMigrationJobStore>>()));
        return services;
    }

    /// <summary>
    /// Substitui InMemoryEmbeddingModelStore pelo PostgresEmbeddingModelStore.
    /// </summary>
    public static IServiceCollection UsePostgresEmbeddingModelStore(this IServiceCollection services, string connectionString)
    {
        ReplaceSingleton<IEmbeddingModelStore>(services, sp =>
            new PostgresEmbeddingModelStore(connectionString, sp.GetRequiredService<ILogger<PostgresEmbeddingModelStore>>()));
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

        services.AddDbContext<AgenticDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsHistoryTable("__ef_migrations_history")));

        ReplaceSingleton<IAgentMemoryStore, EfAgentMemoryStore>(services);
        ReplaceSingleton<ITenantStore, EfTenantStore>(services);
        ReplaceSingleton<IScheduledTaskStore>(services, sp =>
            new PostgresScheduledTaskStore(connectionString, sp.GetRequiredService<ILogger<PostgresScheduledTaskStore>>()));
        ReplaceSingleton<IConfigStore>(services, sp =>
            new PostgresConfigStore(connectionString, sp.GetRequiredService<ILogger<PostgresConfigStore>>()));
        ReplaceSingleton<IRerankingAssetStore>(services, sp =>
            new PostgresRerankingAssetStore(connectionString, sp.GetRequiredService<ILogger<PostgresRerankingAssetStore>>()));

        services.UsePostgresSessionStore(connectionString);
        services.UsePostgresVectorStore(connectionString);
        services.UsePostgresCostTracker(connectionString);
        services.UsePostgresSmartRouter(connectionString);
        services.UsePostgresOperationalStore();
        services.UsePostgresMigrationJobStore(connectionString);
        services.UsePostgresEmbeddingModelStore(connectionString);

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
