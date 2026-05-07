using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Infrastructure.AI;

/// <summary>
/// Task planner baseado em IChatClient + FunctionInvocation (M.E.AI).
/// Substitui SemanticKernelPlanner — usa o pipeline IChatClient com function calling
/// para decomposição inteligente de tarefas.
/// </summary>
public class ChatClientPlanner
{
    private readonly IChatClient _chatClient;
    private readonly ITaskPlanManager _taskPlanManager;
    private readonly UnifiedAIToolProvider? _toolProvider;
    private readonly ILogger<ChatClientPlanner> _logger;
    private readonly ILoggerFactory _loggerFactory;

    private const string PlannerSystemPrompt = """
        You are a task decomposition expert. Given a complex objective, break it down into
        sequential atomic steps that can be executed independently.

        Rules:
        - Each step must be self-contained and have a clear success criteria
        - Steps should be in logical execution order
        - Include the agent best suited for each step (from: Personal, Work, Learning, Creative, Analysis, Calendar, API)
        - Output as JSON array: [{"description": "...", "agent": "..."}]
        - Keep steps concise (max 1 sentence each)
        - Maximum 10 steps per plan
        """;

    public ChatClientPlanner(
        IChatClient chatClient,
        ITaskPlanManager taskPlanManager,
        ILoggerFactory loggerFactory,
        UnifiedAIToolProvider? toolProvider = null)
    {
        _chatClient = chatClient ?? throw new ArgumentNullException(nameof(chatClient));
        _taskPlanManager = taskPlanManager ?? throw new ArgumentNullException(nameof(taskPlanManager));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _toolProvider = toolProvider;
        _logger = loggerFactory.CreateLogger<ChatClientPlanner>();
    }

    // Backward-compatible overload used by existing tests and callers.
    public ChatClientPlanner(
        IChatClient chatClient,
        ITaskPlanManager taskPlanManager,
        ILoggerFactory loggerFactory,
        IToolManager? toolManager)
        : this(chatClient, taskPlanManager, loggerFactory,
            toolManager is null ? null : new UnifiedAIToolProvider(loggerFactory, toolManager))
    {
    }

    /// <summary>
    /// Usa IChatClient + FunctionInvocation para decompor um objetivo em TaskPlan com steps atômicos.
    /// Tools disponíveis são injetados como AIFunction no ChatOptions para auto-invocation.
    /// </summary>
    public async Task<TaskPlan?> PlanAsync(string userId, string objective, CancellationToken ct = default)
    {
        _logger.LogInformation("🧠 ChatClient Planner: decomposing '{Objective}'", objective);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, PlannerSystemPrompt),
            new(ChatRole.User, $"Objective: {objective}")
        };

        var options = new ChatOptions();

        // Inject unified tools (internas + MCP) as AIFunctions for auto-function-calling
        if (_toolProvider is not null)
        {
            var aiTools = await _toolProvider.GetToolsAsync(ct);
            if (aiTools.Count > 0)
            {
                options.Tools = [.. aiTools];
            }
        }

        var response = await _chatClient.GetResponseAsync(messages, options, ct);
        var content = response.Text;

        if (string.IsNullOrWhiteSpace(content))
        {
            _logger.LogWarning("ChatClient Planner returned empty response for '{Objective}'", objective);
            return null;
        }

        var steps = ParseSteps(content);
        if (steps.Count == 0)
        {
            _logger.LogWarning("ChatClient Planner could not parse steps from response");
            return null;
        }

        var plan = await _taskPlanManager.CreatePlanAsync(userId, objective, steps);
        _logger.LogInformation("✅ ChatClient Planner: created plan {PlanId} with {StepCount} steps", plan.Id, steps.Count);

        return plan;
    }

    private static List<TaskStep> ParseSteps(string content)
    {
        var steps = new List<TaskStep>();

        try
        {
            var json = content;
            var startIdx = content.IndexOf('[');
            var endIdx = content.LastIndexOf(']');
            if (startIdx >= 0 && endIdx > startIdx)
                json = content[startIdx..(endIdx + 1)];

            var parsed = JsonSerializer.Deserialize<List<PlanStep>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (parsed is not null)
            {
                for (var i = 0; i < parsed.Count; i++)
                {
                    steps.Add(new TaskStep
                    {
                        Index = i,
                        Description = parsed[i].Description,
                        AssignedAgent = parsed[i].Agent,
                        Status = TaskStepStatus.Pending
                    });
                }
            }
        }
        catch (JsonException)
        {
            var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            for (var i = 0; i < lines.Length && i < 10; i++)
            {
                var line = lines[i].TrimStart('-', '*', '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '.', ' ');
                if (!string.IsNullOrWhiteSpace(line))
                {
                    steps.Add(new TaskStep
                    {
                        Index = i,
                        Description = line,
                        Status = TaskStepStatus.Pending
                    });
                }
            }
        }

        return steps;
    }

    private record PlanStep(string Description, string? Agent);
}
