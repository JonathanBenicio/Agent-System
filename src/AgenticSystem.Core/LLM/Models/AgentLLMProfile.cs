namespace AgenticSystem.Core.LLM.Models;

public class AgentLLMProfile
{
    public string AgentName { get; set; } = string.Empty;
    public string PreferredModel { get; set; } = string.Empty;
    public string? PreferredProvider { get; set; }
    public LLMParameters DefaultParameters { get; set; } = new();
    public Dictionary<string, LLMParameters> TaskParameters { get; set; } = new();

    public LLMParameters GetParametersForTask(string taskType)
    {
        return TaskParameters.TryGetValue(taskType, out var taskParams)
            ? MergeParameters(DefaultParameters, taskParams)
            : DefaultParameters;
    }

    private static LLMParameters MergeParameters(LLMParameters defaults, LLMParameters overrides)
    {
        return new LLMParameters
        {
            Temperature = overrides.Temperature != 0.7 ? overrides.Temperature : defaults.Temperature,
            MaxTokens = overrides.MaxTokens != 2000 ? overrides.MaxTokens : defaults.MaxTokens,
            TopP = overrides.TopP != 1.0 ? overrides.TopP : defaults.TopP,
            FrequencyPenalty = overrides.FrequencyPenalty != 0.0 ? overrides.FrequencyPenalty : defaults.FrequencyPenalty,
            PresencePenalty = overrides.PresencePenalty != 0.0 ? overrides.PresencePenalty : defaults.PresencePenalty,
            Stop = overrides.Stop ?? defaults.Stop
        };
    }
}
