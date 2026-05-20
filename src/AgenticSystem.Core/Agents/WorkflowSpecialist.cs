using Microsoft.Extensions.Logging;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Agents;

/// <summary>
/// Especialista em criação de workflows e automações via chat.
/// Capaz de gerar definições de fluxo estruturadas para o sistema.
/// </summary>
public class WorkflowSpecialist : BaseAgent
{
    public WorkflowSpecialist(
        ISkillManager skillManager,
        ILogger<WorkflowSpecialist> logger,
        IAgentMemoryService? agentMemoryService = null)
        : base(skillManager, logger, agentMemoryService) { }

    public override string Name => "WorkflowSpecialist";
    public override string Description => "Especialista em automação, criação de fluxos de trabalho e orquestração de tarefas.";
    public override AgentTier Tier => AgentTier.Specialist;
    public override string Domain => "workflow";
    public override IEnumerable<string> AvailableTools => new[] { "workflow", "api", "calendar" };

    protected override string GetBaseSystemPrompt() =>
        """
        Você é um Arquiteto de Automação. Sua especialidade é transformar pedidos em linguagem natural em workflows estruturados.
        
        Sempre que o usuário pedir para "criar um fluxo", "automatizar algo" ou "gerar um workflow", você deve:
        1. Planejar as etapas necessárias.
        2. Gerar uma definição de workflow no formato JSON seguindo a estrutura do sistema.
        3. O JSON deve estar dentro de um bloco de código markdown com a linguagem 'json-workflow'.
        
        Estrutura esperada do WorkflowDefinition:
        {
          "name": "Nome do Fluxo",
          "description": "Descrição do que o fluxo faz",
          "steps": [
            {
              "id": "step1",
              "name": "Nome do Passo",
              "stepType": "Action", // Action, Decision, Parallel, Wait, Approval
              "agentName": "NomeDoAgent", // Opcional
              "toolName": "NomeDaTool", // Opcional
              "actionDescription": "O que este passo faz",
              "input": { "key": "value" },
              "dependsOn": [] // IDs de passos anteriores
            }
          ]
        }
        
        Exemplo de pedido: "Crie um fluxo que leia um PDF e me avise no Slack se houver pendências."
        Resposta esperada: Propor o plano e incluir o bloco de código com o JSON.
        """;
}
