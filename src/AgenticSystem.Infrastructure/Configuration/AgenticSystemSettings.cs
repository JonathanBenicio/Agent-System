namespace AgenticSystem.Infrastructure.Configuration;

public class AgenticSystemSettings
{
    public OpenAISettings OpenAI { get; set; } = new();
    public OllamaSettings Ollama { get; set; } = new();
    public GeminiSettings Gemini { get; set; } = new();
    public ClaudeSettings Claude { get; set; } = new();
    public OpenRouterSettings OpenRouter { get; set; } = new();
    public GatewaySettings Gateway { get; set; } = new();
    public MemorySettings Memory { get; set; } = new();
    public SemanticCacheOptions SemanticCache { get; set; } = new();
}

public class SemanticCacheOptions
{
    public bool Enabled { get; set; } = true;
    public string AgentName { get; set; } = "AgenticSystem";
    public double SimilarityThreshold { get; set; } = 0.95;
}

public class OpenAISettings
{
    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.openai.com/";
    public string DefaultModel { get; set; } = "gpt-4o-mini";
    public bool Enabled { get; set; } = false;
    public int Priority { get; set; } = 10;
}

public class OpenRouterSettings
{
    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://openrouter.ai/api/";
    public string DefaultModel { get; set; } = "openrouter/auto";
    public bool Enabled { get; set; } = false;
    public int Priority { get; set; } = 4;
}

public class OllamaSettings
{
    public string BaseUrl { get; set; } = "http://localhost:11434";
    public string DefaultModel { get; set; } = "llama3";
    public string EmbeddingModel { get; set; } = "nomic-embed-text";
    public bool Enabled { get; set; } = true;
    public int Priority { get; set; } = 1;
}

public class GeminiSettings
{
    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/";
    public string DefaultModel { get; set; } = "gemini-1.5-flash";
    public bool Enabled { get; set; } = false;
    public int Priority { get; set; } = 5;
}

public class ClaudeSettings
{
    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.anthropic.com/";
    public string DefaultModel { get; set; } = "claude-3-5-sonnet-latest";
    public bool Enabled { get; set; } = false;
    public int Priority { get; set; } = 3;
}

public class GatewaySettings
{
    public decimal DefaultDailyBudget { get; set; } = 50.00m;
    public int DefaultFailureThreshold { get; set; } = 5;
    public int DefaultBreakDurationSeconds { get; set; } = 30;
    public int DefaultRequestsPerMinute { get; set; } = 60;
}

public class MemorySettings
{
    public string ObsidianVaultPath { get; set; } = string.Empty;
    public string VectorStoreType { get; set; } = "InMemory";
    public string? ConnectionString { get; set; }
}
