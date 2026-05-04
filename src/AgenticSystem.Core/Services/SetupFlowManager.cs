using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Services;

/// <summary>
/// ML15 — Setup Flow Manager: wizard conversacional de onboarding.
/// </summary>
public class SetupFlowManager : ISetupFlowManager
{
    private readonly IUserPreferenceEngine _preferenceEngine;
    private readonly ILogger<SetupFlowManager> _logger;
    private readonly ConcurrentDictionary<string, SetupFlowState> _activeFlows = new();

    private static readonly string[] SetupKeywords =
    [
        "setup", "configurar", "onboarding", "começar", "iniciar",
        "primeiro uso", "first time", "get started", "configure"
    ];

    public SetupFlowManager(
        IUserPreferenceEngine preferenceEngine,
        ILogger<SetupFlowManager> logger)
    {
        _preferenceEngine = preferenceEngine;
        _logger = logger;
    }

    public bool IsSetupRequest(string input, IntentType intent)
    {
        if (intent == IntentType.Setup) return true;
        var lower = input.ToLowerInvariant();
        return SetupKeywords.Any(k => lower.Contains(k));
    }

    public Task<bool> IsInSetupFlowAsync(string userId)
    {
        var inFlow = _activeFlows.TryGetValue(userId, out var state) && !state.IsComplete;
        return Task.FromResult(inFlow);
    }

    public Task<SetupFlowState?> GetSetupStateAsync(string userId)
    {
        _activeFlows.TryGetValue(userId, out var state);
        return Task.FromResult(state);
    }

    public Task<SetupFlowState> StartSetupAsync(string userId)
    {
        var state = new SetupFlowState
        {
            UserId = userId,
            CurrentStep = SetupStep.Welcome,
            StepNumber = 0,
            PromptMessage = BuildStepPrompt(SetupStep.Welcome)
        };

        _activeFlows[userId] = state;
        _logger.LogInformation("🚀 Setup flow started for {UserId}", userId);

        return Task.FromResult(state);
    }

    public async Task<SetupFlowState> ProcessStepResponseAsync(string userId, string response)
    {
        if (!_activeFlows.TryGetValue(userId, out var state))
        {
            state = await StartSetupAsync(userId);
        }

        // Process current step response
        ProcessCurrentStep(state, response);

        // Advance to next step
        state.CurrentStep = GetNextStep(state.CurrentStep);
        state.StepNumber++;
        state.PromptMessage = BuildStepPrompt(state.CurrentStep);

        if (state.CurrentStep == SetupStep.Complete)
        {
            state.IsComplete = true;
            await FinalizeSetupAsync(state);
        }

        return state;
    }

    private void ProcessCurrentStep(SetupFlowState state, string response)
    {
        switch (state.CurrentStep)
        {
            case SetupStep.Welcome:
                // No data to collect, just greeting
                break;

            case SetupStep.Identity:
                var parts = response.Split(',', StringSplitOptions.TrimEntries);
                state.CollectedData["name"] = parts.Length > 0 ? parts[0] : response;
                if (parts.Length > 1)
                    state.CollectedData["email"] = parts[1];
                break;

            case SetupStep.Preferences:
                state.CollectedData["style"] = ParsePreferenceStyle(response);
                state.CollectedData["language"] = ParseLanguage(response);
                break;

            case SetupStep.LLMProvider:
                state.CollectedData["provider"] = ParseProvider(response);
                break;

            case SetupStep.Domains:
                state.CollectedData["domains"] = response;
                break;
        }
    }

    private async Task FinalizeSetupAsync(SetupFlowState state)
    {
        var profile = await _preferenceEngine.GetOrCreateProfileAsync(
            state.UserId,
            state.CollectedData.GetValueOrDefault("name"));

        // Apply collected preferences
        if (state.CollectedData.TryGetValue("style", out var style) && Enum.TryParse<CommunicationStyle>(style, true, out var parsedStyle))
        {
            profile.ResponsePreferences.Style = parsedStyle;
        }

        if (state.CollectedData.TryGetValue("language", out var language))
        {
            profile.ResponsePreferences.PreferredLanguage = language;
        }

        await _preferenceEngine.UpdateProfileAsync(profile);

        _logger.LogInformation("✅ Setup complete for {UserId}: {Data}",
            state.UserId, string.Join(", ", state.CollectedData.Select(kv => $"{kv.Key}={kv.Value}")));
    }

    private static SetupStep GetNextStep(SetupStep current) => current switch
    {
        SetupStep.Welcome => SetupStep.Identity,
        SetupStep.Identity => SetupStep.Preferences,
        SetupStep.Preferences => SetupStep.LLMProvider,
        SetupStep.LLMProvider => SetupStep.Domains,
        SetupStep.Domains => SetupStep.Complete,
        _ => SetupStep.Complete
    };

    private static string BuildStepPrompt(SetupStep step) => step switch
    {
        SetupStep.Welcome =>
            """
            👋 **Bem-vindo ao Agentic System!**
            
            Vou te guiar pela configuração inicial em 5 passos rápidos.
            Responda cada pergunta e eu configuro tudo pra você.
            
            Vamos começar? (responda qualquer coisa para prosseguir)
            """,

        SetupStep.Identity =>
            """
            📋 **Passo 1/5 — Identidade**
            
            Qual seu nome e email?
            Formato: `Nome, email@empresa.com`
            """,

        SetupStep.Preferences =>
            """
            🎨 **Passo 2/5 — Preferências de Comunicação**
            
            Qual estilo de resposta você prefere?
            - **concise** — direto ao ponto
            - **detailed** — explicações completas
            - **technical** — linguagem técnica
            - **conversational** — tom informal
            
            E idioma? (pt-br, en, es)
            """,

        SetupStep.LLMProvider =>
            """
            🤖 **Passo 3/5 — Provider de IA**
            
            Qual provider de LLM quer usar como padrão?
            - **openai** — GPT-4o (recomendado)
            - **gemini** — Google Gemini 1.5 Pro
            - **claude** — Anthropic Claude 3.5
            - **ollama** — Local (sem custo)
            """,

        SetupStep.Domains =>
            """
            🧩 **Passo 4/5 — Domínios de Interesse**
            
            Quais áreas você mais trabalha? (separados por vírgula)
            Ex: `desenvolvimento, análise de dados, produtividade`
            """,

        SetupStep.Complete =>
            """
            ✅ **Setup completo!**
            
            Suas preferências foram salvas. O sistema já está personalizado para você.
            Pode começar a usar os agents normalmente. Diga "ajuda" para ver o que posso fazer.
            """,

        _ => "Passo desconhecido."
    };

    private static string ParsePreferenceStyle(string response)
    {
        var lower = response.ToLowerInvariant();
        if (lower.Contains("concis") || lower.Contains("direct")) return "Concise";
        if (lower.Contains("detail") || lower.Contains("complet")) return "Detailed";
        if (lower.Contains("técn") || lower.Contains("techni")) return "Technical";
        if (lower.Contains("conversa") || lower.Contains("informal")) return "Conversational";
        return "Concise";
    }

    private static string ParseLanguage(string response)
    {
        var lower = response.ToLowerInvariant();
        if (lower.Contains("en")) return "en";
        if (lower.Contains("es")) return "es";
        return "pt-br";
    }

    private static string ParseProvider(string response)
    {
        var lower = response.ToLowerInvariant();
        if (lower.Contains("gemini") || lower.Contains("google")) return "gemini";
        if (lower.Contains("claude") || lower.Contains("anthropic")) return "claude";
        if (lower.Contains("ollama") || lower.Contains("local")) return "ollama";
        return "openai";
    }
}
