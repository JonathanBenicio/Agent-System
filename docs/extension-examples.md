# Extension Examples — Criando Plugins e Agents

Exemplos práticos de como estender o Agentic System com novos agents, tools e skills.

## 1. Criar um Agent Personalizado

Agents são especializações de processamento. Cada agent tem um tier que define suas capacidades.

```csharp
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Extensions.Agents;

/// <summary>
/// Agent especializado em análise de segurança.
/// Tier 2 (Specialist) — domínio único com profundidade.
/// </summary>
public class SecurityAuditAgent : IAgent
{
    public string Name => "SecurityAuditor";
    public AgentTier Tier => AgentTier.Specialist;

    private readonly ILogger<SecurityAuditAgent> _logger;

    public SecurityAuditAgent(ILogger<SecurityAuditAgent> logger)
    {
        _logger = logger;
    }

    public bool CanHandle(AnalysisResult analysis)
    {
        // Aceita requests de análise no domínio de segurança
        return analysis.Intent == IntentType.Analyze
            && analysis.PrimaryDomain.Contains("security", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<AgentResponse> ProcessAsync(
        AgentRequest request,
        RAGContext? ragContext = null,
        CancellationToken ct = default)
    {
        _logger.LogInformation("🔒 SecurityAuditAgent processing: {Query}", request.Query);

        // 1. Use RAG context se disponível
        var contextInfo = ragContext?.BuiltContext ?? "No additional context";

        // 2. Processe a requisição (aqui seria chamada ao LLM)
        var result = $"Security analysis for: {request.Query}\n\nContext used: {contextInfo.Length} chars";

        return AgentResponse.Ok(result, Name, Tier);
    }
}
```

### Registrar o Agent

```csharp
// Em Program.cs ou em um ServiceCollectionExtensions customizado
services.AddSingleton<IAgent, SecurityAuditAgent>();

// O HierarchicalAgentFactory automaticamente descobre agents via DI
```

## 2. Criar uma Tool

Tools são operações atômicas que agents podem invocar.

```csharp
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Extensions.Tools;

/// <summary>
/// Tool para validar URLs e verificar status HTTP.
/// </summary>
public class UrlHealthCheckTool : ITool
{
    public string Name => "url-health-check";
    public string Description => "Verifica se uma URL está acessível e retorna o status HTTP.";

    public ToolParameter[] Parameters => new[]
    {
        new ToolParameter
        {
            Name = "url",
            Description = "URL para verificar",
            Type = "string",
            Required = true
        },
        new ToolParameter
        {
            Name = "timeout",
            Description = "Timeout em segundos (default: 10)",
            Type = "number",
            Required = false
        }
    };

    public async Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        if (!parameters.TryGetValue("url", out var urlObj) || urlObj is not string url)
        {
            return ToolResult.Failure("Parameter 'url' is required");
        }

        // Validate URL format to prevent SSRF
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != "http" && uri.Scheme != "https"))
        {
            return ToolResult.Failure("Invalid URL format. Only http/https supported.");
        }

        var timeoutSeconds = parameters.TryGetValue("timeout", out var t)
            ? Convert.ToInt32(t) : 10;

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(timeoutSeconds) };

        try
        {
            var response = await client.GetAsync(uri);
            return ToolResult.Success(
                $"Status: {(int)response.StatusCode} {response.StatusCode}");
        }
        catch (TaskCanceledException)
        {
            return ToolResult.Failure($"Timeout after {timeoutSeconds}s");
        }
        catch (HttpRequestException ex)
        {
            return ToolResult.Failure($"Connection error: {ex.Message}");
        }
    }
}
```

### Registrar a Tool

```csharp
// Via IToolManager (em runtime)
var toolManager = serviceProvider.GetRequiredService<IToolManager>();
toolManager.RegisterTool(new UrlHealthCheckTool());

// Ou via SeedAgenticDefaults em ServiceCollectionExtensions
```

## 3. Criar uma Skill

Skills são pacotes de conhecimento que enriquecem agents.

```csharp
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Extensions.Skills;

/// <summary>
/// Skill de DevOps — fornece prompts e contexto para operações de CI/CD.
/// </summary>
public class DevOpsSkill : ISkill
{
    public string Name => "devops";
    public string Description => "CI/CD, Docker, Kubernetes, pipelines e deploy.";
    public string[] Tags => new[] { "devops", "ci-cd", "docker", "kubernetes", "deploy" };

    public bool IsRelevant(AnalysisResult analysis)
    {
        return analysis.PrimaryDomain.Contains("devops", StringComparison.OrdinalIgnoreCase)
            || analysis.RequiredTools.Any(t =>
                t.Contains("docker", StringComparison.OrdinalIgnoreCase)
                || t.Contains("k8s", StringComparison.OrdinalIgnoreCase));
    }

    public string GetSystemPromptAddition()
    {
        return """
            You are a DevOps specialist. Follow these principles:
            - Infrastructure as Code (IaC) always
            - Immutable deployments
            - 12-factor app methodology
            - Canary/Blue-Green deployment strategies
            - Security scanning in CI pipeline (SAST + DAST)
            """;
    }
}
```

### Registrar a Skill

```csharp
var skillManager = serviceProvider.GetRequiredService<ISkillManager>();
skillManager.RegisterSkill(new DevOpsSkill());
```

## 4. Criar um Maturity Level Service

Maturity Levels são serviços que evoluem a inteligência do sistema.

```csharp
// 1. Defina os Models em src/AgenticSystem.Core/Models/MaturityModels.cs
public class MyFeatureResult
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Data { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// 2. Defina a Interface em src/AgenticSystem.Core/Interfaces/IMaturityServices.cs
public interface IMyFeatureService
{
    Task<MyFeatureResult> ProcessAsync(string input);
    Task<IEnumerable<MyFeatureResult>> GetHistoryAsync(int count = 10);
}

// 3. Implemente o Service em src/AgenticSystem.Core/Services/
public class MyFeatureService : IMyFeatureService
{
    private readonly ConcurrentBag<MyFeatureResult> _history = new();
    private readonly ILogger<MyFeatureService> _logger;

    public MyFeatureService(ILogger<MyFeatureService> logger) => _logger = logger;

    public Task<MyFeatureResult> ProcessAsync(string input)
    {
        var result = new MyFeatureResult { Data = $"Processed: {input}" };
        _history.Add(result);
        return Task.FromResult(result);
    }

    public Task<IEnumerable<MyFeatureResult>> GetHistoryAsync(int count = 10)
        => Task.FromResult<IEnumerable<MyFeatureResult>>(
            _history.Take(count).ToList());
}

// 4. Registre no DI em ServiceCollectionExtensions.cs
services.AddSingleton<IMyFeatureService, MyFeatureService>();

// 5. Escreva testes em tests/AgenticSystem.Tests/
```

## 5. Substituir o Session Store (ML16)

O sistema usa `ISessionStore` para persistência de sessões. Por padrão, usa `InMemorySessionStore`. Para trocar para SQLite (ou criar seu próprio):

```csharp
// Opção 1 — Usar SQLite (file-based, zero config)
services.UseSqliteSessionStore("./data/sessions");

// Opção 2 — Implementar seu próprio ISessionStore
public class RedisSessionStore : ISessionStore
{
    public Task SaveAsync(SessionData session, CancellationToken ct = default) { /* ... */ }
    public Task<SessionData?> GetAsync(string sessionId, CancellationToken ct = default) { /* ... */ }
    public Task<IReadOnlyList<SessionData>> GetByUserAsync(string userId, CancellationToken ct = default) { /* ... */ }
    public Task DeleteAsync(string sessionId, CancellationToken ct = default) { /* ... */ }
    public Task<bool> ExistsAsync(string sessionId, CancellationToken ct = default) { /* ... */ }
}

// Registrar
services.AddSingleton<ISessionStore, RedisSessionStore>();
```

## 6. Integrar LLM via Microsoft.Extensions.AI (ML17)

Registre qualquer `IChatClient` no DI e o `ChatClientProviderAdapter` o expõe automaticamente como `ILLMProvider`:

```csharp
// 1. Registre um IChatClient (ex: Azure OpenAI, qualquer provider M.E.AI)
services.AddChatClient(new AzureOpenAIClient(...).GetChatClient("gpt-4o"));

// 2. O ChatClientProviderAdapter é registrado automaticamente
//    quando detecta um IChatClient no DI (via Infrastructure DI Extensions)
//    Ele implementa ILLMProvider e funciona como qualquer outro provider

// Zero código adicional — o adapter mapeia:
// - LLMRequest → ChatMessage[]
// - IChatClient.CompleteAsync() → resultado
// - ChatResponse → LLMResponse (com tokens de uso)
```

## 7. Convenções de Extensão

| Item | Convenção |
|------|-----------|
| Namespace | `AgenticSystem.Extensions.{Tipo}` (Agents, Tools, Skills) |
| DI Lifetime | `Singleton` para stateless, `Scoped` para stateful por request |
| Logging | Use `ILogger<T>` com structured logging |
| Testes | 1 arquivo de teste por service, prefixo `{Service}Tests.cs` |
| Models | Adicionar ao arquivo relevante ou criar em `Models/` |
| Interfaces | Adicionar a `IMaturityServices.cs` ou criar arquivo dedicado |

## 8. Diagrama de Extensibilidade

```
                    ┌─────────────────────┐
                    │   MetaAgent (Tier 0) │
                    └─────────┬───────────┘
                              │ delegates
              ┌───────────────┼───────────────┐
              │               │               │
        ┌─────▼─────┐  ┌─────▼─────┐  ┌──────▼─────┐
        │  Agent T1  │  │  Agent T2  │  │  Custom    │
        │ (Executor) │  │(Specialist)│  │  Agent     │
        └─────┬──────┘  └─────┬──────┘  └──────┬─────┘
              │               │               │
        ┌─────▼───────────────▼───────────────▼──────┐
        │              Tools + Skills                 │
        │  ┌─────┐ ┌────────┐ ┌──────┐ ┌──────────┐ │
        │  │Built│ │  URL   │ │DevOps│ │  Custom  │ │
        │  │ -in │ │ Health │ │Skill │ │  Skill   │ │
        │  └─────┘ └────────┘ └──────┘ └──────────┘ │
        └────────────────────────────────────────────┘
```
