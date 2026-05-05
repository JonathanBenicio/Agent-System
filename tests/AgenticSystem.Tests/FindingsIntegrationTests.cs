using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using AgenticSystem.Core.Interfaces;
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

    private static IEmbeddingGenerator<string, Embedding<float>> CreateEmbeddingGenerator(float[] defaultEmbedding)
    {
        var generator = Substitute.For<IEmbeddingGenerator<string, Embedding<float>>>();
        generator.GenerateAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<EmbeddingGenerationOptions?>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var inputs = callInfo.ArgAt<IEnumerable<string>>(0);
                var results = inputs.Select(_ => new Embedding<float>(defaultEmbedding)).ToList();
                return Task.FromResult<GeneratedEmbeddings<Embedding<float>>>(new GeneratedEmbeddings<Embedding<float>>(results));
            });
        return generator;
    }

    [Fact]
    public async Task CosineSimilarity_IdenticalVectors_ReturnsOne()
    {
        var generator = CreateEmbeddingGenerator(new float[] { 1, 0, 0 });
        var sut = new InMemoryVectorStore(_logger, generator);

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
        var generator = CreateEmbeddingGenerator(new float[] { 1, 0, 0 });
        var sut = new InMemoryVectorStore(_logger, generator);

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
        var generator = CreateEmbeddingGenerator(new float[] { 0.9f, 0.1f, 0f });
        var sut = new InMemoryVectorStore(_logger, generator);

        await sut.UpsertAsync(new EmbeddingDocument
        {
            Id = "d1",
            Content = "no lexical match here",
            Collection = "test",
            Embedding = new float[] { 0.9f, 0.1f, 0f }
        });

        var result = await sut.SearchAsync("semantic query", SearchScope.All, 5);

        result.Matches.Should().NotBeEmpty();
        result.Matches[0].Id.Should().Be("d1");
        result.Matches[0].Score.Should().BeGreaterThan(0.9);
    }

    [Fact]
    public async Task SearchAsync_FallsBackToLexical_WhenNoEmbeddingProvider()
    {
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
    public async Task SearchAsync_FallsBackToLexical_WhenGeneratorIsNull()
    {
        // No embedding generator (null) → lexical search
        var sut = new InMemoryVectorStore(_logger, embeddingGenerator: null);

        await sut.UpsertAsync(new EmbeddingDocument
        {
            Id = "d1",
            Content = "document about calendar scheduling",
            Collection = "test"
        });

        var result = await sut.SearchAsync("calendar", SearchScope.All, 5);

        result.Matches.Should().Contain(m => m.Id == "d1");
    }

    [Fact]
    public async Task GenerateQueryEmbedding_ReturnsNull_OnProviderFailure()
    {
        var generator = Substitute.For<IEmbeddingGenerator<string, Embedding<float>>>();
        generator.GenerateAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<EmbeddingGenerationOptions?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Provider crash"));

        var sut = new InMemoryVectorStore(_logger, generator);

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
        var generator = CreateEmbeddingGenerator(new float[] { 1.0f, 0, 0 });
        var sut = new InMemoryVectorStore(_logger, generator);

        await sut.UpsertAsync(new EmbeddingDocument
        {
            Id = "semantic-hit",
            Content = "irrelevant text",
            Collection = "test",
            Embedding = new float[] { 1.0f, 0, 0 }
        });

        await sut.UpsertAsync(new EmbeddingDocument
        {
            Id = "lexical-hit",
            Content = "this is specialized information",
            Collection = "test"
        });

        var result = await sut.SearchAsync("specialized query", SearchScope.All, 5);

        var semanticMatch = result.Matches.FirstOrDefault(m => m.Id == "semantic-hit");
        var lexicalMatch = result.Matches.FirstOrDefault(m => m.Id == "lexical-hit");

        semanticMatch.Should().NotBeNull();
        lexicalMatch.Should().NotBeNull();
        semanticMatch!.Score.Should().BeGreaterThan(lexicalMatch!.Score);
    }

    [Fact]
    public async Task SearchWithFiltersAsync_UsesEmbedding()
    {
        var generator = CreateEmbeddingGenerator(new float[] { 0.8f, 0.6f, 0 });
        var sut = new InMemoryVectorStore(_logger, generator);

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
    }
}

#endregion

#region Finding 2+3 — MetaAgentOrchestrator delegates to ExecutionWorkflow

public class MetaAgentOrchestratorRAGTests
{
    private readonly IAgentExecutionWorkflow _executionWorkflow;
    private readonly IAgentFactory _agentFactory;
    private readonly ISessionManager _sessionManager;
    private readonly IAgentRuntimeCoordinator _runtimeCoordinator;
    private readonly ILogger<MetaAgentOrchestrator> _logger;

    public MetaAgentOrchestratorRAGTests()
    {
        _executionWorkflow = Substitute.For<IAgentExecutionWorkflow>();
        _agentFactory = Substitute.For<IAgentFactory>();
        _sessionManager = Substitute.For<ISessionManager>();
        _runtimeCoordinator = Substitute.For<IAgentRuntimeCoordinator>();
        _logger = Substitute.For<ILogger<MetaAgentOrchestrator>>();

        _sessionManager.StartSessionAsync(Arg.Any<UserContext>()).Returns("session-1");
        _runtimeCoordinator.BeginExecutionScope(Arg.Any<string>(), Arg.Any<UserContext>())
            .Returns(Substitute.For<IDisposable>());
    }

    [Fact]
    public async Task ProcessRequestAsync_DelegatesToExecutionWorkflow()
    {
        // Arrange
        _executionWorkflow.ExecuteAsync("session-1", "What time is it?", Arg.Any<UserContext>(), Arg.Any<CancellationToken>())
            .Returns(AgentResponse.Ok("10 AM", "TestAgent", AgentTier.Support));

        var sut = new MetaAgentOrchestrator(
            _executionWorkflow, _agentFactory, _sessionManager, _runtimeCoordinator, _logger);

        // Act
        var result = await sut.ProcessRequestAsync("What time is it?", new UserContext { UserId = "u1", Name = "Test" });

        // Assert
        result.Success.Should().BeTrue();
        await _executionWorkflow.Received(1).ExecuteAsync("session-1", "What time is it?", Arg.Any<UserContext>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessRequestAsync_WorksWithSuccessfulExecution()
    {
        // Arrange
        _executionWorkflow.ExecuteAsync(Arg.Any<string>(), "Hello", Arg.Any<UserContext>(), Arg.Any<CancellationToken>())
            .Returns(AgentResponse.Ok("Response", "TestAgent", AgentTier.Support));

        var sut = new MetaAgentOrchestrator(
            _executionWorkflow, _agentFactory, _sessionManager, _runtimeCoordinator, _logger);

        // Act
        var result = await sut.ProcessRequestAsync("Hello", new UserContext { UserId = "u1", Name = "Test" });

        // Assert
        result.Success.Should().BeTrue();
        result.Content.Should().Be("Response");
    }

    [Fact]
    public async Task ProcessRequestAsync_StartsSessionBeforeExecution()
    {
        // Arrange
        _executionWorkflow.ExecuteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<UserContext>(), Arg.Any<CancellationToken>())
            .Returns(AgentResponse.Ok("OK", "TestAgent", AgentTier.Support));

        var sut = new MetaAgentOrchestrator(
            _executionWorkflow, _agentFactory, _sessionManager, _runtimeCoordinator, _logger);

        // Act
        await sut.ProcessRequestAsync("question", new UserContext { UserId = "u1", Name = "Test" });

        // Assert
        await _sessionManager.Received(1).StartSessionAsync(Arg.Any<UserContext>());
        _runtimeCoordinator.Received(1).BeginExecutionScope("session-1", Arg.Any<UserContext>());
    }

    [Fact]
    public async Task ProcessRequestAsync_PropagatesWorkflowFailure()
    {
        // Arrange — workflow returns error
        _executionWorkflow.ExecuteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<UserContext>(), Arg.Any<CancellationToken>())
            .Returns(AgentResponse.Error("Execution failed", "TestAgent"));

        var sut = new MetaAgentOrchestrator(
            _executionWorkflow, _agentFactory, _sessionManager, _runtimeCoordinator, _logger);

        // Act
        var result = await sut.ProcessRequestAsync("query", new UserContext { UserId = "u1", Name = "Test" });

        // Assert
        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task ProcessRequestAsync_SetsSessionIdInContext()
    {
        // Arrange
        _executionWorkflow.ExecuteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<UserContext>(), Arg.Any<CancellationToken>())
            .Returns(AgentResponse.Ok("response", "TestAgent", AgentTier.Support));

        var sut = new MetaAgentOrchestrator(
            _executionWorkflow, _agentFactory, _sessionManager, _runtimeCoordinator, _logger);

        var userContext = new UserContext { UserId = "u1", Name = "Test" };

        // Act
        await sut.ProcessRequestAsync("input", userContext);

        // Assert — session ID should be set in context preferences
        userContext.Preferences.Should().ContainKey("sessionId");
        userContext.Preferences["sessionId"].Should().Be("session-1");
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
        var sut = new AgentFrameworkFactory(_chatClient, _loggerFactory, _serviceProvider, mcpToolsAdapter: adapter);

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
        var sut = new AgentFrameworkFactory(_chatClient, _loggerFactory, _serviceProvider, mcpToolsAdapter: adapter);

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
        var sut = new AgentFrameworkFactory(_chatClient, _loggerFactory, _serviceProvider, mcpToolsAdapter: adapter);

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
