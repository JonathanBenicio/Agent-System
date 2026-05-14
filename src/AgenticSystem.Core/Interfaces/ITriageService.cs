using System.Threading;
using System.Threading.Tasks;
using AgenticSystem.Core.Models.Triage;

namespace AgenticSystem.Core.Interfaces;

/// <summary>
/// Interface para o serviço de triagem semântica (Camada 1).
/// </summary>
public interface ITriageService
{
    /// <summary>
    /// Analisa a complexidade e intenção de uma requisição usando um modelo de baixo custo.
    /// </summary>
    Task<QueryTriageResult> AnalyzeComplexityAsync(string input, CancellationToken ct = default);
}
