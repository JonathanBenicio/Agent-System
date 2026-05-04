using System.Collections.Concurrent;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Core.Services;

public class UserPreferenceEngine : IUserPreferenceEngine
{
    private readonly ILogger<UserPreferenceEngine> _logger;
    private readonly ConcurrentDictionary<string, UserPreferenceProfile> _profiles = new();

    public UserPreferenceEngine(ILogger<UserPreferenceEngine> logger)
    {
        _logger = logger;
    }

    public Task<UserPreferenceProfile> GetOrCreateProfileAsync(string userId, string? displayName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var profile = _profiles.AddOrUpdate(userId,
            id =>
            {
                _logger.LogInformation("👤 Creating new user preference profile for {UserId}", id);
                return new UserPreferenceProfile
                {
                    UserId = id,
                    DisplayName = displayName ?? id
                };
            },
            (id, existing) =>
            {
                if (displayName != null && existing.DisplayName != displayName)
                    existing.DisplayName = displayName;
                return existing;
            });

        return Task.FromResult(profile);
    }

    public Task<UserPreferenceProfile> UpdateProfileAsync(UserPreferenceProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentException.ThrowIfNullOrWhiteSpace(profile.UserId);

        _profiles.AddOrUpdate(profile.UserId, profile, (_, _) => profile);
        _logger.LogDebug("👤 Updated profile for {UserId}", profile.UserId);
        return Task.FromResult(profile);
    }

    public Task<PersonalizationAdjustment> PersonalizePromptAsync(string userId, string prompt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        if (!_profiles.TryGetValue(userId, out var profile))
        {
            return Task.FromResult(new PersonalizationAdjustment
            {
                UserId = userId,
                OriginalPrompt = prompt,
                AdjustedPrompt = prompt,
                AppliedRiskLevel = RiskTolerance.Moderate
            });
        }

        var adjustedPrompt = prompt;
        var applied = new List<string>();

        // Apply communication style
        adjustedPrompt = ApplyStyleDirective(adjustedPrompt, profile.ResponsePreferences.Style);
        applied.Add($"style:{profile.ResponsePreferences.Style}");

        // Apply risk tolerance
        adjustedPrompt = ApplyRiskDirective(adjustedPrompt, profile.RiskTolerance);
        applied.Add($"risk:{profile.RiskTolerance}");

        // Apply language preference
        if (profile.ResponsePreferences.PreferredLanguage != "pt-br")
        {
            adjustedPrompt += $"\n[Respond in {profile.ResponsePreferences.PreferredLanguage}]";
            applied.Add($"language:{profile.ResponsePreferences.PreferredLanguage}");
        }

        // Apply code example preference
        if (!profile.ResponsePreferences.IncludeCodeExamples)
        {
            adjustedPrompt += "\n[Skip code examples unless explicitly asked]";
            applied.Add("no-code-examples");
        }

        // Apply max token hint
        if (profile.ResponsePreferences.MaxResponseTokens < 2000)
        {
            adjustedPrompt += $"\n[Keep response under {profile.ResponsePreferences.MaxResponseTokens} tokens]";
            applied.Add($"max-tokens:{profile.ResponsePreferences.MaxResponseTokens}");
        }

        // Recommend agent based on satisfaction history
        var recommendedAgent = GetTopSatisfactionAgent(profile);

        _logger.LogDebug("👤 Personalized prompt for {UserId}: {Applied}",
            userId, string.Join(", ", applied));

        return Task.FromResult(new PersonalizationAdjustment
        {
            UserId = userId,
            OriginalPrompt = prompt,
            AdjustedPrompt = adjustedPrompt,
            AppliedPreferences = applied,
            RecommendedAgent = recommendedAgent,
            AppliedRiskLevel = profile.RiskTolerance
        });
    }

    public Task RecordInteractionAsync(string userId, string agentName, double satisfactionScore)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(agentName);

        if (!_profiles.TryGetValue(userId, out var profile))
            return Task.CompletedTask;

        profile.TotalInteractions++;
        profile.LastActiveAt = DateTime.UtcNow;

        // Update rolling average satisfaction for the agent
        if (profile.AgentSatisfactionScores.TryGetValue(agentName, out var current))
        {
            // Exponential moving average (α = 0.3)
            profile.AgentSatisfactionScores[agentName] = current * 0.7 + satisfactionScore * 0.3;
        }
        else
        {
            profile.AgentSatisfactionScores[agentName] = satisfactionScore;
        }

        _logger.LogDebug("👤 Recorded interaction for {UserId} with {Agent}: satisfaction={Score:F2}",
            userId, agentName, satisfactionScore);

        return Task.CompletedTask;
    }

    public Task<string?> RecommendAgentAsync(string userId, AnalysisResult analysis)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentNullException.ThrowIfNull(analysis);

        if (!_profiles.TryGetValue(userId, out var profile))
            return Task.FromResult<string?>(null);

        // Check if user has preferred agents for this domain
        var preferredForDomain = profile.DomainExpertise
            .Where(kv => kv.Key.Equals(analysis.PrimaryDomain, StringComparison.OrdinalIgnoreCase))
            .Select(kv => kv.Value)
            .FirstOrDefault();

        if (preferredForDomain != null)
            return Task.FromResult<string?>(preferredForDomain);

        // Check preferred agents list
        if (profile.PreferredAgents.Count > 0)
            return Task.FromResult<string?>(profile.PreferredAgents[0]);

        // Recommend based on highest satisfaction score
        var topAgent = GetTopSatisfactionAgent(profile);
        return Task.FromResult(topAgent);
    }

    public Task DeactivateProfileAsync(string userId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        if (_profiles.TryGetValue(userId, out var profile))
        {
            profile.IsActive = false;
            _logger.LogInformation("👤 Deactivated profile for {UserId}", userId);
        }

        return Task.CompletedTask;
    }

    private static string ApplyStyleDirective(string prompt, CommunicationStyle style)
    {
        var directive = style switch
        {
            CommunicationStyle.Concise => "[Be concise and direct]",
            CommunicationStyle.Detailed => "[Provide detailed explanations]",
            CommunicationStyle.Technical => "[Use technical language and precision]",
            CommunicationStyle.Conversational => "[Use a conversational, friendly tone]",
            CommunicationStyle.Formal => "[Use formal, professional language]",
            _ => ""
        };

        return string.IsNullOrEmpty(directive) ? prompt : $"{directive}\n{prompt}";
    }

    private static string ApplyRiskDirective(string prompt, RiskTolerance risk)
    {
        var directive = risk switch
        {
            RiskTolerance.Conservative => "[Prefer safe, well-tested approaches. Flag any risks]",
            RiskTolerance.Moderate => "",
            RiskTolerance.Aggressive => "[Suggest cutting-edge approaches when beneficial]",
            _ => ""
        };

        return string.IsNullOrEmpty(directive) ? prompt : $"{prompt}\n{directive}";
    }

    private static string? GetTopSatisfactionAgent(UserPreferenceProfile profile)
    {
        if (profile.AgentSatisfactionScores.Count == 0)
            return null;

        return profile.AgentSatisfactionScores
            .OrderByDescending(kv => kv.Value)
            .First()
            .Key;
    }
}
