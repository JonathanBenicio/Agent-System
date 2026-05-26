using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AgenticSystem.Core.Interfaces;

/// <summary>
/// Interface para compilação e execução de workflows dinâmicos orientados a grafo do MAF.
/// </summary>
public interface IDynamicWorkflowCompiler
{
    Task<string> ExecuteDynamicWorkflowAsync(
        string workflowDefinitionId,
        string tenantId,
        Dictionary<string, object> parameters,
        CancellationToken ct = default);
}
