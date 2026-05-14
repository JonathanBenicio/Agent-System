# Claude Provider Test Connectivity Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Corrigir a validação de conectividade do provedor Claude para que retorne `available: true` quando a chave for válida, mesmo se o provedor estiver desabilitado no sistema, e corrigir o ID do modelo padrão.

**Architecture:** Ajustar a lógica de `IsAvailableAsync` nos provedores para depender apenas da API Key e atualizar os modelos padrões para valores válidos.

**Tech Stack:** .NET 10, ASP.NET Core, C#

---

### Task 1: Ajustar Modelos Padrões e Catálogo

**Files:**
- Modify: `src/AgenticSystem.Api/appsettings.json`
- Modify: `src/AgenticSystem.Infrastructure/Configuration/AgenticSystemSettings.cs`
- Modify: `src/AgenticSystem.Infrastructure/LLM/LLMManager.cs`

- [ ] **Step 1: Atualizar appsettings.json**
Alterar o `DefaultModel` do Claude.

```json
"Claude": {
  "ApiKey": "",
  "BaseUrl": "https://api.anthropic.com/",
  "DefaultModel": "claude-3-5-sonnet-latest",
  "Enabled": false,
  "Priority": 3
}
```

- [ ] **Step 2: Atualizar AgenticSystemSettings.cs**
Garantir que o valor default no código corresponda ao appsettings.

```csharp
public class ClaudeSettings
{
    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.anthropic.com/";
    public string DefaultModel { get; set; } = "claude-3-5-sonnet-latest";
    public bool Enabled { get; set; } = false;
    public int Priority { get; set; } = 3;
}
```

- [ ] **Step 3: Limpar ProviderModelCatalog em LLMManager.cs**
Remover modelos inexistentes do dicionário estático.

```csharp
private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, HashSet<string>> ProviderModelCatalog = new(StringComparer.OrdinalIgnoreCase)
{
    ["OpenAI"] = new(StringComparer.OrdinalIgnoreCase) { "gpt-4o", "gpt-4o-mini" },
    ["Gemini"] = new(StringComparer.OrdinalIgnoreCase) { "gemini-1.5-pro", "gemini-1.5-flash", "gemini-2.0-flash-exp" },
    ["Claude"] = new(StringComparer.OrdinalIgnoreCase) { "claude-3-5-sonnet-latest", "claude-3-5-haiku-latest", "claude-3-opus-latest" },
    ["Ollama"] = new(StringComparer.OrdinalIgnoreCase) { "llama3", "llama3.1", "mistral", "qwen2.5" }
};
```

### Task 2: Corrigir Verificação de Disponibilidade nos Provedores

**Files:**
- Modify: `src/AgenticSystem.Infrastructure/LLM/ClaudeProvider.cs`
- Modify: `src/AgenticSystem.Infrastructure/LLM/OpenAIProvider.cs`
- Modify: `src/AgenticSystem.Infrastructure/LLM/GeminiProvider.cs`

- [ ] **Step 1: Atualizar ClaudeProvider.cs**
Permitir teste se a chave existir.

```csharp
public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
{
    if (string.IsNullOrWhiteSpace(_settings.ApiKey)) return false;
    // ... restante da lógica de ping ...
}
```

- [ ] **Step 2: Atualizar OpenAIProvider.cs**
```csharp
public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
{
    if (string.IsNullOrWhiteSpace(_settings.ApiKey)) return false;
    // ...
}
```

- [ ] **Step 3: Atualizar GeminiProvider.cs**
```csharp
public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
{
    if (string.IsNullOrWhiteSpace(_settings.ApiKey)) return false;
    // ...
}
```

### Task 3: Verificação Final

- [ ] **Step 1: Compilar o projeto**
`dotnet build src/AgenticSystem.Infrastructure`

- [ ] **Step 2: Validar o endpoint via teste integrado ou manual**
Verificar se `POST /api/admin/llm/providers/Claude/test` retorna `available: true`.
