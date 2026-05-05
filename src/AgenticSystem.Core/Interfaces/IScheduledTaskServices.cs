using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Interfaces;

// ═══════════════════════════════════════════════════════════
// Maturity Level 21 — Scheduled Tasks & Trigger Engine
// ═══════════════════════════════════════════════════════════

/// <summary>
/// ML21 — Gerencia tarefas agendadas com expressões CRON ou intervalos TimeSpan.
/// Registra, pausa, retoma e remove tarefas do scheduler in-process.
/// </summary>
public interface IScheduledTaskManager
{
    Task<ScheduledTask> RegisterAsync(string name, string cronExpression, TriggerRule? rule = null, int maxRetryAttempts = 3, CancellationToken ct = default);
    Task<ScheduledTask> RegisterAsync(string name, TimeSpan interval, TriggerRule? rule = null, int maxRetryAttempts = 3, CancellationToken ct = default);
    Task LinkTasksAsync(string predecessorTaskId, string successorTaskId, CancellationToken ct = default);
    Task<ScheduledTask?> GetAsync(string taskId, CancellationToken ct = default);
    Task<IReadOnlyList<ScheduledTask>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ScheduledTask>> GetActiveAsync(CancellationToken ct = default);
    Task PauseAsync(string taskId, CancellationToken ct = default);
    Task ResumeAsync(string taskId, CancellationToken ct = default);
    Task RemoveAsync(string taskId, CancellationToken ct = default);
    Task<TaskExecution> ExecuteAsync(string taskId, CancellationToken ct = default);
}

/// <summary>
/// ML21 — Motor de regras condicionais: avalia fonte → condição → ação.
/// Suporta HTTP GET/POST → JSONPath/Threshold/Regex → DeliveryChannel.
/// </summary>
public interface ITriggerEngine
{
    Task<TriggerEvaluationResult> EvaluateAsync(TriggerRule rule, CancellationToken ct = default);
    Task<IReadOnlyList<TriggerEvaluationResult>> EvaluateAllAsync(CancellationToken ct = default);
    Task RegisterRuleAsync(TriggerRule rule, CancellationToken ct = default);
    Task<TriggerRule?> GetRuleAsync(string ruleId, CancellationToken ct = default);
    Task<IReadOnlyList<TriggerRule>> GetAllRulesAsync(CancellationToken ct = default);
    Task RemoveRuleAsync(string ruleId, CancellationToken ct = default);
    Task EnableRuleAsync(string ruleId, CancellationToken ct = default);
    Task DisableRuleAsync(string ruleId, CancellationToken ct = default);
}

/// <summary>
/// ML21 — Abstração de canal de entrega de notificações (webhook, email, SMS, push).
/// Cada implementação encapsula protocolo e retry.
/// </summary>
public interface IDeliveryChannel
{
    string ChannelName { get; }
    Task<DeliveryResult> SendAsync(TriggerNotificationPayload payload, Dictionary<string, string> config, CancellationToken ct = default);
    Task<bool> IsHealthyAsync(CancellationToken ct = default);
}

/// <summary>
/// ML21 — Store para persistência de tarefas agendadas e regras.
/// Implementação padrão: in-memory. PostgreSQL como extensão futura.
/// </summary>
public interface IScheduledTaskStore
{
    Task<ScheduledTask> SaveTaskAsync(ScheduledTask task, CancellationToken ct = default);
    Task<ScheduledTask?> GetTaskAsync(string taskId, CancellationToken ct = default);
    Task<IReadOnlyList<ScheduledTask>> GetAllTasksAsync(CancellationToken ct = default);
    Task DeleteTaskAsync(string taskId, CancellationToken ct = default);
    Task<TriggerRule> SaveRuleAsync(TriggerRule rule, CancellationToken ct = default);
    Task<TriggerRule?> GetRuleAsync(string ruleId, CancellationToken ct = default);
    Task<IReadOnlyList<TriggerRule>> GetAllRulesAsync(CancellationToken ct = default);
    Task DeleteRuleAsync(string ruleId, CancellationToken ct = default);
    Task<TaskExecution> SaveExecutionAsync(TaskExecution execution, CancellationToken ct = default);
}
