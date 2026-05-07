# Obsidian Vault — Memória Episódica do Agentic System

## O que é

O "Obsidian Vault" é o subsistema de **memória episódica** do Agentic System. Persiste eventos de sessão e definições de agents como arquivos Markdown com YAML frontmatter — formato compatível com o app [Obsidian](https://obsidian.md), mas **sem dependência dele**.

Na prática, funciona como um file store local que:

1. Grava eventos de cada interação agent/usuário como `.md`
2. Salva definições de agents criados dinamicamente
3. Indexa todos os `.md` no Vector Store para busca semântica
4. Monitora alterações via `FileSystemWatcher` para re-indexar automaticamente

## Arquitetura

```
┌─────────────────────────────────┐
│         IObsidianSync           │  Core — Interface
│  (AgenticSystem.Core)           │
├─────────────────────────────────┤
│       FileObsidianSync          │  Infrastructure — Implementação
│  (AgenticSystem.Infrastructure) │
├──────────┬──────────────────────┤
│  Vault   │   IVectorStore       │
│  (fs)    │   (busca semântica)  │
└──────────┴──────────────────────┘
```

### Interface — `IObsidianSync`

**Arquivo**: `src/AgenticSystem.Core/Interfaces/IObsidianSync.cs`

| Método | Descrição |
|---|---|
| `SaveSessionEventAsync(AgentEvent)` | Salva evento de sessão como nota `.md` |
| `SaveAgentDefinitionAsync(IAgent)` | Salva definição de agent como `.md` |
| `GetRelevantNotesAsync(string query)` | Busca semântica por notas relevantes via Vector Store |
| `StartFileWatcherAsync()` | Inicia watcher que re-indexa `.md` novos/alterados |
| `IndexExistingVaultAsync()` | Indexa todos os `.md` existentes no vault |

### Implementação — `FileObsidianSync`

**Arquivo**: `src/AgenticSystem.Infrastructure/Sync/FileObsidianSync.cs`

Usa `IVectorStore` + file system. Ao salvar, grava o `.md` e faz `UpsertAsync` no vector store.

### Models

**`ObsidianNote`** (`src/AgenticSystem.Core/Models/MemoryModels.cs`):

```csharp
public class ObsidianNote
{
    public string Id { get; set; }
    public string Title { get; set; }
    public string Content { get; set; }
    public string FilePath { get; set; }
    public List<string> Tags { get; set; }
    public List<string> BackLinks { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Dictionary<string, object> Frontmatter { get; set; }
}
```

## Estrutura do Vault

```
vault/
├── sessions/
│   └── {sessionId}/
│       └── 20260501-143000_MetaAgent.md
├── agents/
│   └── PersonalAgent.md
└── notes/
```

### Formato dos Arquivos

**Session Event** (YAML frontmatter + Markdown):

```markdown
---
id: evt-abc123
session: sess-xyz
agent: MetaAgent
tier: 0
timestamp: 2026-05-01T14:30:00Z
tags: [routing, analysis]
---

# Session Event — MetaAgent

## Input
```
Crie um lembrete para amanhã às 14h
```

## Response
Lembrete criado com sucesso.

## Actions
- create_reminder

## Tools Used
- CalendarTool
```

**Agent Definition**:

```markdown
---
name: PersonalAgent
tier: 1
created: 2026-05-01T10:00:00Z
active: true
---

# Agent: PersonalAgent

**Description**: Agente de produtividade pessoal
**Tier**: 1
**Active**: true

## Available Tools
- CalendarTool
- ReminderTool
```

## Configuração

### 1. appsettings.json

O vault path é definido em `AgenticSystem:Memory:ObsidianVaultPath`:

```json
{
  "AgenticSystem": {
    "Memory": {
      "ObsidianVaultPath": "./data/obsidian-vault",
      "VectorStoreType": "InMemory"
    }
  }
}
```

> **Nota**: Se vazio ou não configurado, o fallback é `{AppContext.BaseDirectory}/vault`.

### 2. DI Registration

Em `ServiceCollectionExtensions.cs`, o vault path é lido de **`AgenticSystem:Memory:ObsidianVaultPath`**:

```csharp
services.AddSingleton<IObsidianSync>(sp =>
{
    var vectorStore = sp.GetRequiredService<IVectorStore>();
    var logger = sp.GetRequiredService<ILogger<FileObsidianSync>>();
  var vaultPath = configuration["AgenticSystem:Memory:ObsidianVaultPath"];
    return new FileObsidianSync(vectorStore, logger, vaultPath);
});
```

> Situação atual: a divergência antiga foi resolvida. O runtime e o settings model usam a mesma chave `AgenticSystem:Memory:ObsidianVaultPath`.

### 3. Runtime — API de Settings

O endpoint `PUT /api/settings/memory` permite alterar o vault path em runtime:

```http
PUT /api/settings/memory
Content-Type: application/json

{
  "obsidianVaultPath": "./data/obsidian-vault",
  "vectorStoreType": "InMemory",
  "connectionString": null
}
```

> Altera apenas o valor em `MemorySettings` (in-memory). **Não** recria o `FileObsidianSync` — o singleton já instanciado continua com o path original.

### 4. Por Usuário — WorkspaceConfig

Cada `UserContext` pode ter um vault path personalizado:

```csharp
public class WorkspaceConfig
{
    public string ObsidianVaultPath { get; set; } = string.Empty;
    public string DefaultNotesPath { get; set; } = string.Empty;
    public List<string> ProjectPaths { get; set; } = new();
}
```

Este campo existe no modelo mas **não é usado** pelo `FileObsidianSync` atual (que usa um único vault path global).

## Fluxo de Dados

```
Interação do usuário
        │
        ▼
  Agent processa
        │
        ▼
  AgentEvent criado
        │
        ├──► FileObsidianSync.SaveSessionEventAsync()
        │         │
        │         ├──► Grava .md no vault (file system)
        │         └──► Indexa no IVectorStore (embeddings)
        │
        └──► Próxima interação
                  │
                  ▼
          GetRelevantNotesAsync(query)
                  │
                  └──► Busca semântica no IVectorStore
                            │
                            └──► Retorna List<ObsidianNote>
```

## Limitações Atuais

1. **Vault path é singleton** — definido uma vez no startup. Alterações via API de settings não recriam o singleton.
2. **Chave de configuração divergente** — DI lê `AgenticSystem:VaultPath`, model usa `AgenticSystem:Memory:ObsidianVaultPath`.
3. **`WorkspaceConfig.ObsidianVaultPath`** per-user existe no modelo mas não é utilizado pela implementação.
4. **FileSystemWatcher** é criado mas o `FileSystemWatcher` referência não é mantida como campo — pode ser garbage collected.
5. **Sem Obsidian real** — É apenas formato de arquivo. Não há integração com o app Obsidian (plugins, sync, etc.).
