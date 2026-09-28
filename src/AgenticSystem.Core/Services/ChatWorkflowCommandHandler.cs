using Microsoft.Extensions.Logging;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Services;

/// <summary>
/// Handles workflow commands issued via the conversational chat interface.
/// Extracted from MetaAgentOrchestrator to reduce its complexity.
/// </summary>
public class ChatWorkflowCommandHandler : IChatWorkflowCommandHandler
{
    private readonly IWorkflowEngine? _workflowEngine;
    private readonly IWorkflowStore? _workflowStore;
    private readonly ILogger<ChatWorkflowCommandHandler> _logger;

    public ChatWorkflowCommandHandler(
        ILogger<ChatWorkflowCommandHandler> logger,
        IWorkflowEngine? workflowEngine = null,
        IWorkflowStore? workflowStore = null)
    {
        _logger = logger;
        _workflowEngine = workflowEngine;
        _workflowStore = workflowStore;
    }

    public async Task<AgentResponse?> TryHandleAsync(string input, UserContext context, CancellationToken ct = default)
    {
        if (_workflowEngine == null || _workflowStore == null)
            return null;

        var inputTrimmed = input.Trim();
        var inputLower = inputTrimmed.ToLowerInvariant();
        var tenantId = context.TenantId;

        // 1. Start Workflow
        if (inputLower.StartsWith("iniciar workflow ") || inputLower.StartsWith("executar workflow ") || inputLower.StartsWith("rodar workflow "))
        {
            return await HandleStartWorkflowAsync(inputTrimmed, tenantId, context.UserId, ct);
        }

        // 2. Cancel Workflow
        if (inputLower.StartsWith("cancelar workflow ") || inputLower.StartsWith("parar workflow ") || inputLower.StartsWith("abortar workflow "))
        {
            return await HandleCancelWorkflowAsync(inputTrimmed, tenantId, ct);
        }

        // 3. List Workflows
        if (inputLower == "listar workflows" || inputLower == "status dos workflows" || inputLower == "workflows ativos")
        {
            return await HandleListWorkflowsAsync(tenantId, ct);
        }

        return null;
    }

    private async Task<AgentResponse> HandleStartWorkflowAsync(string inputTrimmed, string tenantId, string userId, CancellationToken ct)
    {
        var workflowId = inputTrimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries).Last();
        var definition = await _workflowStore!.GetDefinitionAsync(tenantId, workflowId, ct);
        if (definition == null)
        {
            return AgentResponse.Error($"❌ Workflow com ID '{workflowId}' não encontrado para o tenant atual.", "WorkflowEngine");
        }

        var execution = await _workflowEngine!.StartAsync(tenantId, definition, initiatedBy: userId, ct: ct);
        return AgentResponse.Ok(
            $"🚀 Workflow **{definition.Name}** iniciado com sucesso!\n\n" +
            $"* **ID da Execução:** `{execution.Id}`\n" +
            $"* **Status:** `{execution.Status}`\n" +
            $"* **Iniciado por:** `{execution.InitiatedBy}`\n\n" +
            $"O progresso detalhado de cada etapa está sendo transmitido em tempo real pelo SignalR.",
            "WorkflowEngine",
            AgentTier.Support);
    }

    private async Task<AgentResponse> HandleCancelWorkflowAsync(string inputTrimmed, string tenantId, CancellationToken ct)
    {
        var executionId = inputTrimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries).Last();
        var execution = await _workflowEngine!.GetExecutionAsync(tenantId, executionId, ct);
        if (execution == null)
        {
            return AgentResponse.Error($"❌ Execução de workflow com ID '{executionId}' não encontrada.", "WorkflowEngine");
        }

        var cancelled = await _workflowEngine.CancelAsync(tenantId, executionId, "Cancelado via chat conversacional pelo usuário.", ct);
        return AgentResponse.Ok(
            $"⏹️ Workflow **{cancelled.WorkflowName}** (Execução `{cancelled.Id}`) foi cancelado com sucesso!\n\n" +
            $"* **Status Atual:** `{cancelled.Status}`\n" +
            $"* **Motivo:** {cancelled.ErrorMessage}\n" +
            $"* **Encerrado em:** {cancelled.CompletedAt?.ToString("g")}",
            "WorkflowEngine",
            AgentTier.Support);
    }

    private async Task<AgentResponse> HandleListWorkflowsAsync(string tenantId, CancellationToken ct)
    {
        var executions = await _workflowEngine!.ListExecutionsAsync(tenantId, limit: 10, ct: ct);
        if (executions.Count == 0)
        {
            return AgentResponse.Ok("📋 Nenhum workflow recente foi executado neste tenant.", "WorkflowEngine", AgentTier.Support);
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("📋 **Histórico Recente de Execuções de Workflows:**\n");
        foreach (var exec in executions)
        {
            var duration = exec.CompletedAt.HasValue ? $" (Duração: {(exec.CompletedAt.Value - exec.StartedAt).TotalSeconds:F1}s)" : "";
            sb.AppendLine($"* **ID:** `{exec.Id}` | **Workflow:** {exec.WorkflowName} | **Status:** `{exec.Status}`{duration}");
        }

        return AgentResponse.Ok(sb.ToString(), "WorkflowEngine", AgentTier.Support);
    }
}
