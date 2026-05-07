# Extension Examples — Criando Plugins e Agents

Exemplos práticos de como estender o Agentic System com novos agents, tools e skills.

## 1. Criar um Agent Personalizado

Agents são especializações de processamento. Cada agent tem um tier que define suas capacidades.

```csharp
using AgenticSystem.Core.Agents;
using AgenticSystem.Core.Interfaces;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Extensions.Agents;

public sealed class SecurityAuditAgent : BaseAgent
{
    public override string Name => "SecurityAuditor";
    public override string Description => "Especialista em revisão de segurança e hardening.";
    public override AgentTier Tier => AgentTier.Specialist;
    public override string Domain => "security";
    public override IEnumerable<string> AvailableTools => ["http", "file-search", "calculator"];

    public SecurityAuditAgent(
        IChatClient chatClient,
        ISkillManager skillManager,
        ILogger<SecurityAuditAgent> logger)
        : base(chatClient, skillManager, logger)
    {
    }

    protected override string GetBaseSystemPrompt() => """
        You are a security specialist.
        Prioritize vulnerability analysis, hardening recommendations and clear remediation steps.
        """;
}
```

### Registrar o Agent

```csharp
// Em Program.cs ou em um ServiceCollectionExtensions customizado
services.AddSingleton<IAgent, SecurityAuditAgent>();

// A resolução via DI continua válida para o caminho direto
// e para a materialização de especialistas do runtime hospedado.
```

## 2. Criar uma Tool

Tools são operações atômicas que agents podem invocar.

```csharp
using AgenticSystem.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Extensions.Tools;

public sealed class UrlHealthCheckTool : ITool
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<UrlHealthCheckTool> _logger;

    public string Id => "url-health-check";
    public string Name => "URL Health Check Tool";
    public string Description => "Valida uma URL e retorna o status HTTP.";
    public ToolCategory Category => ToolCategory.Api;
    public bool RequiresAuth => false;

    public UrlHealthCheckTool(IHttpClientFactory httpClientFactory, ILogger<UrlHealthCheckTool> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<ToolResult> ExecuteAsync(ToolInput input, CancellationToken ct = default)
    {
        if (!input.Parameters.TryGetValue("url", out var urlObj) || urlObj is not string url)
        {
            return ToolResult.Fail("Parâmetro 'url' é obrigatório.");
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != "http" && uri.Scheme != "https"))
        {
            return ToolResult.Fail("URL inválida. Use http:// ou https://.");
        }

        var timeoutSeconds = input.Parameters.TryGetValue("timeout", out var t)
            ? Convert.ToInt32(t) : 10;

        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);

        try
        {
            var response = await client.GetAsync(uri, ct);
            return ToolResult.Ok(new
            {
                statusCode = (int)response.StatusCode,
                reasonPhrase = response.ReasonPhrase,
                url
            });
        }
        catch (TaskCanceledException)
        {
            return ToolResult.Fail($"Timeout after {timeoutSeconds}s");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Erro ao consultar {Url}", url);
            return ToolResult.Fail($"Connection error: {ex.Message}");
        }
    }

    public Task<bool> IsAvailableAsync(CancellationToken ct = default) => Task.FromResult(true);
}
```

### Registrar a Tool

```csharp
services.AddHttpClient();
services.AddSingleton<ITool, UrlHealthCheckTool>();

// Opcionalmente, registre explicitamente no IToolManager durante o bootstrap:
var toolManager = serviceProvider.GetRequiredService<IToolManager>();
toolManager.RegisterTool(new UrlHealthCheckTool(
    serviceProvider.GetRequiredService<IHttpClientFactory>(),
    serviceProvider.GetRequiredService<ILogger<UrlHealthCheckTool>>()));
```

## 3. Criar uma Skill

Skills são pacotes de conhecimento que enriquecem agents.

```csharp
using AgenticSystem.Core.Interfaces;

namespace AgenticSystem.Extensions.Skills;

public sealed class DevOpsSkill : ISkill
{
    public string Id => "devops";
    public string Name => "DevOps";
    public string Domain => "work";
    public SkillType Type => SkillType.Knowledge;

    public Task<SkillContent> GetContentAsync(SkillContext context)
    {
        return Task.FromResult(new SkillContent
        {
            SystemPromptFragment = """
                You are a DevOps specialist. Follow these principles:
                - Infrastructure as Code (IaC) always
                - Immutable deployments
                - 12-factor app methodology
                - Canary/Blue-Green deployment strategies
                - Security scanning in CI pipeline (SAST + DAST)
                """,
            Metadata = new Dictionary<string, string>
            {
                ["topics"] = "devops,ci-cd,docker,kubernetes,deploy"
            }
        });
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

O sistema usa `ISessionStore` para persistência de sessões. Por padrão, usa `InMemorySessionStore`. Para persistência durável, o caminho suportado é PostgreSQL:

```csharp
// Opção 1 — Usar PostgreSQL (persistência durável)
services.UsePostgresSessionStore(configuration.GetConnectionString("AgenticDb")!);

// Opção 2 — Implementar seu próprio ISessionStore
public class RedisSessionStore : ISessionStore
{
    public Task SaveAsync(SessionData session, CancellationToken ct = default) { /* ... */ }
    public Task<SessionData?> GetAsync(string sessionId, CancellationToken ct = default) { /* ... */ }
    public Task<IReadOnlyList<SessionData>> GetByUserAsync(string userId, int maxResults = 10, CancellationToken ct = default) { /* ... */ }
    public Task<IReadOnlyList<SessionData>> GetByTenantAsync(string tenantId, string? userId = null, int maxResults = 10, CancellationToken ct = default) { /* ... */ }
    public Task DeleteAsync(string sessionId, CancellationToken ct = default) { /* ... */ }
    public Task<bool> ExistsAsync(string sessionId, CancellationToken ct = default) { /* ... */ }
}

// Registrar
services.AddSingleton<ISessionStore, RedisSessionStore>();
```

## 6. Integrar LLM via Microsoft.Extensions.AI (ML17)

O caminho suportado hoje é registrar a infraestrutura completa, deixar o `LLMManager` montar o catálogo administrativo e usar o `ContextAwareChatClient` como `IChatClient` principal do runtime:

```csharp
// Recomendado: usar a extensão oficial da infraestrutura
services.AddAgenticSystemInfrastructure(configuration);

// Isso registra, entre outros:
// - LLMManager (catálogo administrativo de providers)
// - ContextAwareChatClient (resolve provider/modelo por contexto)
// - GovernedChatClient como IChatClient principal

// Quando um fluxo legado precisar expor um ILLMProvider como IChatClient:
services.AddSingleton<IChatClient>(sp =>
    new ProviderBackedChatClient(sp.GetRequiredService<ILLMProvider>()));
```

> **Nota**: o runtime principal não depende mais de um adapter automático global de `IChatClient` para `ILLMProvider`; a compatibilidade agora é explícita e contextual.

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
