using System;
using System.Collections.Generic;
using System.Linq;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Services;

/// <summary>
/// Executa a validação lógica e topológica em definições de workflows declarativos,
/// garantindo a ausência de loops infinitos (ciclos direcionados) e a integridade do grafo.
/// </summary>
public static class WorkflowGraphValidator
{
    public static void Validate(WorkflowDefinition workflow)
    {
        ArgumentNullException.ThrowIfNull(workflow);

        if (workflow.Steps == null || workflow.Steps.Count == 0)
        {
            throw new ArgumentException("O workflow deve conter pelo menos uma etapa (Step).");
        }

        var stepsMap = workflow.Steps.ToDictionary(s => s.Id);

        // 1. Validar se todas as arestas referenciam etapas válidas e existentes
        if (workflow.Edges != null)
        {
            foreach (var edge in workflow.Edges)
            {
                if (string.IsNullOrWhiteSpace(edge.FromStepId) || string.IsNullOrWhiteSpace(edge.ToStepId))
                {
                    throw new ArgumentException("Arestas do workflow não podem conter identificadores vazios.");
                }

                if (!stepsMap.ContainsKey(edge.FromStepId))
                {
                    throw new ArgumentException($"Aresta inválida: Etapa de origem '{edge.FromStepId}' não encontrada no workflow.");
                }

                if (!stepsMap.ContainsKey(edge.ToStepId))
                {
                    throw new ArgumentException($"Aresta inválida: Etapa de destino '{edge.ToStepId}' não encontrada no workflow.");
                }

                if (edge.FromStepId == edge.ToStepId)
                {
                    throw new ArgumentException($"Aresta circular inválida: Etapa '{edge.FromStepId}' não pode referenciar a si mesma.");
                }
            }
        }

        // 2. Detecção de Ciclos utilizando DFS (com coloração de nós: 0=Não visitado, 1=Visitando, 2=Visitado completo)
        var visitStates = new Dictionary<string, int>(); // StepId -> Estado (0, 1, 2)
        foreach (var step in workflow.Steps)
        {
            visitStates[step.Id] = 0;
        }

        // Construir lista de adjacência a partir das arestas
        var adjacencyList = workflow.Steps.ToDictionary(s => s.Id, _ => new List<string>());
        if (workflow.Edges != null)
        {
            foreach (var edge in workflow.Edges)
            {
                adjacencyList[edge.FromStepId].Add(edge.ToStepId);
            }
        }

        // Rodar DFS para cada nó não visitado
        foreach (var step in workflow.Steps)
        {
            if (visitStates[step.Id] == 0)
            {
                if (HasCycleDfs(step.Id, adjacencyList, visitStates, out var cyclePath))
                {
                    var cycleStr = string.Join(" -> ", cyclePath);
                    throw new InvalidOperationException($"Ciclo detectado no grafo de execução do workflow: {cycleStr}. A orquestração deve ser um Grafo Direcionado Acíclico (DAG).");
                }
            }
        }
    }

    private static bool HasCycleDfs(
        string currentId,
        Dictionary<string, List<string>> adjList,
        Dictionary<string, int> visitStates,
        out List<string> cyclePath)
    {
        cyclePath = new List<string> { currentId };
        
        // 1 = Visitando (nó ativo na pilha de recursão corrente)
        visitStates[currentId] = 1;

        if (adjList.TryGetValue(currentId, out var neighbors))
        {
            foreach (var neighbor in neighbors)
            {
                if (visitStates[neighbor] == 1)
                {
                    // Ciclo detectado! Adiciona o nó causador e retorna
                    cyclePath.Add(neighbor);
                    return true;
                }
                else if (visitStates[neighbor] == 0)
                {
                    if (HasCycleDfs(neighbor, adjList, visitStates, out var subPath))
                    {
                        cyclePath.AddRange(subPath);
                        return true;
                    }
                }
            }
        }

        // 2 = Visitado completo
        visitStates[currentId] = 2;
        cyclePath.Clear();
        return false;
    }
}
