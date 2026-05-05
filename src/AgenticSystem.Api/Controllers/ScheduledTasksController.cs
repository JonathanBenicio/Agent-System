using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Api.Controllers;

/// <summary>
/// Admin API para configuração de Scheduled Tasks, Trigger Rules e Delivery Channels.
/// Todas as operações são configuráveis via UI.
/// </summary>
[Authorize]
[ApiController]
[Route("api/admin/scheduled-tasks")]
public class ScheduledTasksController : ControllerBase
{
    private readonly IScheduledTaskManager _taskManager;
    private readonly ITriggerEngine _triggerEngine;
    private readonly IEnumerable<IDeliveryChannel> _channels;
    private readonly ILogger<ScheduledTasksController> _logger;

    public ScheduledTasksController(
        IScheduledTaskManager taskManager,
        ITriggerEngine triggerEngine,
        IEnumerable<IDeliveryChannel> channels,
        ILogger<ScheduledTasksController> logger)
    {
        _taskManager = taskManager;
        _triggerEngine = triggerEngine;
        _channels = channels;
        _logger = logger;
    }

    // ═══════════════════════════════════════════════════════════
    // Tasks CRUD
    // ═══════════════════════════════════════════════════════════

    [HttpGet("tasks")]
    [ProducesResponseType(typeof(IReadOnlyList<ScheduledTask>), 200)]
    public async Task<IActionResult> GetAllTasks(CancellationToken ct)
    {
        var tasks = await _taskManager.GetAllAsync(ct);
        return Ok(tasks);
    }

    [HttpGet("tasks/{id}")]
    [ProducesResponseType(typeof(ScheduledTask), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetTask(string id, CancellationToken ct)
    {
        var task = await _taskManager.GetAsync(id, ct);
        if (task is null) return NotFound();
        return Ok(task);
    }

    [HttpPost("tasks")]
    [ProducesResponseType(typeof(ScheduledTask), 201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> CreateTask([FromBody] CreateTaskRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest("Name is required");

        ScheduledTask task;

        if (!string.IsNullOrWhiteSpace(request.CronExpression))
        {
            task = await _taskManager.RegisterAsync(
                request.Name,
                request.CronExpression,
                request.AssociatedRule,
                request.MaxRetryAttempts,
                ct);
        }
        else if (request.IntervalSeconds > 0)
        {
            task = await _taskManager.RegisterAsync(
                request.Name,
                TimeSpan.FromSeconds(request.IntervalSeconds),
                request.AssociatedRule,
                request.MaxRetryAttempts,
                ct);
        }
        else
        {
            return BadRequest("Either cronExpression or intervalSeconds must be provided");
        }

        _logger.LogInformation("Task created: {TaskId} ({TaskName})", task.Id, task.Name);
        return CreatedAtAction(nameof(GetTask), new { id = task.Id }, task);
    }

    [HttpPost("tasks/{id}/pause")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> PauseTask(string id, CancellationToken ct)
    {
        var task = await _taskManager.GetAsync(id, ct);
        if (task is null) return NotFound();

        await _taskManager.PauseAsync(id, ct);
        return NoContent();
    }

    [HttpPost("tasks/{id}/resume")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> ResumeTask(string id, CancellationToken ct)
    {
        var task = await _taskManager.GetAsync(id, ct);
        if (task is null) return NotFound();

        await _taskManager.ResumeAsync(id, ct);
        return NoContent();
    }

    [HttpPost("tasks/{id}/execute")]
    [ProducesResponseType(typeof(TaskExecution), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> ExecuteTask(string id, CancellationToken ct)
    {
        var task = await _taskManager.GetAsync(id, ct);
        if (task is null) return NotFound();

        var execution = await _taskManager.ExecuteAsync(id, ct);
        return Ok(execution);
    }

    [HttpDelete("tasks/{id}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> DeleteTask(string id, CancellationToken ct)
    {
        var task = await _taskManager.GetAsync(id, ct);
        if (task is null) return NotFound();

        await _taskManager.RemoveAsync(id, ct);
        return NoContent();
    }

    // ═══════════════════════════════════════════════════════════
    // Trigger Rules CRUD
    // ═══════════════════════════════════════════════════════════

    [HttpGet("rules")]
    [ProducesResponseType(typeof(IReadOnlyList<TriggerRule>), 200)]
    public async Task<IActionResult> GetAllRules(CancellationToken ct)
    {
        var rules = await _triggerEngine.GetAllRulesAsync(ct);
        return Ok(rules);
    }

    [HttpGet("rules/{id}")]
    [ProducesResponseType(typeof(TriggerRule), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetRule(string id, CancellationToken ct)
    {
        var rule = await _triggerEngine.GetRuleAsync(id, ct);
        if (rule is null) return NotFound();
        return Ok(rule);
    }

    [HttpPost("rules")]
    [ProducesResponseType(typeof(TriggerRule), 201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> CreateRule([FromBody] TriggerRule rule, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(rule.Name))
            return BadRequest("Name is required");
        if (rule.Source is null)
            return BadRequest("Source is required");
        if (rule.Condition is null)
            return BadRequest("Condition is required");

        rule.Id = Guid.NewGuid().ToString();
        rule.CreatedAt = DateTime.UtcNow;

        await _triggerEngine.RegisterRuleAsync(rule, ct);
        _logger.LogInformation("Rule created: {RuleId} ({RuleName})", rule.Id, rule.Name);
        return CreatedAtAction(nameof(GetRule), new { id = rule.Id }, rule);
    }

    [HttpPut("rules/{id}")]
    [ProducesResponseType(typeof(TriggerRule), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> UpdateRule(string id, [FromBody] TriggerRule rule, CancellationToken ct)
    {
        var existing = await _triggerEngine.GetRuleAsync(id, ct);
        if (existing is null) return NotFound();

        rule.Id = id;
        await _triggerEngine.RemoveRuleAsync(id, ct);
        await _triggerEngine.RegisterRuleAsync(rule, ct);

        return Ok(rule);
    }

    [HttpPost("rules/{id}/enable")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> EnableRule(string id, CancellationToken ct)
    {
        var rule = await _triggerEngine.GetRuleAsync(id, ct);
        if (rule is null) return NotFound();

        await _triggerEngine.EnableRuleAsync(id, ct);
        return NoContent();
    }

    [HttpPost("rules/{id}/disable")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> DisableRule(string id, CancellationToken ct)
    {
        var rule = await _triggerEngine.GetRuleAsync(id, ct);
        if (rule is null) return NotFound();

        await _triggerEngine.DisableRuleAsync(id, ct);
        return NoContent();
    }

    [HttpPost("rules/{id}/evaluate")]
    [ProducesResponseType(typeof(TriggerEvaluationResult), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> EvaluateRule(string id, CancellationToken ct)
    {
        var rule = await _triggerEngine.GetRuleAsync(id, ct);
        if (rule is null) return NotFound();

        var result = await _triggerEngine.EvaluateAsync(rule, ct);
        return Ok(result);
    }

    [HttpDelete("rules/{id}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> DeleteRule(string id, CancellationToken ct)
    {
        var rule = await _triggerEngine.GetRuleAsync(id, ct);
        if (rule is null) return NotFound();

        await _triggerEngine.RemoveRuleAsync(id, ct);
        return NoContent();
    }

    // ═══════════════════════════════════════════════════════════
    // Delivery Channels
    // ═══════════════════════════════════════════════════════════

    [HttpGet("channels")]
    [ProducesResponseType(typeof(IEnumerable<ChannelInfo>), 200)]
    public async Task<IActionResult> GetChannels(CancellationToken ct)
    {
        var channels = new List<ChannelInfo>();

        foreach (var channel in _channels)
        {
            var healthy = await channel.IsHealthyAsync(ct);
            channels.Add(new ChannelInfo
            {
                Name = channel.ChannelName,
                Healthy = healthy
            });
        }

        return Ok(channels);
    }

    [HttpPost("channels/{name}/test")]
    [ProducesResponseType(typeof(DeliveryResult), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> TestChannel(string name, [FromBody] Dictionary<string, string> config, CancellationToken ct)
    {
        var channel = _channels.FirstOrDefault(c => c.ChannelName.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (channel is null) return NotFound($"Channel '{name}' not found");

        var testPayload = new TriggerNotificationPayload
        {
            TriggerName = "test-trigger",
            Timestamp = DateTime.UtcNow,
            ConditionResult = "Test notification",
            SuggestedAction = "This is a test — no action needed",
            ActualValue = "test",
            ExpectedValue = "test"
        };

        var result = await channel.SendAsync(testPayload, config, ct);
        return Ok(result);
    }

    // ═══════════════════════════════════════════════════════════
    // Health
    // ═══════════════════════════════════════════════════════════

    [HttpGet("health")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ScheduledTasksHealthReport), 200)]
    public async Task<IActionResult> GetHealth(CancellationToken ct)
    {
        var tasks = await _taskManager.GetAllAsync(ct);
        var rules = await _triggerEngine.GetAllRulesAsync(ct);

        var channelHealths = new List<ChannelInfo>();
        foreach (var channel in _channels)
        {
            var healthy = await channel.IsHealthyAsync(ct);
            channelHealths.Add(new ChannelInfo { Name = channel.ChannelName, Healthy = healthy });
        }

        var report = new ScheduledTasksHealthReport
        {
            TotalTasks = tasks.Count,
            ActiveTasks = tasks.Count(t => t.Status == ScheduledTaskStatus.Active),
            PausedTasks = tasks.Count(t => t.Status == ScheduledTaskStatus.Paused),
            FailedTasks = tasks.Count(t => t.Status == ScheduledTaskStatus.Failed),
            TotalRules = rules.Count,
            EnabledRules = rules.Count(r => r.Enabled),
            Channels = channelHealths,
            OverallHealthy = tasks.All(t => t.Status != ScheduledTaskStatus.Failed) &&
                             channelHealths.All(c => c.Healthy)
        };

        return Ok(report);
    }
}

// ═══════════════════════════════════════════════════════════
// DTOs
// ═══════════════════════════════════════════════════════════

public class CreateTaskRequest
{
    public string Name { get; set; } = string.Empty;
    public string? CronExpression { get; set; }
    public int IntervalSeconds { get; set; }
    public int MaxRetryAttempts { get; set; } = 3;
    public TriggerRule? AssociatedRule { get; set; }
}

public class ChannelInfo
{
    public string Name { get; set; } = string.Empty;
    public bool Healthy { get; set; }
}

public class ScheduledTasksHealthReport
{
    public int TotalTasks { get; set; }
    public int ActiveTasks { get; set; }
    public int PausedTasks { get; set; }
    public int FailedTasks { get; set; }
    public int TotalRules { get; set; }
    public int EnabledRules { get; set; }
    public List<ChannelInfo> Channels { get; set; } = new();
    public bool OverallHealthy { get; set; }
}
