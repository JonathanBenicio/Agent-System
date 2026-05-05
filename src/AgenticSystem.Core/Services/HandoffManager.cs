using System.Collections.Concurrent;
using System.Text;
using Microsoft.Extensions.Logging;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Services;

/// <summary>
/// ML12 — Handoff Manager: orquestra delegação entre agents.
/// Suporta SingleDelegate, FanOut e Chain strategies.
/// </summary>
public class HandoffManager : IHandoffManager
{
    private readonly IAgentFactory _agentFactory;
    private readonly IAgentChannelService? _agentChannelService;
    private readonly IAgentRuntimeCoordinator? _runtimeCoordinator;
    private readonly ILogger<HandoffManager> _logger;
    private readonly ConcurrentDictionary<string, List<HandoffRecord>> _history = new();

    public HandoffManager(
        IAgentFactory agentFactory,
        IAgentChannelService? agentChannelService,
        IAgentRuntimeCoordinator? runtimeCoordinator,
        ILogger<HandoffManager> logger)
    {
        _agentFactory = agentFactory;
        _agentChannelService = agentChannelService;
        _runtimeCoordinator = runtimeCoordinator;
        _logger = logger;
    }

    public HandoffManager(
        IAgentFactory agentFactory,
        IAgentRuntimeCoordinator? runtimeCoordinator,
        ILogger<HandoffManager> logger)
        : this(agentFactory, null, runtimeCoordinator, logger)
    {
    }

    public HandoffManager(
        IAgentFactory agentFactory,
        ILogger<HandoffManager> logger)
        : this(agentFactory, null, null, logger)
    {
    }

    public async Task<HandoffDecision> EvaluateHandoffAsync(AnalysisResult analysis, IAgent currentAgent)
    {
        // No delegation needed if domains match and no secondary domains
        if (!analysis.RequiresDelegation && analysis.SecondaryDomains.Count == 0
            && currentAgent.Domain.Equals(analysis.PrimaryDomain, StringComparison.OrdinalIgnoreCase))
        {
            return new HandoffDecision { ShouldHandoff = false, Strategy = HandoffStrategy.None };
        }

        // Single domain mismatch → SingleDelegate
        if (!currentAgent.Domain.Equals(analysis.PrimaryDomain, StringComparison.OrdinalIgnoreCase)
            && analysis.SecondaryDomains.Count == 0)
        {
            return new HandoffDecision
            {
                ShouldHandoff = true,
                Strategy = HandoffStrategy.SingleDelegate,
                Reason = $"Current agent '{currentAgent.Name}' (domain: {currentAgent.Domain}) doesn't match primary domain '{analysis.PrimaryDomain}'",
                Targets =
                [
                    new HandoffTarget
                    {
                        Domain = analysis.PrimaryDomain,
                        AgentName = analysis.EstimatedAgent ?? "",
                        SubTask = "Full request",
                        Order = 1
                    }
                ]
            };
        }

        // Multiple domains → FanOut
        if (analysis.SecondaryDomains.Count > 0 && analysis.RequiresDelegation)
        {
            var targets = new List<HandoffTarget>
            {
                new()
                {
                    Domain = analysis.PrimaryDomain,
                    SubTask = $"Primary: {analysis.PrimaryDomain}",
                    Order = 1
                }
            };

            var order = 2;
            foreach (var domain in analysis.SecondaryDomains)
            {
                targets.Add(new HandoffTarget
                {
                    Domain = domain,
                    SubTask = $"Secondary: {domain}",
                    Order = order++
                });
            }

            return new HandoffDecision
            {
                ShouldHandoff = true,
                Strategy = HandoffStrategy.FanOut,
                Reason = $"Request spans {targets.Count} domains: {analysis.PrimaryDomain}, {string.Join(", ", analysis.SecondaryDomains)}",
                Targets = targets
            };
        }

        // Complex with delegation flag → Chain
        if (analysis.RequiresDelegation && analysis.Complexity == ComplexityLevel.RequiresPlanning)
        {
            return new HandoffDecision
            {
                ShouldHandoff = true,
                Strategy = HandoffStrategy.Chain,
                Reason = "Complex request requires planning and sequential agent execution",
                Targets =
                [
                    new HandoffTarget
                    {
                        Domain = analysis.PrimaryDomain,
                        SubTask = "Plan and execute",
                        Order = 1
                    }
                ]
            };
        }

        return new HandoffDecision { ShouldHandoff = false, Strategy = HandoffStrategy.None };
    }

    public async Task<AgentResponse> ExecuteHandoffAsync(string input, UserContext context, HandoffDecision decision)
    {
        _logger.LogInformation("🔄 Executing handoff: {Strategy} → {TargetCount} targets",
            decision.Strategy, decision.Targets.Count);

        if (_runtimeCoordinator is not null)
        {
            await _runtimeCoordinator.PublishEventAsync(new AgentStreamEvent
            {
                Type = AgentStreamEventType.HandoffStarted,
                Message = decision.Reason,
                Data = new Dictionary<string, object>
                {
                    ["strategy"] = decision.Strategy.ToString(),
                    ["targetCount"] = decision.Targets.Count
                }
            });
        }

        var response = decision.Strategy switch
        {
            HandoffStrategy.SingleDelegate => await ExecuteSingleDelegateAsync(input, context, decision),
            HandoffStrategy.FanOut => await ExecuteFanOutAsync(input, context, decision),
            HandoffStrategy.Chain => await ExecuteChainAsync(input, context, decision),
            _ => AgentResponse.Error("No handoff strategy matched", "HandoffManager")
        };

        if (_runtimeCoordinator is not null)
        {
            await _runtimeCoordinator.RecordArtifactAsync(new AgentExecutionArtifact
            {
                SessionId = _runtimeCoordinator.CurrentSessionId ?? string.Empty,
                Type = AgentExecutionArtifactType.Handoff,
                Name = decision.Strategy.ToString(),
                AgentName = _runtimeCoordinator.CurrentAgentName,
                Status = response.Success ? "Completed" : "Failed",
                Summary = decision.Reason,
                Data = new Dictionary<string, object>
                {
                    ["targets"] = decision.Targets.Select(target => target.AgentName).ToList(),
                    ["strategy"] = decision.Strategy.ToString()
                }
            });

            await _runtimeCoordinator.PublishEventAsync(new AgentStreamEvent
            {
                Type = AgentStreamEventType.HandoffCompleted,
                Message = response.Content,
                Data = new Dictionary<string, object>
                {
                    ["strategy"] = decision.Strategy.ToString(),
                    ["success"] = response.Success
                }
            });
        }

        return response;
    }

    public Task RecordHandoffAsync(string sessionId, HandoffRecord record)
    {
        record.SessionId = sessionId;
        _history.AddOrUpdate(
            sessionId,
            _ => [record],
            (_, list) => { list.Add(record); return list; });
        return Task.CompletedTask;
    }

    public Task<IEnumerable<HandoffRecord>> GetHandoffHistoryAsync(string sessionId)
    {
        _history.TryGetValue(sessionId, out var records);
        return Task.FromResult<IEnumerable<HandoffRecord>>(records ?? []);
    }

    private async Task<AgentResponse> ExecuteSingleDelegateAsync(string input, UserContext context, HandoffDecision decision)
    {
        var target = decision.Targets.First();
        var analysis = new AnalysisResult
        {
            PrimaryDomain = target.Domain,
            EstimatedAgent = target.AgentName,
            Intent = IntentType.Chat,
            Complexity = ComplexityLevel.Moderate
        };

        var agent = await _agentFactory.GetOrCreateAgentAsync(analysis);
        _logger.LogInformation("🔄 Single delegate → {Agent}", agent.Name);

        input = await BuildChannelAwareInputAsync(agent.Name, input, AgentChannelKind.Direct, decision.Reason);

        using var agentScope = _runtimeCoordinator?.BeginAgentScope(agent.Name, agent.AvailableTools);
        var response = await agent.ExecuteAsync(input, context);
        response.SuggestedHandoffs.Add(new HandoffSuggestion
        {
            TargetAgent = agent.Name,
            Reason = decision.Reason
        });

        return response;
    }

    private async Task<AgentResponse> ExecuteFanOutAsync(string input, UserContext context, HandoffDecision decision)
    {
        var results = new List<(string AgentName, AgentResponse Response)>();

        foreach (var target in decision.Targets.OrderBy(t => t.Order))
        {
            var analysis = new AnalysisResult
            {
                PrimaryDomain = target.Domain,
                Intent = IntentType.Chat,
                Complexity = ComplexityLevel.Moderate
            };

            var agent = await _agentFactory.GetOrCreateAgentAsync(analysis);
            _logger.LogInformation("🔄 FanOut [{Order}] → {Agent} (domain: {Domain})",
                target.Order, agent.Name, target.Domain);

            var fanOutInput = await BuildChannelAwareInputAsync(
                agent.Name,
                $"[Subtask]\n{target.SubTask}\n\n[Original Request]\n{input}",
                AgentChannelKind.FanOut,
                decision.Reason);

            using var agentScope = _runtimeCoordinator?.BeginAgentScope(agent.Name, agent.AvailableTools);
            var response = await agent.ExecuteAsync(fanOutInput, context);
            results.Add((agent.Name, response));
        }

        // Aggregate responses
        return AggregateResponses(results, decision);
    }

    private async Task<AgentResponse> ExecuteChainAsync(string input, UserContext context, HandoffDecision decision)
    {
        var chainInput = input;
        AgentResponse? lastResponse = null;

        foreach (var target in decision.Targets.OrderBy(t => t.Order))
        {
            var analysis = new AnalysisResult
            {
                PrimaryDomain = target.Domain,
                Intent = IntentType.Chat,
                Complexity = ComplexityLevel.Moderate
            };

            var agent = await _agentFactory.GetOrCreateAgentAsync(analysis);
            _logger.LogInformation("🔄 Chain [{Order}] → {Agent}", target.Order, agent.Name);

            chainInput = await BuildChannelAwareInputAsync(agent.Name, chainInput, AgentChannelKind.Chain, decision.Reason);

            using var agentScope = _runtimeCoordinator?.BeginAgentScope(agent.Name, agent.AvailableTools);
            lastResponse = await agent.ExecuteAsync(chainInput, context);

            // Feed output as input to next agent
            if (lastResponse.Success && !string.IsNullOrWhiteSpace(lastResponse.Content))
                chainInput = $"Previous agent ({agent.Name}) said:\n{lastResponse.Content}\n\nOriginal request: {input}";
        }

        return lastResponse ?? AgentResponse.Error("Chain produced no output", "HandoffManager");
    }

    private async Task<string> BuildChannelAwareInputAsync(
        string targetAgent,
        string input,
        AgentChannelKind kind,
        string? reason)
    {
        var sessionId = _runtimeCoordinator?.CurrentSessionId;
        if (_agentChannelService is null || string.IsNullOrWhiteSpace(sessionId))
        {
            return input;
        }

        await _agentChannelService.PublishAsync(
            sessionId,
            _runtimeCoordinator?.CurrentAgentName ?? "HandoffManager",
            targetAgent,
            string.IsNullOrWhiteSpace(reason) ? input : $"{reason}\n\n{input}",
            kind,
            new Dictionary<string, object>
            {
                ["source"] = "handoff-manager"
            });

        return await _agentChannelService.BuildChannelContextAsync(sessionId, targetAgent, input);
    }

    private static AgentResponse AggregateResponses(
        List<(string AgentName, AgentResponse Response)> results,
        HandoffDecision decision)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## Multi-Agent Response\n");

        foreach (var (agentName, response) in results)
        {
            sb.AppendLine($"### 🤖 {agentName}");
            sb.AppendLine(response.Success ? response.Content : $"❌ {response.Content}");
            sb.AppendLine();
        }

        var aggregated = AgentResponse.Ok(sb.ToString(), "HandoffManager", AgentTier.Chief);

        foreach (var (agentName, _) in results)
        {
            aggregated.SuggestedHandoffs.Add(new HandoffSuggestion
            {
                TargetAgent = agentName,
                Reason = decision.Reason
            });
        }

        return aggregated;
    }
}
