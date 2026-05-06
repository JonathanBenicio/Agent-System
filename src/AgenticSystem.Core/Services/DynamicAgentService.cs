using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Services;

/// <summary>
/// ML11 — Criação dinâmica de agents via linguagem natural.
/// Usa LLM para extrair especificação de agent a partir de texto livre.
/// </summary>
public class DynamicAgentService : IDynamicAgentService
{
    private readonly IAgentFactory _agentFactory;
    private readonly IChatClient _chatClient;
    private readonly ILogger<DynamicAgentService> _logger;

    private static readonly string[] CreationKeywords =
    [
        "crie um agente", "criar agente", "cria um agent", "create agent",
        "quero um assistente", "novo agente", "new agent", "make an agent",
        "crie um assistente", "criar um bot", "adicionar agent"
    ];

    public DynamicAgentService(
        IAgentFactory agentFactory,
        IChatClient chatClient,
        ILogger<DynamicAgentService> logger)
    {
        _agentFactory = agentFactory;
        _chatClient = chatClient;
        _logger = logger;
    }

    public Task<bool> IsAgentCreationRequestAsync(string input, AnalysisResult analysis)
    {
        if (analysis.Intent == IntentType.CreateAgent)
            return Task.FromResult(true);

        var lower = input.ToLowerInvariant();
        var hasKeyword = CreationKeywords.Any(k => lower.Contains(k));
        return Task.FromResult(hasKeyword);
    }

    public async Task<AgentSpecification> GenerateSpecificationAsync(string input, UserContext context)
    {
        var prompt = BuildSpecPrompt(input, context);
        var response = await _chatClient.GetResponseAsync(
            [
                new ChatMessage(ChatRole.System, "You are an agent specification generator. Return ONLY valid JSON."),
                new ChatMessage(ChatRole.User, prompt)
            ],
            new ChatOptions
            {
                Temperature = 0.2f,
                MaxOutputTokens = 2000
            });

        if (string.IsNullOrWhiteSpace(response.Text))
        {
            _logger.LogWarning("LLM failed to generate agent spec, using fallback");
            return BuildFallbackSpec(input);
        }

        try
        {
            var spec = ParseSpecFromLLM(response.Text, input);
            _logger.LogInformation("📋 Generated spec for agent: {Name} (domain: {Domain})", spec.Name, spec.Domain);
            return spec;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse LLM spec, using fallback");
            return BuildFallbackSpec(input);
        }
    }

    public async Task<AgentResponse> HandleAgentCreationAsync(string input, UserContext context)
    {
        _logger.LogInformation("🏗️ Processing dynamic agent creation request");

        var spec = await GenerateSpecificationAsync(input, context);

        // Validate spec
        if (string.IsNullOrWhiteSpace(spec.Name) || spec.Name.Length < 2)
        {
            return AgentResponse.Error("Could not determine a valid agent name from your request.", "DynamicAgentService");
        }

        var agent = await _agentFactory.CreateCustomAgentAsync(spec);

        var content = $"✅ Agent **{agent.Name}** criado com sucesso!\n\n" +
                      $"- **Descrição**: {spec.Description}\n" +
                      $"- **Domínio**: {spec.Domain}\n" +
                      $"- **Tier**: {spec.Tier}\n" +
                      $"- **Tools**: {string.Join(", ", spec.AllowedTools.DefaultIfEmpty("nenhuma"))}\n\n" +
                      $"O agent já está disponível e será usado automaticamente para requisições do domínio '{spec.Domain}'.";

        return AgentResponse.Ok(content, "DynamicAgentService", AgentTier.Chief);
    }

    public async Task<IEnumerable<AgentInfo>> GetDynamicAgentsAsync(string? userId = null)
    {
        var all = await _agentFactory.GetAllAgentsAsync();
        // Dynamic agents don't match built-in names
        var builtInNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "PersonalAgent", "WorkAgent", "LearningAgent", "GeneralAgent",
            "CreativeAgent", "CalendarAgent", "AnalysisAgent",
            "NotificationAgent", "APIAgent"
        };

        return all.Where(a => !builtInNames.Contains(a.Name));
    }

    public async Task<bool> RemoveAgentAsync(string agentName)
    {
        return await _agentFactory.RemoveAgentAsync(agentName);
    }

    private static string BuildSpecPrompt(string input, UserContext context)
    {
        return $@"
The user wants to create a custom AI agent. Extract the specification from their request.

USER REQUEST: {input}
USER CONTEXT: Role={context.Role}, Language={context.Language}

Return ONLY a valid JSON object:
{{
  ""name"": ""AgentNameInPascalCase"",
  ""description"": ""Brief description of what the agent does"",
  ""domain"": ""the primary domain (e.g. finance, health, legal, devops, marketing)"",
  ""tier"": ""Specialist"",
  ""allowedTools"": [""tool1"", ""tool2""],
  ""instructions"": ""System prompt instructions for this agent describing its behavior and expertise""
}}

RULES:
- Name must be PascalCase, end with 'Agent' (e.g. FinanceAgent, LegalAgent)
- Domain should be a single word
- Tier should be Support, Specialist, or Master based on complexity
- Instructions should be detailed enough for the agent to function
- AllowedTools can be empty if no specific tools are needed
";
    }

    private AgentSpecification ParseSpecFromLLM(string llmContent, string originalInput)
    {
        // Extract JSON from response
        var jsonStart = llmContent.IndexOf('{');
        var jsonEnd = llmContent.LastIndexOf('}');
        if (jsonStart < 0 || jsonEnd < 0 || jsonEnd <= jsonStart)
            throw new InvalidOperationException("No valid JSON in LLM response");

        var json = llmContent[jsonStart..(jsonEnd + 1)];
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var name = root.GetProperty("name").GetString() ?? "CustomAgent";
        if (!name.EndsWith("Agent", StringComparison.OrdinalIgnoreCase))
            name += "Agent";

        var tierStr = root.TryGetProperty("tier", out var tierEl) ? tierEl.GetString() : "Specialist";
        var tier = Enum.TryParse<AgentTier>(tierStr, true, out var parsedTier) ? parsedTier : AgentTier.Specialist;

        var tools = new List<string>();
        if (root.TryGetProperty("allowedTools", out var toolsEl) && toolsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var t in toolsEl.EnumerateArray())
            {
                var val = t.GetString();
                if (!string.IsNullOrWhiteSpace(val))
                    tools.Add(val);
            }
        }

        return new AgentSpecification
        {
            Name = name,
            Description = root.TryGetProperty("description", out var descEl) ? descEl.GetString() ?? "" : "",
            Domain = root.TryGetProperty("domain", out var domEl) ? domEl.GetString() ?? "general" : "general",
            Tier = tier,
            AllowedTools = tools,
            Instructions = root.TryGetProperty("instructions", out var instrEl) ? instrEl.GetString() ?? "" : ""
        };
    }

    private static AgentSpecification BuildFallbackSpec(string input)
    {
        // Extract a reasonable name from the input
        var lower = input.ToLowerInvariant();
        var domain = "general";

        var domainKeywords = new Dictionary<string, string>
        {
            ["financ"] = "finance", ["finanç"] = "finance", ["dinheiro"] = "finance", ["money"] = "finance",
            ["saúde"] = "health", ["health"] = "health", ["médic"] = "health",
            ["legal"] = "legal", ["jurídic"] = "legal", ["law"] = "legal",
            ["devops"] = "devops", ["deploy"] = "devops", ["infra"] = "devops",
            ["market"] = "marketing", ["vendas"] = "sales", ["sales"] = "sales"
        };

        foreach (var (keyword, dom) in domainKeywords)
        {
            if (lower.Contains(keyword))
            {
                domain = dom;
                break;
            }
        }

        var name = char.ToUpperInvariant(domain[0]) + domain[1..] + "Agent";

        return new AgentSpecification
        {
            Name = name,
            Description = $"Custom agent for {domain} domain",
            Domain = domain,
            Tier = AgentTier.Specialist,
            AllowedTools = [],
            Instructions = $"You are a specialized {domain} assistant. Help the user with {domain}-related tasks."
        };
    }
}
