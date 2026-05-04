using Microsoft.EntityFrameworkCore;
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
using AgenticSystem.Infrastructure.Embeddings;
using AgenticSystem.Infrastructure.Gateway;
using AgenticSystem.Infrastructure.LLM;
using AgenticSystem.Infrastructure.LLM.Adapters;
using AgenticSystem.Infrastructure.MCP;
using AgenticSystem.Infrastructure.Memory;
using AgenticSystem.Infrastructure.Persistence;
using AgenticSystem.Infrastructure.RAG;
using AgenticSystem.Infrastructure.Sync;
using AgenticSystem.Infrastructure.Vision;

namespace AgenticSystem.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAgenticSystemInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Configuration
        services.Configure<AgenticSystemSettings>(configuration.GetSection("AgenticSystem"));
        services.Configure<OpenAISettings>(configuration.GetSection("AgenticSystem:OpenAI"));
        services.Configure<OllamaSettings>(configuration.GetSection("AgenticSystem:Ollama"));
        services.Configure<GeminiSettings>(configuration.GetSection("AgenticSystem:Gemini"));
        services.Configure<ClaudeSettings>(configuration.GetSection("AgenticSystem:Claude"));
        services.Configure<GatewaySettings>(configuration.GetSection("AgenticSystem:Gateway"));

        // LLM Providers
        services.AddHttpClient<OpenAIProvider>();
        services.AddSingleton<ILLMProvider, OpenAIProvider>();

        // Ollama (local)
        services.AddHttpClient<OllamaProvider>();
        services.AddSingleton<ILLMProvider, OllamaProvider>();

        // Gemini
        services.AddHttpClient<GeminiProvider>();
        services.AddSingleton<ILLMProvider, GeminiProvider>();

        // Claude
        services.AddHttpClient<ClaudeProvider>();
        services.AddSingleton<ILLMProvider, ClaudeProvider>();

        // LLM Manager
        services.AddSingleton<ILLMManager, LLMManager>();

        // Embedding Provider
        services.AddHttpClient<OpenAIEmbeddingProvider>();
        services.AddSingleton<IEmbeddingProvider, OpenAIEmbeddingProvider>();

        // Vision Provider
        services.AddHttpClient<OpenAIVisionProvider>();
        services.AddSingleton<IVisionProvider, OpenAIVisionProvider>();

        // Gateway
        services.AddSingleton<CostTracker>();
        services.AddSingleton<IServiceGateway, ServiceGateway>();

        // Quality Gates
        services.AddSingleton<IQualityGate, InputValidationGate>();
        services.AddSingleton<IQualityGate, ResponseQualityGate>();
        services.AddSingleton<IQualityGateService, QualityGateService>();

        // MCP Plugin Manager
        services.AddSingleton<IMCPPluginManager, MCPPluginManager>();

        // Memory / Vector Store
        services.AddSingleton<IVectorStore, InMemoryVectorStore>();

        // Document Parsers
        services.AddSingleton<IDocumentParser, MarkdownParser>();
        services.AddSingleton<IDocumentParser, PlainTextParser>();
        services.AddSingleton<IDocumentParser, HtmlParser>();

        // Chunking Strategy
        services.AddSingleton<IChunkingStrategy, HybridChunkingStrategy>();

        // Document Ingestion Pipeline
        services.AddSingleton<IDocumentIngestionPipeline, DocumentIngestionPipeline>();

        // RAG
        services.AddSingleton<IReRanker, HeuristicReRanker>();
        services.AddSingleton<IRAGService, RAGService>();

        // Obsidian Sync
        services.AddSingleton<IObsidianSync>(sp =>
        {
            var vectorStore = sp.GetRequiredService<IVectorStore>();
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<FileObsidianSync>>();
            var vaultPath = configuration["AgenticSystem:VaultPath"];
            return new FileObsidianSync(vectorStore, logger, vaultPath);
        });

        // HttpClient for tools that need it
        services.AddHttpClient();

        // Microsoft.Extensions.AI adapter — registers if IChatClient is already in DI
        services.AddSingleton<ILLMProvider>(sp =>
        {
            var chatClient = sp.GetService<Microsoft.Extensions.AI.IChatClient>();
            if (chatClient is null)
                return null!;
            var logger = sp.GetRequiredService<ILogger<ChatClientProviderAdapter>>();
            return new ChatClientProviderAdapter(chatClient, logger);
        });

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
    /// Registra EF Core com PostgreSQL + EfSessionStore (produção).
    /// Chamar após AddAgenticSystemCore().
    /// </summary>
    public static IServiceCollection UseEntityFramework(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<AgenticDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsHistoryTable("__ef_migrations_history")));

        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(Core.Interfaces.ISessionStore));
        if (descriptor is not null)
            services.Remove(descriptor);

        services.AddScoped<Core.Interfaces.ISessionStore, EfSessionStore>();

        return services;
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
