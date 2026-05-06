# Backend AgenticSystem — Arquitetura Atual Revalidada

> **Documento de referência** que descreve como o backend funciona no estado atual, revalidado contra o código e contra o [plano de migração framework-first](./framework-first-migration-plan.md).
>
> O sistema já roda framework-first no fluxo principal, com surface hosted do MAF, workflows nativos no slice colaborativo e protocol hosting ativo. Ainda existem dívidas locais abertas: uma cadeia local de composição do orquestrador no runtime hosted (`OrchestratorContextResolver` + `OrchestratorContextFactory`) e middleware de reflection/quality gates via extensões locais.

---

## Sumário

1. [Visão Geral da Arquitetura](#1-visão-geral-da-arquitetura)
2. [Stack Tecnológico](#2-stack-tecnológico)
3. [Camadas e Responsabilidades](#3-camadas-e-responsabilidades)
4. [Inicialização e Registro de Dependências](#4-inicialização-e-registro-de-dependências)
5. [Padrão de Orquestração — Supervisor-with-Tools](#5-padrão-de-orquestração--supervisor-with-tools)
6. [Fluxos de Request](#6-fluxos-de-request)
7. [RAG Pipeline — MessageAIContextProvider + AIFunction](#7-rag-pipeline--messageaicontextprovider--aifunction)
8. [Middleware Pipeline — Reflection e QualityGates](#8-middleware-pipeline--reflection-e-qualitygates)
9. [Workflow de Colaboração — AgentWorkflowBuilder](#9-workflow-de-colaboração--agentworkflowbuilder)
10. [Gestão de Sessões — ISessionStore + ISessionManager](#10-gestão-de-sessões--isessionstore--isessionmanager)
11. [Sistema de Tools — MCP, Built-in e AIFunction](#11-sistema-de-tools--mcp-built-in-e-aifunction)
12. [Ciclo de Vida dos Agentes](#12-ciclo-de-vida-dos-agentes)
13. [Multi-Tenant](#13-multi-tenant)
14. [Autenticação e Segurança](#14-autenticação-e-segurança)
15. [Protocol Hosting — A2A, AG-UI, OpenAI-Compatible](#15-protocol-hosting--a2a-ag-ui-openai-compatible)
16. [Funcionalidades Transversais](#16-funcionalidades-transversais)
17. [Observabilidade e Gateway](#17-observabilidade-e-gateway)
18. [Padrões Arquiteturais Utilizados](#18-padrões-arquiteturais-utilizados)
19. [Glossário](#19-glossário)

---

## 1. Visão Geral da Arquitetura

O AgenticSystem é uma plataforma multi-agent construída sobre .NET 10 e o **Microsoft Agent Framework (MAF) 1.4.0**. O backend expõe agentes de IA especializados por domínio (personal, work, learning, creative, finance, health, etc.) coordenados por um **orquestrador central** que usa o padrão **supervisor-with-tools**.

O LLM do orquestrador decide qual especialista chamar com base no input do usuário, sem lógica imperativa de roteamento no `ExecuteAsync` principal. Cada especialista é exposto como uma `AIFunction` do orquestrador via `AsAIFunction()`, e o fluxo colaborativo planner → executor → reviewer já usa `AgentWorkflowBuilder.BuildSequential(...)`.

```
┌──────────────────────────────────────────────────────────────────┐
│                       FRONTEND (React + Vite)                    │
│  Chat UI ←→ SignalR Hub  |  REST API  |  AG-UI Protocol         │
└────────────┬───────────────────┬───────────────────┬─────────────┘
             │                   │                   │
┌────────────▼───────────────────▼───────────────────▼─────────────┐
│                     CAMADA DE APRESENTAÇÃO                       │
│  ChatHub (SignalR)  |  Controllers (REST)  |  A2A / AG-UI Maps   │
└────────────┬───────────────────────────────────────┬─────────────┘
             │                                       │
┌────────────▼───────────────────────────────────────▼─────────────┐
│                     CORE — CASCA FINA                            │
│  MetaAgentOrchestrator → AgentExecutionWorkflow                  │
│       ↓ delega a IFrameworkOrchestratorService                   │
└────────────┬─────────────────────────────────────────────────────┘
             │
┌────────────▼─────────────────────────────────────────────────────┐
│               INFRASTRUCTURE — FRAMEWORK HOSTING                 │
│  FrameworkOrchestratorService                                    │
│       ↓                                                          │
│  AddAIAgent("Orchestrator") → surface hosted scoped             │
│       ├─ OrchestratorContext (scoped)                           │
│       ├─ OrchestratorContextResolver → OrchestratorContextFactory│
│       ├─ WithAITool(specialist_1)    ← AsAIFunction()            │
│       ├─ WithAITool(specialist_N)    ← AsAIFunction()            │
│       ├─ Tools auxiliares (RAG / Router / Analyzer)             │
│       ├─ UseAIContextProviders(RAGContextProvider)               │
│       ├─ WithSessionStore(AgentFrameworkSessionStoreAdapter)     │
│       ├─ UseReflection()          ← extensão local               │
│       └─ UseQualityGates()        ← extensão local               │
│                                                                  │
│  AddWorkflow("collaboration") → AgentWorkflowBuilder             │
│       ├─ BuildSequential([planner, executor, reviewer])          │
│       └─ AddAsAIAgent()             ← exposto como tool          │
│                                                                  │
│  Protocol Hosting:                                               │
│       ├─ AddA2AServer() / MapA2AHttpJson()                       │
│       ├─ AddAGUI() / MapAGUI()                                   │
│       └─ OpenAI-compatible via controller custom                 │
└──────────────────────────────────────────────────────────────────┘
```

---

## 2. Stack Tecnológico

| Camada | Tecnologia |
|---|---|
| **Runtime** | .NET 10 (ASP.NET Core) |
| **Framework de Agentes** | Microsoft Agent Framework (MAF) 1.4.0 + hosting A2A/AG-UI preview (`Microsoft.Agents.AI`, `Microsoft.Agents.AI.Hosting`, `Microsoft.Agents.AI.Workflows`) |
| **LLM** | `IChatClient` (Microsoft.Extensions.AI) — compatível com OpenAI, Azure OpenAI, Ollama, etc. |
| **Embeddings** | `IEmbeddingGenerator<string, Embedding<float>>` (M.E.AI) |
| **Vector Store** | In-Memory ou PostgreSQL (pgvector) |
| **Banco de dados** | PostgreSQL |
| **Real-time** | SignalR (WebSocket) |
| **Frontend** | React + TypeScript + Vite |
| **MCP** | Model Context Protocol (servidor com HttpTransport) |
| **Observabilidade** | OpenTelemetry, structured logging |
| **Autenticação** | Multi-scheme: API Key + JWT Bearer (`PolicyScheme "MultiAuth"`) |
| **Containerização** | Docker + Kubernetes |

---

## 3. Camadas e Responsabilidades

### 3.1 `AgenticSystem.Api` — Apresentação

Responsável por endpoints HTTP, SignalR hubs, autenticação, middleware de tenant e configuração da aplicação.

- **Controllers**: REST endpoints para agentes, sessões, documentos, configurações, gateway
- **Hubs**: `ChatHub` (chat real-time com streaming), `GatewayHub` (dashboard e status de serviços)
- **Middleware**: `TenantMiddleware` (resolve tenant por JWT claim ou header)
- **Auth**: `ApiKeyAuthHandler` + JWT Bearer com `PolicyScheme` para seleção automática
- **MCP Server**: servidor MCP com `HttpTransport` expondo tools do sistema
- **Protocol Maps**: `MapA2AHttpJson("/a2a")`, `MapAGUI("/agui")` e controllers custom para OpenAI-compatible

### 3.2 `AgenticSystem.Core` — Domínio

Contém interfaces, modelos, agentes base e serviços de domínio. **Não referencia o MAF diretamente** — opera via interfaces (`IFrameworkOrchestratorService`, `IAgentExecutionWorkflow`).

- **Agents**: `BaseAgent` (classe abstrata) + agentes built-in (Personal, Work, Learning, Creative, Calendar, Analysis, Notification, API, General)
- **Services**: `MetaAgentOrchestrator`, `AgentExecutionWorkflow`, `ContextAnalyzer`, `SmartRouter`, `ReflectionEngine`, `CorrectionLoopService`, `DynamicAgentService`, `ConfidenceScoreCalculator`, `SessionManager`, `ScheduledTaskManager`
- **Interfaces**: contratos para todas as funcionalidades (`IMetaAgent`, `IAgentFactory`, `IRAGService`, `ISessionManager`, `IFrameworkOrchestratorService`, etc.)

### 3.3 `AgenticSystem.Infrastructure` — Implementação

Implementa as interfaces do Core com dependências concretas (MAF, PostgreSQL, LLM providers, MCP, RAG).

- **AgentFramework/**: `FrameworkOrchestratorService`, `AgentFrameworkFactory`, `AgentFrameworkSessionStoreAdapter`, `OrchestratorContextResolver`, `OrchestratorContextFactory`, middleware wrappers
- **RAG/**: `RAGService`, `LlmReRanker`, `JinaReRankerProvider`, `LocalOnnxCrossEncoderReRankerProvider`, `SemanticCompressorService`, `QueryCompressor`
- **AI/**: `AgentCollaborationWorkflow` (wrapper para `AgentWorkflowBuilder`), `ChatClientPlanner`, `UnifiedAIToolProvider`
- **MCP/**: `McpToolsAIFunctionAdapter`, MCP client/server
- **VectorStore/**: `InMemoryVectorStore`, `PostgreSQLVectorStore`
- **Documents/**: parsers para PDF, DOCX, Markdown, TXT, HTML, JSON, YAML

---

## 4. Inicialização e Registro de Dependências

### Program.cs

```
WebApplication.CreateBuilder(args)
    │
    ├─ AddAgenticSystemCore()              ← todos os serviços do Core (DI)
    ├─ AddAgenticSystemInfrastructure()     ← todos os serviços do Infrastructure (DI)
    │   ├─ IChatClient pipeline: ContextAwareChatClient → GovernedChatClient
    │   ├─ EmbeddingGenerator (M.E.AI)
    │   ├─ AddAIAgent("Orchestrator")       ← surface hosted do orquestrador principal
    │   ├─ AddWorkflow("collaboration")     ← workflow nativo no slice colaborativo
    │   ├─ AgentFrameworkSessionStoreAdapter
    │   ├─ RAGContextProvider (MessageAIContextProvider)
    │   ├─ Middleware local: UseQualityGates()
    │   ├─ Pós-processamento compartilhado: AgentExecutionPostProcessingPipeline
    │   ├─ MCP plugins (discovery + auto-connect)
    │   ├─ Vector stores (InMemory / PostgreSQL)
    │   ├─ Document parsers
    │   └─ Obsidian sync
    │
    ├─ AddAuthentication (MultiAuth: ApiKey + JWT)
    ├─ AddSignalR
    ├─ AddSwagger (ApiKey + Bearer security)
    ├─ AddA2AServer("AgenticSystem")
    ├─ AddAGUI()
    ├─ Controllers OpenAI-compatible
    │
    ▼
app.Build()
    ├─ UseAuthentication / UseAuthorization
    ├─ UseTenantMiddleware
    ├─ MapControllers                 ← inclui /v1/chat/completions e /v1/models
    ├─ MapHub<ChatHub>("/hubs/chat")
    ├─ MapHub<GatewayHub>("/hubs/gateway")
    ├─ MapMcpServer()
    ├─ MapA2AHttpJson("AgenticSystem", "/a2a")
    └─ MapAGUI("AgenticSystem", "/agui")
```

### Registro do fluxo principal

No runtime atual, `IFrameworkOrchestratorService` faz parte do caminho principal. A referência a optional DI e fallback no plano explica a estratégia de migração, mas `AgentExecutionWorkflow.ExecuteAsync()` já delega diretamente ao orquestrador do framework e não mantém mais o fluxo anterior no método principal.

### AddAIAgent() — Hosting Nativo

```csharp
var orchestratorMetadata = OrchestratorMetadata.Default;

services.AddScoped(sp => sp.GetRequiredService<OrchestratorContextFactory>().Resolve());

services.AddAIAgent(
    orchestratorMetadata.Name,
    static (sp, _) => sp.GetRequiredService<OrchestratorContext>().OrchestratorAgent,
    ServiceLifetime.Scoped)
    .WithSessionStore(
        static (sp, _) => sp.GetRequiredService<AgentFrameworkSessionStoreAdapter>(),
        ServiceLifetime.Singleton);
```

No estado atual, a surface hosted já resolve o `AIAgent` e o `AgentSessionStore` keyed. O `OrchestratorContextFactory` lê a sessão ativa, lista os agentes, compõe o contexto final por sessão e monta o `ChatClientAgent` hospedado com AI context, quality gates, logging e OpenTelemetry; `OrchestratorAuxiliaryToolService` entrega o catálogo de tools auxiliares; e o `FrameworkOrchestratorService` fecha a resposta delegando o pós-processamento comum ao `AgentExecutionPostProcessingPipeline`. O DI já não precisa mais do wrapper `HostedOrchestratorResolution` nem de uma factory intermediária só para o hosted agent.

---

## 5. Padrão de Orquestração — Supervisor-with-Tools

### Conceito

O orquestrador é um `ChatClientAgent` cujo LLM recebe um system prompt descrevendo todos os especialistas disponíveis e seus domínios. Cada especialista é exposto como uma `AIFunction` (tool) via `AsAIFunction()`. O LLM do orquestrador decide qual tool (= especialista) chamar com base no input do usuário.

```
User: "Preciso de ajuda para organizar meu calendário e criar um relatório financeiro"

Orchestrator (LLM):
  1. Analisa input → identifica 2 domínios (calendar + finance)
  2. Chama tool "calendar_agent" com subtarefa de calendário
  3. Chama tool "finance_agent" com subtarefa de relatório
  4. Consolida respostas dos dois especialistas
  5. Retorna resposta unificada ao usuário
```

### System Prompt do Orquestrador

Gerado dinamicamente com base nos agentes ativos:

```
Você é o orquestrador central do sistema Baianinho-Labs.
Sua responsabilidade é analisar a solicitação do usuário e delegar para o especialista mais adequado.

## Regras de Delegação
1. Analise o domínio, intent e complexidade da solicitação.
2. Chame o tool do especialista mais adequado passando o input original do usuário.
3. Se a solicitação envolver múltiplos domínios, chame múltiplos especialistas e consolide.
4. Se nenhum especialista for adequado, responda diretamente.
5. Retorne a resposta do especialista ao usuário.
6. Sempre responda no mesmo idioma do usuário.

## Especialistas Disponíveis

### PersonalAgent
- Domínio: personal
- Tier: Specialist
- Descrição: Assistente pessoal para organização e tarefas do dia a dia

### WorkAgent
- Domínio: work
- Tier: Specialist
- Descrição: Assistente profissional para projetos e tarefas de trabalho

... (todos os agentes ativos)
```

### Tools Auxiliares do Orquestrador

Além dos especialistas, o orquestrador tem acesso a tools auxiliares materializadas por `OrchestratorAuxiliaryToolService` a partir da fábrica `OrchestratorAuxiliaryTools`:

| Tool | Origem | Quando é chamado |
|---|---|---|
| `retrieve_context` | `OrchestratorAuxiliaryTools.CreateRetrieveContextTool(...)` | Quando o LLM precisa de contexto adicional sob demanda |
| `route_to_best_agent` | `OrchestratorAuxiliaryTools.CreateRouteToAgentTool(...)` | Quando quer dados de performance antes de decidir |
| `analyze_request` | `OrchestratorAuxiliaryTools.CreateAnalyzeRequestTool(...)` | Quando quer análise estruturada do input |
| `collaboration_workflow` | `AgentWorkflowBuilder.AddAsAIAgent()` | Quando identifica tarefa complexa que precisa de planner-executor-reviewer |

### Diferença vs Roteamento Imperativo (Legado)

| Aspecto | Legado | Framework |
|---|---|---|
| Quem decide o agente | `ContextAnalyzer` + `SmartRouter` + `HierarchicalAgentFactory` | LLM do orquestrador |
| Como decide | Regras + ML heurístico | Tool calling nativo do LLM |
| Multi-domínio | Regras e delegação imperativas | LLM chama múltiplos tools |
| Determinismo | Alto (regras) | Baixo (LLM, mitigado por instructions) |
| Flexibilidade | Limitada pelas regras | Alta (LLM adapta-se ao contexto) |

---

## 6. Fluxos de Request

### 6.1 Fluxo Principal — Chat via SignalR (Streaming)

Este é o fluxo mais comum: usuário envia mensagem no chat, recebe resposta em streaming.

```
Frontend (React)
    │
    ▼
ChatHub.SendMessage(message, agentName?, llmPreferences?)
    │
    ├─ Resolve ClaimsPrincipal (autenticação)
    ├─ Cria UserContext (userId, role, language, preferences)
    ├─ Se agentName == null:
    │   └─ IMetaAgent.ProcessRequestStreamAsync(input, context)
    │       │
    │       ▼
    │   MetaAgentOrchestrator.ProcessRequestStreamAsync
    │       │
    │       ├─ SessionManager.StartSessionAsync(context) → sessionId
    │       ├─ AgentRuntimeCoordinator.StreamAsync(sessionId, context, executor)
    │       │   │
    │       │   ▼
    │       │ AgentExecutionWorkflow.ExecuteAsync(sessionId, input, context, ct)
    │       │   │
    │       │   ├─ (LLM scope) LLMRuntimeContextAccessor.BeginScope
    │       │   │
    │       │   └─ FrameworkOrchestratorService.ExecuteAsync(sessionId, input, context, ct)
    │       │       │
    │       │       ├─ 1. Resolver orquestrador via surface hosted + OrchestratorContext
    │       │       │      → ChatClientAgent montado por OrchestratorContextFactory
    │       │       │      → RAGContextProvider injeta RAG via MessageAIContextProvider
    │       │       │      → Middleware local: Reflection + QualityGates
    │       │       │
    │       │       ├─ 2. ISessionStore.GetSessionAsync(sessionId) → AgentSession
    │       │       │
    │       │       ├─ 3. Publica evento AgentSelected
    │       │       │
    │       │       ├─ 4. orchestrator.RunAsync(input, session)
    │       │       │      │
    │       │       │      ├─ RAGContextProvider.ProvideMessagesAsync(...)
    │       │       │      │   └─ RAGService.RetrieveContextAsync(query)
    │       │       │      │       ├─ VectorStore.SearchAsync (retrieve)
    │       │       │      │       ├─ ReRanker.ReRankAsync (rerank)
    │       │       │      │       ├─ KnowledgeFreshness (penalize stale)
    │       │       │      │       └─ SemanticCompressor (compress if large)
    │       │       │      │
    │       │       │      ├─ LLM decide qual tool chamar
    │       │       │      │   ├─ Tool: specialist_agent → AgentSession + RunAsync
    │       │       │      │   ├─ Tool: retrieve_context → busca ad-hoc
    │       │       │      │   └─ Tool: collaboration_workflow → planner→executor→reviewer
    │       │       │      │
    │       │       │      ├─ Middleware local: UseReflection()
    │       │       │      │   └─ Avalia qualidade da resposta (auto-reflexão)
    │       │       │      │
    │       │       │      └─ Middleware local: UseQualityGates()
    │       │       │          └─ Valida critérios mínimos (confiança, citação, etc.)
    │       │       │
    │       │       ├─ 5. ExtractContent(response) → string textual
    │       │       │      └─ TextContent de mensagens Assistant; fallback para .Text
    │       │       │
    │       │       ├─ 6. IdentifyCalledAgent(response, bindings) → agentName
    │       │       │      └─ FunctionCallContent → mapeia tool name → agent name
    │       │       │
    │       │       ├─ 7. ISessionStore.SaveSessionAsync(sessionId, session)
    │       │       │
    │       │       └─ 8. SessionManager.AddEventAsync (evento de negócio, sem bridge no fluxo principal)
    │       │
    │       └─ Yield AgentStreamEvents ao SignalR
    │
    └─ Cada evento é enviado ao frontend via SignalR:
        ├─ ProcessingStarted
        ├─ AgentSelected (qual agente foi escolhido)
        ├─ RagStarted / RagCompleted (se RAG foi usado)
        ├─ StreamEvent (tokens parciais)
        └─ ReceiveMessage (resposta final)
```

### 6.2 Fluxo Direto — Chat com Agente Específico

Quando o usuário seleciona um agente específico no frontend (chat dedicado):

```
Frontend → ChatHub.SendMessage(message, agentName: "WorkAgent")
    │
    ▼
MetaAgentOrchestrator.ProcessDirectRequestStreamAsync(input, context, "WorkAgent")
    │
    ▼
AgentExecutionWorkflow.ExecuteDirectAsync(sessionId, input, context, "WorkAgent", ct)
    │
    ├─ IDirectAgentRequestExecutor.ExecuteAsync(...)
    │   │
    │   ├─ Resolve agente por nome via IAgentFactory.GetAllAgentsAsync()
    │   ├─ Cria AnalysisResult mock com o agente solicitado
    │   ├─ IAgentExecutionPreProcessingPipeline.ProcessAsync(...)
    │   ├─ IAgentFactory.ResolveAgentAsync(requestedAgent) → IAgent cru
    │   ├─ IDirectAgentExecutionFactory.CreateDirectExecutionAgentAsync(agent) → AgentFrameworkAdapter
    │   ├─ Usa EffectiveInput enriquecido com correction rules ativas
    │   ├─ Publica AgentSelected event
    │   ├─ agent.ExecuteAsync(enrichedInput, context)
    │   │      └─ ChatClientAgent.RunAsync → IChatClient → LLM
    │   └─ IAgentExecutionPostProcessingPipeline.ProcessAsync(...)
    │          ├─ ReflectionEngine.ReflectAsync
    │          ├─ CorrectionLoop.AddRuleAsync (se reflection gerou suggestion)
    │          ├─ ConfidenceScoreCalculator.Calculate
    │          ├─ FinalResponseApprovalService.EvaluateAsync (se ativado)
    │          ├─ Persistência de sessão + artifacts
    │          └─ AgentMemoryService.RecordInteractionAsync
```

**Diferença chave:** `ExecuteDirectAsync` **não passa pelo orquestrador**. O agente é chamado diretamente, sem tool calling. Este é o escape hatch para o frontend controlar a seleção.

### 6.3 Fluxo REST — Controllers

```
POST /api/agents/chat    → AgentController → IMetaAgent.ProcessRequestAsync
GET  /api/agents         → AgentController → IMetaAgent.GetActiveAgentsAsync
POST /api/agents/create  → AgentController → IDynamicAgentService.HandleAgentCreationAsync
GET  /api/sessions/{id}  → SessionController → ISessionManager.GetSessionAsync
POST /api/documents      → DocumentController → Document ingestion pipeline
GET  /api/config         → SettingsController → Configuração do sistema
```

### 6.4 Fluxo de Criação Dinâmica de Agente

```
Usuário: "Crie um agente especialista em direito trabalhista"
    │
    ▼
ContextAnalyzer.AnalyzeAsync → intent = "CreateAgent"
    │
    ▼
DynamicAgentService.HandleAgentCreationAsync(input, context)
    ├─ GenerateSpecificationAsync(input) → LLM extrai spec JSON
    │   → { name: "TrabalhistaAgent", domain: "legal", tier: "Specialist", ... }
    ├─ IAgentFactory.CreateCustomAgentAsync(spec) → CustomAgent
    ├─ Próximo build de OrchestratorContext percebe a nova lista de agentes
    │   → instruções e tool bindings são recalculados para a sessão
    └─ Retorna confirmação ao usuário

Próximo request: orquestrador agora inclui "TrabalhistaAgent" como tool disponível.
```

---

## 7. RAG Pipeline — MessageAIContextProvider + AIFunction

### Conceito Dual

O RAG opera com duas abordagens complementares:

| Abordagem | Mecanismo | Quando | Determinismo |
|---|---|---|---|
| **`MessageAIContextProvider`** (primária) | Injeta contexto automaticamente a cada request | Sempre, antes de cada `RunAsync` | Determinístico |
| **`AIFunction` tool** (complementar) | LLM decide quando chamar `retrieve_context` | Sob demanda, buscas ad-hoc | Não-determinístico |

### RAGContextProvider

Implementa `MessageAIContextProvider` do MAF. Antes de cada `RunAsync`, o builder chama `ProvideMessagesAsync(...)`, que:

1. Identifica a última mensagem do usuário em `RequestMessages`
2. Chama `IRAGService.RetrieveContextAsync(query)` com a query do usuário
3. Aplica `IContextBudgetManager.TrimContextToBudgetAsync` para respeitar o budget de tokens
4. Injeta o contexto RAG como mensagem `system` com marker próprio, evitando re-injeção em loops de tool calling

```
AgentSession.Messages:
  [0] system: "Você é o orquestrador..."           ← system prompt
  [1] user: "Qual a política de férias?"            ← mensagem anterior
  [2] assistant: "A política de férias é..."        ← resposta anterior
    [3] system: "[Contexto Relevante da Base de Conhecimento]\n..."  ← INJETADO pelo provider
  [4] user: "E sobre licença maternidade?"          ← mensagem atual
```

### Pipeline RAG Completo

```
Query do usuário
    │
    ▼
QueryCompressor.CompressAsync(query)               ← otimiza query
    │
    ▼
Gera variantes de query (original + comprimida)
    │
    ▼
VectorStore.SearchAsync(variants, filters)          ← busca vetorial (pgvector / in-memory)
    │
    ├─ Se poucos resultados: HyDE variant generation
    │   └─ LLM gera documento hipotético → nova busca
    │
    ▼
Filtro por MinRelevanceScore (threshold)
    │
    ▼
ReRanker.ReRankAsync(query, chunks, topK)           ← re-ranqueamento
    │
    ├─ Dedicated provider (Jina / Local ONNX)       ← tentativa 1
    ├─ Embeddings-based scorer                       ← fallback
    └─ LLM-based scorer                             ← último recurso
    │
    ▼
KnowledgeFreshnessService.CalculateFreshnessScoreAsync   ← penaliza chunks stale
    │
    ▼
SemanticCompressor.CompressRankedChunksAsync         ← comprime se contexto > budget
    │
    ▼
RAGContext { BuiltContext, Chunks, Tokens, Strategy, ... }
```

### Fontes de Conhecimento

O RAG ingere documentos de múltiplas fontes:

| Fonte | Formato | Mecanismo |
|---|---|---|
| Upload manual | PDF, DOCX, MD, TXT, HTML, JSON, YAML | Document parsers → chunking → embedding → vector store |
| Obsidian Vault | Markdown | Sync periódico via `ObsidianVaultSyncService` |
| MCP Tools | Qualquer | Resultados de tools podem ser indexados |
| Memórias de agente | Texto | `AgentMemoryService` persiste interações relevantes |

---

## 8. Middleware e Pós-processamento

### Conceito

O MAF suporta pipeline de middleware via `AsBuilder().Use*().Build()`. No projeto atual, `UseQualityGates()` existe como **extensão local** em `AgentBuilderMiddlewareExtensions`; não é API nativa exposta pelo MAF 1.4. O request validation e a aplicação de correction rules convergiram para o `AgentExecutionPreProcessingPipeline` no Core, enquanto a fase final de reflection, confidence, final approval, persistência e agent memory converge no `AgentExecutionPostProcessingPipeline`.

### Pipeline do Orquestrador

```
Input → AgentExecutionPreProcessingPipeline.ProcessAsync() → UseQualityGates() → UseLogging() → UseOpenTelemetry() → ChatClientAgent.RunAsync()
                                                                                                      │
                                                                                                  Response bruta
                                                                                                      │
                                                                                                      ▼
                                                                                FrameworkOrchestratorService / DirectAgentRequestExecutor
                                                                                                      │
                                                                                                      ▼
                                                                                 AgentExecutionPostProcessingPipeline.ProcessAsync()
```

### AgentExecutionPostProcessingPipeline

Consolida a fase final dos dois caminhos de execução:

- Reflection com `sessionId` de negócio
- Aprendizado automático de correction rules quando há sugestão
- Confidence score e final approval
- Persistência de artifacts e memória do agent
- Reflections são persistidas no `IOperationalStore` (PostgreSQL)
- Se reflection gera `ImprovementSuggestion`, `CorrectionLoop.AddRuleAsync` é chamado automaticamente

### AgentExecutionPreProcessingPipeline

Consolida a fase de borda antes da execução nos dois caminhos principais:

- Validation do request com `IQualityGateService` quando disponível
- Fallback local para inputs vazios, muito longos ou com análise de baixa confiança
- Aplicação de correction rules ativas via `ICorrectionLoop`
- Produção de um `EffectiveInput` comum para o caminho direto e para o hosted/orchestrated path

### UseQualityGates() — extensão local

Valida critérios mínimos antes de retornar a resposta:

```csharp
gates => {
    gates.MinConfidence = 0.7;
    gates.RequireSourceCitation = true;
}
```

- **Pre-execution gates**: migraram para o `AgentExecutionPreProcessingPipeline`, compartilhado entre o fluxo direto e o hosted
- **Post-execution gates**: `ResponseQualityGate` — valida resposta (confiança mínima, citação de fontes, coerência)
- O middleware local permanece apenas como guarda de resposta; correction rules não são mais aplicadas nele

### CorrectionLoop como Complemento

`CorrectionLoopService` gerencia regras de correção persistentes por usuário/agente:

```
Regras vêm de:
  ├─ ReflectionEngine (auto-geradas por baixa confiança)
  ├─ Correções humanas (usuário corrige resposta)
  └─ Configuração manual

Regras são aplicadas:
    ├─ No `AgentExecutionPreProcessingPipeline`, antes da execução
    └─ Aprendidas de volta no `AgentExecutionPostProcessingPipeline` quando a reflection gera sugestão útil
```

---

## 9. Workflow de Colaboração — AgentWorkflowBuilder

### Quando é Ativado

O workflow de colaboração é ativado quando a tarefa é complexa e requer planejamento. Critérios:

- `analysis.Complexity == RequiresPlanning`
- `analysis.RequiresDelegation == true`
- Múltiplos domínios secundários
- Input contém palavras-chave: "plan", "etapa", "passo"

### Arquitetura com AgentWorkflowBuilder

```csharp
builder.AddWorkflow("collaboration", workflowBuilder => {
    workflowBuilder
        .BuildSequential([plannerAgent, executorAgent, reviewerAgent])
        .AddAsAIAgent();  // exposto como tool do orquestrador
});
```

### Fluxo

```
Orquestrador (LLM) detecta tarefa complexa
    │
    ▼ chama tool "collaboration_workflow"
    │
    ▼
AgentWorkflowBuilder.BuildSequential
    │
    ├─ 1. Planner Agent (ChatClientAgent)
    │      └─ Decompõe a tarefa em steps via function calling
    │         Usa ITaskPlanManager para persistir plano
    │         Output: TaskPlan { Steps[] }
    │
    ├─ 2. Executor Agent (ChatClientAgent)
    │      └─ Para cada step do plano:
    │         ├─ Resolve agente especialista via IAgentFactory
    │         ├─ Executa step com contexto do channel
    │         ├─ Persiste resultado
    │         └─ Avança plano (ITaskPlanManager.AdvanceStepAsync)
    │
    └─ 3. Reviewer Agent (ChatClientAgent)
           └─ Revisa todos os outputs dos steps
              Avalia coerência, completude e qualidade
              Pode solicitar re-execução de steps específicos
              Output: resposta consolidada final
    │
    ▼
Resposta retorna ao orquestrador → retorna ao usuário
```

### Vantagens do AgentWorkflowBuilder vs Custom

| Capacidade | Custom (legado) | AgentWorkflowBuilder |
|---|---|---|
| Checkpointing | Não | Sim (resume em caso de falha) |
| Streaming | Manual | Nativo (output de cada agent é streamado) |
| Paralelismo | Sequencial apenas | `BuildConcurrent` para steps independentes |
| Visualização | Logs | Grafo tipado com edges |
| Human-in-the-loop | Não | `RequestInfoExecutor` nativo |

### FrameworkAgentChannelService

Canal de comunicação estruturado entre agentes no workflow:

- Publica mensagens planner → specialist, handoff → target, workflow → reviewer como `AgentEvent`
- Reidrata mensagens recentes por target agent
- Constrói bloco `[Native Agent Channel Context]` antes da execução do próximo agent
- Permite que agentes compartilhem contexto sem concatenação manual de strings

---

## 10. Gestão de Sessões — ISessionStore + ISessionManager

### Duas Camadas de Sessão

| Camada | Interface | Responsabilidade |
|---|---|---|
| **Framework** | `ISessionStore` (`PostgresSessionStore`) | Persistência de `AgentSession` (chat history do framework) |
| **Negócio** | `ISessionManager` (`SessionManager`) | Eventos de negócio, consolidação, metadados, métricas |

### ISessionStore — Sessão do Framework

```csharp
public class PostgresSessionStore : ISessionStore
{
    Task<AgentSession?> GetSessionAsync(string sessionId, CancellationToken ct);
    Task SaveSessionAsync(string sessionId, AgentSession session, CancellationToken ct);
}
```

- Sessões são **agent-specific** — cada agente tem sua própria sessão (isolamento por `agentId + sessionId`)
- Serialização/deserialização via `agent.SerializeSessionAsync` / `agent.DeserializeSessionAsync`
- Persistência em PostgreSQL com TTL para sessões inativas
- O framework gerencia automaticamente via `WithSessionStore<PostgresSessionStore>()`

### ISessionManager — Sessão de Negócio

```
SessionManager
    ├─ StartSessionAsync(context) → sessionId
    ├─ AddEventAsync(sessionId, AgentEvent)      ← cada interação é um evento
    ├─ ConsolidateSessionAsync(sessionId)         ← sumarização via LLM
    ├─ EndSessionAsync(sessionId)                 ← finalização com métricas
    ├─ GetRecentEventsAsync(sessionId, count)
    └─ GetSessionAsync(sessionId) → Session
```

- `ConsolidateSessionAsync` usa LLM para sumarizar a sessão (insights, temas, satisfação)
- Chamada a cada N eventos ou quando a sessão atinge um threshold
- Eventos de negócio incluem: input do usuário, resposta do agente, tools usadas, ações performadas, reflections

### Fluxo de Sessão Completo

```
Primeiro request:
  SessionManager.StartSessionAsync → cria sessão de negócio
  ISessionStore → cria AgentSession do framework
  
Cada request subsequente:
  ISessionStore.GetSessionAsync → restaura AgentSession (chat history)
    MessageAIContextProvider → injeta RAG no contexto do request
  RunAsync → executa com histórico completo
  ISessionStore.SaveSessionAsync → persiste AgentSession atualizada
  SessionManager.AddEventAsync → registra evento de negócio
  
A cada N eventos:
  SessionManager.ConsolidateSessionAsync → sumariza via LLM

Finalização (ou cleanup automático):
  SessionManager.EndSessionAsync → registra métricas finais
```

---

## 11. Sistema de Tools — MCP, Built-in e AIFunction

### Três Categorias de Tools

```
UnifiedAIToolProvider
    │
    ├─ MCP Tools (Model Context Protocol)
    │   └─ Descobertas via McpToolsAIFunctionAdapter
    │       ├─ Tools de servidores MCP externos (auto-connect)
    │       └─ Tools do MCP server interno (HttpTransport)
    │
    ├─ Built-in Tools
    │   └─ Registradas no UnifiedAIToolProvider
    │       ├─ RAGContextEnricher (retrieve_context)
    │       ├─ SmartRouter wrapper (route_to_best_agent)
    │       ├─ ContextAnalyzer wrapper (analyze_request)
    │       └─ CorrectionLoop wrapper (apply_corrections)
    │
    └─ Agent-as-Tool (AIFunction)
        └─ Especialistas expostos via AsAIFunction()
            ├─ PersonalAgent → personal_agent tool
            ├─ WorkAgent → work_agent tool
            ├─ LearningAgent → learning_agent tool
            ├─ Custom agents → {name}_agent tool
            └─ Collaboration workflow → collaboration_workflow tool
```

### ToolGovernanceService

Governança centralizada de tools:

- `IToolAvailabilityGuard.CheckAsync(requiredTools)` — verifica quais tools estão disponíveis
- Se tools requeridas estão ausentes, retorna sugestões de extensões/MCPs para instalação
- O `ConfidenceScoreCalculator` penaliza o score quando a cobertura de tools é parcial
- Controle de acesso por tenant e por agente

### MCP Plugins

```
Descoberta automática de MCP servers:
  ├─ Configuração em appsettings.json (lista de servers)
  ├─ Auto-connect com retry
  ├─ Cada tool do MCP é convertida em AIFunction (IList<AITool>)
  └─ Tools disponibilizadas ao orquestrador e especialistas via DI
```

---

## 12. Ciclo de Vida dos Agentes

### Tipos de Agentes

| Tipo | Criação | Persistência | Exemplo |
|---|---|---|---|
| **Built-in** | `HierarchicalAgentFactory.InitializeDefaultAgents()` | Sempre disponível | PersonalAgent, WorkAgent, GeneralAgent |
| **Custom (dinâmico)** | `DynamicAgentService.HandleAgentCreationAsync` | In-memory (pool) | TrabalhistaAgent, MarketingAgent |
| **Framework-hosted** | `AddAIAgent()` no DI | Lifecycle gerenciado pelo hosting | Orchestrator, Collaboration agents |

### Pool de Agentes

```
HierarchicalAgentFactory (ConcurrentDictionary<string, IAgent>)
    │
    ├─ Built-in (inicializados no startup):
    │   ├─ PersonalAgent (domain: personal)
    │   ├─ WorkAgent (domain: work)
    │   ├─ LearningAgent (domain: learning)
    │   └─ GeneralAgent (domain: general)
    │
    ├─ On-demand (criados quando necessário):
    │   ├─ CreativeAgent (domain: creative)
    │   ├─ CalendarAgent (domain: calendar)
    │   ├─ AnalysisAgent (domain: analysis)
    │   ├─ NotificationAgent (domain: notification)
    │   └─ APIAgent (domain: api)
    │
    └─ Custom (criados via linguagem natural):
        └─ Qualquer agente criado pelo DynamicAgentService

Cleanup automático:
  MetaAgentOrchestrator.CleanupInactiveAgentsAsync()
    └─ Remove agentes Support/Specialist inativos há >24h
```

### BaseAgent — Classe Abstrata

Todos os agentes herdam de `BaseAgent`:

```
BaseAgent
    ├─ IChatClient.GetResponseAsync() → execução LLM contextual
    ├─ ISkillManager.BuildEnrichedPromptAsync() → system prompt + skills
    ├─ IAgentMemoryService.GetRelevantMemoriesAsync() → memórias por agent/user
    ├─ Properties: Name, Description, Tier, Domain, AvailableTools, Instructions
    └─ abstract GetBaseSystemPrompt() → cada agent define seu prompt base
```

### Tiers de Agentes

| Tier | Complexidade | Exemplo |
|---|---|---|
| **Support** | Simple | Assistentes básicos |
| **Specialist** | Moderate | Agentes de domínio (Work, Learning, etc.) |
| **Master** | Complex | Agentes com capabilities avançadas |
| **Chief** | RequiresPlanning | Orquestrador, Planner |

---

## 13. Multi-Tenant

### Arquitetura

```
Request HTTP
    │
    ▼
TenantMiddleware
    ├─ Tenta resolver tenant por JWT claim ("tenantId")
    ├─ Fallback: header X-Tenant-Id
    ├─ ITenantResolver.ResolveAsync(tenantId) → TenantInfo
    └─ Popula scoped TenantContext
        │
        ▼
    Todos os serviços acessam TenantContext via DI (scoped)
```

### Serviços Multi-Tenant

- **LLM**: cada tenant pode ter sua própria API key e configuração de modelo
- **ReRanker**: `IRerankingSettingsAccessor` resolve `ReRankingOptions` por tenant em runtime
- **Vector Store**: isolamento por tenant (filtro nos metadados)
- **Settings**: `/config` salva API key, parâmetros e assets por tenant
- **Sessões**: isolamento natural por `sessionId` (que inclui tenant context)
- **Agent Memory**: memórias isoladas por `agentName + userId` (que inclui tenant)

### Skip de Tenant

Endpoints não autenticados (health check, swagger) skipam o middleware de tenant automaticamente.

---

## 14. Autenticação e Segurança

### Multi-Scheme Authentication

```csharp
builder.Services.AddAuthentication("MultiAuth")
    .AddPolicyScheme("MultiAuth", options => {
        options.ForwardDefaultSelector = context => {
            // Se header Authorization contém "Bearer" → JWT
            // Se header X-Api-Key presente → ApiKey scheme
        };
    })
    .AddScheme<ApiKeyAuthHandler>("ApiKey")
    .AddJwtBearer("Bearer", options => { ... });
```

### API Key Masking

API keys são mascaradas em logs e embeddings para evitar vazamento:

- `ApiKeyMaskingService` — mascara keys antes de persistir ou logar
- Padrão: `sk-...****` (mostra prefixo, mascara restante)

### Segurança em Endpoints de Protocolo

Protocol hosting (A2A, AG-UI, OpenAI-compatible) requer:

- Autenticação obrigatória
- Rate limiting independente
- Audit logging de todas as interações
- Validação de input (injection prevention)

---

## 15. Protocol Hosting — A2A, AG-UI, OpenAI-Compatible

### Objetivo

Expor agentes do AgenticSystem via protocolos padronizados para interoperabilidade com sistemas externos.

### Protocolos

| Protocolo | Uso | Endpoint |
|---|---|---|
| **A2A** (Agent-to-Agent) | Outros sistemas de agentes interagem com os agentes do AgenticSystem | `/a2a` via `MapA2AHttpJson(...)` |
| **AG-UI** (Agent-UI) | Frontend usa protocolo padronizado com streaming e typed events | `/agui` via `MapAGUI(...)` |
| **OpenAI-compatible** | Ferramentas que usam formato da API OpenAI (ChatGPT, etc.) | `/v1/chat/completions` e `/v1/models` via controller custom |

### Registro

```csharp
// Program.cs
builder.Services.AddA2AServer("AgenticSystem");
builder.Services.AddAGUI();
// OpenAI-compatible: controller custom mapeado por MapControllers()

app.MapControllers();
app.MapA2AHttpJson("AgenticSystem", "/a2a").RequireAuthorization();
app.MapAGUI("AgenticSystem", "/agui").RequireAuthorization();
```

### Requisitos

- Agentes registrados via `AddAIAgent()` (hosting nativo)
- `ISessionStore` implementado (persistência de sessões)
- Middleware pipeline configurado
- Zero mudança na lógica dos agentes — apenas exposição de endpoints adicionais

---

## 16. Funcionalidades Transversais

### 16.1 Criação Dinâmica de Agentes

- Via linguagem natural: "Crie um agente de finanças"
- `DynamicAgentService` usa `IChatClient` para extrair a spec → cria `CustomAgent`
- O próximo build de `OrchestratorContext` já enxerga o novo agent porque a lista de especialistas muda e gera uma nova chave de instruções
- Agente fica disponível imediatamente como tool do orquestrador

### 16.2 Agent Memory

- `IAgentMemoryService.RecordInteractionAsync` — persiste interações relevantes por agente + usuário
- `IAgentMemoryService.GetRelevantMemoriesAsync` — recupera memórias para enriquecer system prompt
- Memórias são injetadas no system prompt do agente antes da execução

### 16.3 Skills

- `ISkillManager.BuildEnrichedPromptAsync` — enriquece system prompt com skills relevantes
- Skills built-in seeded no startup
- Skills declarativas carregadas de `skills/*.yaml|*.yml|*.json`
- Skills são contextuais (ativadas baseado no domínio/intent do request)

### 16.4 Scheduled Tasks

- `ScheduledTaskManager` — execução de tarefas agendadas
- Suporte a retry com backoff exponencial
- Dead-letter para falhas repetidas
- Configuração por tenant

### 16.5 Embedding Migration

- `IEmbeddingMigrationService` — migração de embeddings entre modelos/providers
- Necessário quando troca de modelo de embedding (ex: text-embedding-ada-002 → text-embedding-3-small)

### 16.6 Document Ingestion

```
Upload de documento
    │
    ▼
Parser selecionado por extensão:
    ├─ PdfDocumentParser
    ├─ DocxDocumentParser
    ├─ MarkdownDocumentParser
    ├─ HtmlDocumentParser
    ├─ JsonDocumentParser
    ├─ YamlDocumentParser
    └─ PlainTextParser
    │
    ▼
Chunking (divisão em trechos)
    │
    ▼
EmbeddingGenerator (M.E.AI) → vetores
    │
    ▼
VectorStore.UpsertAsync (pgvector / in-memory)
```

### 16.7 Obsidian Vault Sync

- `ObsidianVaultSyncService` — sincroniza vault Obsidian com vector store
- Detecta mudanças incrementais
- Preserva metadados (tags, links, frontmatter)

### 16.8 Confidence Score

`ConfidenceScoreCalculator` calcula score de confiança baseado em 5 fatores:

1. **Sucesso da execução** (1.0 se sucesso, 0.1 se erro)
2. **Qualidade do RAG** (média dos scores de re-rank)
3. **Tools utilizadas** (0.8 se usou tools, 0.5 se não)
4. **Histórico de reflexão** (média de confiança das reflections)
5. **Cobertura de tools** (penalidade se tools requeridas estão ausentes)

Resultado: `ConfidenceScore { Value, Level (High/Medium/Low/RequiresHumanReview), Label, Factors[] }`

### 16.9 Final Response Approval (Human-in-the-Loop)

- `IFinalResponseApprovalService.EvaluateAsync` — avalia se resposta precisa de aprovação humana
- Se necessário, resposta fica pendente com metadata `pendingFinalApproval`
- Aprovação via endpoint dedicado
- Critérios configuráveis por tenant/agente

---

## 17. Observabilidade e Gateway

### OpenTelemetry

- Traces distribuídos com `UseOpenTelemetry("AgenticSystem.Orchestrator")`
- Cada agente tem seu próprio scope de telemetria
- Métricas de latência, sucesso, tool usage
- Integração com exporters (Jaeger, OTLP, etc.)

### Structured Logging

- Logging por componente (controller → workflow → orchestrator → agent)
- Emoji-based log markers: 🎯 (routing), 🔍 (RAG), ✅ (success), ❌ (error), 🔄 (handoff), 🏗️ (agent creation), 🧹 (cleanup)

### Gateway Dashboard

- `GatewayHub` (SignalR) — eventos em tempo real do sistema
- `GetDashboard` — status geral dos serviços
- `GetServiceStatus` — health check de cada serviço
- Subscribe/Unsubscribe para grupos de eventos

### Runtime Coordinator

- `IAgentRuntimeCoordinator` — coordena eventos em tempo real durante execução
- `PublishEventAsync` — publica eventos (AgentSelected, RagStarted, StepCompleted, etc.)
- `RecordArtifactAsync` — registra artefatos (planos, steps, reviews, RAG context)
- `BeginExecutionScope` / `BeginAgentScope` — gerencia scopes de execução
- `StreamAsync` — streaming de eventos para o frontend via SignalR

---

## 18. Padrões Arquiteturais Utilizados

### Padrão | Onde é Aplicado

| Padrão | Aplicação |
|---|---|
| **Clean Architecture** | Core (domínio) → Infrastructure (implementação) → Api (apresentação) |
| **Supervisor-with-Tools** | Orquestrador central que delega via tool calling do LLM |
| **Agent-as-Tool** | Especialistas expostos como `AIFunction` via `AsAIFunction()` |
| **Factory** | `AgentFrameworkAgentFactory` cria o wrapper explícito do path direto; `AgentFrameworkFactory` cria `ChatClientAgent` |
| **Adapter** | `AgentFrameworkAdapter` adapta `IAgent` para `ChatClientAgent` apenas no `ExecuteDirectAsync` |
| **Bridge** | Separação entre sessão do framework (`AgentFrameworkSessionStoreAdapter`) e sessão de negócio (`ISessionManager`) |
| **Strategy** | `HandoffStrategy` (SingleDelegate, FanOut, Chain) para delegação entre agentes |
| **Pipeline/Middleware** | `UseReflection()` e `UseQualityGates()` via extensões locais + `UseLogging()` e `UseOpenTelemetry()` |
| **Factory** | `HierarchicalAgentFactory` cria agentes por domínio; `AgentFrameworkFactory` cria `ChatClientAgent` |
| **Feature Flag** | O plano de migração previa optional DI; o runtime principal atual já opera framework-first em `ExecuteAsync` |
| **CQRS (leve)** | Separação entre execução (workflow) e consulta (session manager, rankings) |
| **Event-driven** | `AgentStreamEvent` para comunicação real-time; `AgentEvent` para persistência |
| **Scoped Context** | `TenantContext`, `LLMRuntimeContext` — contexto por request via DI scoped |
| **Workflow/Pipeline** | `AgentWorkflowBuilder.BuildSequential` para planner → executor → reviewer |

---

## 19. Glossário

| Termo | Definição |
|---|---|
| **MAF** | Microsoft Agent Framework — framework de agentes de IA da Microsoft |
| **ChatClientAgent** | Tipo base de agente no MAF que usa `IChatClient` para execução LLM |
| **AsAIFunction()** | Método do MAF que converte um agente em uma `AIFunction` (tool) que pode ser chamada por outro agente |
| **AddAIAgent()** | Hosting nativo do MAF que resolve DI, session store, tools e middleware automaticamente |
| **IHostedAgentBuilder** | Interface retornada por `AddAIAgent()` para configurar o agente (tools, session store, middleware) |
| **AgentSession** | Sessão do framework que mantém chat history e estado do agente |
| **ISessionStore** | Interface nativa do MAF para persistência de sessões |
| **MessageAIContextProvider** | Conceito usado no projeto para injeção automática de contexto antes de cada request |
| **ChatHistoryProvider** | Conceito relacionado do MAF; o projeto atual preferiu `MessageAIContextProvider` para o RAG do orquestrador |
| **AgentWorkflowBuilder** | API do MAF para construção de workflows multi-agent (`BuildSequential`, `BuildConcurrent`) |
| **RunAsync(input, session)** | Método de execução do agente no MAF — aceita 2 argumentos, sem `CancellationToken` |
| **Tool binding** | Associação entre um agente e sua representação como `AIFunction` (tool) |
| **Supervisor-with-tools** | Padrão onde um agente supervisor (orquestrador) coordena especialistas via tool calling |
| **A2A** | Agent-to-Agent protocol — protocolo de comunicação entre sistemas de agentes |
| **AG-UI** | Agent-UI protocol — protocolo de comunicação entre agentes e interfaces de usuário |
| **MCP** | Model Context Protocol — protocolo para exposição de tools e contexto para LLMs |
| **IChatClient** | Interface de Microsoft.Extensions.AI para interação com LLMs |
| **RAG** | Retrieval-Augmented Generation — busca de contexto relevante para enriquecer prompts |
| **Re-Ranker** | Componente que reordena chunks por relevância semântica (Jina, ONNX, LLM-based) |
| **HyDE** | Hypothetical Document Embeddings — técnica que gera documento hipotético para melhorar retrieval |
| **Quality Gate** | Validação de critérios mínimos na entrada (pre) e na saída (post) do agente |
| **Reflection** | Auto-avaliação da qualidade da resposta com registro de deviations e lessons learned |
| **Correction Loop** | Regras de correção persistentes por usuário/agente, aplicadas no prompt |
| **Confidence Score** | Score calculado multi-fator que indica o nível de confiança na resposta |
