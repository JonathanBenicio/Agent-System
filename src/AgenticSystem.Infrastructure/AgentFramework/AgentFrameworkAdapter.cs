using System.Text;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using FrameworkAgent = Microsoft.Agents.AI.AIAgent;
using FrameworkAgentResponse = Microsoft.Agents.AI.AgentResponse;
using FrameworkAgentSession = Microsoft.Agents.AI.AgentSession;

namespace AgenticSystem.Infrastructure.AgentFramework;

/// <summary>
/// Adapter que envolve um IAgent existente com o Microsoft Agent Framework.
/// Mantém compatibilidade com a interface IAgent enquanto delega execução
/// ao ChatClientAgent do Agent Framework (com pipeline de logging + telemetry).
/// </summary>
public class AgentFrameworkAdapter : IAgent
{
    private readonly IAgent _innerAgent;
    private readonly FrameworkAgent _frameworkAgent;
    private readonly AgentSessionBridge _sessionBridge;
    private readonly ILogger<AgentFrameworkAdapter> _logger;
    private readonly IAgentRuntimeCoordinator? _runtimeCoordinator;
    private readonly bool _enableStreaming;

    public AgentFrameworkAdapter(
        IAgent innerAgent,
        FrameworkAgent frameworkAgent,
        AgentSessionBridge sessionBridge,
        ILogger<AgentFrameworkAdapter> logger,
        IAgentRuntimeCoordinator? runtimeCoordinator = null,
        bool enableStreaming = false)
    {
        _innerAgent = innerAgent ?? throw new ArgumentNullException(nameof(innerAgent));
        _frameworkAgent = frameworkAgent ?? throw new ArgumentNullException(nameof(frameworkAgent));
        _sessionBridge = sessionBridge ?? throw new ArgumentNullException(nameof(sessionBridge));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _runtimeCoordinator = runtimeCoordinator;
        _enableStreaming = enableStreaming;
    }

    public string Name => _innerAgent.Name;
    public string Description => _innerAgent.Description;
    public AgentTier Tier => _innerAgent.Tier;
    public string Domain => _innerAgent.Domain;
    public DateTime CreatedAt => _innerAgent.CreatedAt;
    public DateTime LastUsedAt => _innerAgent.LastUsedAt;
    public bool IsActive => _innerAgent.IsActive;
    public IEnumerable<string> AvailableTools => _innerAgent.AvailableTools;
    public string Instructions => _innerAgent.Instructions;

    /// <summary>
    /// Executa via Agent Framework pipeline (ChatClientAgent → logging → telemetry → IChatClient).
    /// Reutiliza AgentSession via SessionBridge para manter contexto de conversa.
    /// </summary>
    /// <remarks>
    /// [Fase 3] Com o FrameworkOrchestratorService como ponto central de orquestração,
    /// especialistas são chamados via tool bindings — não mais via ExecuteAsync.
    /// Este método permanece apenas para chamada direta a agentes nomeados (ExecuteDirectAsync).
    /// Prefira usar IFrameworkOrchestratorService.ExecuteAsync para fluxos orquestrados.
    /// </remarks>
    [Obsolete("Especialistas devem ser chamados via tool bindings do FrameworkOrchestratorService. " +
              "Este método será removido na Fase 4. Use IFrameworkOrchestratorService.ExecuteAsync.", error: false)]
    public async Task<AgentResponse> ExecuteAsync(string input, UserContext context)
    {
        UpdateLastUsed();

        try
        {
            var sessionId = ResolveSessionId(context);
            var session = await _sessionBridge.GetOrCreateFrameworkSessionAsync(_frameworkAgent, sessionId);

            string content;
            FrameworkAgentResponse? frameworkResponse = null;

            if (_enableStreaming)
            {
                var sb = new StringBuilder();
                await foreach (var update in _frameworkAgent.RunStreamingAsync(input, session))
                {
                    if (!string.IsNullOrEmpty(update.Text))
                    {
                        sb.Append(update.Text);
                        if (_runtimeCoordinator is not null)
                        {
                            await _runtimeCoordinator.PublishEventAsync(new AgentStreamEvent
                            {
                                Type = AgentStreamEventType.Token,
                                AgentName = Name,
                                Message = update.Text
                            });
                        }
                    }

                }

                content = sb.ToString();
            }
            else
            {
                frameworkResponse = await _frameworkAgent.RunAsync(input, session);

                content = string.Join("\n", frameworkResponse.Messages
                    .Where(m => m.Role == ChatRole.Assistant)
                    .SelectMany(m => m.Contents.OfType<TextContent>())
                    .Select(t => t.Text));

                if (string.IsNullOrWhiteSpace(content))
                {
                    content = string.Join("\n", frameworkResponse.Messages
                        .Where(m => m.Role == ChatRole.Assistant)
                        .Select(m => m.Text));
                }
            }

            var result = new AgentResponse
            {
                Content = content ?? string.Empty,
                AgentName = Name,
                AgentTier = Tier,
                Success = true,
                SessionId = sessionId,
                Metadata = new Dictionary<string, object>
                {
                    ["frameworkAgentId"] = _frameworkAgent.Id,
                    ["frameworkStreaming"] = _enableStreaming
                }
            };

            await _sessionBridge.SyncResponseAsync(sessionId, Name, input, result);
            await _sessionBridge.PersistFrameworkSessionAsync(sessionId, _frameworkAgent, session);

            return result;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            _logger.LogWarning(ex, "Agent Framework execution failed for {Agent}, falling back to inner agent", Name);
            if (_runtimeCoordinator is not null)
            {
                await _runtimeCoordinator.PublishEventAsync(new AgentStreamEvent
                {
                    Type = AgentStreamEventType.Error,
                    AgentName = Name,
                    Message = ex.Message,
                    Data = new Dictionary<string, object>
                    {
                        ["fallback"] = true,
                        ["frameworkAgentId"] = _frameworkAgent.Id
                    }
                });
            }
            var fallbackResponse = await _innerAgent.ExecuteAsync(input, context);
            fallbackResponse.Metadata ??= new Dictionary<string, object>();
            fallbackResponse.Metadata["frameworkFallback"] = true;
            fallbackResponse.Metadata["frameworkError"] = ex.Message;
            return fallbackResponse;
        }
    }

    public Task<bool> CanHandleAsync(AnalysisResult analysis)
        => _innerAgent.CanHandleAsync(analysis);

    public void UpdateLastUsed()
        => _innerAgent.UpdateLastUsed();

    private static string ResolveSessionId(UserContext context)
    {
        if (context.Preferences.TryGetValue("sessionId", out var raw) && raw is not null)
        {
            var preferred = raw.ToString();
            if (!string.IsNullOrWhiteSpace(preferred))
                return preferred;
        }

        if (!string.IsNullOrWhiteSpace(context.UserId))
            return context.UserId;

        return Guid.NewGuid().ToString();
    }
}
