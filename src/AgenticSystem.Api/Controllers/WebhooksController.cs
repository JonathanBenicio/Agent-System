using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.Persistence;
using AgenticSystem.Infrastructure.Persistence.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace AgenticSystem.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WebhooksController : ControllerBase
{
    private readonly IDbContextFactory<AgenticDbContext> _dbContextFactory;
    private readonly IWorkflowEngine _workflowEngine;
    private readonly IDirectAgentRequestExecutor _agentExecutor;
    private readonly ILogger<WebhooksController> _logger;

    public WebhooksController(
        IDbContextFactory<AgenticDbContext> dbContextFactory,
        IWorkflowEngine workflowEngine,
        IDirectAgentRequestExecutor agentExecutor,
        ILogger<WebhooksController> logger)
    {
        _dbContextFactory = dbContextFactory;
        _workflowEngine = workflowEngine;
        _agentExecutor = agentExecutor;
        _logger = logger;
    }

    // ═══════════════════════════════════════════════════════════════
    // Admin Actions
    // ═══════════════════════════════════════════════════════════════

    [HttpGet("admin")]
    public async Task<IActionResult> ListAsync()
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        var webhooks = await db.InboundWebhooks.OrderByDescending(w => w.CreatedAt).ToListAsync();
        return Ok(webhooks);
    }

    [HttpPost("admin")]
    public async Task<IActionResult> CreateAsync([FromBody] InboundWebhookEntity webhook)
    {
        if (string.IsNullOrEmpty(webhook.Name)) return BadRequest("Name is required");
        
        webhook.Id = Guid.NewGuid().ToString("n")[..12];
        webhook.Secret = Guid.NewGuid().ToString("n");
        webhook.CreatedAt = DateTime.UtcNow;

        await using var db = await _dbContextFactory.CreateDbContextAsync();
        db.InboundWebhooks.Add(webhook);
        await db.SaveChangesAsync();

        return Ok(webhook);
    }

    [HttpDelete("admin/{id}")]
    public async Task<IActionResult> DeleteAsync(string id)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        var webhook = await db.InboundWebhooks.FindAsync(id);
        if (webhook == null) return NotFound();

        db.InboundWebhooks.Remove(webhook);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ═══════════════════════════════════════════════════════════════
    // Public Receiver
    // ═══════════════════════════════════════════════════════════════

    [HttpPost("receive/{id}")]
    public async Task<IActionResult> ReceiveAsync(string id, [FromBody] JsonElement payload, [FromHeader(Name = "X-Webhook-Secret")] string? secret)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        var webhook = await db.InboundWebhooks.FindAsync(id);

        if (webhook == null || !webhook.IsActive)
        {
            _logger.LogWarning("⚠️ Received webhook for unknown or inactive ID: {Id}", id);
            return NotFound();
        }

        // Optional Secret Verification
        if (!string.IsNullOrEmpty(webhook.Secret) && webhook.Secret != secret)
        {
            _logger.LogWarning("🚫 Invalid secret for webhook: {Id}", id);
            return Unauthorized();
        }

        _logger.LogInformation("📬 Processing inbound webhook: {Name} ({Id})", webhook.Name, id);

        webhook.LastTriggeredAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var payloadString = payload.GetRawText();
        var variables = new Dictionary<string, object>
        {
            ["webhook_id"] = id,
            ["webhook_name"] = webhook.Name,
            ["payload"] = payloadString,
            ["timestamp"] = DateTime.UtcNow
        };

        // Trigger Workflow
        if (!string.IsNullOrEmpty(webhook.TargetWorkflowId))
        {
            _logger.LogInformation("⚙️ Triggering workflow {WorkflowId} from webhook {Id}", webhook.TargetWorkflowId, id);
            // In a real scenario, we might want to parse the payload and pass it as variables
            await _workflowEngine.ResumeAsync(webhook.TargetWorkflowId, variables);
        }

        // Trigger Agent
        if (!string.IsNullOrEmpty(webhook.TargetAgentName))
        {
            _logger.LogInformation("🤖 Triggering agent {AgentName} from webhook {Id}", webhook.TargetAgentName, id);
            var prompt = $"Received webhook '{webhook.Name}' with payload: {payloadString}. Please process this according to your instructions.";
            var context = new UserContext { UserId = "webhook_trigger" };
            _ = Task.Run(() => _agentExecutor.ExecuteAsync(Guid.NewGuid().ToString(), prompt, context, webhook.TargetAgentName));
        }

        return Ok(new { message = "Webhook received and processing started", timestamp = DateTime.UtcNow });
    }
}
