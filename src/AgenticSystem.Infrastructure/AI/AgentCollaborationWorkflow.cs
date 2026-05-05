using System.Diagnostics;
using System.Text;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.AgentFramework;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

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

    public AgentCollaborationWorkflow(
        ChatClientPlanner planner,
        IAgentFactory agentFactory,
        ITaskPlanManager taskPlanManager,
        IAgentRuntimeCoordinator runtimeCoordinator,
        ILogger<AgentCollaborationWorkflow> logger,
        IAgentChannelService? agentChannelService = null,
        IRAGService? ragService = null,
        AgentFrameworkFactory? agentFrameworkFactory = null)
    {
        _planner = planner;
        _agentFactory = agentFactory;
        _taskPlanManager = taskPlanManager;
        _runtimeCoordinator = runtimeCoordinator;
        _logger = logger;
        _agentChannelService = agentChannelService;
        _ragService = ragService;
        _agentFrameworkFactory = agentFrameworkFactory;
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

        var stepOutputs = new List<(TaskStep Step, IAgent Agent, AgentResponse Response)>();

        foreach (var step in plan.Steps.OrderBy(step => step.Index))
        {
            var stepAnalysis = BuildStepAnalysis(step, analysis);
            var agent = await _agentFactory.GetOrCreateAgentAsync(stepAnalysis);
            var stepInput = await BuildStepInputAsync(input, step, agent, ct);
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
            var response = await agent.ExecuteAsync(stepInput, context);
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

                return response;
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

        var reviewer = await _agentFactory.GetOrCreateAgentAsync(new AnalysisResult
        {
            PrimaryDomain = "analysis",
            EstimatedAgent = "AnalysisAgent",
            Intent = IntentType.Analyze,
            Complexity = ComplexityLevel.Complex,
            RecommendedTier = AgentTier.Specialist
        });

        var reviewPrompt = BuildReviewPrompt(input, plan, stepOutputs);
        reviewPrompt = await BuildChannelAwareReviewInputAsync(sessionId, reviewer.Name, reviewPrompt, ct);
        var reviewSw = Stopwatch.StartNew();
        using (var reviewerScope = _runtimeCoordinator.BeginAgentScope(reviewer.Name, reviewer.AvailableTools))
        {
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

            var finalContent = BuildFinalResponse(plan, stepOutputs, reviewResponse);
            return new AgentResponse
            {
                Content = finalContent,
                AgentName = "CollaborativeWorkflow",
                AgentTier = AgentTier.Chief,
                Success = reviewResponse.Success,
                ActionsPerformed = stepOutputs.Select(output => output.Step.Description).ToList(),
                ToolsUsed = stepOutputs.SelectMany(output => output.Response.ToolsUsed).Distinct().ToList(),
                Metadata = new Dictionary<string, object>
                {
                    ["executionMode"] = "planner-executor-reviewer",
                    ["planId"] = plan.Id,
                    ["reviewer"] = reviewer.Name,
                    ["stepCount"] = stepOutputs.Count,
                    ["nativeAgentTools"] = reviewResponse.Metadata.GetValueOrDefault("nativeAgentTools", false)
                }
            };
        }
    }

    private async Task<AgentResponse> ExecuteReviewAsync(
        string sessionId,
        string reviewPrompt,
        IAgent reviewer,
        IReadOnlyList<(TaskStep Step, IAgent Agent, AgentResponse Response)> stepOutputs,
        UserContext context,
        CancellationToken ct)
    {
        if (_agentFrameworkFactory is null)
        {
            return await reviewer.ExecuteAsync(reviewPrompt, context);
        }

        try
        {
            var bindings = new List<AgentToolBinding>();
            foreach (var agent in stepOutputs.Select(output => output.Agent).DistinctBy(agent => agent.Name))
            {
                if (string.Equals(agent.Name, reviewer.Name, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var binding = await _agentFrameworkFactory.CreateToolBindingAsync(agent, sessionId, ct);
                if (binding is not null)
                {
                    bindings.Add(binding);
                }
            }

            if (bindings.Count == 0)
            {
                return await reviewer.ExecuteAsync(reviewPrompt, context);
            }

            var frameworkReviewer = await _agentFrameworkFactory.CreateFromAgentAsync(reviewer, bindings.Select(binding => binding.Tool), ct);
            var reviewSession = await _agentFrameworkFactory.GetOrCreateSessionAsync(frameworkReviewer, sessionId, ct);
            var frameworkResponse = await frameworkReviewer.RunAsync(reviewPrompt, reviewSession, cancellationToken: ct);

            var content = string.Join("\n", frameworkResponse.Messages
                .Where(message => message.Role == ChatRole.Assistant)
                .SelectMany(message => message.Contents.OfType<TextContent>())
                .Select(text => text.Text));

            if (string.IsNullOrWhiteSpace(content))
            {
                content = string.Join("\n", frameworkResponse.Messages
                    .Where(message => message.Role == ChatRole.Assistant)
                    .Select(message => message.Text));
            }

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
            return await reviewer.ExecuteAsync(reviewPrompt, context);
        }
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
        if (_ragService is null)
        {
            return $"[Step]\n{step.Description}\n\n[Original Objective]\n{originalInput}";
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
            return $"[Step]\n{step.Description}\n\n[Original Objective]\n{originalInput}";
        }

        return $"[Step]\n{step.Description}\n\n[Relevant Context]\n{ragContext.BuiltContext}\n\n[Original Objective]\n{originalInput}";
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

    private static string BuildReviewPrompt(string originalInput, TaskPlan plan, IReadOnlyList<(TaskStep Step, IAgent Agent, AgentResponse Response)> stepOutputs)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Review the collaborative workflow output.");
        sb.AppendLine();
        sb.AppendLine($"Original objective: {originalInput}");
        sb.AppendLine($"Plan: {plan.Title}");
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
}