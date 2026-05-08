# Technical Architecture Guide — AgenticSystem

> Guia técnico complementar da arquitetura interna do AgenticSystem.
> Quando houver conflito com a topologia corrente do backend, prevalece [architecture/backend-architecture-explained.md](architecture/backend-architecture-explained.md) como fonte de verdade arquitetural.
> Este documento aprofunda subsistemas, integrações e superfícies técnicas específicas.

## Governança de Escopo (Core x Laboratório)

### Core de Produto

Para decisões arquiteturais, o core estável do produto é composto por:

- chat principal
- sessão e seu ciclo de vida
- streaming fim a fim
- um único caminho principal de execução
- observabilidade mínima para operar o runtime

### Trilhas de Laboratório

Capacidades experimentais não alteram o core por padrão. Elas devem entrar isoladas, com:

- feature flag
- módulo separado
- rollout opcional
- fallback para o comportamento atual

Exemplos típicos: protocolos extras, plugins MCP, workflows colaborativos avançados, approvals avançados, self-improvement loops e superfícies administrativas especializadas.

### Critérios de incubação e descarte

Toda capacidade experimental deve declarar, desde o início, hipótese, critério de sucesso e critério de remoção. A promoção para o core exige ganho recorrente comprovado contra baseline, estabilidade operacional e ausência de segundo caminho principal. Sem ganho mensurável ou com aumento de risco estrutural, a capacidade deve permanecer em laboratório, fazer rollback ou ser descartada.

> Referência de planejamento: [planejamento/AI_Advanced_Capabilities_Roadmap.md](planejamento/AI_Advanced_Capabilities_Roadmap.md)

---

## Sumário

1. [Fluxo de Execução Atual](#1-fluxo-de-execução-atual)
2. [Microsoft Agent Framework](#2-microsoft-agent-framework)
3. [LLM — IChatClient Pipeline](#3-llm--ichatclient-pipeline)
4. [RAG Pipeline](#4-rag-pipeline)
5. [Memória — Obsidian Vault](#5-memória--obsidian-vault)
6. [MCP — Model Context Protocol](#6-mcp--model-context-protocol)
7. [Multi-Tenant](#7-multi-tenant)
8. [Autenticação (MultiAuth)](#8-autenticação-multiauth)
9. [SignalR — Comunicação Real-Time](#9-signalr--comunicação-real-time)
10. [Service Gateway](#10-service-gateway)
11. [Frontend — SPA React](#11-frontend--spa-react)
12. [Over-Engineering Check](#12-over-engineering-check)
13. [Scheduler — Task Chaining DAG-lite](#13-scheduler--task-chaining-dag-lite)
14. [Agent Memory & Self-Improvement](#14-agent-memory--self-improvement)
15. [Tool Registry & Local Providers](#15-tool-registry--local-providers)

---

## 1. Fluxo de Execução Atual

**Arquivo**: `src/AgenticSystem.Core/Services/MetaAgentOrchestrator.cs`
**Interface**: `IMetaAgent`

Desde maio/2026, o `MetaAgentOrchestrator` atua como **fachada de sessão + streaming** e delega a execução operacional para `IAgentExecutionWorkflow`.

### 1.1 Papel do MetaAgentOrchestrator (fachada)

- Inicia sessão (`ISessionManager.StartSessionAsync`)
- Abre escopo de execução (`IAgentRuntimeCoordinator.BeginExecutionScope`)
- Expõe chamadas sync/stream (`ProcessRequestAsync`, `ProcessRequestStreamAsync`, `ProcessDirectRequestAsync`, `ProcessDirectRequestStreamAsync`)
- Delega a execução para `IAgentExecutionWorkflow`

### 1.2 Pipeline do AgentExecutionWorkflow

**Arquivo**: `src/AgenticSystem.Core/Services/AgentExecutionWorkflow.cs`

No runtime atual, o `AgentExecutionWorkflow` deixou de concentrar a decisão imperativa de orquestração. O caminho principal virou uma **casca fina** que abre o contexto de runtime e delega para `IFrameworkOrchestratorService`.

```
User Input
    │
    ▼
┌─────────────────────────────────────┐
│ 1. MetaAgentOrchestrator            │ ← fachada de sessão + streaming
├─────────────────────────────────────┤
│ 2. AgentExecutionWorkflow           │ ← BeginScope + delegação fina
│    ILLMRuntimeContextAccessor       │    IFrameworkOrchestratorService
├─────────────────────────────────────┤
│ 3. FrameworkOrchestratorService     │ ← hosted orchestration path
│    resolve OrchestratorContext      │    AIAgent + AgentSessionStore keyed
├─────────────────────────────────────┤
│ 4. Shared Pre-Processing Pipeline   │ ← validação de request + correction rules
│    IAgentExecutionPreProcessing     │
├─────────────────────────────────────┤
│ 5. Hosted AIAgent.RunAsync()        │ ← supervisor-with-tools
│    specialists + aux tools + RAG    │    collaboration workflow + protocol surfaces
├─────────────────────────────────────┤
│ 6. Shared Post-Processing Pipeline  │ ← reflection, confidence, final approval,
│    IAgentExecutionPostProcessing    │    persistência de artifacts e agent memory
└─────────────────────────────────────┘
    │
    ▼
AgentResponse (com Confidence, SessionId, Metadata)
```

### 1.3 Streaming e Eventos de Runtime

- `IAgentRuntimeCoordinator.StreamAsync` controla streaming fim a fim
- Eventos operacionais incluem: planning, step, tool, rag, review, handoff, approvals, session completion
- SSE: `POST /api/chat/stream`
- SignalR: evento `StreamEvent` no `ChatHub`

### 1.4 Approvals de Produção

- Tool approvals: `IToolGovernanceService`
- Final response approval (ML33): `IFinalResponseApprovalService`
- Endpoints em `AgentController` para listar/aprovar/rejeitar pendências

### 1.5 Dependências Principais do Workflow

| Dependência | Obrigatória | Papel |
|-------------|:-----------:|-------|
| `IDirectAgentRequestExecutor` | ✅ | Escape hatch do `ExecuteDirectAsync` |
| `ISessionManager` | ✅ | Gerencia sessões e eventos |
| `IAgentRuntimeCoordinator` | ✅ | Streaming, artefatos e métricas |
| `ILLMRuntimeContextAccessor` | ✅ | Abre o escopo contextual de LLM por sessão/request |
| `IFrameworkOrchestratorService` | ✅ | Caminho principal framework-first/hosted |

As dependências antes centralizadas no workflow principal migraram para o `FrameworkOrchestratorService`, para os pipelines compartilhados de pre/post-processing e para os specialist tool bindings/context providers do orquestrador hosted.

---

## 2. Microsoft Agent Framework

**Diretório**: `src/AgenticSystem.Infrastructure/AgentFramework/`

O sistema usa o **Microsoft.Agents.AI** como runtime **hosted** do fluxo principal. O `IAgentFactory` permanece cru para o domínio e para o path direto; quando o escape hatch `ExecuteDirectAsync` precisa do framework, ele delega para `AgentFrameworkDirectExecutionService`, sem criar um wrapper transitório de `IAgent`.

### 2.1 Arquitetura do Framework

```
┌──────────────────────────────────────────────────────────────┐
│ Program.cs / Hosting DI                                      │
│   ├─ AddAIAgent("Orchestrator")                             │
│   ├─ AddAIAgent("AgenticSystem") → alias do hosted agent    │
│   ├─ AddWorkflow("collaboration")                           │
│   └─ A2A / AG-UI reutilizam AgentSessionStore do Orchestrator│
├──────────────────────────────────────────────────────────────┤
│ OrchestratorContextFactory                                   │
│   ├─ delega montagem ao OrchestratorHostBuilder              │
│   ├─ system prompt do supervisor                             │
│   ├─ specialist bindings via AsAIFunction()                  │
│   ├─ tools auxiliares (RAG / Router / Analyzer)              │
│   ├─ SimpleSessionStoreAdapter keyed                         │
│   └─ logging + OpenTelemetry                                 │
├──────────────────────────────────────────────────────────────┤
│ FrameworkOrchestratorService                                 │
│   ├─ resolve AIAgent + AgentSessionStore do hosting          │
│   ├─ pre-processing pipeline                                 │
│   ├─ orchestrator.RunAsync(...)                              │
│   └─ post-processing pipeline                                │
├──────────────────────────────────────────────────────────────┤
│ Protocol surfaces                                             │
│   ├─ A2A                                                      │
│   ├─ AG-UI                                                    │
│   └─ OpenAI-compatible controller                             │
└──────────────────────────────────────────────────────────────┘
```

O caminho direto continua explícito, mas a execução nativa do framework agora fica concentrada em um serviço dedicado. O adapter transitório saiu do runtime.

No fluxo hosted, a composição local remanescente está concentrada em `OrchestratorContextFactory` + `OrchestratorHostBuilder`. Não existe mais um resolvedor scoped paralelo nem wrapper específico de protocolo fora desse caminho.

### 2.2 Componentes

#### `AgentFrameworkFactory`

Fábrica que transforma `IAgent` em `ChatClientAgent` do Microsoft Agent Framework:

- Recebe `IChatClient` (pipeline com logging + telemetry + function invocation)
- Injeta `Instructions` do agent como system prompt
- Conecta MCP tools via `McpToolsAIFunctionAdapter`
- Também consegue expor agents especializados como tools nativas via `AIAgentExtensions.AsAIFunction(...)`, reaproveitando `SimpleSessionStoreAdapter` para restaurar e persistir a sessão correta
- Aplica `.AsBuilder().UseLogging().UseOpenTelemetry().Build()`

#### `AgentFrameworkDirectExecutionService`

Serviço explícito do path direto. Só entra quando `ExecuteDirectAsync` precisa rodar um `IAgent` cru pelo Microsoft Agent Framework:

```csharp
public Task<AgentResponse> ExecuteDirectAsync(
    IAgent agent,
    string sessionId,
    string input,
    UserContext context,
    CancellationToken ct = default)
```

Responsabilidades:

1. Criar o `ChatClientAgent` via `AgentFrameworkFactory`
2. Restaurar a `AgentSession` no `SimpleSessionStoreAdapter`
3. Executar `RunAsync` ou `RunStreamingAsync`
4. Persistir a sessão e sincronizar o evento de negócio
5. Fazer fallback para o agente cru só em erro do framework

#### `DirectAgentRequestExecutor`

Executor dedicado do escape hatch direto. O `AgentExecutionWorkflow` só abre o escopo de runtime/LLM e delega para esse serviço:

1. Resolve o agent solicitado no catálogo ativo
2. Chama `IDirectAgentExecutionService` quando a infraestrutura nativa do framework está disponível
3. Aplica quality gates e correction loop antes da execução
4. Delega o pós-processamento final para o `IAgentExecutionPostProcessingPipeline`

#### `AgentExecutionPostProcessingPipeline`

Pipeline compartilhado do Core para o pós-processamento dos fluxos direto e hosted:

1. Valida a resposta quando o caller habilita esse passo
2. Executa reflection com o `sessionId` de negócio e aprende correction rules quando configurado
3. Calcula confidence e avalia final approval
4. Persiste sessão, artifacts e memória do agent

O adapter `AgentFrameworkAdapter` foi removido na Fase 4. O comportamento de sessão, execução e fallback foi incorporado ao `AgentFrameworkDirectExecutionService`.

#### `SimpleSessionStoreAdapter`

Adapter final entre a sessão do framework e a sessão de negócio:

- Serializa e persiste o `AgentSession` no store da aplicação por nome estável do agent
- Restaura a thread nativa do framework sem depender de cache in-memory entre requests
- Faz fallback para criar nova `AgentSession` quando o estado persistido está ausente ou inválido
- Substituiu o adapter legado e removeu os warnings de obsolescência do runtime

#### `FrameworkAgentChannelService`

Canal estruturado entre agents, persistido na própria sessão de negócio:

- Publica mensagens planner → specialist, handoff → target e workflow → reviewer como `AgentEvent`
- Reidrata mensagens recentes por target agent
- Constrói um bloco `[Native Agent Channel Context]` antes da execução do próximo agent
- É usado pelo `AgentCollaborationWorkflow` e pelos fluxos orquestrados para compartilhar contexto entre agents sem depender apenas de concatenação manual de strings

#### `AgentCollaborationWorkflow`

Workflow planner-executor-reviewer com um corte híbrido de orquestração nativa:

- Continua usando planejamento e execução custom para preservar o pipeline atual
- No estágio de review, pode montar um reviewer nativo do Agent Framework com specialist agents publicados como tools
- Persiste a sessão do reviewer e das agent tools após a execução, mantendo continuidade por agent dentro da mesma sessão de negócio
- Faz fallback automático para `reviewer.ExecuteAsync()` se o framework não estiver disponível ou se a montagem das agent tools falhar

### 2.3 BaseAgent

**Arquivo**: `src/AgenticSystem.Core/Agents/BaseAgent.cs`

Classe abstrata que todo agent herda:

- Usa `IChatClient.GetResponseAsync()` para execução contextual
- `ISkillManager.BuildEnrichedPromptAsync()` para system prompts enriquecidos com skills
- `IAgentMemoryService.GetRelevantMemoriesAsync()` injeta memórias persistentes relevantes por agent/usuário no system prompt
- Skills built-in continuam seeded em startup e skills declarativas adicionais podem ser carregadas de `skills/*.yaml|*.yml|*.json`
- Agents concretos: `MasterAgents`, `SpecialistAgents`, `SupportAgents`
- Subclasses definem: `Name`, `Description`, `Tier`, `Domain`

---

## 3. LLM — IChatClient Pipeline

**Diretórios**: `src/AgenticSystem.Core/LLM/`, `src/AgenticSystem.Infrastructure/LLM/`

### 3.1 Arquitetura Multi-Provider

```
┌───────────────────────────────────────────────────┐
│              IChatClient Pipeline                  │
│  (Microsoft.Extensions.AI)                         │
│                                                    │
│  GovernedChatClient                                │
│    └─→ ContextAwareChatClient                      │
│         └─→ Provider-resolved IChatClient          │
│              (OpenAI | Ollama | Gemini | Claude)   │
└───────────────────────────────────────────────────┘

┌───────────────────────────────────────────────────┐
│     LLMManager + ILLMAdministrationService         │
│                                                    │
│  Providers: OpenAI | Ollama | Gemini | Claude      │
│  IChatClient registry por provider                 │
│  ↓                                                 │
│  Seleção contextual (request | sessão | tenant)    │
│  ↓                                                 │
│  Hot-reload de config + fallback por prioridade    │
│  Superfície admin estreita consumida pelo controller│
└───────────────────────────────────────────────────┘
```

### 3.2 LLMManager

**Arquivo**: `src/AgenticSystem.Infrastructure/LLM/LLMManager.cs`

Gerenciador centralizado de providers LLM com:

- **Multi-provider**: registra todos os `ILLMProvider` via DI
- **Registry de `IChatClient` por provider**: cada provider tem seu próprio client reutilizável
- **Seleção contextual**: resolve provider/model por request, sessão, tenant e default global
- **Priority-based fallback**: se provider requisitado não existe, usa o de menor priority
- **Superfície administrativa**: o `LLMManager` expõe catálogo de providers, atualização de configuração e seleção default por meio de `ILLMAdministrationService`
- **Hot-update**: `UpdateProviderAsync()` permite alterar API key, modelo, enabled e priority em runtime
- **Default selection runtime**: `UpdateDefaultSelectionAsync()` persiste a IA inicial usada no chat
- **Health check**: `TestProviderAsync()` verifica disponibilidade de cada provider

#### Providers Disponíveis

| Provider | Classe | Uso |
|----------|--------|-----|
| OpenAI | `OpenAIProvider` | Provider principal (GPT-4o, etc.) |
| Ollama | `OllamaProvider` | LLMs locais (Llama, Mistral, etc.) |
| Gemini | `GeminiProvider` | Google Gemini |
| Claude | `ClaudeProvider` | Anthropic Claude |

### 3.3 Runtime de Seleção de IA

Desde maio/2026 o roteamento de LLM deixou de depender de um único provider lógico. O fluxo atual é:

1. `Program.cs` e `ChatHub` recebem `provider`, `model` e `apiKey` opcionais por request.
2. `AgentExecutionWorkflow` abre um escopo em `ILLMRuntimeContextAccessor`.
3. `LLMManager` resolve a seleção efetiva nesta ordem: request explícito → preferências de sessão → preferências do tenant → default global.
4. O provider resolvido devolve um `IChatClient` dedicado; se houver BYOK, o manager cria um provider efêmero para aquela chamada.
5. `IConfigReloadNotifier` invalida overrides em memória e força reload real quando a configuração administrativa muda.

### 3.4 API Administrativa de IA

**Controller**: `src/AgenticSystem.Api/Controllers/LLMController.cs`

Endpoints principais:

- `GET /api/admin/llm/configuration` — carrega catálogo de providers + seleção default atual
- `PUT /api/admin/llm/providers/{name}` — altera API key, modelo, enabled e prioridade do provider
- `PUT /api/admin/llm/default-selection` — define provider/modelo default apresentados no chat
- `POST /api/admin/llm/providers/{name}/test` — valida conectividade sob demanda

### 3.5 IChatClient Pipeline (M.E.AI)

O `IChatClient` é registrado em `AddAgenticSystemInfrastructure()` como um pipeline em duas camadas:

```
GovernedChatClient                  // concurrency cap + queue timeout + quality gates
    └─→ ContextAwareChatClient        // resolve provider/model por request/sessão/tenant
            └─→ Provider-specific client  // devolvido pelo LLMManager
```

Detalhes do decorator de governança:

- `GovernedChatClient` aplica limite de concorrência configurável em `AgenticSystem:ChatClientMiddleware`.
- Requests ainda podem passar por `IQualityGateService.ValidateRequestAsync(...)` no `GovernedChatClient` como defense-in-depth, mas o dono de borda da validação de request agora é o `IAgentExecutionPreProcessingPipeline` no Core.
- Respostas non-streaming passam por `ValidateResponseAsync(...)` antes de retornar ao chamador.
- No streaming, a resposta é acumulada para validação pós-stream sem remover o comportamento de token streaming.

Arquivos principais:

- `src/AgenticSystem.Infrastructure/LLM/GovernedChatClient.cs`
- `src/AgenticSystem.Infrastructure/LLM/ContextAwareChatClient.cs`
- `src/AgenticSystem.Infrastructure/Configuration/ChatClientMiddlewareOptions.cs`
- `src/AgenticSystem.Infrastructure/Extensions/ServiceCollectionExtensions.cs`

### 3.6 ToolAIFunctionFactory

**Arquivo**: `src/AgenticSystem.Infrastructure/AI/ToolAIFunctionFactory.cs`

Converte `ITool` registrados em `AIFunction` (M.E.AI):

- Cada `ITool` vira um `AIFunction` com parâmetros `action` + `parametersJson`
- Usado pelo `ChatClientPlanner` para function calling nativo
- O `FunctionInvokingChatClient` auto-invoca essas funções

### 3.7 ChatClientPlanner

**Arquivo**: `src/AgenticSystem.Infrastructure/AI/ChatClientPlanner.cs`

Planner de tarefas baseado em `IChatClient` + function calling:

- System prompt especializado em decomposição de tarefas
- Injeta tools disponíveis como `AIFunction` via `ToolAIFunctionFactory`
- Output: `TaskPlan` com steps atômicos (máximo 10)
- Cada step tem `Description` e `AssignedAgent`

---

## 4. RAG Pipeline

**Diretório**: `src/AgenticSystem.Infrastructure/RAG/`

### 4.1 Fluxo Completo

```
User Query
    │
    ▼
┌──────────────────────────────┐
│ 1. Query Compression +        │ ← IQueryCompressor.CompressAsync()
│    Query Variants             │   + variantes heurísticas
├──────────────────────────────┤
│ 2. Vector Search por variante │ ← IVectorStore.SearchAsync()
│    + Filtros por strategy     │   ou SearchWithFiltersAsync()
│    - DomainKnowledge          │   filter: content_type=domain
│    - DecisionHistory          │   filter: content_type=decision
│    - Episodic                 │   filter: content_type=session
│    - Default                  │   sem filtro extra
├──────────────────────────────┤
│ 3. HyDE condicional           │ ← IChatClient.GetResponseAsync()
│    (se recall inicial vier    │   gera passagem hipotética para
│    fraco)                     │   um segundo retrieval
├──────────────────────────────┤
│ 4. Merge distinto + Min Score │ ← query.MinRelevanceScore (0.3)
├──────────────────────────────┤
│ 5. Re-Ranking                 │ ← IReRanker.ReRankAsync()
│    LlmReRanker                │   Heurístico como shortlist +
│    + ONNX cross-encoder local │   caminho default para rerank forte local
│    + Jina provider opcional   │   provider externo alternativo quando desejado
│    + Embedding scorer         │   fallback neural leve
│    + LLM fallback             │   opcional, desabilitado por default no modo local
├──────────────────────────────┤
│ 6. Freshness Penalty (GAP-06) │ ← IKnowledgeFreshnessService
│    Score < 0.5 → penaliza     │   stalePenalized = true
├──────────────────────────────┤
│ 7. Semantic Compression       │ ← ISemanticCompressor
│    (quando excede budget)     │   CompressRankedChunksAsync()
├──────────────────────────────┤
│ 8. Context Build              │ ← BuildContextString()
│    Concatena chunks rankeados │   EstimateTokens()
├──────────────────────────────┤
│ 9. Context Budget (opcional)  │ ← IContextBudgetManager
│    Trim para budget do agent  │   TrimContextToBudgetAsync()
└──────────────────────────────┘
    │
    ▼
RAGContext {
    BuiltContext, Chunks, TotalTokensUsed,
    StrategyUsed, RetrievalTime, ReRankTime,
    EffectiveQuery, QueryVariants,
    UsedHydeExpansion, HydeVariant,
    SemanticSummary, UsedSemanticCompression,
    OriginalContextTokens
}
```

### 4.2 RAGContext Injection

O `MetaAgentOrchestrator` injeta o contexto RAG no prompt do usuário:

```
[Contexto Relevante]
{ragContext.BuiltContext}

[Pergunta do Usuário]
{input original}
```

### 4.3 Retrieval Strategies

| Strategy | Filtro | Uso |
|----------|--------|-----|
| `Default` | Nenhum | Busca geral |
| `DomainKnowledge` | `content_type=domain` | Conhecimento de domínio |
| `DecisionHistory` | `content_type=decision` | Decisões passadas (ADRs) |
| `Episodic` | `content_type=session` | Sessões anteriores |
| `RecentMemory` | — | Memória recente |

### 4.4 Métricas

O `RAGService` expõe métricas detalhadas:

- `RetrievalTime` — tempo de busca vetorial
- `ReRankTime` — tempo de re-ranking
- `TotalTime` — tempo total do pipeline
- `CandidatesRetrieved` → `CandidatesAfterReRank` — funil de chunks
- `EffectiveQuery` / `QueryVariants` — query comprimida + variantes realmente usadas
- `UsedHydeExpansion` / `HydeVariant` — evidencia quando houve fallback HyDE por baixo recall
- `UsedSemanticCompression` / `SemanticSummary` — evidência de compressão quando o contexto excede o budget

---

## 5. Memória — Obsidian Vault

**Arquivo**: `src/AgenticSystem.Infrastructure/Sync/FileObsidianSync.cs`
**Interface**: `IObsidianSync`

### 5.1 Estrutura do Vault

```
vault/
├── sessions/
│   └── {sessionId}/
│       └── {timestamp}_{agentName}.md    ← Eventos de sessão
├── agents/
│   └── {agentName}.md                    ← Definições de agents
└── knowledge/                            ← Conhecimento indexado
```

### 5.2 Formato dos Arquivos

Cada arquivo segue formato **Obsidian-compatible** com YAML frontmatter:

```markdown
---
id: {event.Id}
session: {sessionId}
agent: {agentName}
tier: {tier}
timestamp: {ISO 8601}
tags: [tag1, tag2]
---

# Session Event — {AgentName}

## Input
```
{userInput}
```

## Response
{agentResponse}

## Actions
- action1
- action2

## Tools Used
- tool1
```

### 5.3 Indexação Vetorial

Cada evento salvo é **automaticamente indexado** no `IVectorStore`:

```csharp
var doc = new EmbeddingDocument
{
    Id = agentEvent.Id,
    Content = $"{agentEvent.UserInput}\n\n{agentEvent.AgentResponse}",
    Type = "session_event",
    Collection = "sessions",
    Metadata = { ["agent"], ["session"], ["tier"] }
};
await _vectorStore.UpsertAsync(doc);
```

Isso permite que o **RAG** busque eventos passados via retrieval strategy `Episodic`.

### 5.4 Session Consolidation

O `SessionManager` consolida sessões via `ISessionConsolidator`:

1. `SummarizeSessionAsync()` — resume a sessão
2. `ExtractInsightsAsync()` — extrai insights
3. `ISemanticCompressor.CompressSessionAsync()` (GAP-10) — comprime semanticamente

### 5.5 Agent Memory Per-Agent

Além da memória de sessão, o runtime agora mantém memória persistente por agente/usuário:

- `AgentMemoryService` grava fatos, correções e regras aprendidas após execuções relevantes
- `InMemoryAgentMemoryStore` é o default para dev/test
- `EfAgentMemoryStore` usa `AgenticDbContext` quando o runtime é configurado com EF/PostgreSQL
- `BaseAgent` consulta as memórias mais relevantes e injeta o resultado no system prompt antes de chamar o LLM
- A seleção atual usa relevância heurística por termos, recência, confiança e uso acumulado

---

## 6. MCP — Model Context Protocol

**Diretório**: `src/AgenticSystem.Infrastructure/MCP/`

O MCP agora funciona em dois sentidos:

- **Client mode** para carregar tools externas dinamicamente via servidores MCP (stdio ou SSE/HTTP)
- **Server mode** para expor capabilities do próprio AgenticSystem em `/mcp` para outros agentes e clientes MCP autenticados

### 6.1 Arquitetura

```
┌─────────────────────────────────────────────────┐
│ MCPPluginManager (IMCPPluginManager)             │
│   ├─ LoadPluginAsync(config)                     │
│   ├─ UnloadPluginAsync(id)                       │
│   ├─ ExecutePluginToolAsync(id, tool, params)    │
│   └─ GetAllToolDetailsAsync()                    │
│                                                  │
│   ┌─────────────────────────────┐                │
│   │ McpClientPlugin             │ × N plugins    │
│   │   ├─ IMcpClient (SDK)       │                │
│   │   ├─ Transport: stdio | SSE │                │
│   │   ├─ ProvidedTools[]        │                │
│   │   ├─ ProvidedResources[]    │                │
│   │   └─ Status: Starting/      │                │
│   │     Running/Stopped/Error   │                │
│   └─────────────────────────────┘                │
│                                                  │
│ McpToolsAIFunctionAdapter                        │
│   └─ Converte MCP tools → AIFunction (M.E.AI)   │
│      para function calling nativo                │
└─────────────────────────────────────────────────┘
```

### 6.2 Lifecycle do Plugin

```
MCPPluginConfig
    │
    ▼
McpClientPlugin.InitializeAsync()
    ├─ CreateTransport() (stdio ou SSE)
    ├─ McpClientFactory.CreateAsync()
    ├─ ListToolsAsync() → _tools[]
    ├─ ListResourcesAsync() → _resources[]
    └─ Status = Running
    │
    ▼
ExecuteToolAsync(toolName, params)
    ├─ Verifica status e tool existence
    ├─ _client.CallToolAsync()
    └─ Retorna MCPResponse { Success, Data, Metadata }
    │
    ▼
ShutdownAsync()
    └─ DisposeAsync (IAsyncDisposable)
```

### 6.3 MCP → Agent Framework Bridge

O `McpToolsAIFunctionAdapter` converte tools MCP em `AIFunction` (M.E.AI):

- Cada tool MCP vira uma `AIFunction` com nome qualificado `{pluginName}_{toolName}`
- Injetado em `ChatClientAgent` via `AgentFrameworkFactory`
- O `FunctionInvokingChatClient` do pipeline M.E.AI auto-invoca essas funções

### 6.4 MCP Server Mode (`/mcp`)

**Arquivos**:
- `src/AgenticSystem.Api/Program.cs`
- `src/AgenticSystem.Api/MCP/AgenticMcpTools.cs`

O host ASP.NET também expõe um endpoint MCP autenticado:

```csharp
builder.Services.AddMcpServer()
    .WithHttpTransport(options =>
    {
        options.IdleTimeout = TimeSpan.FromMinutes(30);
    })
    .WithTools<AgenticMcpTools>();

app.MapMcp("/mcp").RequireAuthorization();
```

Tools expostas inicialmente:

- `list_agents` — inventário dos agents ativos do runtime
- `search_knowledge` — execução do pipeline RAG com query variants, ranking e contexto consolidado
- `list_runtime_tools` — inventário de tools internas e tools vindas de plugins MCP carregados
- `execute_agent` — execução autenticada do `IMetaAgent`, com roteamento automático ou target agent explícito

Esse modo fecha o loop bidirecional do subsistema MCP: o AgenticSystem continua consumindo plugins externos, mas também passa a ser consumível como servidor MCP por outros agentes.

---

## 7. Multi-Tenant

**Arquivos**:
- `src/AgenticSystem.Api/Middleware/TenantMiddleware.cs`
- `src/AgenticSystem.Core/Models/Tenant.cs`, `TenantConfig.cs`
- `src/AgenticSystem.Core/Services/TenantResolver.cs`

### 7.1 Fluxo de Resolução

```
HTTP Request
    │
    ▼
TenantMiddleware.InvokeAsync()
    │
    ├─ 1. JWT claim "tenant_id"        ← Prioridade 1
    ├─ 2. Header "X-Tenant-Id"         ← Prioridade 2
    ├─ 3. Sem tenant                    ← Fallback
    │
    ▼
ITenantResolver.ResolveAsync(tenantId)
    │
    ▼
TenantContext (scoped DI)
    ├─ TenantId
    ├─ TenantName
    ├─ Plan
    ├─ Limits (MaxRequestsPerMinute, etc.)
    └─ IsAuthenticated
```

### 7.2 Regras de Segurança

- **Endpoints protegidos** (com `[Authorize]`): request sem tenant → **403 Forbidden**
- **Endpoints anônimos**: tenant é opcional, processamento segue sem contexto de tenant
- `TenantContext` é **scoped** no DI — vive durante toda a request

### 7.3 Rate Limiting por Tenant

O endpoint `/api/chat` implementa rate limiting por tenant via sliding window:

```csharp
var maxPerMinute = tenantContext.Limits?.MaxRequestsPerMinute ?? 30;
// Window de 1 minuto, prune automático de entries antigas
```

---

## 8. Autenticação (MultiAuth)

**Arquivos**: `src/AgenticSystem.Api/Auth/`

### 8.1 Esquema MultiAuth

O sistema usa **Policy Scheme** para suportar dois mecanismos simultaneamente:

```
Request
    │
    ├─ Header "Authorization: Bearer {token}"  → JwtTenantAuthenticationHandler
    │
    └─ Header "X-Api-Key: {key}"               → ApiKeyAuthenticationHandler
```

Configuração em `Program.cs`:

```csharp
.AddPolicyScheme("MultiAuth", "ApiKey or JWT", options =>
{
    options.ForwardDefaultSelector = context =>
    {
        if (context.Request.Headers.ContainsKey("Authorization"))
            return JwtTenantAuthenticationHandler.SchemeName;
        return ApiKeyAuthenticationHandler.SchemeName;
    };
});
```

### 8.2 ApiKey Authentication

- Header: `X-Api-Key`
- Valida contra `AgenticSystem:AdminApiKey` (appsettings)
- Usa **constant-time comparison** (`CryptographicOperations.FixedTimeEquals`) — previne timing attacks
- Claims gerados: `Name=admin`, `Role=Admin`, `tenant_id=default`

### 8.3 JWT Authentication

- Header: `Authorization: Bearer {token}`
- Valida assinatura com `SymmetricSecurityKey`
- **Requer** claim `tenant_id` — rejeita tokens sem ele
- Clock skew: 2 minutos
- Configurável: `Issuer`, `Audience`, `SecretKey`
- Em Development: usa chave padrão se não configurada

---

## 9. SignalR — Comunicação Real-Time

**Arquivos**: `src/AgenticSystem.Api/Hubs/`

### 9.1 ChatHub (`/hubs/chat`)

Hub para interação em tempo real com agents:

| Método Server | Descrição |
|---------------|-----------|
| `SendMessage(message, targetAgent?, provider?, model?, apiKey?)` | Processa mensagem via MetaAgentOrchestrator com seleção opcional de IA |

| Evento Client | Payload |
|---------------|---------|
| `ProcessingStarted` | `{ timestamp }` |
| `ReceiveMessage` | `{ content, agentName, agentTier, actions, tools, success, sessionId, timestamp }` |
| `ReceiveError` | `{ error, timestamp }` |
| `Connected` | `{ connectionId, timestamp }` |

Fluxo:
1. Client conecta → recebe `Connected`
2. Client envia `SendMessage` → recebe `ProcessingStarted`
3. MetaAgentOrchestrator processa (mesma lógica do endpoint REST)
4. Client recebe `ReceiveMessage` ou `ReceiveError`

### 9.2 GatewayHub (`/hubs/gateway`)

Hub para monitoramento do Service Gateway em tempo real:

| Método Server | Descrição |
|---------------|-----------|
| `GetDashboard()` | Retorna dashboard completo |
| `GetServiceStatus(serviceName)` | Status de um serviço |
| `SubscribeToService(serviceName)` | Inscreve em grupo de atualizações |
| `UnsubscribeFromService(serviceName)` | Remove inscrição |

| Evento Client | Payload |
|---------------|---------|
| `DashboardUpdate` | Dashboard completo do gateway |
| `ServiceStatusChanged` | Status individual de serviço |
| `Error` | Mensagem de erro |

---

## 10. Service Gateway

**Diretório**: `src/AgenticSystem.Infrastructure/Gateway/`

### 10.1 Arquitetura

```
┌─────────────────────────────────────────┐
│ ServiceGateway (IServiceGateway)         │
│                                          │
│ ExecuteAsync<T>(service, action, ct)     │
│   ├─ 1. Verifica serviço registrado      │
│   ├─ 2. Verifica enabled                 │
│   ├─ 3. CircuitBreaker.AllowRequest()    │
│   ├─ 4. RateLimiter.AllowRequest()       │
│   ├─ 5. Executa action (com Stopwatch)   │
│   ├─ 6. Registra sucesso/falha           │
│   └─ 7. CostTracker.RecordCost()         │
│                                          │
│ Components:                              │
│   ├─ CircuitBreaker (per service)        │
│   ├─ RateLimiter (per service)           │
│   └─ CostTracker (global)               │
└─────────────────────────────────────────┘
```

### 10.2 Circuit Breaker

- **Closed** → permite requests, registra falhas
- **Open** → bloqueia requests (após N falhas consecutivas)
- **Half-Open** → permite 1 request de teste
- Configurável por serviço via `ServiceRegistration.CircuitBreaker`

### 10.3 Monitoramento

- `GetHealthReportAsync()` — saúde de todos os serviços
- `GetCostReportAsync(range?)` — custos por período
- `GetAllServicesStatusAsync()` — status individual
- `GetServicesByCategoryAsync(category)` — filtro por categoria

---

## 11. Frontend — SPA React

**Diretório**: `frontend/`

### 11.1 Stack

| Tecnologia | Versão | Papel |
|-----------|--------|-------|
| React | 19.x | UI framework |
| Vite | 8.x | Build + HMR + dev server |
| TypeScript | 6.x | Tipagem estática |
| TailwindCSS | v4 | Estilização utility-first |
| @microsoft/signalr | 10.x | Comunicação real-time (WebSocket) |
| react-router-dom | 7.x | SPA routing |
| react-markdown | 10.x | Renderização markdown nas respostas |
| lucide-react | 1.x | Ícones SVG |
| CVA + clsx + twMerge | — | Padrão shadcn/ui para variantes de componentes |

### 11.2 Arquitetura

```
┌──────────────────────────────────────────────────────┐
│ Browser (SPA)                                        │
│                                                      │
│  ┌─────────┐  ┌───────────────┐  ┌────────────────┐ │
│  │  Router  │→ │   Layout      │→ │  Pages         │ │
│  │ (App.tsx)│  │ Sidebar+      │  │ Chat, Agents,  │ │
│  │         │  │ StatusBar     │  │ Dashboard, ... │ │
│  └─────────┘  └───────────────┘  └───────┬────────┘ │
│                                          │          │
│  ┌──────────────────────┐  ┌─────────────┴────────┐ │
│  │  Custom Hooks         │  │  Shared Components   │ │
│  │ useChat, useAgents,   │  │ Badge, Toast,        │ │
│  │ useDashboard, ...     │  │ ConfirmModal, Loading │ │
│  └──────────┬───────────┘  └──────────────────────┘ │
│             │                                        │
│  ┌──────────┴───────────┐                            │
│  │  Lib Layer            │                            │
│  │ api.ts — REST client  │                            │
│  │ signalr.ts — ChatHub  │                            │
│  │ signalr-gateway.ts    │                            │
│  └──────────┬───────────┘                            │
└─────────────┼────────────────────────────────────────┘
              │ Vite proxy
              ▼
    Backend (.NET 10)
      /api/*  → REST
      /hubs/* → WebSocket (SignalR)
```

### 11.3 Rotas

| Rota | Componente | Descrição |
|------|-----------|-----------|
| `/` | `ChatPage` | Chat principal (roteamento automático de agent) |
| `/chat/:agentName` | `AgentChatPage` | Chat dedicado a um agent específico |
| `/dashboard` | `DashboardPage` | Métricas, saúde, custos — auto-refresh 30s |
| `/agents` | `AgentsPage` | CRUD de agents com filtro por Tier |
| `/tools` | `ToolsPage` | Listagem e execução de tools |
| `/skills` | `SkillsPage` | Gestão de skills por domain |
| `/rag` | `RAGPage` | Pipeline RAG & knowledge base (placeholder) |
| `/gateway` | `ServicesPage` | Status dos serviços externos |
| `/gateway/health` | `HealthPage` | Health checks detalhados |
| `/costs` | `CostsPage` | Custos por provider/model/serviço |
| `/ai` | `ProvidersPage` | Gestão dedicada de IAs: catálogo de providers + IA default do chat |
| `/providers` | `Navigate` | Rota legada redirecionada para `/ai` |
| `/plugins` | `PluginsPage` | Plugins MCP — carregar/remover/inspecionar |
| `/scheduled-tasks` | `ScheduledTasksPage` | Tarefas agendadas & trigger rules |
| `/config` | `SettingsPage` | Configurações do sistema |
| `/config/advanced` | `ConfigAdvancedPage` | Config avançada |
| `/embedding-migration` | `EmbeddingMigrationWizard` | Wizard administrativo ativo de migração de embeddings |

### 11.4 Comunicação em Tempo Real (SignalR)

O frontend conecta a dois SignalR Hubs:

#### ChatHub (`/hubs/chat`)

```typescript
// signalr.ts — Singleton connection com auto-reconnect
const connection = new HubConnectionBuilder()
  .withUrl('/hubs/chat')
  .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
  .build()
```

**Eventos recebidos**:
- `ReceiveMessage` → resposta do agent (content, agentName, agentTier, tools, actions)
- `ProcessingStarted` → indicador de "pensando"
- `ReceiveError` → erro do backend
- `Connected` → confirmação com connectionId

**Envio**: `conn.invoke('SendMessage', text, targetAgent, provider, model, apiKey)`

**Fallback**: Se o SignalR estiver desconectado, o `useChat` faz POST em `/api/chat` propagando `provider` e `model` como REST fallback.

#### GatewayHub (`/hubs/gateway`)

```typescript
// signalr-gateway.ts — Monitoramento de serviços
const connection = new HubConnectionBuilder()
  .withUrl('/hubs/gateway')
  .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
  .build()
```

Usado para atualizações em tempo real de status de serviços no Dashboard e Gateway pages.

### 11.5 Camada de API (REST)

O `api.ts` encapsula todas as chamadas REST em objetos tipados:

| Client | Endpoints | Usado por |
|--------|----------|-----------|
| `agentApi` | `/api/agent/agents/*` | `useAgents` |
| `toolApi` | `/api/agent/tools/*` | `useTools` |
| `skillApi` | `/api/agent/skills/*` | `useSkills` |
| `sessionApi` | `/api/agent/sessions/*` | `useChat` |
| `gatewayApi` | `/api/admin/gateway/*` | `useDashboard`, `useGatewayServices` |
| `llmApi` | `/api/admin/llm/configuration`, `/api/admin/llm/providers/*`, `/api/admin/llm/default-selection` | `useLLMProviders`, `useChat` |
| `pluginApi` | `/api/admin/plugins/*` | `usePlugins` |
| `settingsApi` | `/api/admin/settings/*` | `useSettings` |
| `scheduledTasksApi` | `/api/admin/scheduled-tasks/*` | `ScheduledTasksPage` |

Base URL configurável via `VITE_API_BASE_URL`. Em dev, o proxy do Vite roteia `/api/*` e `/hubs/*` para `https://localhost:5001`.

### 11.6 Custom Hooks

Cada domínio tem um hook que encapsula: estado, loading, error, e métodos de mutação.

| Hook | Responsabilidade |
|------|-----------------|
| `useChat(targetAgent?)` | Conexão SignalR, envio de mensagens, fallback REST, session state e seleção de provider/modelo |
| `useAgents()` | CRUD de agents, listagem, filtros |
| `useDashboard(pollInterval?)` | Polling automático do dashboard (default 30s) |
| `usePlugins()` | Load/delete plugins MCP, listagem de tools |
| `useLLMProviders()` | Gestão de providers LLM e da IA default exibida no chat |
| `useGatewayServices()` | Status de serviços do gateway |
| `useSettings()` | Configurações do sistema |
| `useSkills()` | Listagem de skills |
| `useTools()` | Listagem e execução de tools |

### 11.7 Configuração de IA e Chat

- A rota `/ai` concentra a administração de providers e a definição do provider/modelo default do chat.
- A rota legada `/providers` apenas redireciona para `/ai` para preservar compatibilidade.
- `AISelectorBar` aparece em `ChatPage` e `AgentChatPage` com duas ações: selecionar provider/modelo e navegar para a tela de configuração.
- A última seleção do usuário fica em `localStorage` (`agentic.chat.provider` e `agentic.chat.model`) e é reconciliada com a configuração default carregada do backend.

### 11.8 Componentes Notáveis

#### MessageBubble

Renderiza mensagens do chat com:
- **Markdown** (via `react-markdown`) para respostas do agent — com sanitização (bloqueia `script`, `iframe`, `object`, `embed`, `form`)
- **Badges** de tier coloridos (Chief=violeta, Master=azul, Specialist=verde, Support=âmbar)
- **Tags** de actions (⚡) e tools (🔧) usados pelo agent
- Timestamp no hover

#### DashboardPage

Dashboard operacional com:
- **MetricsRow**: Total requests, taxa de sucesso, falhas, latência média
- **HealthCard**: Serviços saudáveis vs problemáticos com drill-down
- **CostCard**: Budget diário, progress bar, top-5 serviços por custo
- **ServicesTable**: Tabela completa com circuit state, success rate, custo
- Auto-refresh via `useDashboard(30000)`

#### PluginsPage

Gestão de plugins MCP:
- Cards com status de conexão, contagem de tools, transporte (stdio/sse)
- Modal para carregar novos plugins (path, command, args, URL)
- Listagem consolidada de todas as tools MCP

### 11.8 Design System

- **Dark mode only** — paleta zinc-950/900/800 com acentos violeta-600
- **TailwindCSS v4** com `@tailwindcss/vite` plugin
- Padrão **shadcn/ui** (sem lib — copia componentes): CVA para variantes, `cn()` helper (`clsx` + `twMerge`)
- Sidebar colapsável com ícones Lucide
- Modais com overlay + backdrop
- Toast notifications via Context Provider

### 11.9 Testes

| Tipo | Framework | Diretório |
|------|----------|-----------|
| E2E API | Cypress 13+ | `frontend/cypress/e2e/` |
| Performance | K6 | `frontend/k6/` |

**Cenários Cypress**:
- `agents.api.cy.js` — CRUD de agents via API
- `gateway.api.cy.js` — Dashboard e serviços
- `plugins.api.cy.js` — Gestão de plugins MCP

**K6**: `gateway-load-test.js` — teste de carga no gateway

### 11.10 Build & Dev

```bash
# Desenvolvimento (HMR + proxy)
npm run dev          # http://localhost:5173

# Produção
npm run build        # tsc -b && vite build → dist/
npm run preview      # Serve build local
```

O `vite.config.ts` configura:
- **Alias**: `@` → `./src`
- **Proxy**: `/api` → backend, `/hubs` → backend (WebSocket, insecure ok para dev)
- **Plugins**: `@vitejs/plugin-react` + `@tailwindcss/vite`

---

## 12. Over-Engineering Check

Avaliação objetiva dos principais hotspots de complexidade excessiva no runtime atual.

### 12.1 Findings (por severidade)

| Severidade | Hotspot | Evidência | Impacto |
|:--:|---|---|---|
| Alta | Construtores muito extensos no pipeline central | `MetaAgentOrchestrator` + `AgentExecutionWorkflow` concentram muitas dependências opcionais e condicionais | Dificulta testes focados, aumenta risco de regressão e onboarding lento |
| Alta | Sobreposição de responsabilidade entre fachada e workflow | Persistência/eventos aparecem em mais de um ponto de coordenação | Risco de duplicidade de eventos/artefatos e fluxo difícil de rastrear |
| Média | Granularidade de ML acima do necessário para manutenção | Muitas capacidades pequenas com boundaries próximos | Custo de documentação e governança cresce mais que o ganho funcional |
| Média | Crescimento de contratos com baixa coesão | Interface count elevado no Core para operações correlatas | Complexidade acidental e maior acoplamento por DI |
| Baixa | Múltiplos caminhos de execução sync/stream/direct | Quatro entradas similares com regras quase iguais | Repetição de lógica e divergência de comportamento ao longo do tempo |

### 12.2 Recomendações de simplificação

1. Consolidar dependências por facetas (`ExecutionPolicies`, `ExecutionObservability`, `ExecutionGuards`) para reduzir acoplamento explícito em construtores.
2. Definir fronteira única de persistência operacional: workflow grava artefatos; fachada apenas abre/fecha escopo.
3. Agrupar MLs operacionais de runtime em blocos de governança para reduzir dispersão documental.
4. Introduzir testes de contrato para garantir paridade entre fluxo sync e stream.
5. Padronizar uma matriz de ownership por capability para evitar overlap entre serviços de coordenação.

---

## 13. Scheduler — Task Chaining DAG-lite

**Arquivos**: `src/AgenticSystem.Core/Services/ScheduledTaskManager.cs`, `src/AgenticSystem.Core/Interfaces/IScheduledTaskServices.cs`, `src/AgenticSystem.Core/Models/MaturityModels.cs`

O scheduler agora suporta encadeamento de tarefas sem trocar a arquitetura base de CRON/intervalo. O desenho adotado foi um **DAG-lite**: simples o bastante para caber no runtime atual, mas já útil para fluxos do tipo "A termina, libera B; B libera C".

### 13.1 Modelo de dependência

- `ScheduledTask.DependencyTaskIds`: predecessores obrigatórios.
- `ScheduledTask.ContinuationTaskIds`: sucessores liberados quando a tarefa atual conclui com sucesso.
- `IScheduledTaskManager.LinkTasksAsync(predecessorTaskId, successorTaskId)`: cria a relação entre tasks já registradas.

### 13.2 Regras de execução

- Tasks com dependências continuam `Active`, mas ficam com `NextRunAt = null` até que todos os predecessores tenham executado com sucesso.
- `ExecuteAsync()` recusa execução manual prematura quando ainda existem dependências pendentes.
- Quando a execução conclui com sucesso, o manager reavalia as continuações e agenda imediatamente as que ficaram prontas.
- Tasks dependentes não entram em loop periódico automático; elas são rearmadas pela cadeia de predecessores.

### 13.3 Garantias operacionais

- `LinkTasksAsync()` impede auto-dependência (`A -> A`).
- O manager faz busca de alcançabilidade pelas continuações existentes e bloqueia links que introduziriam ciclos.
- `RemoveAsync()` limpa referências órfãs em outras tasks para manter o grafo consistente.

### 13.4 Escopo atual

- Resolve encadeamento e dependências multi-predecessor dentro do scheduler in-memory atual.
- Não adiciona visualização de grafo, persistência relacional dedicada ou políticas avançadas de fan-out/fan-in.

---

## 14. Agent Memory & Self-Improvement

**Arquivos**: `src/AgenticSystem.Core/Services/AgentMemoryService.cs`, `src/AgenticSystem.Core/Agents/BaseAgent.cs`, `src/AgenticSystem.Core/Services/AgentExecutionWorkflow.cs`, `src/AgenticSystem.Core/Services/CorrectionLoopService.cs`

O runtime agora diferencia dois níveis de aprendizado:

- memória episódica/per-agent para reuso entre sessões;
- autoajuste de comportamento a partir de reflexões críticas.

### 14.1 Agent Memory Service

- `AgentMemoryService.RecordInteractionAsync()` registra memórias do tipo `Fact`, `LearnedRule`, `Correction` e `Reflection`.
- As memórias são indexadas por `userId + agentName`.
- Cada entrada mantém `Confidence`, `LastUsedAt`, `UsageCount`, `Keywords` e `Metadata`.

### 14.2 Injeção no Prompt

- `BaseAgent.BuildSystemPromptAsync()` consulta `IAgentMemoryService`.
- Quando existem memórias relevantes, o prompt recebe a seção `## Agent Memory`.
- Isso permite que o mesmo agent carregue preferências, correções e padrões aprendidos entre sessões sem depender apenas do histórico corrente.

### 14.3 Self-Improvement Heurístico

- `ReflectionEngine` continua gerando `LessonsLearned` e `ImprovementSuggestion`.
- `AgentExecutionWorkflow` converte sugestões críticas em regras automáticas via `ICorrectionLoop.AddRuleAsync(..., scope: "auto-reflection")`.
- `CorrectionLoopService` agora deduplica regras ativas para evitar multiplicação de instruções iguais.

---

## 15. Tool Registry & Local Baseline

**Arquivos**: `src/AgenticSystem.Core/Services/InMemoryToolManager.cs`, `src/AgenticSystem.Core/Models/ToolVariantModels.cs`, `src/AgenticSystem.Core/Tools/BuiltInTools.cs`, `src/AgenticSystem.Infrastructure/AI/ToolAIFunctionFactory.cs`, `src/AgenticSystem.Infrastructure/MCP/McpToolsAIFunctionAdapter.cs`, `src/AgenticSystem.Infrastructure/Sync/FileObsidianSync.cs`

### 15.1 Tool Versioning / A-B

- `InMemoryToolManager` agora mantém `ToolRegistration` por logical tool id.
- `RegisterTool()` segue compatível e registra a variante default `1.0.0`.
- `RegisterToolVariant()` permite múltiplas versões, nome de variante e rollout percentual.
- `ExecuteToolAsync()` resolve explicitamente por `toolVersion` / `toolVariant` ou faz seleção determinística por usuário/sessão para A/B.

### 15.2 Baseline Local de Tools e Integrações

O runtime atual não mantém mais uma pasta `Integrations/` com providers locais dedicados. O baseline funcional para dev/local está distribuído entre o catálogo interno de tools, a superfície MCP e o sync do vault:

- `DateTimeTool`, `CalculatorTool` e `FileSearchTool` no Core como ferramentas internas always-on.
- `HttpTool` registrado via DI para chamadas HTTP controladas a serviços externos.
- `UnifiedAIToolProvider` + `ToolAIFunctionFactory` para expor tools internas e MCP como `AIFunction` no runtime hosted.
- `FileObsidianSync` para sincronização do vault legível em disco com a camada semântica.
- `MCPPluginManager` / `McpToolsAIFunctionAdapter` para plugins MCP conectados ao runtime.

Esse conjunto substitui a antiga camada de providers locais nomeados e funciona como baseline operacional antes de integrações externas específicas.

---

## Apêndice: Mapa de Arquivos

| Camada | Diretório | Responsabilidade |
|--------|-----------|------------------|
| **Api** | `Controllers/` | REST endpoints |
| **Api** | `Hubs/` | SignalR real-time |
| **Api** | `Auth/` | MultiAuth (ApiKey + JWT) |
| **Api** | `Middleware/` | Tenant resolution |
| **Core** | `Agents/` | BaseAgent, Master/Specialist/Support |
| **Core** | `Interfaces/` | Contratos de runtime, memória, tools, channels e integrações |
| **Core** | `Services/` | MetaAgentOrchestrator (fachada), AgentExecutionWorkflow (casca fina), SessionManager, SmartRouter, AgentMemoryService, etc. |
| **Core** | `LLM/` | Interfaces e modelos LLM |
| **Core** | `Models/` | DTOs, entidades, agent memory, tool variants e channel messages |
| **Core** | `Skills/` | BuiltInSkills |
| **Infra** | `Skills/` | DeclarativeSkill, DynamicSkillCatalogHostedService |
| **Core** | `Tools/` | BuiltInTools |
| **Infra** | `AgentFramework/` | Microsoft Agent Framework integration + FrameworkAgentChannelService |
| **Infra** | `AI/` | ChatClientPlanner, ToolAIFunctionFactory |
| **Infra** | `LLM/` | LLMManager, Providers (OpenAI/Ollama/Gemini/Claude) |
| **Infra** | `MCP/` | MCPPluginManager, McpClientPlugin, McpToolsAIFunctionAdapter |
| **Infra** | `RAG/` | RAGService, HeuristicReRanker, LlmReRanker, QueryCompressorService, SemanticCompressorService, LocalOnnxCrossEncoderReRankerProvider, JinaReRankerProvider, dedicated rerank provider abstraction |
| **Infra** | `Gateway/` | ServiceGateway, CircuitBreaker, RateLimiter, CostTracker |
| **Infra** | `Sync/` | FileObsidianSync (Obsidian Vault) |
| **Infra** | `Persistence/` | PostgreSQL stores (EF Core + Npgsql), EfAgentMemoryStore |
| **Infra** | `Embeddings/` | EmbeddingService, MultiProviderEmbeddingService |
| **Infra** | `Documents/` | DocumentPipeline, DocumentParser |
| **Frontend** | `frontend/src/components/` | React pages, modais e shared UI |
| **Frontend** | `frontend/src/hooks/` | Custom hooks (useChat, useAgents, useDashboard, etc.) |
| **Frontend** | `frontend/src/lib/` | API client, SignalR connections, utils |
| **Frontend** | `frontend/src/types/` | TypeScript types (mirror dos DTOs backend) |
| **Frontend** | `frontend/cypress/` | Testes E2E de API |
| **Frontend** | `frontend/k6/` | Testes de performance |

## Apêndice: Maturity Levels (ML24–ML33)

| ML | Nome | Serviços | Categoria |
|----|------|----------|-----------|
| ML24 | Quality Gates Pipeline | `IQualityGateService`, `InputValidationGate`, `ResponseQualityGate` | Observability & Self-Healing |
| ML25 | Agent Cleanup | `AgentCleanupHostedService` | Observability & Self-Healing |
| ML26 | Vision (Image Analysis) | `IVisionProvider`, `OpenAIVisionProvider` | Vision |
| ML27 | MCP Plugin System | `IMCPPluginManager`, `McpClientPlugin`, `McpToolsAIFunctionAdapter` | MCP & Extensibility |
| ML28 | Storage Abstraction | `IStorageProvider`, `StorageFile` | MCP & Extensibility |
| ML29 | Agent Execution Workflow | `IAgentExecutionWorkflow`, `AgentExecutionWorkflow` | Agent Runtime Platform (thin execution shell) |
| ML30 | End-to-End Streaming Runtime | `IAgentRuntimeCoordinator`, `ChatHub`, `POST /api/chat/stream` | Agent Runtime Platform |
| ML31 | Governed Capabilities | `IToolGovernanceService`, approvals de tool | Agent Runtime Platform |
| ML32 | Operational Artifacts & Metrics | `AgentExecutionArtifact`, `AgentRuntimeMetricsSnapshot`, `RuntimeEvaluationResult` | Agent Runtime Platform |
| ML33 | Final Human Approval | `IFinalResponseApprovalService`, endpoints `final-approvals` | Agent Runtime Platform |

> MLs anteriores (ML1–ML23) documentados em [USER-STORIES.md](USER-STORIES.md). Total: **33 MLs**, **53 serviços**, **549+ testes**.
