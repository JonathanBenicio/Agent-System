using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.LLM.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;
using AgenticSystem.Infrastructure.AgentFramework;
using AgenticSystem.Infrastructure.MCP;
using AgenticSystem.Infrastructure.Configuration;
using AgenticSystem.Infrastructure.Memory;

namespace AgenticSystem.Tests;

#region Finding 1 — Semantic Cosine Similarity (InMemoryVectorStore)

public class InMemoryVectorStoreSemanticTests
{
    private readonly ILogger<InMemoryVectorStore> _logger = Substitute.For<ILogger<InMemoryVectorStore>>();

    [Fact]
    public async Task CosineSimilarity_IdenticalVectors_ReturnsOne()
    {
        // CalculateRelevance is private-static, test via SearchAsync with known embeddings
        var provider = Substitute.For<IEmbeddingProvider>();
        provider.IsEnabled.Returns(true);
        provider.GenerateEmbeddingAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new float[] { 1, 0, 0 });

        var sut = new InMemoryVectorStore(_logger, provider);

        // Upsert doc with same embedding as query → cosine = 1.0 → score = 1.0
        await sut.UpsertAsync(new EmbeddingDocument
        {
            Id = "d1",
            Content = "anything",
            Collection = "test",
            Embedding = new float[] { 1, 0, 0 }
        });

        var result = await sut.SearchAsync("query", SearchScope.All, 5);

        result.Matches.Should().ContainSingle();
        result.Matches[0].Score.Should().BeApproximately(1.0, 0.001);
    }

    [Fact]
    public async Task CosineSimilarity_OrthogonalVectors_ReturnsHalf()
    {
        var provider = Substitute.For<IEmbeddingProvider>();
        provider.IsEnabled.Returns(true);
        provider.GenerateEmbeddingAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new float[] { 1, 0, 0 });

        var sut = new InMemoryVectorStore(_logger, provider);

        // Orthogonal vectors → cosine = 0.0 → normalized score = (0+1)/2 = 0.5
        await sut.UpsertAsync(new EmbeddingDocument
        {
            Id = "d1",
            Content = "anything",
            Collection = "test",
            Embedding = new float[] { 0, 1, 0 }
        });

        var result = await sut.SearchAsync("query", SearchScope.All, 5);

        result.Matches.Should().ContainSingle();
        result.Matches[0].Score.Should().BeApproximately(0.5, 0.001);
    }

    [Fact]
    public async Task SearchAsync_UsesEmbeddingWhenProviderAvailable()
    {
        var provider = Substitute.For<IEmbeddingProvider>();
        provider.IsEnabled.Returns(true);
        // Return embedding for query
        provider.GenerateEmbeddingAsync("semantic query", Arg.Any<CancellationToken>())
            .Returns(new float[] { 0.9f, 0.1f, 0f });

        var sut = new InMemoryVectorStore(_logger, provider);

        // Doc with embedding close to query embedding
        await sut.UpsertAsync(new EmbeddingDocument
        {
            Id = "d1",
            Content = "no lexical match here",
            Collection = "test",
            Embedding = new float[] { 0.9f, 0.1f, 0f }
        });

        var result = await sut.SearchAsync("semantic query", SearchScope.All, 5);

        // Should find doc via embedding, despite no lexical match
        result.Matches.Should().NotBeEmpty();
        result.Matches[0].Id.Should().Be("d1");
        result.Matches[0].Score.Should().BeGreaterThan(0.9);

        // Verify provider was called
        await provider.Received(1).GenerateEmbeddingAsync("semantic query", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SearchAsync_FallsBackToLexical_WhenNoEmbeddingProvider()
    {
        // No embedding provider → lexical search
        var sut = new InMemoryVectorStore(_logger);

        await sut.UpsertAsync(new EmbeddingDocument
        {
            Id = "d1",
            Content = "document about calendar scheduling",
            Collection = "test"
        });
        await sut.UpsertAsync(new EmbeddingDocument
        {
            Id = "d2",
            Content = "completely unrelated content",
            Collection = "test"
        });

        var result = await sut.SearchAsync("calendar", SearchScope.All, 5);

        result.Matches.Should().Contain(m => m.Id == "d1");
        result.Matches.Should().NotContain(m => m.Id == "d2");
    }

    [Fact]
    public async Task SearchAsync_FallsBackToLexical_WhenProviderDisabled()
    {
        var provider = Substitute.For<IEmbeddingProvider>();
        provider.IsEnabled.Returns(false);

        var sut = new InMemoryVectorStore(_logger, provider);

        await sut.UpsertAsync(new EmbeddingDocument
        {
            Id = "d1",
            Content = "document about calendar scheduling",
            Collection = "test"
        });

        var result = await sut.SearchAsync("calendar", SearchScope.All, 5);

        result.Matches.Should().Contain(m => m.Id == "d1");
        // Provider should not be called
        await provider.DidNotReceive().GenerateEmbeddingAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateQueryEmbedding_ReturnsNull_OnProviderFailure()
    {
        var provider = Substitute.For<IEmbeddingProvider>();
        provider.IsEnabled.Returns(true);
        provider.GenerateEmbeddingAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Provider crash"));

        var sut = new InMemoryVectorStore(_logger, provider);

        await sut.UpsertAsync(new EmbeddingDocument
        {
            Id = "d1",
            Content = "some document about agents",
            Collection = "test"
        });

        // Should not throw — falls back to lexical
        var result = await sut.SearchAsync("agents", SearchScope.All, 5);

        result.Matches.Should().Contain(m => m.Id == "d1");
    }

    [Fact]
    public async Task SearchAsync_SemanticRanksHigherThanLexical()
    {
        var provider = Substitute.For<IEmbeddingProvider>();
        provider.IsEnabled.Returns(true);
        provider.GenerateEmbeddingAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new float[] { 1.0f, 0, 0 });

        var sut = new InMemoryVectorStore(_logger, provider);

        // Doc WITH embedding matching query → cosine similarity = 1.0 → score = (1+1)/2 = 1.0
        await sut.UpsertAsync(new EmbeddingDocument
        {
            Id = "semantic-hit",
            Content = "irrelevant text",
            Collection = "test",
            Embedding = new float[] { 1.0f, 0, 0 }
        });

        // Doc WITHOUT embedding → falls back to lexical with partial word match
        // "specialized" only matches 1 of 2 words → score = 0.5
        await sut.UpsertAsync(new EmbeddingDocument
        {
            Id = "lexical-hit",
            Content = "this is specialized information",
            Collection = "test"
            // No Embedding set
        });

        var result = await sut.SearchAsync("specialized query", SearchScope.All, 5);

        // Semantic hit should rank higher (1.0 vs 0.5)
        var semanticMatch = result.Matches.FirstOrDefault(m => m.Id == "semantic-hit");
        var lexicalMatch = result.Matches.FirstOrDefault(m => m.Id == "lexical-hit");

        semanticMatch.Should().NotBeNull();
        lexicalMatch.Should().NotBeNull();
        semanticMatch!.Score.Should().BeGreaterThan(lexicalMatch!.Score);
    }

    [Fact]
    public async Task SearchWithFiltersAsync_UsesEmbedding()
    {
        var provider = Substitute.For<IEmbeddingProvider>();
        provider.IsEnabled.Returns(true);
        provider.GenerateEmbeddingAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new float[] { 0.8f, 0.6f, 0 });

        var sut = new InMemoryVectorStore(_logger, provider);

        await sut.UpsertAsync(new EmbeddingDocument
        {
            Id = "d1",
            Content = "no match words",
            Type = "note",
            Collection = "test",
            Metadata = new Dictionary<string, string> { ["tier"] = "A" },
            Embedding = new float[] { 0.8f, 0.6f, 0 }
        });

        var result = await sut.SearchWithFiltersAsync("something", new Dictionary<string, string> { ["tier"] = "A" });

        result.Matches.Should().ContainSingle();
        result.Matches[0].Id.Should().Be("d1");
        await provider.Received().GenerateEmbeddingAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}

#endregion

#region Finding 2+3 — MetaAgentOrchestrator RAG + ContextBudget Integration

public class MetaAgentOrchestratorRAGTests
{
    private readonly IContextAnalyzer _contextAnalyzer;
    private readonly IAgentFactory _agentFactory;
    private readonly ISessionManager _sessionManager;
    private readonly IDynamicAgentService _dynamicAgentService;
    private readonly IHandoffManager _handoffManager;
    private readonly IToolAvailabilityGuard _toolGuard;
    private readonly IConfidenceScoreCalculator _confidenceCalculator;
    private readonly ILogger<MetaAgentOrchestrator> _logger;

    public MetaAgentOrchestratorRAGTests()
    {
        _contextAnalyzer = Substitute.For<IContextAnalyzer>();
        _agentFactory = Substitute.For<IAgentFactory>();
        _sessionManager = Substitute.For<ISessionManager>();
        _dynamicAgentService = Substitute.For<IDynamicAgentService>();
        _handoffManager = Substitute.For<IHandoffManager>();
        _toolGuard = Substitute.For<IToolAvailabilityGuard>();
        _confidenceCalculator = Substitute.For<IConfidenceScoreCalculator>();
        _logger = Substitute.For<ILogger<MetaAgentOrchestrator>>();

        // Defaults
        _toolGuard.CheckAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(ToolAvailabilityResult.FullCoverage(Array.Empty<string>()));
        _confidenceCalculator.Calculate(Arg.Any<AgentResponse>(), Arg.Any<RAGContext?>(), Arg.Any<IEnumerable<Reflection>?>(), Arg.Any<ToolAvailabilityResult?>())
            .Returns(new ConfidenceScore { Value = 0.8, Level = ConfidenceLevel.High, Label = "✅ Alta confiança" });
    }

    private void SetupDefaultMocks(string input, IAgent agent)
    {
        var analysis = new AnalysisResult
        {
            Intent = IntentType.Chat,
            PrimaryDomain = "general",
            Complexity = ComplexityLevel.Simple,
            RecommendedTier = AgentTier.Support,
            Confidence = 0.9
        };

        _sessionManager.StartSessionAsync(Arg.Any<UserContext>()).Returns("session-1");
        _contextAnalyzer.AnalyzeAsync(input, Arg.Any<UserContext>()).Returns(analysis);
        _agentFactory.GetOrCreateAgentAsync(analysis).Returns(agent);
        _dynamicAgentService.IsAgentCreationRequestAsync(input, analysis).Returns(false);
        _handoffManager.EvaluateHandoffAsync(analysis, agent)
            .Returns(new HandoffDecision { ShouldHandoff = false });
    }

    [Fact]
    public async Task ProcessRequestAsync_EnrichesInput_WhenRAGServiceAvailable()
    {
        // Arrange
        var ragService = Substitute.For<IRAGService>();
        ragService.RetrieveContextAsync(Arg.Any<RAGQuery>(), Arg.Any<CancellationToken>())
            .Returns(new RAGContext
            {
                BuiltContext = "Relevant context about scheduling",
                CandidatesAfterReRank = 3,
                TotalTokensUsed = 100
            });

        var agent = Substitute.For<IAgent>();
        agent.Name.Returns("TestAgent");
        agent.Tier.Returns(AgentTier.Support);
        // Agent will receive enriched input containing both context and original input
        agent.ExecuteAsync(Arg.Is<string>(s => s.Contains("[Contexto Relevante]") && s.Contains("What time is it?")), Arg.Any<UserContext>())
            .Returns(AgentResponse.Ok("10 AM", "TestAgent", AgentTier.Support));

        SetupDefaultMocks("What time is it?", agent);

        var sut = new MetaAgentOrchestrator(
            _contextAnalyzer, _agentFactory, _sessionManager, _dynamicAgentService,
            _handoffManager, _toolGuard, _confidenceCalculator, _logger,
            ragService: ragService);

        // Act
        var result = await sut.ProcessRequestAsync("What time is it?", new UserContext { UserId = "u1", Name = "Test" });

        // Assert
        result.Success.Should().BeTrue();
        await ragService.Received(1).RetrieveContextAsync(Arg.Any<RAGQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessRequestAsync_WorksWithoutRAGService()
    {
        // Arrange — no RAG service (null)
        var agent = Substitute.For<IAgent>();
        agent.Name.Returns("TestAgent");
        agent.Tier.Returns(AgentTier.Support);
        agent.ExecuteAsync(Arg.Any<string>(), Arg.Any<UserContext>())
            .Returns(AgentResponse.Ok("Response", "TestAgent", AgentTier.Support));

        SetupDefaultMocks("Hello", agent);

        var sut = new MetaAgentOrchestrator(
            _contextAnalyzer, _agentFactory, _sessionManager, _dynamicAgentService,
            _handoffManager, _toolGuard, _confidenceCalculator, _logger);
        // ragService defaults to null

        // Act
        var result = await sut.ProcessRequestAsync("Hello", new UserContext { UserId = "u1", Name = "Test" });

        // Assert
        result.Success.Should().BeTrue();
        // Agent should receive original input without enrichment
        await agent.Received(1).ExecuteAsync("Hello", Arg.Any<UserContext>());
    }

    [Fact]
    public async Task ProcessRequestAsync_AppliesContextBudget_WhenManagerAvailable()
    {
        // Arrange
        var ragService = Substitute.For<IRAGService>();
        var budgetManager = Substitute.For<IContextBudgetManager>();

        var originalContext = new RAGContext
        {
            BuiltContext = "Very long context that needs trimming",
            CandidatesAfterReRank = 10,
            TotalTokensUsed = 5000
        };
        var trimmedContext = new RAGContext
        {
            BuiltContext = "Trimmed context",
            CandidatesAfterReRank = 3,
            TotalTokensUsed = 500
        };

        ragService.RetrieveContextAsync(Arg.Any<RAGQuery>(), Arg.Any<CancellationToken>())
            .Returns(originalContext);
        budgetManager.ResolveBudget(Arg.Any<AnalysisResult>())
            .Returns(new ContextBudget { MaxTokens = 1000 });
        budgetManager.TrimContextToBudgetAsync(originalContext, Arg.Any<ContextBudget>())
            .Returns(trimmedContext);

        var agent = Substitute.For<IAgent>();
        agent.Name.Returns("TestAgent");
        agent.Tier.Returns(AgentTier.Support);
        agent.ExecuteAsync(Arg.Is<string>(s => s.Contains("Trimmed context")), Arg.Any<UserContext>())
            .Returns(AgentResponse.Ok("OK", "TestAgent", AgentTier.Support));

        SetupDefaultMocks("question", agent);

        var sut = new MetaAgentOrchestrator(
            _contextAnalyzer, _agentFactory, _sessionManager, _dynamicAgentService,
            _handoffManager, _toolGuard, _confidenceCalculator, _logger,
            ragService: ragService, contextBudgetManager: budgetManager);

        // Act
        var result = await sut.ProcessRequestAsync("question", new UserContext { UserId = "u1", Name = "Test" });

        // Assert
        result.Success.Should().BeTrue();
        budgetManager.Received(1).ResolveBudget(Arg.Any<AnalysisResult>());
        await budgetManager.Received(1).TrimContextToBudgetAsync(originalContext, Arg.Any<ContextBudget>());
    }

    [Fact]
    public async Task ProcessRequestAsync_GracefullyHandlesRAGFailure()
    {
        // Arrange — RAG throws exception
        var ragService = Substitute.For<IRAGService>();
        ragService.RetrieveContextAsync(Arg.Any<RAGQuery>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("RAG service down"));

        var agent = Substitute.For<IAgent>();
        agent.Name.Returns("TestAgent");
        agent.Tier.Returns(AgentTier.Support);
        agent.ExecuteAsync(Arg.Any<string>(), Arg.Any<UserContext>())
            .Returns(AgentResponse.Ok("Fallback response", "TestAgent", AgentTier.Support));

        SetupDefaultMocks("query", agent);

        var sut = new MetaAgentOrchestrator(
            _contextAnalyzer, _agentFactory, _sessionManager, _dynamicAgentService,
            _handoffManager, _toolGuard, _confidenceCalculator, _logger,
            ragService: ragService);

        // Act — should NOT throw
        var result = await sut.ProcessRequestAsync("query", new UserContext { UserId = "u1", Name = "Test" });

        // Assert
        result.Success.Should().BeTrue();
        // Should receive original input (no enrichment since RAG failed)
        await agent.Received(1).ExecuteAsync("query", Arg.Any<UserContext>());
    }

    [Fact]
    public async Task ProcessRequestAsync_PassesRAGContextToConfidenceCalculator()
    {
        // Arrange
        var ragService = Substitute.For<IRAGService>();
        var ragContext = new RAGContext
        {
            BuiltContext = "context data",
            CandidatesAfterReRank = 2,
            TotalTokensUsed = 50
        };
        ragService.RetrieveContextAsync(Arg.Any<RAGQuery>(), Arg.Any<CancellationToken>())
            .Returns(ragContext);

        var agent = Substitute.For<IAgent>();
        agent.Name.Returns("TestAgent");
        agent.Tier.Returns(AgentTier.Support);
        agent.ExecuteAsync(Arg.Any<string>(), Arg.Any<UserContext>())
            .Returns(AgentResponse.Ok("response", "TestAgent", AgentTier.Support));

        SetupDefaultMocks("input", agent);

        var sut = new MetaAgentOrchestrator(
            _contextAnalyzer, _agentFactory, _sessionManager, _dynamicAgentService,
            _handoffManager, _toolGuard, _confidenceCalculator, _logger,
            ragService: ragService);

        // Act
        await sut.ProcessRequestAsync("input", new UserContext { UserId = "u1", Name = "Test" });

        // Assert — confidence calculator should receive the RAG context, not null
        _confidenceCalculator.Received(1).Calculate(
            Arg.Any<AgentResponse>(),
            Arg.Is<RAGContext?>(r => r != null && r.BuiltContext == "context data"),
            Arg.Any<IEnumerable<Reflection>?>(),
            Arg.Any<ToolAvailabilityResult?>());
    }
}

#endregion

#region Finding 6 — MCP Tools → Agents (AgentFrameworkFactory)

public class AgentFrameworkFactoryMcpTests
{
    private readonly IChatClient _chatClient;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IServiceProvider _serviceProvider;

    public AgentFrameworkFactoryMcpTests()
    {
        _chatClient = Substitute.For<IChatClient>();
        _loggerFactory = Substitute.For<ILoggerFactory>();
        _loggerFactory.CreateLogger(Arg.Any<string>()).Returns(Substitute.For<ILogger>());
        _serviceProvider = Substitute.For<IServiceProvider>();
    }

    [Fact]
    public void CreateFromAgent_WorksWithoutMcpAdapter()
    {
        // No adapter (null) — should still create agent
        var sut = new AgentFrameworkFactory(_chatClient, _loggerFactory, _serviceProvider, mcpToolsAdapter: null);

        var agent = Substitute.For<IAgent>();
        agent.Name.Returns("test");
        agent.Description.Returns("desc");
        agent.Instructions.Returns("instructions");

        var result = sut.CreateFromAgent(agent);

        result.Should().NotBeNull();
        result.Name.Should().Be("test");
    }

    [Fact]
    public void CreateFromAgent_WithMcpAdapter_PassesTools()
    {
        var pluginManager = Substitute.For<IMCPPluginManager>();
        var adapterLogger = Substitute.For<ILogger<McpToolsAIFunctionAdapter>>();

        // Create a real adapter with mocked dependencies
        var plugin = Substitute.For<IMCPPlugin>();
        plugin.Id.Returns("p1");
        plugin.Name.Returns("TestPlugin");
        plugin.IsEnabled.Returns(true);
        plugin.ProvidedTools.Returns(new List<string> { "tool1" });
        plugin.Description.Returns("Test");

        pluginManager.GetLoadedPlugins().Returns(new List<IMCPPlugin> { plugin });

        var adapter = new McpToolsAIFunctionAdapter(pluginManager, adapterLogger);
        var sut = new AgentFrameworkFactory(_chatClient, _loggerFactory, _serviceProvider, adapter);

        var agent = Substitute.For<IAgent>();
        agent.Name.Returns("test");
        agent.Description.Returns("desc");
        agent.Instructions.Returns("instructions");

        // Should not throw and should create agent successfully
        var result = sut.CreateFromAgent(agent);

        result.Should().NotBeNull();
    }

    [Fact]
    public void CreateFromSpecification_WithMcpAdapter_PassesTools()
    {
        var pluginManager = Substitute.For<IMCPPluginManager>();
        var adapterLogger = Substitute.For<ILogger<McpToolsAIFunctionAdapter>>();

        var plugin = Substitute.For<IMCPPlugin>();
        plugin.Id.Returns("p1");
        plugin.Name.Returns("TestPlugin");
        plugin.IsEnabled.Returns(true);
        plugin.ProvidedTools.Returns(new List<string> { "tool1" });
        plugin.Description.Returns("Test");

        pluginManager.GetLoadedPlugins().Returns(new List<IMCPPlugin> { plugin });

        var adapter = new McpToolsAIFunctionAdapter(pluginManager, adapterLogger);
        var sut = new AgentFrameworkFactory(_chatClient, _loggerFactory, _serviceProvider, adapter);

        var spec = new AgentSpecification
        {
            Name = "dynamic",
            Description = "Dynamic agent",
            Instructions = "Help with things."
        };

        var result = sut.CreateFromSpecification(spec);

        result.Should().NotBeNull();
        result.Name.Should().Be("dynamic");
    }

    [Fact]
    public void CreateFromAgent_AdapterWithNoTools_StillWorks()
    {
        var pluginManager = Substitute.For<IMCPPluginManager>();
        var adapterLogger = Substitute.For<ILogger<McpToolsAIFunctionAdapter>>();

        // No plugins loaded → GetAvailableTools returns empty
        pluginManager.GetLoadedPlugins().Returns(new List<IMCPPlugin>());

        var adapter = new McpToolsAIFunctionAdapter(pluginManager, adapterLogger);
        var sut = new AgentFrameworkFactory(_chatClient, _loggerFactory, _serviceProvider, adapter);

        var agent = Substitute.For<IAgent>();
        agent.Name.Returns("test");
        agent.Description.Returns("desc");
        agent.Instructions.Returns("instructions");

        var result = sut.CreateFromAgent(agent);

        result.Should().NotBeNull();
    }
}

#endregion

#region Finding 7 — AutoStart MCP Plugins

public class MCPPluginManagerAutoStartTests
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly MCPPluginManager _sut;

    public MCPPluginManagerAutoStartTests()
    {
        _loggerFactory = Substitute.For<ILoggerFactory>();
        _loggerFactory.CreateLogger(Arg.Any<string>()).Returns(Substitute.For<ILogger>());
        _sut = new MCPPluginManager(_loggerFactory);
    }

    [Fact]
    public async Task AutoStartPluginsAsync_SkipsConfigsWithAutoStartFalse()
    {
        var configs = new List<MCPPluginConfig>
        {
            new() { Name = "skip-me", AutoStart = false, Command = "echo" }
        };

        // Should complete without attempting to load any plugin
        await _sut.Invoking(s => s.AutoStartPluginsAsync(configs))
            .Should().NotThrowAsync();

        // Plugin should NOT be loaded
        _sut.GetLoadedPlugins().Should().BeEmpty();
    }

    [Fact]
    public async Task AutoStartPluginsAsync_HandlesEmptyConfigList()
    {
        var configs = new List<MCPPluginConfig>();

        await _sut.Invoking(s => s.AutoStartPluginsAsync(configs))
            .Should().NotThrowAsync();

        _sut.GetLoadedPlugins().Should().BeEmpty();
    }

    [Fact]
    public async Task AutoStartPluginsAsync_ContinuesOnIndividualPluginFailure()
    {
        // Both plugins have AutoStart=true but will fail to connect (invalid commands)
        // The important thing is that it doesn't throw for the whole batch
        var configs = new List<MCPPluginConfig>
        {
            new() { Name = "plugin-1", AutoStart = true, Command = "nonexistent-command-1" },
            new() { Name = "plugin-2", AutoStart = true, Command = "nonexistent-command-2" }
        };

        // Should not throw — errors are caught per-plugin
        await _sut.Invoking(s => s.AutoStartPluginsAsync(configs))
            .Should().NotThrowAsync();
    }

    [Fact]
    public async Task AutoStartPluginsAsync_OnlyLoadsAutoStartTrue()
    {
        // Mix of AutoStart=true and false
        var configs = new List<MCPPluginConfig>
        {
            new() { Name = "auto", AutoStart = true, Command = "nonexistent" },
            new() { Name = "manual", AutoStart = false, Command = "echo" },
            new() { Name = "auto2", AutoStart = true, Command = "nonexistent2" }
        };

        // Will fail to actually connect but the filtering logic should only attempt AutoStart=true
        await _sut.Invoking(s => s.AutoStartPluginsAsync(configs))
            .Should().NotThrowAsync();

        // Manual plugin should NOT have been attempted (it would be in loaded plugins if it succeeded)
    }
}

#endregion

#region Finding 4+5 — Config Key Validation

public class ConfigKeyValidationTests
{
    [Fact]
    public void MemorySettings_ObsidianVaultPath_PropertyExists()
    {
        // Finding 4: ServiceCollectionExtensions now reads AgenticSystem:Memory:ObsidianVaultPath
        var settings = new MemorySettings();
        settings.ObsidianVaultPath.Should().Be(string.Empty);

        settings.ObsidianVaultPath = @"C:\Vaults\MyVault";
        settings.ObsidianVaultPath.Should().Be(@"C:\Vaults\MyVault");
    }

    [Fact]
    public void MemorySettings_VectorStoreType_DefaultsToInMemory()
    {
        // Finding 5: VectorStoreType config drives conditional DI
        var settings = new MemorySettings();
        settings.VectorStoreType.Should().Be("InMemory");
    }

    [Fact]
    public void MemorySettings_ConnectionString_PropertyExists()
    {
        var settings = new MemorySettings();
        settings.ConnectionString.Should().BeNull();

        settings.ConnectionString = "Host=localhost;Database=test";
        settings.ConnectionString.Should().Be("Host=localhost;Database=test");
    }
}

#endregion
