using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using System.Text;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.AI;
using AgenticSystem.Infrastructure.MCP;
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
    private readonly UnifiedAIToolProvider? _toolProvider;
    private readonly McpToolsAIFunctionAdapter? _mcpToolsAdapter;
    private readonly AgentFrameworkSessionStoreAdapter? _sessionStore;

    // Exposed for OrchestratorContextFactory to create the orchestrator ChatClientAgent
    internal IChatClient ChatClient => _chatClient;
    internal ILoggerFactory LoggerFactory => _loggerFactory;
    internal IServiceProvider ServiceProvider => _serviceProvider;

    public AgentFrameworkFactory(
        IChatClient chatClient,
        ILoggerFactory loggerFactory,
        IServiceProvider serviceProvider,
        UnifiedAIToolProvider? toolProvider = null,
        McpToolsAIFunctionAdapter? mcpToolsAdapter = null,
        AgentFrameworkSessionStoreAdapter? sessionStore = null)
    {
        _chatClient = chatClient;
        _loggerFactory = loggerFactory;
        _serviceProvider = serviceProvider;
        _toolProvider = toolProvider;
        _mcpToolsAdapter = mcpToolsAdapter;
        _sessionStore = sessionStore;
    }

    /// <summary>
    /// Cria um ChatClientAgent com pipeline (logging + telemetry) a partir de um IAgent existente.
    /// Nota: Usa construtor posicional para suportar Instructions (system prompt rico).
    /// ChatHistoryProvider não é setado aqui pois a reutilização de AgentSession
    /// é controlada pelo AgentFrameworkSessionStoreAdapter quando o agent roda.
    /// </summary>
    public async Task<FrameworkAgent> CreateFromAgentAsync(IAgent agent, CancellationToken ct = default)
        => await CreateFromAgentAsync(agent, additionalTools: null, ct);

    public async Task<FrameworkAgent> CreateFromAgentAsync(
        IAgent agent,
        IEnumerable<AITool>? additionalTools,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(agent);

        var tools = await GetUnifiedToolsAsync(ct);
        tools = MergeTools(tools, additionalTools);

        var chatAgent = new ChatClientAgent(
            _chatClient,
            agent.Instructions,  // instructions (system prompt rico)
            agent.Name,          // name
            agent.Description,   // description
            tools,               // tools — MCP tools via adapter
            _loggerFactory,
            _serviceProvider);

        return chatAgent.AsBuilder()
            .UseLogging(_loggerFactory)
            .UseOpenTelemetry("AgenticSystem.Agents")
            .Build(_serviceProvider);
    }

    /// <summary>
    /// Cria um ChatClientAgent a partir de uma AgentSpecification (agents dinâmicos).
    /// </summary>
    public async Task<FrameworkAgent> CreateFromSpecificationAsync(AgentSpecification spec, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(spec);

        var tools = await GetUnifiedToolsAsync(ct);

        var chatAgent = new ChatClientAgent(
            _chatClient,
            spec.Instructions,   // instructions
            spec.Name,           // name
            spec.Description,    // description
            tools,               // tools — MCP tools via adapter
            _loggerFactory,
            _serviceProvider);

        return chatAgent.AsBuilder()
            .UseLogging(_loggerFactory)
            .UseOpenTelemetry("AgenticSystem.Agents")
            .Build(_serviceProvider);
    }

    // Backward-compatible sync wrappers used by tests and older callers.
    public FrameworkAgent CreateFromAgent(IAgent agent)
        => CreateFromAgentAsync(agent).GetAwaiter().GetResult();

    public FrameworkAgent CreateFromSpecification(AgentSpecification spec)
        => CreateFromSpecificationAsync(spec).GetAwaiter().GetResult();

    public async Task<AgentToolBinding?> CreateToolBindingAsync(IAgent agent, string sessionId, CancellationToken ct = default)
    {
        if (_sessionStore is null)
        {
            return null;
        }

        var frameworkAgent = await CreateFromAgentAsync(agent, ct);
        var session = await _sessionStore.GetSessionAsync(frameworkAgent, sessionId, ct);
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
            throw new InvalidOperationException("AgentFrameworkSessionStoreAdapter is not available.");
        }

        return await _sessionStore.GetSessionAsync(agent, sessionId, ct);
    }

    public async Task PersistSessionAsync(string sessionId, FrameworkAgent agent, FrameworkAgentSession session, CancellationToken ct = default)
    {
        if (_sessionStore is null)
        {
            return;
        }

        await _sessionStore.SaveSessionAsync(agent, sessionId, session, ct);
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
