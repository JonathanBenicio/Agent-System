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
    private readonly ILogger<HandoffManager> _logger;
    private readonly ConcurrentDictionary<string, List<HandoffRecord>> _history = new();

    public HandoffManager(
        IAgentFactory agentFactory,
        ILogger<HandoffManager> logger)
    {
        _agentFactory = agentFactory;
        _logger = logger;
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

        return decision.Strategy switch
        {
            HandoffStrategy.SingleDelegate => await ExecuteSingleDelegateAsync(input, context, decision),
            HandoffStrategy.FanOut => await ExecuteFanOutAsync(input, context, decision),
            HandoffStrategy.Chain => await ExecuteChainAsync(input, context, decision),
            _ => AgentResponse.Error("No handoff strategy matched", "HandoffManager")
        };
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

        // Execute all targets (could be parallel, but sequential for simplicity)
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

            var response = await agent.ExecuteAsync(input, context);
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

            lastResponse = await agent.ExecuteAsync(chainInput, context);

            // Feed output as input to next agent
            if (lastResponse.Success && !string.IsNullOrWhiteSpace(lastResponse.Content))
                chainInput = $"Previous agent ({agent.Name}) said:\n{lastResponse.Content}\n\nOriginal request: {input}";
        }

        return lastResponse ?? AgentResponse.Error("Chain produced no output", "HandoffManager");
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
