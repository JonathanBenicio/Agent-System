using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Agents.AI.DurableTask.Workflows;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;
using AgenticSystem.Core.Skills;
using AgenticSystem.Infrastructure.AgentFramework;

namespace AgenticSystem.Infrastructure.AI;

/// <summary>
/// Compilador declarativo de workflows duráveis do Microsoft Agent Framework (MAF).
/// Mapeia o Grafo de Passos do banco para orquestrações e atividades do DurableTask com PostgreSQL.
/// </summary>
public class DurableWorkflowCompiler : IDynamicWorkflowCompiler
{
    private readonly IWorkflowStore _workflowStore;
    private readonly IAgentFactory _agentFactory;
    private readonly AgentFrameworkFactory _frameworkFactory;
    private readonly BannerProductionSkills _bannerSkills;
    private readonly IWorkflowClient _workflowClient;
    private readonly DurableTaskClient _durableClient;
    private readonly ILogger<DurableWorkflowCompiler> _logger;

    public DurableWorkflowCompiler(
        IWorkflowStore workflowStore,
        IAgentFactory agentFactory,
        AgentFrameworkFactory frameworkFactory,
        BannerProductionSkills bannerSkills,
        IWorkflowClient workflowClient,
        DurableTaskClient durableClient,
        ILogger<DurableWorkflowCompiler> logger)
    {
        _workflowStore = workflowStore ?? throw new ArgumentNullException(nameof(workflowStore));
        _agentFactory = agentFactory ?? throw new ArgumentNullException(nameof(agentFactory));
        _frameworkFactory = frameworkFactory ?? throw new ArgumentNullException(nameof(frameworkFactory));
        _bannerSkills = bannerSkills ?? throw new ArgumentNullException(nameof(bannerSkills));
        _workflowClient = workflowClient ?? throw new ArgumentNullException(nameof(workflowClient));
        _durableClient = durableClient ?? throw new ArgumentNullException(nameof(durableClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<string> ExecuteDynamicWorkflowAsync(
        string workflowDefinitionId,
        string tenantId,
        Dictionary<string, object> parameters,
        CancellationToken ct = default)
    {
        _logger.LogInformation("🛠️ [DurableTask] Compilando workflow MAF durável '{WorkflowId}' para o Tenant '{TenantId}'", workflowDefinitionId, tenantId);

        // 1. Carregar a definição do banco de dados
        var definition = await _workflowStore.GetDefinitionAsync(tenantId, workflowDefinitionId, ct);
        if (definition == null)
        {
            throw new InvalidOperationException($"Definição de workflow '{workflowDefinitionId}' não encontrada para o Tenant '{tenantId}'.");
        }

        // 2. Executar a validação lógica e topológica do grafo
        WorkflowGraphValidator.Validate(definition);

        // 3. Resolver os subagentes declarados no grafo
        var compiledAgents = new Dictionary<string, Microsoft.Agents.AI.AIAgent>();
        foreach (var step in definition.Steps.Where(s => s.StepType == WorkflowStepType.Agent))
        {
            if (string.IsNullOrWhiteSpace(step.AgentName))
            {
                throw new InvalidOperationException($"O passo '{step.Name}' ({step.Id}) do tipo Agent deve especificar uma propriedade 'AgentName' válida.");
            }

            _logger.LogDebug("👤 [DurableTask] Resolvendo agente '{AgentName}' para a etapa '{StepName}'", step.AgentName, step.Name);
            var coreAgent = await _agentFactory.ResolveAgentAsync(new AgentInfo { Name = step.AgentName });
            if (coreAgent == null)
            {
                throw new InvalidOperationException($"Agente especialista '{step.AgentName}' requerido pelo workflow não pôde ser resolvido pelo catálogo.");
            }

            // 4. Resolver e mapear ferramentas (AIFunctions) autorizadas para esta etapa
            var additionalTools = new List<AITool>();
            var allowedToolNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (coreAgent.AvailableTools != null)
            {
                foreach (var tool in coreAgent.AvailableTools) allowedToolNames.Add(tool);
            }
            if (step.AllowedToolsOverride != null)
            {
                foreach (var tool in step.AllowedToolsOverride) allowedToolNames.Add(tool);
            }

            // Mapear as ferramentas locais C# de Banner se estiverem na lista de autorizadas
            if (allowedToolNames.Contains("CleanImageAsync"))
            {
                var cleanFunc = AIFunctionFactory.Create(
                    (string path, string items) => _bannerSkills.CleanImageAsync(path, items),
                    "CleanImageAsync",
                    "Remove fios, postes e lixo de uma imagem"
                );
                additionalTools.Add(cleanFunc);
            }
            if (allowedToolNames.Contains("RenderBannerAsync"))
            {
                var renderFunc = AIFunctionFactory.Create(
                    (string path, decimal prc, int beds) => _bannerSkills.RenderBannerAsync(path, prc, beds),
                    "RenderBannerAsync",
                    "Gera o banner publicitário final desenhando preços e quartos sobre a imagem"
                );
                additionalTools.Add(renderFunc);
            }

            // Compilar e materializar o agente nativo do MAF com suas ferramentas dedicadas
            var frameworkAgent = await _frameworkFactory.CreateFromAgentAsync(coreAgent, additionalTools, ct);
            compiledAgents[step.Id] = frameworkAgent;
        }

        if (compiledAgents.Count == 0)
        {
            throw new InvalidOperationException("O workflow em grafo do MAF deve conter pelo menos um nó do tipo 'Agent' para execução.");
        }

        // 5. Construir a topologia do grafo do MAF via WorkflowBuilder
        var hasIncomingEdge = new HashSet<string>();
        if (definition.Edges != null)
        {
            foreach (var edge in definition.Edges)
            {
                hasIncomingEdge.Add(edge.ToStepId);
            }
        }

        var startNodeId = definition.Steps
            .Where(s => s.StepType == WorkflowStepType.Agent)
            .Select(s => s.Id)
            .FirstOrDefault(id => !hasIncomingEdge.Contains(id)) ?? compiledAgents.Keys.First();

        _logger.LogInformation("🕸️ [DurableTask] Construindo grafo do MAF a partir do nó inicial: '{StartNodeId}'", startNodeId);
        var builder = new WorkflowBuilder(compiledAgents[startNodeId]);

        // Vincular todos os outros agentes executores no builder do MAF
        foreach (var kvp in compiledAgents)
        {
            if (kvp.Key != startNodeId)
            {
                builder.BindExecutor(kvp.Value);
            }
        }

        // Adicionar as arestas (Edges) direcionadas
        if (definition.Edges != null)
        {
            foreach (var edge in definition.Edges)
            {
                if (compiledAgents.TryGetValue(edge.FromStepId, out var fromAgent) &&
                    compiledAgents.TryGetValue(edge.ToStepId, out var toAgent))
                {
                    _logger.LogDebug("🔗 [DurableTask] Adicionando transição de grafo: '{FromStepId}' ──> '{ToStepId}'", edge.FromStepId, edge.ToStepId);
                    builder.AddEdge(fromAgent, toAgent);
                }
            }
        }

        builder.WithName(definition.Id);
        var workflowGraph = builder.Build();

        // 6. Injetar Payload dinâmico de variáveis no prompt template
        var promptText = definition.PromptTemplate ?? "Inicie a tarefa.";
        foreach (var parameter in parameters)
        {
            promptText = promptText.Replace("{{" + parameter.Key + "}}", parameter.Value?.ToString() ?? string.Empty);
        }

        var initialUserMessage = new ChatMessage(ChatRole.User, promptText);

        // Se o payload contiver 'imagePath' e for um arquivo válido em disco, anexar como conteúdo multimodal
        if (parameters.TryGetValue("imagePath", out var imagePathObj) && imagePathObj is string imagePath && File.Exists(imagePath))
        {
            var imageBytes = await File.ReadAllBytesAsync(imagePath);
            var extension = Path.GetExtension(imagePath).TrimStart('.').ToLower();
            if (extension == "jpg") extension = "jpeg";
            
            _logger.LogDebug("📸 [DurableTask] Anexando arquivo de imagem multimodal '{ImagePath}' à mensagem do workflow.", imagePath);
            initialUserMessage.Contents.Add(new DataContent(imageBytes, $"image/{extension}"));
        }

        var messages = new List<ChatMessage> { initialUserMessage };

        // 7. Execução nativa DURÁVEL do MAF via IWorkflowClient
        // Multi-tenant isolated instance key prefixing
        var runId = $"{tenantId}:durable-run-{Guid.NewGuid().ToString("N")[..8]}";
        _logger.LogInformation("⚡ [DurableTask] Disparando execução durável do workflow MAF: '{RunId}'", runId);

        var run = await _workflowClient.RunAsync(workflowGraph, messages, runId, ct);
        
        // Aguarda a conclusão resiliente da orquestração durável usando DurableTaskClient
        await _durableClient.WaitForInstanceCompletionAsync(run.RunId, ct);

        // Recupera o estado final e a saída serializada da orquestração
        var instance = await _durableClient.GetInstanceAsync(run.RunId, ct);

        if (instance != null && !string.IsNullOrWhiteSpace(instance.SerializedOutput))
        {
            try
            {
                // Tenta desserializar as mensagens de retorno
                var responseMessages = JsonSerializer.Deserialize<List<ChatMessage>>(instance.SerializedOutput);
                var lastAssistantMessage = responseMessages?.LastOrDefault(m => m.Role == ChatRole.Assistant);
                if (lastAssistantMessage != null)
                {
                    return lastAssistantMessage.Text ?? "Sucesso (execução do workflow durável concluída sem conteúdo textual de saída).";
                }
            }
            catch
            {
                return instance.SerializedOutput;
            }
        }

        return "O processamento durável do MAF em grafo foi concluído, mas nenhuma mensagem de saída textual foi detectada.";
    }
}
