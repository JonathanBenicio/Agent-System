using System.Diagnostics;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.Configuration;
using AgenticSystem.Infrastructure.AgentFramework;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Agents.AI.Workflows;
using WorkflowAgent = Microsoft.Agents.AI.AIAgent;
using WorkflowChatAgent = Microsoft.Agents.AI.ChatClientAgent;

namespace AgenticSystem.Infrastructure.AI;

public class AgentCollaborationWorkflow : IAgentCollaborationWorkflow
{
    private readonly ChatClientPlanner _planner;
    private readonly IAgentFactory _agentFactory;
    private readonly ITaskPlanManager _taskPlanManager;
    private readonly IRAGService? _ragService;
    private readonly IAgentChannelService? _agentChannelService;
    private readonly AgentFrameworkFactory? _agentFrameworkFactory;
    private readonly IAgentRuntimeCoordinator _runtimeCoordinator;
    private readonly ILogger<AgentCollaborationWorkflow> _logger;
    private readonly CollaborationWorkflowOptions _workflowOptions;
    private readonly IDirectAgentExecutionService _directAgentExecutionService;

    public AgentCollaborationWorkflow(
        ChatClientPlanner planner,
        IAgentFactory agentFactory,
        ITaskPlanManager taskPlanManager,
        IAgentRuntimeCoordinator runtimeCoordinator,
        IDirectAgentExecutionService directAgentExecutionService,
        ILogger<AgentCollaborationWorkflow> logger,
        IAgentChannelService? agentChannelService = null,
        IRAGService? ragService = null,
        AgentFrameworkFactory? agentFrameworkFactory = null,
        IOptions<CollaborationWorkflowOptions>? workflowOptions = null)
    {
        _planner = planner;
        _agentFactory = agentFactory;
        _taskPlanManager = taskPlanManager;
        _runtimeCoordinator = runtimeCoordinator;
        _directAgentExecutionService = directAgentExecutionService;
        _logger = logger;
        _agentChannelService = agentChannelService;
        _ragService = ragService;
        _agentFrameworkFactory = agentFrameworkFactory;
        _workflowOptions = workflowOptions?.Value ?? new CollaborationWorkflowOptions();
    }

    public Task<bool> ShouldRunAsync(string input, AnalysisResult analysis, CancellationToken ct = default)
    {
        var shouldRun = analysis.Complexity == ComplexityLevel.RequiresPlanning
            || analysis.RequiresDelegation
            || analysis.SecondaryDomains.Count > 0
            || input.Contains("plan", StringComparison.OrdinalIgnoreCase)
            || input.Contains("etapa", StringComparison.OrdinalIgnoreCase)
            || input.Contains("passo", StringComparison.OrdinalIgnoreCase);

        return Task.FromResult(shouldRun);
    }

    public async Task<AgentResponse> ExecuteAsync(string sessionId, string input, UserContext context, AnalysisResult analysis, CancellationToken ct = default)
        => _agentFrameworkFactory is not null
            ? await ExecuteWithWorkflowBuilderAsync(sessionId, input, context, analysis, ct)
            : await ExecuteLegacyAsync(sessionId, input, context, analysis, ct);

    private async Task<AgentResponse> ExecuteWithWorkflowBuilderAsync(
        string sessionId,
        string input,
        UserContext context,
        AnalysisResult analysis,
        CancellationToken ct)
    {
        var state = new CollaborationWorkflowState(sessionId, input, context, analysis)
        {
            ExecutionMode = ShouldUseConcurrentContextStage()
                ? "context-parallel-planner-executor-reviewer-workflow"
                : "planner-executor-reviewer-workflow",
            WorkflowEngine = "AgentWorkflowBuilder",
            AdvancedWorkflowEnabled = _workflowOptions.EnableAdvancedWorkflow,
            CheckpointingRequested = _workflowOptions.EnableCheckpointing
        };
        var workflow = await BuildCollaborationWorkflowAsync(state, ct);

        try
        {
            var messages = new List<ChatMessage>
            {
                new(ChatRole.User, input)
            };

            if (_workflowOptions.EnableCheckpointing)
            {
                await using var run = await InProcessExecution.RunAsync(workflow, messages, CheckpointManager.Default, sessionId, ct);
                return await CompleteWorkflowRunAsync(run, state, ct);
            }

            await using var nonCheckpointedRun = await InProcessExecution.RunAsync(workflow, messages, sessionId, ct);
            return await CompleteWorkflowRunAsync(nonCheckpointedRun, state, ct);
        }
        catch (CollaborationWorkflowStageFailedException)
        {
            return state.FailedResponse ?? AgentResponse.Error(
                "Erro ao executar workflow colaborativo.", "CollaborativeWorkflow");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AgentWorkflowBuilder collaboration pipeline failed for session {SessionId}", sessionId);
            return AgentResponse.Error("Erro ao executar workflow colaborativo.", "CollaborativeWorkflow");
        }
    }

    private async Task<AgentResponse> ExecuteLegacyAsync(
        string sessionId,
        string input,
        UserContext context,
        AnalysisResult analysis,
        CancellationToken ct)
    {
        var plan = await CreatePlanAsync(sessionId, input, context, analysis, ct);
        var stepExecution = await ExecutePlanStepsAsync(sessionId, input, context, analysis, plan, null, ct);

        if (stepExecution.FailedResponse is not null)
        {
            return stepExecution.FailedResponse;
        }

        var review = await ReviewPlanAsync(sessionId, input, context, plan, stepExecution.StepOutputs, null, ct);
        return BuildCollaborativeResponse(
            plan,
            stepExecution.StepOutputs,
            review.ReviewResponse,
            review.Reviewer.Name,
            "planner-executor-reviewer",
            "LegacyLoop");
    }

    private async Task<Workflow> BuildCollaborationWorkflowAsync(CollaborationWorkflowState state, CancellationToken ct)
    {
        var agents = new List<WorkflowAgent>();

        if (ShouldUseConcurrentContextStage())
        {
            agents.Add(CreateWorkflowStageAgent(
                "CollaborationContextCoordinator",
                "Coleta contexto compartilhado em paralelo antes da execução do plano.",
                (_, cancellationToken) => ExecuteConcurrentContextStageAsync(state, cancellationToken)));
        }

        // 1. Resolver agentes reais via Factory (Adoção Agressiva: Agentes como First-Class Steps)
        var plannerAgent = await _agentFactory.ResolveAgentAsync(new AgentInfo { Name = "project-planner" }) as WorkflowAgent;
        var executorAgent = await _agentFactory.ResolveAgentAsync(new AgentInfo { Name = "backend-specialist" }) as WorkflowAgent;
        var reviewerAgent = await _agentFactory.ResolveAgentAsync(new AgentInfo { Name = "test-engineer" }) as WorkflowAgent;

        // Se falhar ao resolver nativos, mantém o fallback para as lógicas internas (mas agora preferindo os nativos)
        if (plannerAgent != null) agents.Add(plannerAgent);
        else agents.Add(CreateWorkflowStageAgent("CollaborationPlanner", "Planeja os steps do workflow colaborativo.", (_, cancellationToken) => ExecutePlanningStageAsync(state, cancellationToken)));

        if (executorAgent != null) agents.Add(executorAgent);
        else agents.Add(CreateWorkflowStageAgent("CollaborationExecutor", "Executa os steps do workflow colaborativo.", (_, cancellationToken) => ExecuteExecutionStageAsync(state, cancellationToken)));

        if (reviewerAgent != null) agents.Add(reviewerAgent);
        else agents.Add(CreateWorkflowStageAgent("CollaborationReviewer", "Revisa os resultados do workflow colaborativo.", (_, cancellationToken) => ExecuteReviewerStageAsync(state, cancellationToken)));

        return AgentWorkflowBuilder.BuildSequential(
            ShouldUseConcurrentContextStage() ? "collaboration-workflow-advanced" : "collaboration-workflow",
            agents);
    }

    private bool ShouldUseConcurrentContextStage()
        => _workflowOptions.EnableAdvancedWorkflow
            && _workflowOptions.EnableConcurrentContextStage
            && (_agentChannelService is not null || _ragService is not null);

    private async Task<AgentResponse> CompleteWorkflowRunAsync(Run run, CollaborationWorkflowState state, CancellationToken ct)
    {
        CaptureWorkflowRunMetadata(run, state);
        await RecordWorkflowStateArtifactAsync(state, ct);

        if (state.FailedResponse is not null)
        {
            MergeWorkflowMetadata(state.FailedResponse.Metadata, state.WorkflowMetadata);
            return state.FailedResponse;
        }

        if (state.FinalResponse is not null)
        {
            MergeWorkflowMetadata(state.FinalResponse.Metadata, state.WorkflowMetadata);
            return state.FinalResponse;
        }

        var workflowError = run.OutgoingEvents.OfType<WorkflowErrorEvent>().FirstOrDefault();
        var executorFailure = run.OutgoingEvents.OfType<ExecutorFailedEvent>().FirstOrDefault();

        if (workflowError is not null)
        {
            _logger.LogWarning(workflowError.Exception,
                "AgentWorkflowBuilder collaboration run failed for session {SessionId}", state.SessionId);
        }
        else if (executorFailure?.Data is Exception executorException)
        {
            _logger.LogWarning(executorException,
                "AgentWorkflowBuilder collaboration executor failed for session {SessionId}", state.SessionId);
        }

        var failedResponse = AgentResponse.Error("Erro ao executar workflow colaborativo.", "CollaborativeWorkflow");
        MergeWorkflowMetadata(failedResponse.Metadata, state.WorkflowMetadata);
        return failedResponse;
    }

    private async Task<string> ExecuteConcurrentContextStageAsync(
        CollaborationWorkflowState state,
        CancellationToken ct)
    {
        var contextWorkflow = BuildConcurrentContextWorkflow(state);
        if (contextWorkflow is null)
        {
            return "Concurrent context stage skipped.";
        }

        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, state.OriginalInput)
        };

        try
        {
            if (_workflowOptions.EnableCheckpointing)
            {
                await using var checkpointedRun = await InProcessExecution.RunAsync(
                    contextWorkflow,
                    messages,
                    CheckpointManager.Default,
                    $"{state.SessionId}:context",
                    ct);
                CaptureContextRunMetadata(checkpointedRun, state);
            }
            else
            {
                await using var run = await InProcessExecution.RunAsync(
                    contextWorkflow,
                    messages,
                    $"{state.SessionId}:context",
                    ct);
                CaptureContextRunMetadata(run, state);
            }

            state.SharedContextSupplement = BuildSharedContextSupplement(state.ContextSegments);

            return string.IsNullOrWhiteSpace(state.SharedContextSupplement)
                ? "Concurrent context stage produced no supplemental context."
                : $"Concurrent context stage prepared {state.ContextSegments.Count} context source(s).";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Advanced concurrent context stage failed for session {SessionId}. Continuing without shared context.",
                state.SessionId);
            state.SharedContextSupplement = null;
            state.ContextSegments.Clear();
            return "Concurrent context stage failed; continuing without supplemental context.";
        }
    }

    private Workflow? BuildConcurrentContextWorkflow(CollaborationWorkflowState state)
    {
        var agents = new List<WorkflowAgent>();

        if (_agentChannelService is not null)
        {
            agents.Add(CreateWorkflowStageAgent(
                "CollaborationChannelContext",
                "Agrega sinais de canal compartilhado para o workflow colaborativo.",
                (_, cancellationToken) => ExecuteChannelContextStageAsync(state, cancellationToken)));
        }

        if (_ragService is not null)
        {
            agents.Add(CreateWorkflowStageAgent(
                "CollaborationRagContext",
                "Agrega contexto RAG compartilhado para o workflow colaborativo.",
                (_, cancellationToken) => ExecuteRagContextStageAsync(state, cancellationToken)));
        }

        return agents.Count == 0
            ? null
            : AgentWorkflowBuilder.BuildConcurrent(
                "collaboration-context-workflow",
                agents,
                AggregateConcurrentContextMessages);
    }

    private async Task<string> ExecuteChannelContextStageAsync(
        CollaborationWorkflowState state,
        CancellationToken ct)
    {
        if (_agentChannelService is null)
        {
            return "Channel context unavailable.";
        }

        var messages = await _agentChannelService.GetMessagesAsync(state.SessionId, "CollaborativeWorkflow", maxCount: 5, ct);
        if (messages.Count == 0)
        {
            return "No prior native channel context available.";
        }

        var builder = new StringBuilder();
        builder.AppendLine("[Shared Channel Context]");
        foreach (var message in messages)
        {
            builder.Append("- From ");
            builder.Append(message.SourceAgent);
            builder.Append(" via ");
            builder.Append(message.Kind);
            builder.Append(": ");
            builder.AppendLine(message.Content);
        }

        var summary = builder.ToString().TrimEnd();
        state.ContextSegments["channel"] = summary;
        return summary;
    }

    private async Task<string> ExecuteRagContextStageAsync(
        CollaborationWorkflowState state,
        CancellationToken ct)
    {
        if (_ragService is null)
        {
            return "RAG context unavailable.";
        }

        var ragContext = await _ragService.RetrieveContextAsync(new RAGQuery
        {
            Query = state.OriginalInput,
            AgentId = state.Analysis.EstimatedAgent,
            SessionId = state.SessionId,
            Scope = SearchScope.All,
            MaxResults = 8,
            TopKAfterReRank = 4,
            MinRelevanceScore = 0.25
        }, ct);

        if (string.IsNullOrWhiteSpace(ragContext.BuiltContext))
        {
            return "No shared RAG context available.";
        }

        var summary = $"[Shared RAG Context]\n{ragContext.BuiltContext}";
        state.ContextSegments["rag"] = summary;
        return summary;
    }

    private WorkflowAgent CreateWorkflowStageAgent(
        string name,
        string description,
        Func<IEnumerable<ChatMessage>, CancellationToken, Task<string>> executeAsync)
    {
        var stageClient = new CollaborationWorkflowStageChatClient(executeAsync);
        return new WorkflowChatAgent(
            stageClient,
            description,
            name,
            description,
            null,
            _agentFrameworkFactory!.LoggerFactory,
            _agentFrameworkFactory.ServiceProvider);
    }

    private async Task<string> ExecutePlanningStageAsync(
        CollaborationWorkflowState state,
        CancellationToken ct)
    {
        state.Plan = await CreatePlanAsync(state.SessionId, state.OriginalInput, state.Context, state.Analysis, ct);
        return $"Plan {state.Plan.Id} created with {state.Plan.Steps.Count} steps.";
    }

    private async Task<string> ExecuteExecutionStageAsync(
        CollaborationWorkflowState state,
        CancellationToken ct)
    {
        if (state.Plan is null)
        {
            throw new InvalidOperationException("Collaboration workflow execution requires a plan.");
        }

        var stepExecution = await ExecutePlanStepsAsync(
            state.SessionId,
            state.OriginalInput,
            state.Context,
            state.Analysis,
            state.Plan,
            state.SharedContextSupplement,
            ct);

        state.StepOutputs.Clear();
        state.StepOutputs.AddRange(stepExecution.StepOutputs);

        if (stepExecution.FailedResponse is not null)
        {
            state.FailedResponse = stepExecution.FailedResponse;
            throw new CollaborationWorkflowStageFailedException(stepExecution.FailedResponse.Content);
        }

        return stepExecution.StepOutputs.Count == 0
            ? "No steps were executed."
            : string.Join("\n\n", stepExecution.StepOutputs.Select(output =>
                $"Step {output.Step.Index + 1}: {output.Response.Content}"));
    }

    private async Task<string> ExecuteReviewerStageAsync(
        CollaborationWorkflowState state,
        CancellationToken ct)
    {
        if (state.Plan is null)
        {
            throw new InvalidOperationException("Collaboration workflow review requires a plan.");
        }

        var review = await ReviewPlanAsync(
            state.SessionId,
            state.OriginalInput,
            state.Context,
            state.Plan,
            state.StepOutputs,
            state.SharedContextSupplement,
            ct);

        state.ReviewResponse = review.ReviewResponse;
        state.FinalResponse = BuildCollaborativeResponse(
            state.Plan,
            state.StepOutputs,
            review.ReviewResponse,
            review.Reviewer.Name,
            state.ExecutionMode,
            state.WorkflowEngine);

        return state.FinalResponse.Content;
    }

    private async Task<TaskPlan> CreatePlanAsync(
        string sessionId,
        string input,
        UserContext context,
        AnalysisResult analysis,
        CancellationToken ct)
    {
        var plannerSw = Stopwatch.StartNew();
        await _runtimeCoordinator.PublishEventAsync(new AgentStreamEvent
        {
            Type = AgentStreamEventType.PlanningStarted,
            Message = input,
            Data = new Dictionary<string, object>
            {
                ["analysisDomain"] = analysis.PrimaryDomain,
                ["requiresDelegation"] = analysis.RequiresDelegation
            }
        }, ct);

        var plan = await _planner.PlanAsync(context.UserId, input, ct)
            ?? await BuildFallbackPlanAsync(context.UserId, input, analysis, ct);

        plan.SessionId = sessionId;
        plan.Status = TaskPlanStatus.InProgress;

        if (plan.Steps.Count > 0)
        {
            plan.Steps[0].Status = TaskStepStatus.InProgress;
            plan.Steps[0].StartedAt = DateTime.UtcNow;
        }

        plannerSw.Stop();

        await _runtimeCoordinator.RecordArtifactAsync(new AgentExecutionArtifact
        {
            SessionId = sessionId,
            Type = AgentExecutionArtifactType.Plan,
            Name = plan.Title,
            AgentName = _runtimeCoordinator.CurrentAgentName,
            Status = plan.Status.ToString(),
            Summary = plan.Description,
            Data = new Dictionary<string, object>
            {
                ["planId"] = plan.Id,
                ["steps"] = plan.Steps.Select(step => new Dictionary<string, object>
                {
                    ["index"] = step.Index,
                    ["description"] = step.Description,
                    ["assignedAgent"] = step.AssignedAgent ?? string.Empty
                }).ToList(),
                ["latencyMs"] = plannerSw.Elapsed.TotalMilliseconds
            }
        }, ct);

        await _runtimeCoordinator.PublishEventAsync(new AgentStreamEvent
        {
            Type = AgentStreamEventType.PlanningCompleted,
            Message = plan.Title,
            Data = new Dictionary<string, object>
            {
                ["planId"] = plan.Id,
                ["stepCount"] = plan.Steps.Count,
                ["latencyMs"] = plannerSw.Elapsed.TotalMilliseconds
            }
        }, ct);

        return plan;
    }

    private async Task<StepExecutionResult> ExecutePlanStepsAsync(
        string sessionId,
        string input,
        UserContext context,
        AnalysisResult analysis,
        TaskPlan plan,
        string? sharedContextSupplement,
        CancellationToken ct)
    {
        var stepOutputs = new List<(TaskStep Step, IAgent Agent, AgentResponse Response)>();

        foreach (var step in plan.Steps.OrderBy(step => step.Index))
        {
            var stepAnalysis = BuildStepAnalysis(step, analysis);
            var agent = await _agentFactory.ResolveAgentAsync(stepAnalysis);
            var stepInput = await BuildStepInputAsync(input, step, agent, sharedContextSupplement, ct);
            stepInput = await BuildChannelAwareStepInputAsync(sessionId, step, agent, stepInput, AgentChannelKind.Planner, ct);

            await _runtimeCoordinator.PublishEventAsync(new AgentStreamEvent
            {
                Type = AgentStreamEventType.StepStarted,
                AgentName = agent.Name,
                Message = step.Description,
                Data = new Dictionary<string, object>
                {
                    ["stepIndex"] = step.Index,
                    ["assignedAgent"] = agent.Name
                }
            }, ct);

            step.Status = TaskStepStatus.InProgress;
            step.StartedAt = DateTime.UtcNow;
            var executionSw = Stopwatch.StartNew();

            using var agentScope = _runtimeCoordinator.BeginAgentScope(agent.Name, agent.AvailableTools);
            var response = await _directAgentExecutionService.ExecuteDirectAsync(agent, sessionId, stepInput, context, ct);
            executionSw.Stop();

            if (!response.Success)
            {
                await _taskPlanManager.FailStepAsync(plan.Id, response.ErrorMessage ?? response.Content);
                step.Status = TaskStepStatus.Failed;
                step.Result = response.Content;

                await _runtimeCoordinator.PublishEventAsync(new AgentStreamEvent
                {
                    Type = AgentStreamEventType.StepFailed,
                    AgentName = agent.Name,
                    Message = response.Content,
                    Data = new Dictionary<string, object>
                    {
                        ["stepIndex"] = step.Index,
                        ["latencyMs"] = executionSw.Elapsed.TotalMilliseconds
                    }
                }, ct);

                return new StepExecutionResult(stepOutputs, response);
            }

            step.Status = TaskStepStatus.Completed;
            step.CompletedAt = DateTime.UtcNow;
            step.Result = response.Content;
            stepOutputs.Add((step, agent, response));
            await _taskPlanManager.AdvanceStepAsync(plan.Id, response.Content);

            await _runtimeCoordinator.RecordArtifactAsync(new AgentExecutionArtifact
            {
                SessionId = sessionId,
                Type = AgentExecutionArtifactType.Step,
                Name = $"Step {step.Index + 1}",
                AgentName = agent.Name,
                Status = step.Status.ToString(),
                Summary = step.Description,
                Data = new Dictionary<string, object>
                {
                    ["stepIndex"] = step.Index,
                    ["result"] = response.Content,
                    ["latencyMs"] = executionSw.Elapsed.TotalMilliseconds,
                    ["assignedAgent"] = agent.Name
                }
            }, ct);

            await _runtimeCoordinator.PublishEventAsync(new AgentStreamEvent
            {
                Type = AgentStreamEventType.StepCompleted,
                AgentName = agent.Name,
                Message = response.Content,
                Data = new Dictionary<string, object>
                {
                    ["stepIndex"] = step.Index,
                    ["latencyMs"] = executionSw.Elapsed.TotalMilliseconds,
                    ["assignedAgent"] = agent.Name
                }
            }, ct);
        }

        return new StepExecutionResult(stepOutputs, null);
    }

    private async Task<ReviewExecutionResult> ReviewPlanAsync(
        string sessionId,
        string input,
        UserContext context,
        TaskPlan plan,
        IReadOnlyList<(TaskStep Step, IAgent Agent, AgentResponse Response)> stepOutputs,
        string? sharedContextSupplement,
        CancellationToken ct)
    {
        await _runtimeCoordinator.PublishEventAsync(new AgentStreamEvent
        {
            Type = AgentStreamEventType.ReviewStarted,
            Message = plan.Title,
            Data = new Dictionary<string, object>
            {
                ["planId"] = plan.Id,
                ["stepCount"] = stepOutputs.Count
            }
        }, ct);

        var reviewer = await _agentFactory.ResolveAgentAsync(new AnalysisResult
        {
            PrimaryDomain = "analysis",
            EstimatedAgent = "AnalysisAgent",
            Intent = IntentType.Analyze,
            Complexity = ComplexityLevel.Complex,
            RecommendedTier = AgentTier.Specialist
        });

        var reviewPrompt = BuildReviewPrompt(input, plan, stepOutputs, sharedContextSupplement);
        reviewPrompt = await BuildChannelAwareReviewInputAsync(sessionId, reviewer.Name, reviewPrompt, ct);
        var reviewSw = Stopwatch.StartNew();

        using var reviewerScope = _runtimeCoordinator.BeginAgentScope(reviewer.Name, reviewer.AvailableTools);
        var reviewResponse = await ExecuteReviewAsync(sessionId, reviewPrompt, reviewer, stepOutputs, context, ct);
        reviewSw.Stop();

        await _runtimeCoordinator.RecordArtifactAsync(new AgentExecutionArtifact
        {
            SessionId = sessionId,
            Type = AgentExecutionArtifactType.Review,
            Name = $"Review for {plan.Title}",
            AgentName = reviewer.Name,
            Status = reviewResponse.Success ? "Completed" : "Failed",
            Summary = reviewResponse.Content,
            Data = new Dictionary<string, object>
            {
                ["latencyMs"] = reviewSw.Elapsed.TotalMilliseconds,
                ["planId"] = plan.Id
            }
        }, ct);

        await _runtimeCoordinator.PublishEventAsync(new AgentStreamEvent
        {
            Type = AgentStreamEventType.ReviewCompleted,
            AgentName = reviewer.Name,
            Message = reviewResponse.Content,
            Data = new Dictionary<string, object>
            {
                ["latencyMs"] = reviewSw.Elapsed.TotalMilliseconds,
                ["planId"] = plan.Id
            }
        }, ct);

        return new ReviewExecutionResult(reviewer, reviewResponse);
    }

    private static AgentResponse BuildCollaborativeResponse(
        TaskPlan plan,
        IReadOnlyList<(TaskStep Step, IAgent Agent, AgentResponse Response)> stepOutputs,
        AgentResponse reviewResponse,
        string reviewerName,
        string executionMode,
        string workflowEngine)
    {
        var response = new AgentResponse
        {
            Content = BuildFinalResponse(plan, stepOutputs, reviewResponse),
            AgentName = "CollaborativeWorkflow",
            AgentTier = AgentTier.Chief,
            Success = reviewResponse.Success,
            ActionsPerformed = stepOutputs.Select(output => output.Step.Description).ToList(),
            ToolsUsed = stepOutputs.SelectMany(output => output.Response.ToolsUsed).Distinct().ToList(),
            Metadata = new Dictionary<string, object>
            {
                ["executionMode"] = executionMode,
                ["workflowEngine"] = workflowEngine,
                ["planId"] = plan.Id,
                ["reviewer"] = reviewerName,
                ["stepCount"] = stepOutputs.Count,
                ["nativeAgentTools"] = reviewResponse.Metadata.GetValueOrDefault("nativeAgentTools", false),
                ["nativeHandoffWorkflow"] = reviewResponse.Metadata.GetValueOrDefault("nativeHandoffWorkflow", false)
            }
        };

        MergeWorkflowMetadata(response.Metadata, reviewResponse.Metadata);
        return response;
    }

    private async Task<AgentResponse> ExecuteReviewAsync(
        string sessionId,
        string reviewPrompt,
        IAgent reviewer,
        IReadOnlyList<(TaskStep Step, IAgent Agent, AgentResponse Response)> stepOutputs,
        UserContext context,
        CancellationToken ct)
    {
        var distinctStepAgents = stepOutputs
            .Select(output => output.Agent)
            .DistinctBy(agent => agent.Name)
            .Where(agent => !string.Equals(agent.Name, reviewer.Name, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (_agentFrameworkFactory is null)
        {
            return await _directAgentExecutionService.ExecuteDirectAsync(reviewer, sessionId, reviewPrompt, context, ct);
        }

        if (ShouldUseNativeGroupChatReview(distinctStepAgents))
        {
            var groupChatReview = await TryExecuteNativeGroupChatReviewAsync(
                sessionId,
                reviewPrompt,
                reviewer,
                distinctStepAgents,
                ct);

            if (groupChatReview is not null)
            {
                return groupChatReview;
            }
        }

        if (ShouldUseNativeHandoffReview(distinctStepAgents))
        {
            var handoffReview = await TryExecuteNativeHandoffReviewAsync(
                sessionId,
                reviewPrompt,
                reviewer,
                distinctStepAgents,
                ct);

            if (handoffReview is not null)
            {
                return handoffReview;
            }
        }

        try
        {
            var bindings = new List<AgentToolBinding>();
            foreach (var agent in distinctStepAgents)
            {
                var binding = await _agentFrameworkFactory.CreateToolBindingAsync(agent, sessionId, ct);
                if (binding is not null)
                {
                    bindings.Add(binding);
                }
            }

            if (bindings.Count == 0)
            {
                return await _directAgentExecutionService.ExecuteDirectAsync(reviewer, sessionId, reviewPrompt, context, ct);
            }

            var frameworkReviewer = await _agentFrameworkFactory.CreateFromAgentAsync(reviewer, bindings.Select(binding => binding.Tool), ct);
            var reviewSession = await _agentFrameworkFactory.GetOrCreateSessionAsync(frameworkReviewer, sessionId, ct);
            var frameworkResponse = await frameworkReviewer.RunAsync(reviewPrompt, reviewSession, cancellationToken: ct);
            var content = ExtractFrameworkResponseText(frameworkResponse);

            await _agentFrameworkFactory.PersistSessionAsync(sessionId, frameworkReviewer, reviewSession, ct);
            foreach (var binding in bindings)
            {
                await _agentFrameworkFactory.PersistSessionAsync(sessionId, binding.FrameworkAgent, binding.Session, ct);
            }

            return new AgentResponse
            {
                Content = content,
                AgentName = reviewer.Name,
                AgentTier = reviewer.Tier,
                Success = true,
                Metadata = new Dictionary<string, object>
                {
                    ["nativeAgentTools"] = true,
                    ["nativeToolAgents"] = bindings.Select(binding => binding.Agent.Name).ToList()
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Native agent-tool review failed, falling back to direct reviewer execution");
            return await _directAgentExecutionService.ExecuteDirectAsync(reviewer, sessionId, reviewPrompt, context, ct);
        }
    }

    private bool ShouldUseNativeHandoffReview(IReadOnlyCollection<IAgent> distinctStepAgents)
        => _workflowOptions.EnableAdvancedWorkflow
            && _workflowOptions.EnableNativeHandoffReview
            && _agentFrameworkFactory is not null
            && distinctStepAgents.Count > 0;

    private bool ShouldUseNativeGroupChatReview(IReadOnlyCollection<IAgent> distinctStepAgents)
        => _workflowOptions.EnableAdvancedWorkflow
            && _workflowOptions.EnableNativeGroupChatTermination
            && _agentFrameworkFactory is not null
            && distinctStepAgents.Count > 0;

    private async Task<AgentResponse?> TryExecuteNativeGroupChatReviewAsync(
        string sessionId,
        string reviewPrompt,
        IAgent reviewer,
        IReadOnlyList<IAgent> participantAgents,
        CancellationToken ct)
    {
        if (_agentFrameworkFactory is null || participantAgents.Count == 0)
        {
            return null;
        }

        RoundRobinGroupChatManager? groupChatManager = null;
        string? terminationReason = null;
        var participantNames = new List<string> { reviewer.Name };
        participantNames.AddRange(participantAgents.Select(agent => agent.Name));

        try
        {
            var frameworkParticipants = new List<WorkflowAgent>
            {
                await _agentFrameworkFactory.CreateFromAgentAsync(reviewer, ct)
            };

            foreach (var agent in participantAgents)
            {
                frameworkParticipants.Add(await _agentFrameworkFactory.CreateFromAgentAsync(agent, ct));
            }

            #pragma warning disable MAAIW001
            var groupChatBuilder = AgentWorkflowBuilder.CreateGroupChatBuilderWith(agents =>
            {
                groupChatManager = new RoundRobinGroupChatManager(
                    agents,
                    (_, history, _) => new ValueTask<bool>(TryResolveGroupChatTerminationReason(history, out terminationReason)));
                groupChatManager.MaximumIterationCount = Math.Max(1, _workflowOptions.GroupChatMaximumIterations);
                return groupChatManager;
            })
                .AddParticipants(frameworkParticipants)
                .WithName("collaboration-review-group-chat")
                .WithDescription("Review colaborativo com política nativa de terminação baseada em group chat.");
            #pragma warning restore MAAIW001

            var groupChatWorkflow = groupChatBuilder.Build();
            var inputMessages = new List<ChatMessage>
            {
                new(ChatRole.User, reviewPrompt)
            };

            if (_workflowOptions.EnableCheckpointing)
            {
                await using var checkpointedRun = await InProcessExecution.RunAsync(
                    groupChatWorkflow,
                    inputMessages,
                    CheckpointManager.Default,
                    $"{sessionId}:review-group-chat",
                    ct);

                return await BuildNativeGroupChatReviewResponseAsync(
                    checkpointedRun,
                    reviewer,
                    participantNames,
                    groupChatManager,
                    ResolveGroupChatTerminationReason(terminationReason, groupChatManager),
                    ct);
            }

            await using var run = await InProcessExecution.RunAsync(
                groupChatWorkflow,
                inputMessages,
                $"{sessionId}:review-group-chat",
                ct);

            return await BuildNativeGroupChatReviewResponseAsync(
                run,
                reviewer,
                participantNames,
                groupChatManager,
                ResolveGroupChatTerminationReason(terminationReason, groupChatManager),
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Native group-chat review failed, falling back to handoff/native tool review");
            return null;
        }
    }

    private async Task<AgentResponse?> TryExecuteNativeHandoffReviewAsync(
        string sessionId,
        string reviewPrompt,
        IAgent reviewer,
        IReadOnlyList<IAgent> handoffAgents,
        CancellationToken ct)
    {
        if (_agentFrameworkFactory is null || handoffAgents.Count == 0)
        {
            return null;
        }

        var handoffTargets = handoffAgents.Select(agent => agent.Name).ToList();

        await _runtimeCoordinator.PublishEventAsync(new AgentStreamEvent
        {
            Type = AgentStreamEventType.HandoffStarted,
            AgentName = reviewer.Name,
            Message = "Native review handoff workflow started.",
            Data = new Dictionary<string, object>
            {
                ["targetAgents"] = handoffTargets,
                ["source"] = "collaboration-review"
            }
        }, ct);

        try
        {
            var frameworkReviewer = await _agentFrameworkFactory.CreateFromAgentAsync(reviewer, ct);
            var frameworkHandoffAgents = new List<WorkflowAgent>();
            foreach (var agent in handoffAgents)
            {
                var frameworkAgent = await _agentFrameworkFactory.CreateFromAgentAsync(agent, ct);
                frameworkHandoffAgents.Add(frameworkAgent);
            }

            #pragma warning disable MAAIW001
            var handoffWorkflowBuilder = AgentWorkflowBuilder.CreateHandoffBuilderWith(frameworkReviewer)
                .EmitAgentResponseEvents(true)
                .WithHandoffs(frameworkReviewer, frameworkHandoffAgents)
                .WithHandoffs(
                    frameworkHandoffAgents,
                    frameworkReviewer,
                    "Return findings to the review coordinator so the final recommendation can be produced.");
            #pragma warning restore MAAIW001

            var handoffWorkflow = handoffWorkflowBuilder.Build();
            var inputMessages = new List<ChatMessage>
            {
                new(ChatRole.User, reviewPrompt)
            };

            if (_workflowOptions.EnableCheckpointing)
            {
                await using var checkpointedRun = await InProcessExecution.RunAsync(
                    handoffWorkflow,
                    inputMessages,
                    CheckpointManager.Default,
                    $"{sessionId}:review-handoff",
                    ct);

                return await BuildNativeHandoffReviewResponseAsync(
                    checkpointedRun,
                    reviewer,
                    handoffTargets,
                    sessionId,
                    ct);
            }

            await using var run = await InProcessExecution.RunAsync(
                handoffWorkflow,
                inputMessages,
                $"{sessionId}:review-handoff",
                ct);

            return await BuildNativeHandoffReviewResponseAsync(
                run,
                reviewer,
                handoffTargets,
                sessionId,
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Native handoff review failed, falling back to native tool review");
            return null;
        }
    }

    private async Task<AgentResponse?> BuildNativeHandoffReviewResponseAsync(
        Run run,
        IAgent reviewer,
        IReadOnlyList<string> handoffTargets,
        string sessionId,
        CancellationToken ct)
    {
        var agentResponses = run.OutgoingEvents.OfType<AgentResponseEvent>().ToList();
        var content = string.Join("\n", agentResponses
            .Select(agentResponseEvent => ExtractFrameworkResponseText(agentResponseEvent.Response))
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .Select(text => text.Trim()));

        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        await _runtimeCoordinator.RecordArtifactAsync(new AgentExecutionArtifact
        {
            SessionId = sessionId,
            Type = AgentExecutionArtifactType.Handoff,
            Name = "CollaborationReviewHandoff",
            AgentName = reviewer.Name,
            Status = "Completed",
            Summary = content,
            Data = new Dictionary<string, object>
            {
                ["targetAgents"] = handoffTargets,
                ["checkpointingEnabled"] = run.IsCheckpointingEnabled,
                ["checkpointCount"] = run.Checkpoints.Count,
                ["outgoingEventCount"] = run.OutgoingEvents.Count(),
                ["agentResponseCount"] = agentResponses.Count
            }
        }, ct);

        await _runtimeCoordinator.PublishEventAsync(new AgentStreamEvent
        {
            Type = AgentStreamEventType.HandoffCompleted,
            AgentName = reviewer.Name,
            Message = "Native review handoff workflow completed.",
            Data = new Dictionary<string, object>
            {
                ["targetAgents"] = handoffTargets,
                ["checkpointingEnabled"] = run.IsCheckpointingEnabled,
                ["agentResponseCount"] = agentResponses.Count
            }
        }, ct);

        return new AgentResponse
        {
            Content = content,
            AgentName = reviewer.Name,
            AgentTier = reviewer.Tier,
            Success = true,
            Metadata = new Dictionary<string, object>
            {
                ["nativeHandoffWorkflow"] = true,
                ["nativeReviewMode"] = "HandoffWorkflowBuilder",
                ["handoffTargets"] = handoffTargets,
                ["handoffCheckpointingEnabled"] = run.IsCheckpointingEnabled,
                ["handoffCheckpointCount"] = run.Checkpoints.Count,
                ["handoffOutgoingEventCount"] = run.OutgoingEvents.Count(),
                ["handoffAgentResponseCount"] = agentResponses.Count
            }
        };
    }

    private async Task<AgentResponse?> BuildNativeGroupChatReviewResponseAsync(
        Run run,
        IAgent reviewer,
        IReadOnlyList<string> participantNames,
        RoundRobinGroupChatManager? groupChatManager,
        string terminationReason,
        CancellationToken ct)
    {
        var agentResponses = run.OutgoingEvents.OfType<AgentResponseEvent>().ToList();
        var content = ExtractWorkflowRunText(run);

        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        await _runtimeCoordinator.RecordArtifactAsync(new AgentExecutionArtifact
        {
            SessionId = _runtimeCoordinator.CurrentSessionId ?? string.Empty,
            Type = AgentExecutionArtifactType.SessionState,
            Name = "CollaborationReviewGroupChat",
            AgentName = reviewer.Name,
            Status = "Completed",
            Summary = terminationReason,
            Data = new Dictionary<string, object>
            {
                ["participants"] = participantNames,
                ["terminationReason"] = terminationReason,
                ["iterationCount"] = groupChatManager?.IterationCount ?? 0,
                ["maximumIterationCount"] = groupChatManager?.MaximumIterationCount ?? _workflowOptions.GroupChatMaximumIterations,
                ["checkpointingEnabled"] = run.IsCheckpointingEnabled,
                ["checkpointCount"] = run.Checkpoints.Count,
                ["outgoingEventCount"] = run.OutgoingEvents.Count(),
                ["agentResponseCount"] = agentResponses.Count
            }
        }, ct);

        return new AgentResponse
        {
            Content = content,
            AgentName = reviewer.Name,
            AgentTier = reviewer.Tier,
            Success = true,
            Metadata = new Dictionary<string, object>
            {
                ["nativeGroupChatTermination"] = true,
                ["nativeReviewMode"] = "GroupChatWorkflowBuilder",
                ["groupChatParticipants"] = participantNames,
                ["groupChatTerminationReason"] = terminationReason,
                ["groupChatIterationCount"] = groupChatManager?.IterationCount ?? 0,
                ["groupChatMaximumIterationCount"] = groupChatManager?.MaximumIterationCount ?? _workflowOptions.GroupChatMaximumIterations,
                ["groupChatCheckpointingEnabled"] = run.IsCheckpointingEnabled,
                ["groupChatCheckpointCount"] = run.Checkpoints.Count,
                ["groupChatOutgoingEventCount"] = run.OutgoingEvents.Count(),
                ["groupChatAgentResponseCount"] = agentResponses.Count
            }
        };
    }

    private static string ExtractWorkflowRunText(Run run)
    {
        var texts = new List<string>();

        texts.AddRange(run.OutgoingEvents
            .OfType<AgentResponseEvent>()
            .Select(agentResponseEvent => ExtractFrameworkResponseText(agentResponseEvent.Response)));

        texts.AddRange(run.OutgoingEvents
            .OfType<AgentResponseUpdateEvent>()
            .Select(agentResponseUpdateEvent => ExtractFrameworkResponseText(agentResponseUpdateEvent.AsResponse())));

        foreach (var outputEvent in run.OutgoingEvents.OfType<WorkflowOutputEvent>())
        {
            if (outputEvent.Is<ChatMessage>(out var chatMessage))
            {
                texts.Add(ExtractChatMessageText(chatMessage));
                continue;
            }

            if (outputEvent.Is<string>(out var textOutput))
            {
                texts.Add(textOutput);
                continue;
            }

            texts.Add(ExtractFrameworkResponseText(outputEvent.AsType(typeof(object))));
        }

        return string.Join("\n", texts
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .Select(text => text.Trim()));
    }

    private bool TryResolveGroupChatTerminationReason(IEnumerable<ChatMessage> history, out string? reason)
    {
        foreach (var message in history.Where(message => message.Role == ChatRole.Assistant))
        {
            var text = ExtractChatMessageText(message);
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            foreach (var phrase in _workflowOptions.GroupChatTerminationPhrases)
            {
                if (string.IsNullOrWhiteSpace(phrase))
                {
                    continue;
                }

                if (text.Contains(phrase, StringComparison.OrdinalIgnoreCase))
                {
                    reason = $"phrase:{phrase}";
                    return true;
                }
            }
        }

        reason = null;
        return false;
    }

    private string ResolveGroupChatTerminationReason(string? explicitReason, RoundRobinGroupChatManager? groupChatManager)
    {
        if (!string.IsNullOrWhiteSpace(explicitReason))
        {
            return explicitReason;
        }

        if (groupChatManager is not null && groupChatManager.IterationCount >= groupChatManager.MaximumIterationCount)
        {
            return "max-iterations";
        }

        return "workflow-completed";
    }

    private static string ExtractChatMessageText(ChatMessage message)
    {
        var content = string.Join("\n", message.Contents.OfType<TextContent>().Select(text => text.Text));
        return string.IsNullOrWhiteSpace(content) ? message.Text ?? string.Empty : content;
    }

    private static string ExtractFrameworkResponseText(object? frameworkResponse)
    {
        if (frameworkResponse is null)
        {
            return string.Empty;
        }

        var messagesProperty = frameworkResponse.GetType().GetProperty("Messages");
        if (messagesProperty?.GetValue(frameworkResponse) is IEnumerable<ChatMessage> messages)
        {
            var content = string.Join("\n", messages
                .Where(message => message.Role == ChatRole.Assistant)
                .SelectMany(message => message.Contents.OfType<TextContent>())
                .Select(text => text.Text));

            if (!string.IsNullOrWhiteSpace(content))
            {
                return content;
            }

            return string.Join("\n", messages
                .Where(message => message.Role == ChatRole.Assistant)
                .Select(message => message.Text)
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .Select(text => text!));
        }

        var textProperty = frameworkResponse.GetType().GetProperty("Text");
        return textProperty?.GetValue(frameworkResponse) as string ?? string.Empty;
    }

    private async Task<TaskPlan> BuildFallbackPlanAsync(string userId, string input, AnalysisResult analysis, CancellationToken ct)
    {
        return await _taskPlanManager.CreatePlanAsync(userId, input,
        [
            new TaskStep
            {
                Description = input,
                AssignedAgent = string.IsNullOrWhiteSpace(analysis.EstimatedAgent) ? ResolveAgentName(analysis.PrimaryDomain) : analysis.EstimatedAgent
            }
        ]);
    }

    private async Task<string> BuildStepInputAsync(string originalInput, TaskStep step, IAgent agent, CancellationToken ct)
    {
        return await BuildStepInputAsync(originalInput, step, agent, null, ct);
    }

    private async Task<string> BuildStepInputAsync(
        string originalInput,
        TaskStep step,
        IAgent agent,
        string? sharedContextSupplement,
        CancellationToken ct)
    {
        var sections = new List<string>
        {
            $"[Step]\n{step.Description}"
        };

        if (!string.IsNullOrWhiteSpace(sharedContextSupplement))
        {
            sections.Add(sharedContextSupplement);
        }

        if (_ragService is null)
        {
            sections.Add($"[Original Objective]\n{originalInput}");
            return string.Join("\n\n", sections);
        }

        var ragContext = await _ragService.RetrieveContextAsync(new RAGQuery
        {
            Query = $"{originalInput}\n{step.Description}",
            AgentId = agent.Name,
            Scope = SearchScope.All,
            MaxResults = 8,
            TopKAfterReRank = 4,
            MinRelevanceScore = 0.25
        }, ct);

        if (string.IsNullOrWhiteSpace(ragContext.BuiltContext))
        {
            sections.Add($"[Original Objective]\n{originalInput}");
            return string.Join("\n\n", sections);
        }

        sections.Add($"[Relevant Context]\n{ragContext.BuiltContext}");
        sections.Add($"[Original Objective]\n{originalInput}");
        return string.Join("\n\n", sections);
    }

    private static AnalysisResult BuildStepAnalysis(TaskStep step, AnalysisResult original)
    {
        var agentName = ResolveAgentName(step.AssignedAgent ?? original.EstimatedAgent ?? original.PrimaryDomain);
        return new AnalysisResult
        {
            PrimaryDomain = ResolveDomain(agentName, original.PrimaryDomain),
            EstimatedAgent = agentName,
            Intent = original.Intent,
            Complexity = ComplexityLevel.Moderate,
            RecommendedTier = AgentTier.Specialist,
            Confidence = original.Confidence,
            RequiredTools = new List<string>(original.RequiredTools)
        };
    }

    private static string ResolveAgentName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "GeneralAgent";
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "personal" or "personalagent" => "PersonalAgent",
            "work" or "workagent" => "WorkAgent",
            "learning" or "learningagent" => "LearningAgent",
            "creative" or "creativeagent" => "CreativeAgent",
            "analysis" or "analysisagent" or "reviewer" => "AnalysisAgent",
            "calendar" or "calendaragent" => "CalendarAgent",
            "api" or "apiagent" => "APIAgent",
            "notification" or "notificationagent" => "NotificationAgent",
            _ when value.EndsWith("Agent", StringComparison.OrdinalIgnoreCase) => value,
            _ => "GeneralAgent"
        };
    }

    private static string ResolveDomain(string agentName, string fallback)
        => agentName.ToLowerInvariant() switch
        {
            "personalagent" => "personal",
            "workagent" => "work",
            "learningagent" => "learning",
            "creativeagent" => "creative",
            "analysisagent" => "analysis",
            "calendaragent" => "calendar",
            "apiagent" => "api",
            "notificationagent" => "notification",
            _ => fallback
        };

    private static string BuildReviewPrompt(
        string originalInput,
        TaskPlan plan,
        IReadOnlyList<(TaskStep Step, IAgent Agent, AgentResponse Response)> stepOutputs,
        string? sharedContextSupplement)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Review the collaborative workflow output.");
        sb.AppendLine();
        sb.AppendLine($"Original objective: {originalInput}");
        sb.AppendLine($"Plan: {plan.Title}");

        if (!string.IsNullOrWhiteSpace(sharedContextSupplement))
        {
            sb.AppendLine();
            sb.AppendLine(sharedContextSupplement);
        }

        sb.AppendLine();
        sb.AppendLine("Steps and outputs:");
        sb.AppendLine("If a step output is incomplete or ambiguous, you may call the step agent as a tool before writing the final review.");

        foreach (var (step, agent, response) in stepOutputs)
        {
            sb.AppendLine($"- Step {step.Index + 1}: {step.Description}");
            sb.AppendLine($"  Agent: {step.AssignedAgent ?? agent.Name}");
            sb.AppendLine($"  Output: {response.Content}");
        }

        sb.AppendLine();
        sb.AppendLine("Provide a concise review with:");
        sb.AppendLine("1. Completeness assessment");
        sb.AppendLine("2. Risks or gaps");
        sb.AppendLine("3. Final recommendation");
        return sb.ToString();
    }

    private async Task<string> BuildChannelAwareStepInputAsync(
        string sessionId,
        TaskStep step,
        IAgent agent,
        string stepInput,
        AgentChannelKind kind,
        CancellationToken ct)
    {
        if (_agentChannelService is null)
        {
            return stepInput;
        }

        await _agentChannelService.PublishAsync(
            sessionId,
            "Planner",
            agent.Name,
            $"Step {step.Index + 1}: {step.Description}",
            kind,
            new Dictionary<string, object>
            {
                ["stepIndex"] = step.Index,
                ["assignedAgent"] = agent.Name
            },
            ct);

        return await _agentChannelService.BuildChannelContextAsync(sessionId, agent.Name, stepInput, ct: ct);
    }

    private async Task<string> BuildChannelAwareReviewInputAsync(string sessionId, string reviewerName, string reviewPrompt, CancellationToken ct)
    {
        if (_agentChannelService is null)
        {
            return reviewPrompt;
        }

        await _agentChannelService.PublishAsync(
            sessionId,
            "CollaborativeWorkflow",
            reviewerName,
            "Review consolidated multi-agent output and produce a final synthesis.",
            AgentChannelKind.Review,
            new Dictionary<string, object>
            {
                ["source"] = "collaboration-review"
            },
            ct);

        return await _agentChannelService.BuildChannelContextAsync(sessionId, reviewerName, reviewPrompt, ct: ct);
    }

    private static string BuildFinalResponse(TaskPlan plan, IReadOnlyList<(TaskStep Step, IAgent Agent, AgentResponse Response)> stepOutputs, AgentResponse reviewResponse)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"## Plan: {plan.Title}");
        sb.AppendLine();

        foreach (var (step, _, response) in stepOutputs)
        {
            sb.AppendLine($"### Step {step.Index + 1} — {step.Description}");
            sb.AppendLine(response.Content);
            sb.AppendLine();
        }

        sb.AppendLine("## Reviewer");
        sb.AppendLine(reviewResponse.Content);
        return sb.ToString().TrimEnd();
    }

    private static List<ChatMessage> AggregateConcurrentContextMessages(IList<List<ChatMessage>> outputs)
    {
        var content = string.Join("\n\n", outputs
            .SelectMany(messages => messages)
            .Where(message => message.Role == ChatRole.Assistant)
            .Select(message => message.Text)
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .Select(text => text!));

        return string.IsNullOrWhiteSpace(content)
            ? []
            : [new ChatMessage(ChatRole.Assistant, content)];
    }

    private static string? BuildSharedContextSupplement(ConcurrentDictionary<string, string> contextSegments)
    {
        if (contextSegments.IsEmpty)
        {
            return null;
        }

        var orderedSegments = new List<string>();
        if (contextSegments.TryGetValue("rag", out var ragContext))
        {
            orderedSegments.Add(ragContext);
        }

        if (contextSegments.TryGetValue("channel", out var channelContext))
        {
            orderedSegments.Add(channelContext);
        }

        var additionalSegments = contextSegments
            .Where(entry => entry.Key is not "rag" and not "channel")
            .ToList();
        additionalSegments.Sort((left, right) => string.Compare(left.Key, right.Key, StringComparison.Ordinal));

        foreach (var segment in additionalSegments.Select(entry => entry.Value))
        {
            orderedSegments.Add(segment);
        }

        if (orderedSegments.Count == 0)
        {
            return null;
        }

        var builder = new StringBuilder();
        builder.AppendLine("[Workflow Shared Context]");
        builder.AppendLine("Use the supplemental context below when executing and reviewing the plan.");
        builder.AppendLine();
        builder.AppendLine(string.Join("\n\n", orderedSegments));
        return builder.ToString().TrimEnd();
    }

    private static void MergeWorkflowMetadata(Dictionary<string, object> target, IReadOnlyDictionary<string, object> source)
    {
        foreach (var entry in source)
        {
            target[entry.Key] = entry.Value;
        }
    }

    private void CaptureWorkflowRunMetadata(Run run, CollaborationWorkflowState state)
    {
        state.WorkflowMetadata["advancedWorkflow"] = state.AdvancedWorkflowEnabled;
        state.WorkflowMetadata["concurrentContextStageEnabled"] = ShouldUseConcurrentContextStage();
        state.WorkflowMetadata["workflowCheckpointingRequested"] = state.CheckpointingRequested;
        state.WorkflowMetadata["workflowCheckpointingEnabled"] = run.IsCheckpointingEnabled;
        state.WorkflowMetadata["workflowCheckpointCount"] = run.Checkpoints.Count;
        state.WorkflowMetadata["workflowOutgoingEventCount"] = run.OutgoingEvents.Count();
        state.WorkflowMetadata["workflowContextEnriched"] = !string.IsNullOrWhiteSpace(state.SharedContextSupplement);
        state.WorkflowMetadata["concurrentContextSourcesCount"] = state.ContextSegments.Count;

        if (state.ContextSegments.Count > 0)
        {
            var orderedKeys = state.ContextSegments.Keys.ToList();
            orderedKeys.Sort(StringComparer.Ordinal);
            state.WorkflowMetadata["concurrentContextSources"] = orderedKeys;
        }

        if (run.LastCheckpoint is not null)
        {
            state.WorkflowMetadata["workflowLastCheckpointId"] = run.LastCheckpoint.CheckpointId;
            state.WorkflowMetadata["workflowLastCheckpointSessionId"] = run.LastCheckpoint.SessionId;
        }
    }

    private void CaptureContextRunMetadata(Run run, CollaborationWorkflowState state)
    {
        state.WorkflowMetadata["contextCheckpointingEnabled"] = run.IsCheckpointingEnabled;
        state.WorkflowMetadata["contextCheckpointCount"] = run.Checkpoints.Count;
        state.WorkflowMetadata["contextOutgoingEventCount"] = run.OutgoingEvents.Count();

        if (run.LastCheckpoint is not null)
        {
            state.WorkflowMetadata["contextLastCheckpointId"] = run.LastCheckpoint.CheckpointId;
            state.WorkflowMetadata["contextLastCheckpointSessionId"] = run.LastCheckpoint.SessionId;
        }
    }

    private async Task RecordWorkflowStateArtifactAsync(CollaborationWorkflowState state, CancellationToken ct)
    {
        if (state.WorkflowMetadata.Count == 0)
        {
            return;
        }

        await _runtimeCoordinator.RecordArtifactAsync(new AgentExecutionArtifact
        {
            SessionId = state.SessionId,
            Type = AgentExecutionArtifactType.SessionState,
            Name = "CollaborationWorkflowState",
            AgentName = "CollaborativeWorkflow",
            Status = state.FailedResponse is not null
                ? "Failed"
                : state.FinalResponse?.Success == true
                    ? "Completed"
                    : "Unknown",
            Summary = state.ExecutionMode,
            Data = new Dictionary<string, object>(state.WorkflowMetadata)
        }, ct);
    }

    private sealed class CollaborationWorkflowState
    {
        public CollaborationWorkflowState(string sessionId, string originalInput, UserContext context, AnalysisResult analysis)
        {
            SessionId = sessionId;
            OriginalInput = originalInput;
            Context = context;
            Analysis = analysis;
        }

        public string SessionId { get; }
        public string OriginalInput { get; }
        public UserContext Context { get; }
        public AnalysisResult Analysis { get; }
        public TaskPlan? Plan { get; set; }
        public List<(TaskStep Step, IAgent Agent, AgentResponse Response)> StepOutputs { get; } = [];
        public ConcurrentDictionary<string, string> ContextSegments { get; } = new(StringComparer.OrdinalIgnoreCase);
        public AgentResponse? FailedResponse { get; set; }
        public AgentResponse? FinalResponse { get; set; }
        public AgentResponse? ReviewResponse { get; set; }
        public string? SharedContextSupplement { get; set; }
        public string ExecutionMode { get; set; } = "planner-executor-reviewer-workflow";
        public string WorkflowEngine { get; set; } = "AgentWorkflowBuilder";
        public bool AdvancedWorkflowEnabled { get; set; }
        public bool CheckpointingRequested { get; set; }
        public Dictionary<string, object> WorkflowMetadata { get; } = new();
    }

    private sealed class CollaborationWorkflowStageChatClient(
        Func<IEnumerable<ChatMessage>, CancellationToken, Task<string>> executeAsync) : IChatClient
    {
        private readonly Func<IEnumerable<ChatMessage>, CancellationToken, Task<string>> _executeAsync = executeAsync;

        public async Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var content = await _executeAsync(messages, cancellationToken);
            return new ChatResponse(new ChatMessage(ChatRole.Assistant, content));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation]
            CancellationToken cancellationToken = default)
        {
            var response = await GetResponseAsync(messages, options, cancellationToken);
            var text = response.Text ?? string.Empty;

            if (string.IsNullOrWhiteSpace(text))
            {
                yield break;
            }

            yield return new ChatResponseUpdate(ChatRole.Assistant, text);
        }

        public object? GetService(Type serviceType, object? serviceKey = null)
            => serviceType == typeof(IChatClient) ? this : null;

        public void Dispose()
        {
        }
    }

    private sealed class CollaborationWorkflowStageFailedException(string message) : Exception(message);

    private sealed record StepExecutionResult(
        List<(TaskStep Step, IAgent Agent, AgentResponse Response)> StepOutputs,
        AgentResponse? FailedResponse);

    private sealed record ReviewExecutionResult(IAgent Reviewer, AgentResponse ReviewResponse);
}