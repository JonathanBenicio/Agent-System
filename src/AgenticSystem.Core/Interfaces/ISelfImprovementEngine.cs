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

    Task<IReadOnlyList<SelfImprovementRecord>> GetProposalsAsync(CancellationToken ct = default);
    Task<bool> ApproveProposalAsync(string improvementId, string approvedBy, CancellationToken ct = default);
    Task<bool> RejectProposalAsync(string improvementId, string rejectedBy, CancellationToken ct = default);
    Task<bool> RollbackProposalAsync(string improvementId, string rolledBackBy, CancellationToken ct = default);
}

public interface ISelfImprovementProposalStore
{
    Task SaveAsync(SelfImprovementRecord proposal, CancellationToken ct = default);
    Task<SelfImprovementRecord?> GetAsync(string proposalId, CancellationToken ct = default);
    Task<IReadOnlyList<SelfImprovementRecord>> GetAllAsync(CancellationToken ct = default);
    Task UpdateAsync(SelfImprovementRecord proposal, CancellationToken ct = default);
}
