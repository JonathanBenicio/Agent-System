using Microsoft.Extensions.Logging;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Services;

/// <summary>
/// Meta-Agent principal: analisa contexto, roteia para agents e gerencia sessões.
/// Inspirado no Tech Lead do Baianinho-Labs com capacidade de orquestração.
/// </summary>
public class MetaAgentOrchestrator : IMetaAgent
{
    private readonly IContextAnalyzer _contextAnalyzer;
    private readonly IAgentFactory _agentFactory;
    private readonly ISessionManager _sessionManager;
    private readonly IDynamicAgentService _dynamicAgentService;
    private readonly IHandoffManager _handoffManager;
    private readonly IToolAvailabilityGuard _toolGuard;
    private readonly IConfidenceScoreCalculator _confidenceCalculator;
    private readonly ILogger<MetaAgentOrchestrator> _logger;

    public MetaAgentOrchestrator(
        IContextAnalyzer contextAnalyzer,
        IAgentFactory agentFactory,
        ISessionManager sessionManager,
        IDynamicAgentService dynamicAgentService,
        IHandoffManager handoffManager,
        IToolAvailabilityGuard toolGuard,
        IConfidenceScoreCalculator confidenceCalculator,
        ILogger<MetaAgentOrchestrator> logger)
    {
        _contextAnalyzer = contextAnalyzer;
        _agentFactory = agentFactory;
        _sessionManager = sessionManager;
        _dynamicAgentService = dynamicAgentService;
        _handoffManager = handoffManager;
        _toolGuard = toolGuard;
        _confidenceCalculator = confidenceCalculator;
        _logger = logger;
    }

    public async Task<AgentResponse> ProcessRequestAsync(string input, UserContext context)
    {
        var sessionId = await _sessionManager.StartSessionAsync(context);
        
        try
        {
            _logger.LogInformation("🎯 Meta-Agent processando: {Input}", input.Substring(0, Math.Min(50, input.Length)));
            
            // 1. Context Analysis (conceito do Baianinho-Labs)
            var analysis = await _contextAnalyzer.AnalyzeAsync(input, context);
            _logger.LogDebug("📊 Análise: {Domain} | {Intent} | Tier {Tier}", 
                analysis.PrimaryDomain, analysis.Intent, analysis.RecommendedTier);
            
            // 2. Quality Gate Pre-execution (do Baianinho-Labs)
            var qualityCheck = await ValidateRequestAsync(input, analysis);
            if (!qualityCheck.IsValid)
            {
                return AgentResponse.Error(qualityCheck.Message, "MetaAgent");
            }

            // 2.5 ML20 — Tool Availability Guard
            var toolCheck = await _toolGuard.CheckAsync(analysis.RequiredTools, default);
            if (toolCheck.NoneAvailable)
            {
                _logger.LogWarning("🚫 Nenhuma tool requerida disponível: [{Tools}]",
                    string.Join(", ", toolCheck.MissingTools));

                var disclaimer = FormatToolUnavailableResponse(toolCheck);
                return new AgentResponse
                {
                    Success = false,
                    Content = disclaimer,
                    AgentName = "MetaAgent",
                    SessionId = sessionId,
                    Metadata = new Dictionary<string, object>
                    {
                        ["toolAvailability"] = toolCheck,
                        ["suggestions"] = toolCheck.Suggestions
                    }
                };
            }
            else if (!toolCheck.AllAvailable)
            {
                _logger.LogWarning("⚠️ Cobertura parcial de tools: {Coverage:P0} — ausentes: [{Missing}]",
                    toolCheck.CoverageRatio, string.Join(", ", toolCheck.MissingTools));
            }

            // 3. ML11 — Dynamic Agent Creation
            if (await _dynamicAgentService.IsAgentCreationRequestAsync(input, analysis))
            {
                _logger.LogInformation("🏗️ Detected agent creation request");
                var creationResponse = await _dynamicAgentService.HandleAgentCreationAsync(input, context);
                creationResponse.SessionId = sessionId;
                return creationResponse;
            }
            
            // 3. Agent Selection/Creation (Tier-based)
            var agent = await _agentFactory.GetOrCreateAgentAsync(analysis);
            _logger.LogInformation("🤖 Agent selecionado: {AgentName} (Tier {Tier})", agent.Name, agent.Tier);

            // 3.5 ML12 — Handoff evaluation
            var handoffDecision = await _handoffManager.EvaluateHandoffAsync(analysis, agent);
            AgentResponse response;

            if (handoffDecision.ShouldHandoff)
            {
                _logger.LogInformation("🔄 Handoff: {Strategy} → {Targets}",
                    handoffDecision.Strategy, string.Join(", ", handoffDecision.Targets.Select(t => t.Domain)));
                response = await _handoffManager.ExecuteHandoffAsync(input, context, handoffDecision);

                await _handoffManager.RecordHandoffAsync(sessionId, new HandoffRecord
                {
                    SourceAgent = agent.Name,
                    TargetAgent = string.Join(",", handoffDecision.Targets.Select(t => t.AgentName)),
                    Reason = handoffDecision.Reason,
                    Strategy = handoffDecision.Strategy,
                    Success = response.Success
                });
            }
            else
            {
                // 4. Execute with Session Tracking
                response = await agent.ExecuteAsync(input, context);
            }
            response.SessionId = sessionId;
            
            // 5. Memory Consolidation Event
            var agentEvent = new AgentEvent
            {
                SessionId = sessionId,
                AgentName = agent.Name,
                AgentTier = agent.Tier,
                UserInput = input,
                AgentResponse = response.Content,
                ActionsPerformed = response.ActionsPerformed,
                ToolsUsed = response.ToolsUsed,
                Context = new Dictionary<string, object>
                {
                    ["analysis"] = analysis,
                    ["user_context"] = context
                }
            };
            
            await _sessionManager.AddEventAsync(sessionId, agentEvent);
            
            // 6. Quality Gate Post-execution
            await ValidateResponseAsync(response);

            // 7. ML7+ML20 — Confidence Score com Tool Availability
            response.Confidence = _confidenceCalculator.Calculate(response, ragContext: null, reflections: null, toolAvailability: toolCheck);
            
            _logger.LogInformation("✅ Processamento concluído por {AgentName} (confidence: {Score})", 
                agent.Name, response.Confidence.Value);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erro no processamento: {Message}", ex.Message);
            return AgentResponse.Error($"Erro interno: {ex.Message}", "MetaAgent");
        }
    }
    
    public async Task<IEnumerable<AgentInfo>> GetActiveAgentsAsync()
    {
        return await _agentFactory.GetAllAgentsAsync();
    }
    
    public async Task CleanupInactiveAgentsAsync()
    {
        var inactiveThreshold = TimeSpan.FromHours(24);
        var cutoff = DateTime.UtcNow - inactiveThreshold;
        var totalCleaned = 0;

        foreach (var tier in new[] { AgentTier.Support, AgentTier.Specialist })
        {
            var agents = await _agentFactory.GetAgentsByTierAsync(tier);
            foreach (var agent in agents)
            {
                if (agent.LastUsedAt < cutoff)
                {
                    await _agentFactory.RemoveAgentAsync(agent.Name);
                    _logger.LogInformation("🧹 Agent removido por inatividade: {Agent} (Tier {Tier}, LastUsed: {LastUsed})",
                        agent.Name, agent.Tier, agent.LastUsedAt);
                    totalCleaned++;
                }
            }
        }

        _logger.LogInformation("🧹 Cleanup concluído: {Count} agents inativos removidos", totalCleaned);
    }
    
    private async Task<(bool IsValid, string Message)> ValidateRequestAsync(string input, AnalysisResult analysis)
    {
        // Quality gates básicos
        if (string.IsNullOrWhiteSpace(input))
            return (false, "Input não pode estar vazio");
            
        if (input.Length > 10000)
            return (false, "Input muito longo (máximo 10.000 caracteres)");
            
        if (analysis.Confidence < 0.3)
            return (false, "Não foi possível entender a solicitação. Tente ser mais específico.");
            
        return (true, string.Empty);
    }
    
    private async Task ValidateResponseAsync(AgentResponse response)
    {
        // Post-execution quality gates
        if (response.Success && string.IsNullOrEmpty(response.Content))
        {
            _logger.LogWarning("⚠️ Response vazia de agent {AgentName}", response.AgentName);
        }
        
        await Task.CompletedTask;
    }

    private static string FormatToolUnavailableResponse(ToolAvailabilityResult toolCheck)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("❌ Não é possível executar esta solicitação — ferramentas necessárias não estão disponíveis.");
        sb.AppendLine();
        sb.AppendLine($"**Tools ausentes:** {string.Join(", ", toolCheck.MissingTools)}");

        if (toolCheck.Suggestions.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("**Sugestões de extensões/MCPs:**");
            foreach (var suggestion in toolCheck.Suggestions.Where(s => s.RelevanceScore >= 0.5))
            {
                sb.AppendLine($"- `{suggestion.PackageName}` ({suggestion.Source}) — {suggestion.Description}");
                sb.AppendLine($"  Instalar: `{suggestion.InstallCommand}`");
            }
        }

        return sb.ToString();
    }

    public async Task<AgentResponse> ProcessDirectRequestAsync(string input, UserContext context, string targetAgent)
    {
        var sessionId = await _sessionManager.StartSessionAsync(context);

        try
        {
            _logger.LogInformation("🎯 Direct request to {Agent}: {Input}", targetAgent, input[..Math.Min(50, input.Length)]);

            var agents = await _agentFactory.GetAllAgentsAsync();
            var agent = agents.FirstOrDefault(a => a.Name.Equals(targetAgent, StringComparison.OrdinalIgnoreCase));

            if (agent == null)
            {
                _logger.LogWarning("⚠️ Agent '{Agent}' not found for direct request", targetAgent);
                return AgentResponse.Error($"Agent '{targetAgent}' não encontrado.", "MetaAgent");
            }

            var executableAgent = await _agentFactory.GetOrCreateAgentAsync(new AnalysisResult
            {
                PrimaryDomain = agent.Domain,
                Intent = IntentType.Chat,
                RecommendedTier = agent.Tier,
                RequiredTools = new List<string> { agent.Name }
            });

            var response = await executableAgent.ExecuteAsync(input, context);
            response.SessionId = sessionId;

            var agentEvent = new AgentEvent
            {
                SessionId = sessionId,
                AgentName = executableAgent.Name,
                AgentTier = executableAgent.Tier,
                UserInput = input,
                AgentResponse = response.Content,
                ActionsPerformed = response.ActionsPerformed,
                ToolsUsed = response.ToolsUsed,
                Context = new Dictionary<string, object>
                {
                    ["directRequest"] = true,
                    ["targetAgent"] = targetAgent,
                    ["user_context"] = context
                }
            };

            await _sessionManager.AddEventAsync(sessionId, agentEvent);

            response.Confidence = _confidenceCalculator.Calculate(response, ragContext: null, reflections: null, toolAvailability: null);

            _logger.LogInformation("✅ Direct request concluído por {AgentName}", executableAgent.Name);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erro em direct request para {Agent}: {Message}", targetAgent, ex.Message);
            return AgentResponse.Error($"Erro ao processar requisição direta: {ex.Message}", "MetaAgent");
        }
    }
}