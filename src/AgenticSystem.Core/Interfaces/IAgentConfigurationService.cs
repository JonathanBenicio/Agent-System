using System.Threading;
using System.Threading.Tasks;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Interfaces;

public record AgentConfigurationResult(bool Success, string Message, AgentInfo? Agent = null, AgentVersion? Version = null);

public interface IAgentConfigurationService
{
    Task<AgentConfigurationResult> SaveAgentFromYamlAsync(string yaml, string requestedBy = "System", CancellationToken ct = default);
    Task<AgentConfigurationResult> RollbackAgentAsync(string name, string versionId, string rolledBackBy = "System", CancellationToken ct = default);
}
