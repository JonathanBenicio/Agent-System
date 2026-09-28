using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AgenticSystem.Core.Interfaces;

/// <summary>
/// Result of starting a dynamic workflow. Carries the RunId for async polling.
/// </summary>
public record WorkflowStartResult(
    string RunId,
    string Message,
    bool IsAsync = true);

/// <summary>
/// Interface para compilação e execução de workflows dinâmicos orientados a grafo do MAF.
/// Segue o padrão Async HTTP API: executa em background e retorna RunId para polling.
/// </summary>
public interface IDynamicWorkflowCompiler
{
    Task<WorkflowStartResult> ExecuteDynamicWorkflowAsync(
        string workflowDefinitionId,
        string tenantId,
        Dictionary<string, object> parameters,
        CancellationToken ct = default);
}
