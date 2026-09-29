#pragma warning disable MAAI001 // Required experimental MAF session-store integration; reviewed under issue #120.

using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using System.Text;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.AI;
using AgenticSystem.Infrastructure.MCP;
using AgenticSystem.Infrastructure.LLM;
using FrameworkAgent = Microsoft.Agents.AI.AIAgent;
using FrameworkAgentSession = Microsoft.Agents.AI.AgentSession;

namespace AgenticSystem.Infrastructure.AgentFramework;

/// <summary>
/// Factory que cria ChatClientAgent do Microsoft Agent Framework
/// a partir de definições de agent existentes (IAgent).
/// Cada ChatClientAgent usa o IChatClient pipeline (OpenAI + M.E.AI) do DI.
/// </summary>
public class AgentFrameworkFactory
{
    private readonly IChatClient _chatClient;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IServiceProvider _serviceProvider;
    private readonly ISkillManager? _skillManager;
    private readonly UnifiedAIToolProvider? _toolProvider;
    private readonly McpToolsAIFunctionAdapter? _mcpToolsAdapter;
    private readonly AgentSessionStore? _sessionStore;
    private readonly RAGContextProvider? _ragContextProvider;
    private readonly AgentSkillsProvider? _skillsProvider;

    // Exposed for OrchestratorContextFactory to create the orchestrator ChatClientAgent
    internal IChatClient ChatClient => _chatClient;
    internal ILoggerFactory LoggerFactory => _loggerFactory;
    internal IServiceProvider ServiceProvider => _serviceProvider;

    public AgentFrameworkFactory(
        IChatClient chatClient,
        ILoggerFactory loggerFactory,
        IServiceProvider serviceProvider,
        ISkillManager? skillManager = null,
        UnifiedAIToolProvider? toolProvider = null,
        McpToolsAIFunctionAdapter? mcpToolsAdapter = null,
        AgentSessionStore? sessionStore = null,
        RAGContextProvider? ragContextProvider = null,
        AgentSkillsProvider? skillsProvider = null)
    {
        _chatClient = chatClient;
        _loggerFactory = loggerFactory;
        _serviceProvider = serviceProvider;
        _skillManager = skillManager;
        _toolProvider = toolProvider;
        _mcpToolsAdapter = mcpToolsAdapter;
        _sessionStore = sessionStore;
        _ragContextProvider = ragContextProvider;
        _skillsProvider = skillsProvider;
    }

    /// <summary>
    /// Cria um ChatClientAgent com pipeline (logging + telemetry) a partir de um IAgent existente.
    /// Nota: Usa construtor posicional para suportar Instructions (system prompt rico).
    /// ChatHistoryProvider não é setado aqui pois a reutilização de AgentSession
    /// é controlada pelo SimpleSessionStoreAdapter quando o agent roda.
    /// </summary>
    public async Task<FrameworkAgent> CreateFromAgentAsync(IAgent agent, CancellationToken ct = default)
         => await CreateFromAgentAsync(agent, additionalTools: null, modelOverride: null, ct);

    public async Task<FrameworkAgent> CreateFromAgentAsync(
        IAgent agent,
        IEnumerable<AITool>? additionalTools,
        CancellationToken ct = default)
         => await CreateFromAgentAsync(agent, additionalTools, modelOverride: null, ct);

    public async Task<FrameworkAgent> CreateFromAgentAsync(
        IAgent agent,
        IEnumerable<AITool>? additionalTools,
        string? modelOverride,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(agent);

        var tools = await GetUnifiedToolsAsync(ct);
        tools = MergeTools(tools, additionalTools);

        // Enriquecer as instruções do especialista usando as C# Skills!
        var enrichedInstructions = _skillManager != null
            ? await _skillManager.BuildEnrichedPromptAsync(agent.Name, agent.Domain, agent.Instructions)
            : agent.Instructions;

        var effectiveClient = !string.IsNullOrWhiteSpace(modelOverride)
            ? new ModelIdOverridingChatClient(_chatClient, modelOverride)
            : _chatClient;

        var chatAgent = new ChatClientAgent(
            effectiveClient,
            enrichedInstructions, // instructions (system prompt rico com skills)
            agent.Name,          // name
            agent.Description,   // description
            tools,               // tools — MCP tools via adapter
            _loggerFactory,
            _serviceProvider);

        var builder = chatAgent.AsBuilder();
        var contextProviders = new List<MessageAIContextProvider>();
        if (_ragContextProvider is not null)
        {
            contextProviders.Add(_ragContextProvider);
        }
        if (_skillsProvider is not null)
        {
            contextProviders.Add(_skillsProvider);
        }
        if (contextProviders.Count > 0)
        {
            builder = builder.UseAIContextProviders(contextProviders.ToArray());
        }

        return builder
            .UseLogging(_loggerFactory)
            .UseOpenTelemetry("AgenticSystem.Agents")
            .Build(_serviceProvider);
    }

    /// <summary>
    /// Cria um ChatClientAgent a partir de uma AgentSpecification (agents dinâmicos).
    /// </summary>
    public async Task<FrameworkAgent> CreateFromSpecificationAsync(AgentSpecification spec, CancellationToken ct = default)
         => await CreateFromSpecificationAsync(spec, modelOverride: null, ct);

    /// <summary>
    /// Cria um ChatClientAgent a partir de uma AgentSpecification com suporte a override de modelo.
    /// </summary>
    public async Task<FrameworkAgent> CreateFromSpecificationAsync(
        AgentSpecification spec,
        string? modelOverride,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(spec);

        var tools = await GetUnifiedToolsAsync(ct);

        // Enriquecer as instruções da especificação dinâmica usando as C# Skills!
        var enrichedInstructions = _skillManager != null
            ? await _skillManager.BuildEnrichedPromptAsync(spec.Name, spec.Domain ?? "general", spec.Instructions)
            : spec.Instructions;

        var effectiveClient = !string.IsNullOrWhiteSpace(modelOverride)
            ? new ModelIdOverridingChatClient(_chatClient, modelOverride)
            : _chatClient;

        var chatAgent = new ChatClientAgent(
            effectiveClient,
            enrichedInstructions,   // instructions (system prompt rico com skills)
            spec.Name,           // name
            spec.Description,    // description
            tools,               // tools — MCP tools via adapter
            _loggerFactory,
            _serviceProvider);

        var builder = chatAgent.AsBuilder();
        var contextProviders = new List<MessageAIContextProvider>();
        if (_ragContextProvider is not null)
        {
            contextProviders.Add(_ragContextProvider);
        }
        if (_skillsProvider is not null)
        {
            contextProviders.Add(_skillsProvider);
        }
        if (contextProviders.Count > 0)
        {
            builder = builder.UseAIContextProviders(contextProviders.ToArray());
        }

        return builder
            .UseLogging(_loggerFactory)
            .UseOpenTelemetry("AgenticSystem.Agents")
            .Build(_serviceProvider);
    }



    public async Task<AgentToolBinding?> CreateToolBindingAsync(IAgent agent, string sessionId, CancellationToken ct = default)
    {
        if (_sessionStore is null)
        {
            return null;
        }

        var frameworkAgent = await CreateFromAgentAsync(agent, ct);
        var sessionKey = await CreateSessionStoreKeyAsync(sessionId, ct);
        var session = await _sessionStore.GetOrCreateSessionAsync(frameworkAgent, sessionKey, ct);
        var tool = frameworkAgent.AsAIFunction(
            new AIFunctionFactoryOptions
            {
                Name = BuildAgentToolName(agent.Name),
                Description = agent.Description
            },
            session);

        return new AgentToolBinding(agent, frameworkAgent, session, tool);
    }

    public async Task<FrameworkAgentSession> GetOrCreateSessionAsync(FrameworkAgent agent, string sessionId, CancellationToken ct = default)
    {
        if (_sessionStore is null)
        {
            throw new InvalidOperationException("AgentSessionStore is not available.");
        }

        var sessionKey = await CreateSessionStoreKeyAsync(sessionId, ct);
        return await _sessionStore.GetOrCreateSessionAsync(agent, sessionKey, ct);
    }

    public async Task PersistSessionAsync(string sessionId, FrameworkAgent agent, FrameworkAgentSession session, CancellationToken ct = default)
    {
        if (_sessionStore is null)
        {
            return;
        }

        var sessionKey = await CreateSessionStoreKeyAsync(sessionId, ct);
        await _sessionStore.SaveSessionAsync(agent, sessionKey, session, ct);
    }

    internal async ValueTask<AgentSessionStoreKey> CreateSessionStoreKeyAsync(string sessionId, CancellationToken ct)
    {
        var key = new AgentSessionStoreKey(sessionId);
        var isolationProvider = _serviceProvider.GetService<AgentIsolationKeyProvider>();
        if (isolationProvider is null)
            return key;

        var isolationKey = await isolationProvider.GetIsolationKeyAsync(ct);
        if (string.IsNullOrWhiteSpace(isolationKey))
            throw new InvalidOperationException("Authenticated tenant/user isolation is required for MAF session access.");

        return key.WithPartition("isolation", isolationKey);
    }

    private async Task<IList<AITool>?> GetUnifiedToolsAsync(CancellationToken ct)
    {
        if (_toolProvider is not null)
        {
            var unified = await _toolProvider.GetToolsAsync(ct);
            if (unified.Count > 0)
                return unified.ToList();
        }

        if (_mcpToolsAdapter is not null)
        {
            var mcpTools = _mcpToolsAdapter.GetAvailableTools();
            if (mcpTools.Count > 0)
                return mcpTools.ToList();
        }

        return null;
    }

    private static IList<AITool>? MergeTools(IList<AITool>? baseTools, IEnumerable<AITool>? additionalTools)
    {
        if (additionalTools is null)
        {
            return baseTools;
        }

        var merged = baseTools?.ToList() ?? [];
        var names = merged.Select(tool => tool.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var tool in additionalTools)
        {
            if (names.Add(tool.Name))
            {
                merged.Add(tool);
            }
        }

        return merged.Count == 0 ? null : merged;
    }

    private static string BuildAgentToolName(string agentName)
    {
        if (string.IsNullOrWhiteSpace(agentName))
        {
            return "agent_tool";
        }

        var builder = new StringBuilder(agentName.Length);
        foreach (var ch in agentName)
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(char.ToLowerInvariant(ch));
            }
            else if (builder.Length == 0 || builder[^1] != '_')
            {
                builder.Append('_');
            }
        }

        var sanitized = builder.ToString().Trim('_');
        return string.IsNullOrWhiteSpace(sanitized) ? "agent_tool" : sanitized;
    }
}

public sealed record AgentToolBinding(
    IAgent Agent,
    FrameworkAgent FrameworkAgent,
    FrameworkAgentSession Session,
    AITool Tool);
