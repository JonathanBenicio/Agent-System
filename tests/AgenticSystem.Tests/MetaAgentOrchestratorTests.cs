using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.Models.Triage;
using AgenticSystem.Core.Services;

namespace AgenticSystem.Tests;

public class MetaAgentOrchestratorTests
{
    private readonly IFrameworkOrchestratorService _frameworkOrchestrator;
    private readonly IDirectAgentRequestExecutor _directAgentRequestExecutor;
    private readonly ILLMRuntimeContextAccessor _llmRuntimeContextAccessor;
    private readonly IAgentFactory _agentFactory;
    private readonly ISessionManager _sessionManager;
    private readonly ISessionLifecycleCoordinator _sessionCoordinator;
    private readonly IContextAnalyzer _contextAnalyzer;
    private readonly ISmartRouter _smartRouter;
    private readonly ILogger<MetaAgentOrchestrator> _logger;
    private readonly MetaAgentOrchestrator _sut;

    public MetaAgentOrchestratorTests()
    {
        _frameworkOrchestrator = Substitute.For<IFrameworkOrchestratorService>();
        _directAgentRequestExecutor = Substitute.For<IDirectAgentRequestExecutor>();
        _llmRuntimeContextAccessor = Substitute.For<ILLMRuntimeContextAccessor>();
        _agentFactory = Substitute.For<IAgentFactory>();
        _sessionManager = Substitute.For<ISessionManager>();
        _sessionCoordinator = Substitute.For<ISessionLifecycleCoordinator>();
        _contextAnalyzer = Substitute.For<IContextAnalyzer>();
        _smartRouter = Substitute.For<ISmartRouter>();
        _logger = Substitute.For<ILogger<MetaAgentOrchestrator>>();

        _sessionCoordinator.BeginExecutionScope(Arg.Any<string>(), Arg.Any<UserContext>())
            .Returns(Substitute.For<IDisposable>());

        _sessionCoordinator.CanStartSessionAsync(Arg.Any<string>())
            .Returns(true);
            
        _llmRuntimeContextAccessor.BeginScope(Arg.Any<UserContext>(), Arg.Any<string>())
            .Returns(Substitute.For<IDisposable>());

        _smartRouter.TriageAsync(Arg.Any<string>(), Arg.Any<UserContext>(), Arg.Any<CancellationToken>())
            .Returns((false, null, null));

        _contextAnalyzer.AnalyzeAsync(Arg.Any<string>(), Arg.Any<UserContext>())
            .Returns(new AnalysisResult { Intent = AgenticSystem.Core.Models.IntentType.Chat, Confidence = 1.0 });

        _sut = new MetaAgentOrchestrator(
            _frameworkOrchestrator, 
            _directAgentRequestExecutor, 
            _llmRuntimeContextAccessor, 
            _agentFactory, 
            _sessionCoordinator,
            _sessionManager,
            _contextAnalyzer,
            _smartRouter,
            _logger);
    }

    [Fact]
    public async Task ProcessRequestAsync_WithValidInput_ReturnsSuccessResponse()
    {
        var input = "What time is it?";
        var userContext = new UserContext { UserId = "user1", Name = "Test" };
        var sessionId = "session-1";

        _sessionCoordinator.StartSessionAsync(userContext, Arg.Any<string>()).Returns(sessionId);
        _frameworkOrchestrator.ExecuteAsync(sessionId, input, userContext, Arg.Any<CancellationToken>())
            .Returns(AgentResponse.Ok("It's 10 AM", "GeneralAgent", AgentTier.Support));

        var result = await _sut.ProcessRequestAsync(input, userContext);

        result.Success.Should().BeTrue();
        result.Content.Should().Be("It's 10 AM");
        result.AgentName.Should().Be("GeneralAgent");
    }

    [Fact]
    public async Task ProcessRequestAsync_DelegatesToExecutionWorkflow()
    {
        var input = "some request";
        var userContext = new UserContext { UserId = "user1" };
        var sessionId = "session-1";

        _sessionCoordinator.StartSessionAsync(userContext, Arg.Any<string>()).Returns(sessionId);
        _frameworkOrchestrator.ExecuteAsync(sessionId, input, userContext, Arg.Any<CancellationToken>())
            .Returns(AgentResponse.Ok("response", "Agent", AgentTier.Support));

        await _sut.ProcessRequestAsync(input, userContext);

        await _frameworkOrchestrator.Received(1).ExecuteAsync(sessionId, input, userContext, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessRequestAsync_StartsSession_AndBeginsScope()
    {
        var input = "test";
        var userContext = new UserContext { UserId = "user1" };
        var sessionId = "session-1";

        _sessionCoordinator.StartSessionAsync(userContext, Arg.Any<string>()).Returns(sessionId);
        _frameworkOrchestrator.ExecuteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<UserContext>(), Arg.Any<CancellationToken>())
            .Returns(AgentResponse.Ok("ok", "Agent", AgentTier.Support));

        await _sut.ProcessRequestAsync(input, userContext);

        await _sessionCoordinator.Received(1).StartSessionAsync(userContext, Arg.Any<string>());
        _sessionCoordinator.Received(1).BeginExecutionScope(sessionId, userContext);
    }

    [Fact]
    public async Task CleanupInactiveAgentsAsync_ExecutesWithoutError()
    {
        _agentFactory.GetAgentsByTierAsync(Arg.Any<AgentTier>())
            .Returns(Enumerable.Empty<AgentInfo>());

        await _sut.Invoking(s => s.CleanupInactiveAgentsAsync())
            .Should().NotThrowAsync();
    }

    [Fact]
    public async Task GetActiveAgentsAsync_DelegatesToFactory()
    {
        var agents = new List<AgentInfo> { new() { Name = "Chief", Tier = AgentTier.Chief } };
        _agentFactory.GetAllAgentsAsync().Returns(agents);

        var result = await _sut.GetActiveAgentsAsync();

        result.Should().HaveCount(1);
        result.First().Name.Should().Be("Chief");
    }

    [Fact]
    public async Task ProcessDirectRequestAsync_DelegatesToDirectAgentRequestExecutor()
    {
        var input = "hello";
        var userContext = new UserContext { UserId = "user-1" };
        var sessionId = "session-1";
        var targetAgent = "FinanceAgent";

        _sessionCoordinator.StartSessionAsync(userContext, Arg.Any<string>()).Returns(sessionId);
        _directAgentRequestExecutor.ExecuteAsync(sessionId, input, userContext, targetAgent, Arg.Any<CancellationToken>())
            .Returns(AgentResponse.Ok("Direct response", targetAgent, AgentTier.Specialist));

        var result = await _sut.ProcessDirectRequestAsync(input, userContext, targetAgent);

        result.Success.Should().BeTrue();
        await _directAgentRequestExecutor.Received(1).ExecuteAsync(sessionId, input, userContext, targetAgent, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessRequestAsync_WithStartWorkflowCommand_StartsWorkflowAndReturnsMarkdown()
    {
        // Arrange
        var workflowStore = Substitute.For<IWorkflowStore>();
        var workflowEngine = Substitute.For<IWorkflowEngine>();
        var chatWorkflowHandler = new ChatWorkflowCommandHandler(
            Substitute.For<ILogger<ChatWorkflowCommandHandler>>(),
            workflowEngine,
            workflowStore);
        
        var sutWithWorkflows = new MetaAgentOrchestrator(
            _frameworkOrchestrator,
            _directAgentRequestExecutor,
            _llmRuntimeContextAccessor,
            _agentFactory,
            _sessionCoordinator,
            _sessionManager,
            _contextAnalyzer,
            _smartRouter,
            _logger,
            chatWorkflowCommandHandler: chatWorkflowHandler);

        var input = "iniciar workflow wf-abc";
        var userContext = new UserContext { UserId = "user-1", TenantId = "tenant-1" };
        var sessionId = "session-1";

        var definition = new WorkflowDefinition { Id = "wf-abc", Name = "Test Workflow" };
        var execution = new WorkflowExecution { Id = "exec-123", Status = WorkflowExecutionStatus.Running, InitiatedBy = "user-1" };

        _sessionCoordinator.StartSessionAsync(userContext, Arg.Any<string>()).Returns(sessionId);
        workflowStore.GetDefinitionAsync("tenant-1", "wf-abc", Arg.Any<CancellationToken>()).Returns(definition);
        workflowEngine.StartAsync("tenant-1", definition, initiatedBy: "user-1", ct: Arg.Any<CancellationToken>()).Returns(execution);

        // Act
        var result = await sutWithWorkflows.ProcessRequestAsync(input, userContext);

        // Assert
        result.Success.Should().BeTrue();
        result.Content.Should().Contain("Test Workflow");
        result.Content.Should().Contain("exec-123");
        result.Content.Should().Contain("Running");
        await workflowEngine.Received(1).StartAsync("tenant-1", definition, initiatedBy: "user-1", ct: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessRequestAsync_WithCancelWorkflowCommand_CancelsWorkflowAndReturnsMarkdown()
    {
        // Arrange
        var workflowStore = Substitute.For<IWorkflowStore>();
        var workflowEngine = Substitute.For<IWorkflowEngine>();
        var chatWorkflowHandler = new ChatWorkflowCommandHandler(
            Substitute.For<ILogger<ChatWorkflowCommandHandler>>(),
            workflowEngine,
            workflowStore);
        
        var sutWithWorkflows = new MetaAgentOrchestrator(
            _frameworkOrchestrator,
            _directAgentRequestExecutor,
            _llmRuntimeContextAccessor,
            _agentFactory,
            _sessionCoordinator,
            _sessionManager,
            _contextAnalyzer,
            _smartRouter,
            _logger,
            chatWorkflowCommandHandler: chatWorkflowHandler);

        var input = "cancelar workflow exec-456";
        var userContext = new UserContext { UserId = "user-1", TenantId = "tenant-1" };
        var sessionId = "session-1";

        var execution = new WorkflowExecution { Id = "exec-456", WorkflowName = "Test Workflow", Status = WorkflowExecutionStatus.Running };
        var cancelledExecution = new WorkflowExecution 
        { 
            Id = "exec-456", 
            WorkflowName = "Test Workflow", 
            Status = WorkflowExecutionStatus.Cancelled, 
            ErrorMessage = "Cancelado via chat conversacional pelo usuário.",
            CompletedAt = DateTime.UtcNow
        };

        _sessionCoordinator.StartSessionAsync(userContext, Arg.Any<string>()).Returns(sessionId);
        workflowEngine.GetExecutionAsync("tenant-1", "exec-456", Arg.Any<CancellationToken>()).Returns(execution);
        workflowEngine.CancelAsync("tenant-1", "exec-456", Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(cancelledExecution);

        // Act
        var result = await sutWithWorkflows.ProcessRequestAsync(input, userContext);

        // Assert
        result.Success.Should().BeTrue();
        result.Content.Should().Contain("Test Workflow");
        result.Content.Should().Contain("exec-456");
        result.Content.Should().Contain("Cancelled");
        await workflowEngine.Received(1).CancelAsync("tenant-1", "exec-456", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessRequestAsync_WithListWorkflowsCommand_ReturnsWorkflowsList()
    {
        // Arrange
        var workflowStore = Substitute.For<IWorkflowStore>();
        var workflowEngine = Substitute.For<IWorkflowEngine>();
        var chatWorkflowHandler = new ChatWorkflowCommandHandler(
            Substitute.For<ILogger<ChatWorkflowCommandHandler>>(),
            workflowEngine,
            workflowStore);
        
        var sutWithWorkflows = new MetaAgentOrchestrator(
            _frameworkOrchestrator,
            _directAgentRequestExecutor,
            _llmRuntimeContextAccessor,
            _agentFactory,
            _sessionCoordinator,
            _sessionManager,
            _contextAnalyzer,
            _smartRouter,
            _logger,
            chatWorkflowCommandHandler: chatWorkflowHandler);

        var input = "listar workflows";
        var userContext = new UserContext { UserId = "user-1", TenantId = "tenant-1" };
        var sessionId = "session-1";

        var executions = new List<WorkflowExecution>
        {
            new() { Id = "exec-1", WorkflowName = "Workflow 1", Status = WorkflowExecutionStatus.Completed, StartedAt = DateTime.UtcNow.AddMinutes(-5), CompletedAt = DateTime.UtcNow },
            new() { Id = "exec-2", WorkflowName = "Workflow 2", Status = WorkflowExecutionStatus.Running, StartedAt = DateTime.UtcNow }
        };

        _sessionCoordinator.StartSessionAsync(userContext, Arg.Any<string>()).Returns(sessionId);
        workflowEngine.ListExecutionsAsync("tenant-1", limit: 10, ct: Arg.Any<CancellationToken>()).Returns(executions);

        // Act
        var result = await sutWithWorkflows.ProcessRequestAsync(input, userContext);

        // Assert
        result.Success.Should().BeTrue();
        result.Content.Should().Contain("Workflow 1");
        result.Content.Should().Contain("exec-1");
        result.Content.Should().Contain("Completed");
        result.Content.Should().Contain("Workflow 2");
        result.Content.Should().Contain("exec-2");
        result.Content.Should().Contain("Running");
        await workflowEngine.Received(1).ListExecutionsAsync("tenant-1", limit: 10, ct: Arg.Any<CancellationToken>());
    }
}
