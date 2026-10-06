using Microsoft.Agents.AI;
using System.Collections.Generic;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Infrastructure.AgentFramework;

public class OrchestratorContextState
{
    public AIAgent? OrchestratorAgent { get; set; }
    public IReadOnlyList<AgentInfo>? ActiveAgents { get; set; }
    public IReadOnlyList<AgentToolBinding>? SpecialistBindings { get; set; }
}
