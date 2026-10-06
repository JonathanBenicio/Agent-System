
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.AgentFramework;

namespace AgenticSystem.Tests;

public class RAGContextProviderTests
{
    private readonly IRAGService _ragService;
    private readonly IContextBudgetManager _budgetManager;
    private readonly ILogger<RAGContextProvider> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly ITenantContextAccessor _tenantContextAccessor;
    private readonly ILLMRuntimeContextAccessor _llmRuntimeContextAccessor;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IServiceScope _serviceScope;
    private readonly IServiceProvider _scopeServiceProvider;

    private readonly IKnowledgeRoomService _knowledgeRoomService;
    private readonly IAgentKnowledgeRoomStore _agentKnowledgeRoomStore;

    public RAGContextProviderTests()
    {
        _ragService = Substitute.For<IRAGService>();
        _budgetManager = Substitute.For<IContextBudgetManager>();
        _logger = Substitute.For<ILogger<RAGContextProvider>>();
        _serviceProvider = Substitute.For<IServiceProvider>();
        _tenantContextAccessor = Substitute.For<ITenantContextAccessor>();
        _tenantContextAccessor.CurrentTenantId.Returns("tenant-1");
        _llmRuntimeContextAccessor = Substitute.For<ILLMRuntimeContextAccessor>();

        _serviceScopeFactory = Substitute.For<IServiceScopeFactory>();
        _serviceScope = Substitute.For<IServiceScope>();
        _scopeServiceProvider = Substitute.For<IServiceProvider>();

        _serviceProvider.GetService(typeof(IServiceScopeFactory)).Returns(_serviceScopeFactory);
        _serviceScopeFactory.CreateScope().Returns(_serviceScope);
        _serviceScope.ServiceProvider.Returns(_scopeServiceProvider);

        _knowledgeRoomService = Substitute.For<IKnowledgeRoomService>();
        _agentKnowledgeRoomStore = Substitute.For<IAgentKnowledgeRoomStore>();

        _scopeServiceProvider.GetService(typeof(IKnowledgeRoomService)).Returns(_knowledgeRoomService);
        _scopeServiceProvider.GetService(typeof(IAgentKnowledgeRoomStore)).Returns(_agentKnowledgeRoomStore);
    }

    private TestableRAGContextProvider CreateSut()
    {
        return new TestableRAGContextProvider(
            _ragService,
            _budgetManager,
            _logger,
            _serviceProvider,
            _tenantContextAccessor,
            _llmRuntimeContextAccessor
        );
    }

    private MessageAIContextProvider.InvokingContext CreateInvokingContext(
        string agentName,
        IEnumerable<ChatMessage> messages)
    {
        var agent = Substitute.For<AIAgent>();
        agent.Name.Returns(agentName);
        var session = Substitute.For<AgentSession>();
#pragma warning disable MAAI001
        return new MessageAIContextProvider.InvokingContext(agent, session, messages);
#pragma warning restore MAAI001
    }

    [Fact]
    public async Task ProvideMessagesAsync_WhenContextAlreadyHasRAGMarker_ReturnsEmpty()
    {
        var sut = CreateSut();
        var messages = new List<ChatMessage>
        {
            new ChatMessage(ChatRole.System, $"{RAGContextProvider.ContextMarker} some relevance"),
            new ChatMessage(ChatRole.User, "hello")
        };
        var context = CreateInvokingContext("TestAgent", messages);

        var result = await sut.CallProvideMessagesAsync(context, CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ProvideMessagesAsync_WhenNoUserMessages_ReturnsEmpty()
    {
        var sut = CreateSut();
        var messages = new List<ChatMessage>
        {
            new ChatMessage(ChatRole.System, "system directive")
        };
        var context = CreateInvokingContext("TestAgent", messages);

        var result = await sut.CallProvideMessagesAsync(context, CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ProvideMessagesAsync_WhenKnowledgeRoomIdSpecified_UserMissing_ReturnsEmpty()
    {
        var sut = CreateSut();
        var messages = new List<ChatMessage>
        {
            new ChatMessage(ChatRole.User, "semantic query")
        };
        var context = CreateInvokingContext("TestAgent", messages);

        var runtimeContext = new LLMRuntimeContext
        {
            UserId = "", // Missing UserId
            KnowledgeRoomId = "room-123"
        };
        _llmRuntimeContextAccessor.Current.Returns(runtimeContext);

        var result = await sut.CallProvideMessagesAsync(context, CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ProvideMessagesAsync_WhenKnowledgeRoomIdSpecified_UserHasNoAccess_ReturnsEmpty()
    {
        var sut = CreateSut();
        var messages = new List<ChatMessage>
        {
            new ChatMessage(ChatRole.User, "semantic query")
        };
        var context = CreateInvokingContext("TestAgent", messages);

        var runtimeContext = new LLMRuntimeContext
        {
            UserId = "user-1",
            KnowledgeRoomId = "room-123",
            TenantId = "tenant-1"
        };
        _llmRuntimeContextAccessor.Current.Returns(runtimeContext);

        _knowledgeRoomService.GetRoomAsync("room-123", "tenant-1", "user-1", Arg.Any<CancellationToken>())
            .Returns((KnowledgeRoom?)null);

        var result = await sut.CallProvideMessagesAsync(context, CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ProvideMessagesAsync_WhenKnowledgeRoomIdSpecified_UserHasAccess_RetrieveCalledWithFilter()
    {
        var sut = CreateSut();
        var messages = new List<ChatMessage>
        {
            new ChatMessage(ChatRole.User, "semantic query")
        };
        var context = CreateInvokingContext("TestAgent", messages);

        var runtimeContext = new LLMRuntimeContext
        {
            UserId = "user-1",
            KnowledgeRoomId = "room-123",
            TenantId = "tenant-1"
        };
        _llmRuntimeContextAccessor.Current.Returns(runtimeContext);

        var mockRoom = new KnowledgeRoom { Id = "room-123", Name = "Doc Room" };
        _knowledgeRoomService.GetRoomAsync("room-123", "tenant-1", "user-1", Arg.Any<CancellationToken>())
            .Returns(mockRoom);

        var ragContext = new RAGContext
        {
            BuiltContext = "Injected document text",
            TotalTokensUsed = 150,
            CandidatesAfterReRank = 3
        };
        _ragService.RetrieveContextAsync(Arg.Any<RAGQuery>(), Arg.Any<CancellationToken>())
            .Returns(ragContext);

        var budget = new ContextBudget { MaxTokens = 1000 };
        _budgetManager.ResolveBudget(Arg.Any<AnalysisResult>()).Returns(budget);
        _budgetManager.TrimContextToBudgetAsync(ragContext, budget).Returns(ragContext);

        var result = await sut.CallProvideMessagesAsync(context, CancellationToken.None);

        result.Should().HaveCount(1);
        result.First().Role.Should().Be(ChatRole.System);
        result.First().Text.Should().Contain(RAGContextProvider.ContextMarker);
        result.First().Text.Should().Contain("Injected document text");

        await _ragService.Received(1).RetrieveContextAsync(Arg.Is<RAGQuery>(q =>
            q.Query == "semantic query" &&
            q.Filters != null &&
            q.Filters.ContainsKey("room_ids") &&
            q.Filters["room_ids"] == "room-123" && q.Filters["tenant_id"] == "tenant-1"
        ), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProvideMessagesAsync_WhenSessionIdSpecified_RetrieveCalledWithFilter()
    {
        var sut = CreateSut();
        var messages = new List<ChatMessage>
        {
            new ChatMessage(ChatRole.User, "session search")
        };
        var context = CreateInvokingContext("TestAgent", messages);

        var runtimeContext = new LLMRuntimeContext
        {
            UserId = "user-1",
            SessionId = "session-999",
            TenantId = "tenant-1"
        };
        _llmRuntimeContextAccessor.Current.Returns(runtimeContext);

        var ragContext = new RAGContext
        {
            BuiltContext = "Session context text",
            TotalTokensUsed = 100,
            CandidatesAfterReRank = 2
        };
        _ragService.RetrieveContextAsync(Arg.Any<RAGQuery>(), Arg.Any<CancellationToken>())
            .Returns(ragContext);

        var budget = new ContextBudget { MaxTokens = 1000 };
        _budgetManager.ResolveBudget(Arg.Any<AnalysisResult>()).Returns(budget);
        _budgetManager.TrimContextToBudgetAsync(ragContext, budget).Returns(ragContext);

        var result = await sut.CallProvideMessagesAsync(context, CancellationToken.None);

        result.Should().HaveCount(1);
        result.First().Text.Should().Contain("Session context text");

        await _ragService.Received(1).RetrieveContextAsync(Arg.Is<RAGQuery>(q =>
            q.Query == "session search" &&
            q.Filters != null &&
            q.Filters.ContainsKey("collection") &&
            q.Filters["collection"] == "session-999" && q.Filters["tenant_id"] == "tenant-1"
        ), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProvideMessagesAsync_WhenDefaultBehavior_AgentHasNoRooms_ReturnsEmpty()
    {
        var sut = CreateSut();
        var messages = new List<ChatMessage>
        {
            new ChatMessage(ChatRole.User, "default query")
        };
        var context = CreateInvokingContext("SpecialAgent", messages);

        var runtimeContext = new LLMRuntimeContext
        {
            UserId = "user-1",
            TenantId = "tenant-1"
        };
        _llmRuntimeContextAccessor.Current.Returns(runtimeContext);

        _agentKnowledgeRoomStore.GetRoomIdsForAgentAsync("SpecialAgent", "tenant-1", Arg.Any<CancellationToken>())
            .Returns(Enumerable.Empty<string>());

        var result = await sut.CallProvideMessagesAsync(context, CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ProvideMessagesAsync_WhenDefaultBehavior_AgentHasRooms_RetrieveCalledWithFilters()
    {
        var sut = CreateSut();
        var messages = new List<ChatMessage>
        {
            new ChatMessage(ChatRole.User, "default query")
        };
        var context = CreateInvokingContext("SpecialAgent", messages);

        var runtimeContext = new LLMRuntimeContext
        {
            UserId = "user-1",
            TenantId = "tenant-1"
        };
        _llmRuntimeContextAccessor.Current.Returns(runtimeContext);

        var roomIds = new List<string> { "room-a", "room-b" };
        _agentKnowledgeRoomStore.GetRoomIdsForAgentAsync("SpecialAgent", "tenant-1", Arg.Any<CancellationToken>())
            .Returns(roomIds);

        var ragContext = new RAGContext
        {
            BuiltContext = "Default rooms context text",
            TotalTokensUsed = 200,
            CandidatesAfterReRank = 4
        };
        _ragService.RetrieveContextAsync(Arg.Any<RAGQuery>(), Arg.Any<CancellationToken>())
            .Returns(ragContext);

        var budget = new ContextBudget { MaxTokens = 1000 };
        _budgetManager.ResolveBudget(Arg.Any<AnalysisResult>()).Returns(budget);
        _budgetManager.TrimContextToBudgetAsync(ragContext, budget).Returns(ragContext);

        var result = await sut.CallProvideMessagesAsync(context, CancellationToken.None);

        result.Should().HaveCount(1);
        result.First().Text.Should().Contain("Default rooms context text");

        await _ragService.Received(1).RetrieveContextAsync(Arg.Is<RAGQuery>(q =>
            q.Query == "default query" &&
            q.Filters != null &&
            q.Filters.ContainsKey("room_ids") &&
            q.Filters["room_ids"] == "room-a,room-b" && q.Filters["tenant_id"] == "tenant-1"
        ), Arg.Any<CancellationToken>());
    }
}

public class TestableRAGContextProvider : RAGContextProvider
{
    public TestableRAGContextProvider(
        IRAGService ragService,
        IContextBudgetManager? budgetManager,
        ILogger<RAGContextProvider> logger,
        IServiceProvider serviceProvider,
        ITenantContextAccessor tenantContextAccessor,
        ILLMRuntimeContextAccessor llmRuntimeContextAccessor)
        : base(ragService, budgetManager, logger, serviceProvider, tenantContextAccessor, llmRuntimeContextAccessor)
    {
    }

    public ValueTask<IEnumerable<ChatMessage>> CallProvideMessagesAsync(
        InvokingContext context, CancellationToken ct)
    {
        return ProvideMessagesAsync(context, ct);
    }
}
