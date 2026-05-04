using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Interfaces;

/// <summary>
/// ML15 — Setup Flow: onboarding conversacional para novos usuários.
/// </summary>
public interface ISetupFlowManager
{
    /// <summary>
    /// Inicia o flow de onboarding para um novo usuário
    /// </summary>
    Task<SetupFlowState> StartSetupAsync(string userId);

    /// <summary>
    /// Processa a resposta do usuário ao step atual
    /// </summary>
    Task<SetupFlowState> ProcessStepResponseAsync(string userId, string response);

    /// <summary>
    /// Verifica se o usuário está no meio de um flow de setup
    /// </summary>
    Task<bool> IsInSetupFlowAsync(string userId);

    /// <summary>
    /// Obtém o estado atual do flow de setup
    /// </summary>
    Task<SetupFlowState?> GetSetupStateAsync(string userId);

    /// <summary>
    /// Detecta se input do usuário é um pedido de setup
    /// </summary>
    bool IsSetupRequest(string input, IntentType intent);
}

public class SetupFlowState
{
    public string UserId { get; set; } = string.Empty;
    public SetupStep CurrentStep { get; set; }
    public bool IsComplete { get; set; }
    public string PromptMessage { get; set; } = string.Empty;
    public Dictionary<string, string> CollectedData { get; set; } = [];
    public int StepNumber { get; set; }
    public int TotalSteps { get; set; } = 5;
}

public enum SetupStep
{
    Welcome,
    Identity,
    Preferences,
    LLMProvider,
    Domains,
    Complete
}
