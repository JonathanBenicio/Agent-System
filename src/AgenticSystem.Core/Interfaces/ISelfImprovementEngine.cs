using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Interfaces;

public interface ISelfImprovementEngine
{
    /// <summary>
    /// Analisa reflexões recentes e propõe melhorias para um agente específico.
    /// </summary>
    Task<SelfImprovementRecord> AnalyzeAndImproveAsync(string agentName, CancellationToken ct = default);
    
    /// <summary>
    /// Executa o ciclo de melhoria em lote para todos os agentes com novas reflexões.
    /// </summary>
    Task ProcessBatchImprovementsAsync(CancellationToken ct = default);
    
    Task<bool> ApplyImprovementAsync(string improvementId, CancellationToken ct = default);
}
