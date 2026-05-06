using System.Collections.Concurrent;
using System.Text;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Infrastructure.AgentFramework;

/// <summary>
/// Monta e cacheia as instruções do orquestrador com base nos especialistas ativos e ferramentas auxiliares.
/// </summary>
public class OrchestratorInstructionService(ILogger<OrchestratorInstructionService> logger)
{
    private readonly ConcurrentDictionary<string, string> _cache = new();
    private readonly ILogger<OrchestratorInstructionService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <summary>
    /// Retorna as instruções do orquestrador para o conjunto atual de especialistas ativos e ferramentas auxiliares.
    /// </summary>
    public string GetInstructions(
        IReadOnlyList<AgentInfo> activeAgents,
        IReadOnlyList<AITool> auxiliaryTools)
    {
        ArgumentNullException.ThrowIfNull(activeAgents);
        ArgumentNullException.ThrowIfNull(auxiliaryTools);

        var cacheKey = BuildCacheKey(activeAgents);
        return _cache.GetOrAdd(cacheKey, _ =>
        {
            _logger.LogInformation(
                "Orchestrator instructions built with {AgentCount} specialist tools + {AuxCount} auxiliary tools",
                activeAgents.Count,
                auxiliaryTools.Count);
            return BuildInstructions(activeAgents, auxiliaryTools);
        });
    }

    /// <summary>
    /// Invalida o cache de instruções quando o conjunto de especialistas muda.
    /// </summary>
    public void Invalidate()
    {
        _cache.Clear();
    }

    private static string BuildInstructions(
        IReadOnlyList<AgentInfo> activeAgents,
        IReadOnlyList<AITool> auxiliaryTools)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Você é o orquestrador central do sistema Baianinho-Labs.");
        sb.AppendLine("Sua responsabilidade é analisar a solicitação do usuário e delegar para o especialista mais adequado.");
        sb.AppendLine();
        sb.AppendLine("## Regras de Delegação");
        sb.AppendLine("1. Analise o domínio, intent e complexidade da solicitação.");
        sb.AppendLine("2. Chame o tool do especialista mais adequado passando o input original do usuário.");
        sb.AppendLine("3. Se a solicitação envolver múltiplos domínios, chame múltiplos especialistas e consolide as respostas.");
        sb.AppendLine("4. Se nenhum especialista for adequado, responda diretamente com uma mensagem informativa.");
        sb.AppendLine("5. Retorne a resposta do especialista ao usuário, sem adicionar comentários desnecessários.");
        sb.AppendLine("6. Sempre responda no mesmo idioma do usuário.");
        sb.AppendLine();

        if (auxiliaryTools.Count > 0)
        {
            sb.AppendLine("## Ferramentas Auxiliares");
            sb.AppendLine("Você tem acesso a ferramentas auxiliares que ajudam na tomada de decisão:");
            foreach (var tool in auxiliaryTools)
            {
                var description = (tool as AIFunction)?.Description ?? tool.Name;
                sb.AppendLine($"- **{tool.Name}**: {description}");
            }

            sb.AppendLine();
            sb.AppendLine("Use `retrieve_context` quando precisar de informações da base de conhecimento.");
            sb.AppendLine("Use `analyze_request` ou `route_to_best_agent` se tiver dúvida sobre qual especialista delegar.");
            sb.AppendLine();
        }

        sb.AppendLine("## Especialistas Disponíveis");

        foreach (var agent in activeAgents)
        {
            sb.AppendLine();
            sb.AppendLine($"### {agent.Name}");
            sb.AppendLine($"- **Domínio:** {agent.Domain}");
            sb.AppendLine($"- **Tier:** {agent.Tier}");
            sb.AppendLine($"- **Descrição:** {agent.Description}");
            if (agent.AvailableTools.Count > 0)
            {
                sb.AppendLine($"- **Tools:** {string.Join(", ", agent.AvailableTools)}");
            }
        }

        return sb.ToString();
    }

    private static string BuildCacheKey(IReadOnlyList<AgentInfo> activeAgents)
    {
        var names = activeAgents
            .OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
            .Select(a => a.Name);
        return string.Join("|", names);
    }
}