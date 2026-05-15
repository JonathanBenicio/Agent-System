# Plano de Migração: Framework-First Orchestration

> **[TRANSITIONAL]** Este documento mistura plano residual de migração com registro histórico do cutover framework-first.
> Use-o como trilha de decisão e registro das pendências remanescentes de migração; não como descrição primária do runtime atual.

> **Status documental:** Plano de migração e registro de cutover.
> **Escopo:** consolidar decisões, backlog residual e critérios de encerramento da migração framework-first; não substitui a arquitetura canônica do backend.
> **Fonte de verdade operacional:** [../architecture/backend-architecture-explained.md](../architecture/backend-architecture-explained.md).

> **Criado:** 2026-05-05  
> **Status:** Fase 1 ✅ completa | Fase 2 ✅ completa | Fase 3 🟡 núcleo concluído, com bloqueio real do framework em reflection/quality gates nativos | Fase 4 ✅ núcleo concluído, com pendências locais documentadas  
> **Escopo:** Inverter controle de orquestração do `AgentExecutionWorkflow` para o Microsoft Agent Framework  
> **Referências:** [../architecture/backend-architecture-explained.md](../architecture/backend-architecture-explained.md), [AI_Capabilities_Gaps.md](AI_Capabilities_Gaps.md), [../architecture/design-philosophy.md](../architecture/design-philosophy.md)

---

## Sumário

1. [Motivação e Objetivo](#1-motivação-e-objetivo)
2. [Estado Atual (AS-IS)](#2-estado-atual-as-is)
3. [Estado Alvo (TO-BE)](#3-estado-alvo-to-be)
4. [Estratégia de Migração](#4-estratégia-de-migração)
5. [Fase 1 — Centralizar Entrada no Framework](#5-fase-1--centralizar-entrada-no-framework)
6. [Fase 2 — Mover Cross-Cutting Concerns para o Framework](#6-fase-2--mover-cross-cutting-concerns-para-o-framework)
7. [Fase 3 — Remover Duplicidade Arquitetural](#7-fase-3--remover-duplicidade-arquitetural)
8. [Fase 4 — Protocol Hosting e Interoperabilidade](#8-fase-4--protocol-hosting-e-interoperabilidade)
9. [Grafo de Dependências](#9-grafo-de-dependências)
10. [Análise contra Documentação Oficial do MAF](#10-análise-contra-documentação-oficial-do-maf)
11. [Riscos e Mitigações](#11-riscos-e-mitigações)
12. [Critérios de Sucesso por Fase](#12-critérios-de-sucesso-por-fase)
13. [Decisões Globais](#13-decisões-globais)
14. [Análise de Dead Code, Duplicidades e Fluxos Não Utilizados](#14-análise-de-dead-code-duplicidades-e-fluxos-não-utilizados)

---

## 1. Motivação e Objetivo

### Problema

O `AgentExecutionWorkflow` é hoje o **cérebro** do sistema: ele decide qual agente chamar, quando buscar RAG, se faz handoff, se chama reflection. O Microsoft Agent Framework funciona como **subordinado** — apenas executa o agente já selecionado pelo workflow.

Isso gera:

- **Duplicidade de decisão** — o workflow decide routing, mas o framework também poderia decidir via tool bindings
- **Rigidez de pipeline** — 11 steps sequenciais hardcoded, difícil de reordenar ou condicionar
- **Subutilização do framework** — capabilities como agent-as-tool, sessões nativas e middleware não são usadas para orquestração
- **Complexidade crescente** — cada novo cross-cutting concern (reflection, correction, RAG, approval) adiciona mais dependências ao workflow

### Objetivo

Inverter a direção de controle: o **framework orquestrador** decide *qual agente chamar*, *quando buscar contexto*, *se precisa de handoff*. O `AgentExecutionWorkflow` vira **casca fina** (escopo, sessão e persistência).

### Princípios

1. **Migração incremental** — 3 fases, cada uma independente e reversível durante o desenvolvimento pré-v1
2. **Zero breaking change externo** — `IAgentExecutionWorkflow` mantém sua interface; controllers e hubs não mudam
3. **Cutover curto e removível** — o fallback para o caminho anterior foi tratado como etapa transitória de migração e já saiu do `ExecuteAsync` principal
4. **`ExecuteDirectAsync` preservado** — seleção manual de agente pelo frontend continua via escape hatch
5. **Coexistência curta** — como o produto ainda está pré-v1, caminhos anteriores e wrappers transitórios devem ser removidos assim que cada slice estiver validado

---

## 2. Estado Atual (AS-IS)

> **Nota:** esta seção descreve o ponto de partida histórico do plano, não uma obrigação de compatibilidade com versões já lançadas. Como o produto ainda está pré-v1, as referências a caminhos anteriores devem ser lidas como transição interna de desenvolvimento.

### Fluxo de Execução

```
User Input
    │
    ▼
MetaAgentOrchestrator (fachada de sessão/streaming)
    │
    ▼
AgentExecutionWorkflow.ExecuteAsync (cérebro)
    │
    ├─ 1. ContextAnalyzer.AnalyzeAsync()
    ├─ 2. AgentExecutionPreProcessingPipeline.ProcessAsync()
    ├─ 3. ToolAvailabilityGuard.CheckAsync()
    ├─ 4. DynamicAgentService.IsAgentCreationRequestAsync()
    ├─ 5. SmartRouter.RouteAsync()
    ├─ 6. AgentCollaborationWorkflow.ShouldRunAsync()
    ├─ 7. AgentFactory.ResolveAgentAsync() + RAG + Correction + Handoff
    ├─ 8. QualityGateService + ReflectionEngine
    ├─ 9. ConfidenceScoreCalculator.Calculate()
    ├─ 10. FinalResponseApprovalService.EvaluateAsync()
    └─ 11. Persist Artifacts + Metrics
    │
    ▼
ExecuteDirectAsync → executor direto nativo do framework (apenas path direto explícito)
    │
    ▼
ChatClientAgent.RunAsync (framework)
```

### Responsabilidades do Workflow (19+ dependências)

| Responsabilidade | Ideal para workflow? |
|---|:---:|
| Escopo de execução (BeginScope, LLM context) | ✅ |
| Persistência de resultado | ✅ |
| Streaming coordination | ✅ |
| Seleção de agente | ❌ → framework |
| RAG enrichment | ❌ → framework tool |
| Handoff decision | ❌ → framework tool binding |
| Reflection | ❌ → framework pós-processamento |
| Correction loop | ❌ → framework tool |
| Smart routing | ❌ → framework tool |

---

## 3. Estado Alvo (TO-BE)

### Fluxo de Execução (Evolução em 4 estágios)

**Estágio atual (revalidado):** resolução hosted via `OrchestratorContextFactory` sobre `AddAIAgent()`

```
User Input
    │
    ▼
MetaAgentOrchestrator (inalterado)
    │
    ▼
AgentExecutionWorkflow.ExecuteAsync (casca fina)
    ├─ BeginScope + LLM context
    ├─ Delega a IFrameworkOrchestratorService.ExecuteAsync()
    └─ Persist resultado
    │
    ▼
FrameworkOrchestratorService
    │
    ▼
OrchestratorContextFactory → ChatClientAgent (orquestrador)
    │
    ├─ System prompt: lista de especialistas + domínios + critérios
    ├─ Tool bindings: cada agente exposto via AsAIFunction()
    ├─ Tools auxiliares: RAG, SmartRouter, ContextAnalyzer
    ├─ Middleware: .UseReflection(), .UseQualityGates() (Fase 2+)
    ├─ RAGContextProvider (`MessageAIContextProvider`): RAG injetado no contexto
    │
    ▼
ChatClientAgent.RunAsync() (framework decide)
    ├─ Chama tool do especialista quando apropriado
    ├─ Chama RAG quando precisa de contexto
    ├─ Chama routing quando ambíguo
    │
    ▼
Resposta + identificação do agente chamado
    │
    ▼
FrameworkOrchestratorService
    ├─ Publica AgentSelected event
    ├─ Persiste sessão do framework
    └─ Sincroniza resposta
```

**Estado alvo (pós-Fase 3):** hosting nativo via `AddAIAgent()` + `IHostedAgentBuilder`

```
User Input
    │
    ▼
MetaAgentOrchestrator (inalterado)
    │
    ▼
AgentExecutionWorkflow.ExecuteAsync (casca fina)
    ├─ BeginScope + LLM context
    ├─ Delega a IFrameworkOrchestratorService.ExecuteAsync()
    └─ Persist resultado
    │
    ▼
FrameworkOrchestratorService
    │
    ▼
AddAIAgent("orchestrator") → IHostedAgentBuilder
    ├─ .WithAITool(specialist_1)     ← AsAIFunction()
    ├─ .WithAITool(specialist_N)     ← AsAIFunction()
  ├─ .WithAITool(retrieve_context) ← AIFunction complementar; injeção primária via `MessageAIContextProvider`
    ├─ .WithSessionStore(postgres)   ← ISessionStore nativo (elimina bridge)
    ├─ Middleware pipeline:          ← .UseReflection().UseQualityGates()
    │
    ▼
AddWorkflow("collaboration") → AgentWorkflowBuilder
    ├─ BuildSequential([planner, executor, reviewer])  ← substitui AgentCollaborationWorkflow
    └─ .AddAsAIAgent()               ← workflow exposto como agent tool do orquestrador
    │
    ▼
Protocol Hosting (Fase 4):
    ├─ AddA2AServer() / MapA2AServer()
    └─ AG-UI endpoints
```

### Diagrama de Componentes (TO-BE)

```mermaid
graph TB
    subgraph "Camada de Apresentação"
        API[Controllers / Hubs]
        A2A[A2A Protocol — Fase 4]
        AGUI[AG-UI Protocol — Fase 4]
    end

    subgraph "Core — Casca Fina"
        WF[AgentExecutionWorkflow]
        IOrch[IFrameworkOrchestratorService]
    end

    subgraph "Infrastructure — Framework Hosting"
        Orch[FrameworkOrchestratorService]
        Builder["AddAIAgent() / OrchestratorContextFactory"]
        SessStore["ISessionStore (PostgreSQL)"]
    end

    subgraph "Microsoft Agent Framework"
        OrcAgent[ChatClientAgent — Orquestrador]
        SpecTools["Agent Tools (AsAIFunction)"]
        AuxTools["RAG / Router / Analyzer Tools"]
        Middleware["Middleware: Reflection, QualityGate"]
        Wkf["AgentWorkflowBuilder (planner → executor → reviewer)"]
    end

    API --> WF
    A2A -.-> OrcAgent
    AGUI -.-> OrcAgent
    WF --> IOrch
    IOrch -.-> Orch
    Orch --> Builder
    Orch --> SessStore
    Builder --> OrcAgent
    OrcAgent --> SpecTools
    OrcAgent --> AuxTools
    OrcAgent --> Middleware
    OrcAgent --> Wkf
```

---

## 4. Estratégia de Migração

### Visão Geral das Fases

| Fase | Objetivo | Impacto no Workflow | Saída segura |
|:---:|---|---|---|
| **1** | Centralizar entrada no framework | Delega para orquestrador e elimina a decisão imperativa do workflow principal | Cutover concluído no runtime principal |
| **2** | Mover cross-cutting concerns + hosting nativo | Remove RAG manual, handoff manual; migra para `AddAIAgent()` | Remover tools auxiliares do builder → volta ao builder manual atual por tempo limitado |
| **3** | Eliminar duplicidade | Simplifica Adapter, unifica sessão via `ISessionStore`, middleware nativo | Restaurar steps removidos do workflow |
| **4** | Protocol hosting e interoperabilidade | Expor agentes via A2A, AG-UI, OpenAI-compatible | Remover endpoints de protocolo |

### Premissas

- .NET 10 com Microsoft.Agents.AI 1.4.0 no estado atual do projeto
- `IChatClient` configurado (registro condicional)
- `IAgentFactory` permanece cru para orquestração, colaboração e handoff
- O path direto já converge em `IDirectAgentExecutionService` / `AgentFrameworkDirectExecutionService`
- `CreateToolBindingAsync` já existe em `AgentFrameworkFactory`
- **`AddAIAgent()` + `IHostedAgentBuilder`** disponíveis no MAF 1.4.0 para hosting nativo (DI, session store e tools resolvidos automaticamente)
- **`AgentWorkflowBuilder`** disponível no MAF 1.4.0 para orquestração multi-agent (`.BuildSequential`, `.BuildConcurrent`)
- **`MessageAIContextProvider`** disponível como first-class concept para injeção automática de contexto; o projeto o concretiza via `RAGContextProvider`
- **Session store de hosting** disponível via `.WithInMemorySessionStore()` e `.WithSessionStore(...)`; integração com o store atual da aplicação ainda exige adapter local

---

## 5. Fase 1 — Centralizar Entrada no Framework

> **Status: ✅ COMPLETA — compilando com 0 erros, 0 warnings**

### Objetivo

Toda execução passa por um agente orquestrador `ChatClientAgent` do framework. O workflow deixa de decidir "qual agente" e "qual contexto".

### Arquivos Criados

| Arquivo | Descrição |
|---|---|
| [`Core/Interfaces/IFrameworkOrchestratorService.cs`](../../src/AgenticSystem.Core/Interfaces/IFrameworkOrchestratorService.cs) | Interface com `ExecuteAsync(sessionId, input, context, ct)` → `AgentResponse` |
| [`Infrastructure/AgentFramework/OrchestratorContextFactory.cs`](../../src/AgenticSystem.Infrastructure/AgentFramework/OrchestratorContextFactory.cs) | Resolve o contexto ativo do runtime e compõe o `OrchestratorContext` com specialist bindings, tools auxiliares, instruções e `AIAgent` hospedado |
| [`Infrastructure/AgentFramework/FrameworkOrchestratorService.cs`](../../src/AgenticSystem.Infrastructure/AgentFramework/FrameworkOrchestratorService.cs) | Implementação: resolve agent hosted + session store keyed → `RunAsync` → extrai conteúdo → identifica agente → sincroniza evento de negócio |

### Arquivos Modificados

| Arquivo | Mudança |
|---|---|
| [`Infrastructure/AgentFramework/AgentFrameworkFactory.cs`](../../src/AgenticSystem.Infrastructure/AgentFramework/AgentFrameworkFactory.cs) | Expôs `ChatClient`, `LoggerFactory`, `ServiceProvider` como propriedades `internal` |
| [`Core/Services/AgentExecutionWorkflow.cs`](../../src/AgenticSystem.Core/Services/AgentExecutionWorkflow.cs) | `ExecuteAsync` delega diretamente ao `IFrameworkOrchestratorService` no runtime atual |
| [`Infrastructure/Extensions/ServiceCollectionExtensions.cs`](../../src/AgenticSystem.Infrastructure/Extensions/ServiceCollectionExtensions.cs) | Resolve `OrchestratorContext` direto do `OrchestratorContextFactory` e registra `IFrameworkOrchestratorService` no bloco `if (hasChatClient)` |

### Steps Detalhados

#### Step 1 — Interface `IFrameworkOrchestratorService`

```csharp
namespace AgenticSystem.Core.Interfaces;

public interface IFrameworkOrchestratorService
{
    Task<AgentResponse> ExecuteAsync(
        string sessionId, string input, UserContext context, CancellationToken ct);
}
```

- Fica no Core para que o workflow (também no Core) possa referenciar
- Retorna `AgentResponse` — mesma assinatura que `IAgent.ExecuteAsync`

#### Step 2 — `OrchestratorContextFactory`

Responsável por:

1. **Ler o `sessionId` atual** do `ILLMRuntimeContextAccessor`
2. **Listar agentes ativos** via `IAgentFactory.GetAllAgentsAsync()`
3. **Criar tool bindings** dos especialistas por sessão
4. **Gerar instruções dinâmicas** com base nos especialistas e tools auxiliares
5. **Montar `ChatClientAgent`** hospedado e devolver `OrchestratorContext`

```
OrchestratorContextFactory
  ├─ _runtimeContextAccessor.Current.SessionId
  ├─ _agentFactory.GetAllAgentsAsync() → AgentInfo[]
  └─ CreateAsync(agents, sessionId, ct)
     ├─ _toolBindingService.CreateSpecialistBindingsAsync(...)
     ├─ _instructionService.GetInstructions(...)
     └─ _hostedAgentFactory.Create(...)
```

#### Step 3 — `FrameworkOrchestratorService`

Fluxo de execução:

```
ExecuteAsync(sessionId, input, context, ct)
    │
    ├─ scopedServices.GetRequiredService<OrchestratorContext>()
    ├─ scopedServices.GetRequiredKeyedService<AIAgent>(_orchestratorMetadata.Name)
    ├─ scopedServices.GetRequiredKeyedService<AgentSessionStore>(_orchestratorMetadata.Name)
    ├─ sessionStore.GetSessionAsync(orchestrator, sessionId, ct) → AgentSession
    ├─ coordinator.PublishRuntimeEventAsync("AgentSelected", ...)
    ├─ orchestrator.RunAsync(input, session)  ← 2 args, SEM CancellationToken
    ├─ ExtractContent(response) → string
    ├─ IdentifyCalledAgent(response, bindings) → agentName?
    ├─ sessionStore.SaveSessionAsync(orchestrator, sessionId, session, ct)
    └─ sessionManager.AddEventAsync(sessionId, agentEvent)
```

**Pontos importantes:**
- `RunAsync` do MAF aceita apenas 2 argumentos: `(string input, AgentSession session)` — sem `CancellationToken`
- `ExtractContent` busca `TextContent` em mensagens `Assistant`; fallback para `.Text`
- `IdentifyCalledAgent` varre `FunctionCallContent` nas mensagens para mapear tool name → agent name via bindings

#### Step 4 — Reduzir `AgentExecutionWorkflow.ExecuteAsync`

```csharp
// Runtime atual: dependência obrigatória do orquestrador de framework
public AgentExecutionWorkflow(
    IDirectAgentRequestExecutor directAgentRequestExecutor,
    ISessionManager sessionManager,
    IAgentRuntimeCoordinator runtimeCoordinator,
    ILLMRuntimeContextAccessor llmRuntimeContextAccessor,
    IFrameworkOrchestratorService frameworkOrchestrator,
    ILogger<AgentExecutionWorkflow> logger)

// ExecuteAsync: casca fina sem fallback imperativo
public async Task<AgentResponse> ExecuteAsync(...)
{
    using var llmScope = _llmRuntimeContextAccessor.BeginScope(context, sessionId);
    _logger.LogDebug("Delegating to Framework Orchestrator");
    return await _frameworkOrchestrator.ExecuteAsync(sessionId, input, context, ct);
}
```

**`ExecuteDirectAsync` permanece intacto** — seleção manual de agente pelo frontend.

#### Step 5 — Registro DI

Em `ServiceCollectionExtensions.cs`, dentro de `if (hasChatClient)`:

```csharp
services.AddSingleton<OrchestratorContextFactory>();
services.AddScoped(sp => sp.GetRequiredService<OrchestratorContextFactory>().Resolve());
services.AddSingleton<IFrameworkOrchestratorService, FrameworkOrchestratorService>();
```

O desenho original previa registro condicional. No runtime atual, `IFrameworkOrchestratorService` integra o caminho principal e `ExecuteAsync` já não mantém o caminho anterior.

### Decisões Fase 1

| Decisão | Justificativa |
|---|---|
| Orquestrador é `ChatClientAgent` com system prompt de coordenação | Padrão supervisor-with-tools documentado no MAF |
| Especialistas expostos como `AIFunction` via `AsAIFunction()` | Base já existia em `CreateToolBindingAsync` |
| `IAgentExecutionWorkflow` mantém interface | Zero impacto em consumidores externos |
| Dependência obrigatória de `IFrameworkOrchestratorService` | O cutover do caminho principal foi concluído; o fallback não permanece no runtime atual |
| `ExecuteDirectAsync` continua bypassando orquestrador | Preserva seleção direta de agente pelo frontend |

### Débito Técnico Fase 1

> **Steps 2-3 ainda usam uma composição local do agente orquestrador.**
> O MAF oferece `AddAIAgent()` + `IHostedAgentBuilder` como modelo de hosting nativo que resolve DI, session store, tools e middleware automaticamente.
> A composição atual via `OrchestratorContextFactory` funciona e está comprovada, porém ainda é um arranjo local que pode encolher mais para alinhar com a direção do framework.
>
> ```csharp
> // Fase 1 (atual — manual)
> var agent = new ChatClientAgent(chatClient, "orchestrator", options)
>     .AsBuilder().UseLogging().UseOpenTelemetry().Build();
>
> // Fase 2+ (alvo — hosting nativo)
> builder.Services.AddAIAgent("orchestrator", agentBuilder => {
>     agentBuilder
>         .WithAITool(specialist1)
>         .WithAITool(specialist2)
>         .WithSessionStore(postgresStore);
> });
> ```

---

## 6. Fase 2 — Mover Cross-Cutting Concerns para o Framework

> **Status: ✅ COMPLETA — compilando com 0 erros; warning local de `McpClientPlugin.cs` já resolvido**

### Objetivo

RAG vira tool do framework + context provider automático. Handoff vira delegação nativa via tool binding. SmartRouter e ContextAnalyzer expostos como tools auxiliares. O workflow não monta mais `enrichedInput` nem decide handoff manualmente no caminho principal (o path anterior é bypassado pelo early return do framework).

### Decisões de Implementação vs. Plano Original

| Aspecto | Plano Original | Implementação Real | Justificativa |
|---|---|---|---|
| **Step 5b** — `AddAIAgent()` hosting | Migrar para `AddAIAgent()` + `IHostedAgentBuilder` | **Disponível no MAF 1.4** — já adotado no fluxo principal e na superfície de protocolo; a resolução scoped já foi absorvida pelo `OrchestratorContextFactory` | Resta apenas enxugar a composição por sessão que ainda vive dentro do `OrchestratorContextFactory` |
| **Step 6** — RAG | Hipótese inicial: `ChatHistoryProvider` (primário) + `AIFunction` (complemento) | `MessageAIContextProvider` (primário) + `AIFunction` (complemento) | MAF 1.3.0 usa `MessageAIContextProvider` (não `ChatHistoryProvider`); pipeline consolidado: histórico de chat → AIContextProviders → `IChatClient` |
| **Step 7** — Handoff | Simplificar `HandoffManager` | Delegação via tool bindings + instructions do orquestrador | Handoff agora é implícito — LLM decide qual tool/agente chamar |
| **Step 8** — SmartRouter/ContextAnalyzer | Tools auxiliares | Implementado via `OrchestratorAuxiliaryToolService` + `OrchestratorAuxiliaryTools` | `AIFunctionFactory.Create(Delegate, AIFunctionFactoryOptions)` — pattern validado |
| **Step 9** — Remover `enrichedInput` | Remover do workflow | **Concluído** — `AgentExecutionWorkflow.ExecuteAsync` virou casca fina sem montagem manual de input | O caminho principal delega direto ao framework; o path direto mantém pré-processamento próprio e compartilhado |

### Arquivos Criados

| Arquivo | Descrição |
|---|---|
| [`Infrastructure/AgentFramework/RAGContextProvider.cs`](../../src/AgenticSystem.Infrastructure/AgentFramework/RAGContextProvider.cs) | `MessageAIContextProvider` que injeta contexto RAG automaticamente antes de cada LLM call; guard contra re-injeção em loops de tool-calling via marker `[Contexto Relevante da Base de Conhecimento]` |
| [`Infrastructure/AgentFramework/OrchestratorAuxiliaryTools.cs`](../../src/AgenticSystem.Infrastructure/AgentFramework/OrchestratorAuxiliaryTools.cs) | Fábrica estática com 3 `AITool` auxiliares: `retrieve_context` (RAG on-demand), `route_to_best_agent` (SmartRouter), `analyze_request` (ContextAnalyzer); expõe `AllToolNames` para filtragem |
| [`Infrastructure/AgentFramework/OrchestratorAuxiliaryToolService.cs`](../../src/AgenticSystem.Infrastructure/AgentFramework/OrchestratorAuxiliaryToolService.cs) | Materializa o catálogo estável de tools auxiliares do orquestrador e remove essa montagem do construtor do builder |
| [`Core/Services/AgentExecutionPostProcessingPipeline.cs`](../../src/AgenticSystem.Core/Services/AgentExecutionPostProcessingPipeline.cs) | Novo pipeline compartilhado que centraliza reflection, confidence, final approval, persistência e agent memory para os fluxos direto e hosted |

### Arquivos Modificados

| Arquivo | Mudança |
|---|---|
| [`Infrastructure/AgentFramework/OrchestratorContextFactory.cs`](../../src/AgenticSystem.Infrastructure/AgentFramework/OrchestratorContextFactory.cs) | Resolve o `sessionId` do runtime context, lista agentes ativos e compõe o `OrchestratorContext` por sessão |
| [`Infrastructure/AgentFramework/FrameworkOrchestratorService.cs`](../../src/AgenticSystem.Infrastructure/AgentFramework/FrameworkOrchestratorService.cs) | `IdentifyCalledAgent` filtra `OrchestratorAuxiliaryTools.AllToolNames` antes de mapear specialist bindings — evita que tool calls auxiliares sejam interpretadas como delegação a especialista |

### Arquivos Inalterados (decisão consciente)

| Arquivo | Motivo |
|---|---|
| `Infrastructure/Extensions/ServiceCollectionExtensions.cs` | O DI agora resolve `OrchestratorContext` direto do `OrchestratorContextFactory` e centraliza nome/descrição do orquestrador em `OrchestratorMetadata` |
| `Core/Services/AgentExecutionWorkflow.cs` | O workflow principal já é casca fina e delega diretamente ao framework; `ExecuteDirectAsync` permanece como escape hatch explícito |

### Detalhes Técnicos da Implementação

#### RAGContextProvider (MessageAIContextProvider)

```csharp
public class RAGContextProvider : MessageAIContextProvider
{
    // Overrides ProvideMessagesAsync(InvokingContext, CancellationToken)
    // → ValueTask<IEnumerable<ChatMessage>>
    
  // Pipeline consolidado: chat history → AIContextProviders → IChatClient
    // RAGContextProvider executa APÓS chat history, ANTES do LLM call
    
    // Guard contra re-injeção:
    // - Verifica se ContextMarker já existe em RequestMessages
    // - Evita RAG duplicado em loops de tool-calling (LLM chama tool → recebe resultado → chama novamente)
    
    // Budget management:
    // - IContextBudgetManager? optional
    // - Se disponível, trima contexto via ResolveBudget + TrimContextToBudgetAsync
}
```

#### OrchestratorAuxiliaryTools (AIFunctionFactory)

```csharp
// Pattern: AIFunctionFactory.Create(Delegate, AIFunctionFactoryOptions)
// - [Description("...")] em parâmetros do lambda → schema JSON automático
// - CancellationToken excluído do schema, passado em runtime
// - Retorno string → texto passado de volta ao LLM

retrieve_context(query)     → IRAGService.RetrieveContextAsync → BuiltContext
route_to_best_agent(domain, intent) → ISmartRouter.RouteAsync → PrimaryAgent + ConfidenceScore
analyze_request(input)      → IContextAnalyzer.AnalyzeAsync → Domain/Intent/Complexity/Agent
```

#### Resolution Chain (OrchestratorContextFactory)

```csharp
// OrchestratorContextFactory agora resolve a sessao ativa e o catalogo de agentes:
var sessionId = _runtimeContextAccessor.Current?.SessionId;
var agents = (await _agentFactory.GetAllAgentsAsync()).ToList();

// A composicao final do contexto hospedado por sessao continua concentrada no proprio factory:
return await CreateAsync(agents, sessionId, ct);
```

### Débito Técnico Fase 2

> **`AddAIAgent()` + `IHostedAgentBuilder` já estão disponíveis no MAF 1.4 e já são usados no protocolo A2A/AG-UI.**
> A cadeia local do fluxo principal encolheu para o `OrchestratorContextFactory`.
> A dívida remanescente é reduzir ainda mais essa composição por sessão, não mais migrar o hosting principal em si.
>
> **`AgentExecutionWorkflow` já é casca fina no caminho principal.**
> O legado imperativo de `ExecuteAsync` e o `HandoffManager` saíram do runtime.
> A dívida restante ficou concentrada no builder local do orquestrador e nos middlewares nativos ainda indisponíveis no MAF.

---

## 7. Fase 3 — Remover Duplicidade Arquitetural

> **Status: 🟡 Parcial — collaboration migrado; hosting principal e sessão via framework já estão no runtime; permanecem em aberto apenas reflection/quality gates nativos, ainda indisponíveis no MAF**

### Notas de Implementação e Revalidação (MAF 1.4)

> **Implementado inicialmente em:** 2025-07  
> **Revalidado em:** 2026-05  
> **MAF atual no projeto:** Microsoft.Agents.AI 1.4.0 + Hosting/A2A/AG-UI 1.4.0-preview

#### Realidade Atual — suporte do MAF 1.4 vs situação do projeto

| API / capability | Suporte no MAF 1.4 | Situação no projeto |
|---|---|---|
| `UseReflection()` nativo | ❌ | Continua via extensões custom (`ReflectionDelegatingAgent` + `AgentBuilderMiddlewareExtensions`) |
| `UseQualityGates()` nativo | ❌ | Continua via extensões custom (`QualityGateDelegatingAgent` + `AgentBuilderMiddlewareExtensions`) |
| `AgentWorkflowBuilder` | ✅ | Package presente e integrado no `AgentCollaborationWorkflow` via `BuildSequential` + `InProcessExecution` |
| `BuildSequential` / `BuildConcurrent` | ✅ | `BuildSequential` já usado no workflow colaborativo; `BuildConcurrent` segue disponível para futuras etapas paralelas |
| Session store de hosting (`WithInMemorySessionStore` / `WithSessionStore`) | ✅ | Já adotado no fluxo principal via `SimpleSessionStoreAdapter`; o runtime já não depende mais de bridge dedicada |
| `AddAIAgent()` / `IHostedAgentBuilder` | ✅ | Já usado no fluxo principal e nos endpoints A2A/AG-UI; a resolução scoped foi absorvida pelo `OrchestratorContextFactory`, restando apenas a composição local por sessão dentro dele |
| `WithSessionStore(...)` | ✅ | Adapter local já implementado (`SimpleSessionStoreAdapter`) e já usado no fluxo principal e nos tool bindings |

#### Arquivos Criados/Modificados

| Arquivo | Ação | Step |
|---|---|---|
| `Infrastructure/AgentFramework/ReflectionDelegatingAgent.cs` | **Criado** — `DelegatingAIAgent` que chama `IReflectionEngine.ReflectAsync` pós-resposta | 12 |
| `Infrastructure/AgentFramework/QualityGateDelegatingAgent.cs` | **Criado** — `DelegatingAIAgent` com pre/post validation via `IQualityGateService` | 12 |
| `Infrastructure/AgentFramework/AgentBuilderMiddlewareExtensions.cs` | **Criado** — `UseReflection()` e `UseQualityGates()` extension methods em `AIAgentBuilder` | 12 |
| `Infrastructure/AgentFramework/AgentFrameworkDirectExecutionService.cs` | **Criado** — executor explícito final do path direto no runtime atual | 11 |
| `Infrastructure/AgentFramework/OrchestratorContextFactory.cs` | Novo factory que compõe `OrchestratorContext` por sessão, incluindo bindings, instruções, tools auxiliares e hosted agent | 7 |
| `Infrastructure/AgentFramework/SimpleSessionStoreAdapter.cs` | **Criado** — adapter local final para `WithSessionStore(...)` do hosting nativo | 14 |
| `Infrastructure/AI/AgentCollaborationWorkflow.cs` | **Migrado** — planner/executor/reviewer agora executam via `AgentWorkflowBuilder.BuildSequential(...)`; o fallback transitório pertence apenas ao snapshot histórico da migração | 13 |
| `Core/Services/HierarchicalAgentFactory.cs` | `ResolveAgentAsync` explicita resolução/materialização do agent; seleção deixou de ser responsabilidade do factory | 15 |

#### Tech Debt Remanescente para Fase 4+

1. **Composição manual remanescente do orquestrador** — o fluxo principal já resolve `OrchestratorContext` direto do `OrchestratorContextFactory`, mas ainda mantém nessa classe a montagem por sessão de specialist bindings, instruções e hosted agent.
2. **Quality gates no builder continuam locais** — o framework ainda não expõe essas políticas nativamente como parte do hosting atual; reflection, confidence, final approval, persistência e agent memory já convergiram para o `IAgentExecutionPostProcessingPipeline` compartilhado.

### Objetivo

Eliminar caminhos paralelos. O framework é a autoridade de decisão. O `AgentExecutionWorkflow` contém apenas escopo e persistência.

### Steps

#### Step 11 — Consolidar o caminho direto nativo

- `AgentExecutionWorkflow.ExecuteDirectAsync` agora delega para `IDirectAgentRequestExecutor`, mantendo o workflow como casca fina também no escape hatch direto
- `DirectAgentRequestExecutor` usa `IDirectAgentExecutionService` / `AgentFrameworkDirectExecutionService` quando precisa rodar um `IAgent` cru pelo runtime hospedado
- `IDirectAgentRequestExecutor` e `FrameworkOrchestratorService` agora convergem no `IAgentExecutionPostProcessingPipeline` para a fase final da execução
- Com o orquestrador no centro, o path direto deixou de depender de wrapper transitório de `IAgent`
- Especialistas são chamados diretamente como tool bindings — nunca como `IAgent.ExecuteAsync` pelo workflow
- O fallback para o agente cru fica concentrado no `AgentFrameworkDirectExecutionService`, apenas em erro crítico do framework

#### Step 12 — Pós-processamento compartilhado no Core

O pós-processamento final não fica mais espalhado entre `DirectAgentRequestExecutor` e variações do path hosted. A fronteira nova é o `IAgentExecutionPostProcessingPipeline`:

```csharp
// Path direto
response = await executableAgent.ExecuteAsync(enrichedInput, context);
return await _postProcessingPipeline.ProcessAsync(contexto, ct);

// Path hosted
frameworkResponse = await orchestrator.RunAsync(input, session);
await sessionStore.SaveSessionAsync(orchestrator, sessionId, session, ct);
return await _postProcessingPipeline.ProcessAsync(contexto, ct);
```

| Responsabilidade | Dono atual |
|---|---|
| Reflection com `sessionId` de negócio | `AgentExecutionPostProcessingPipeline` |
| `CorrectionLoop.AddRuleAsync` a partir da reflection | `AgentExecutionPostProcessingPipeline` |
| Aplicação de correction rules antes da execução | `AgentExecutionPreProcessingPipeline` |
| Confidence score | `AgentExecutionPostProcessingPipeline` |
| Final approval | `AgentExecutionPostProcessingPipeline` |
| Persistência de sessão de negócio + artifacts | `AgentExecutionPostProcessingPipeline` |
| Agent memory | `AgentExecutionPostProcessingPipeline` |
| Quality gate de request | `AgentExecutionPreProcessingPipeline` |
| Quality gate de response | `AgentExecutionPostProcessingPipeline` quando habilitado pelo caller; middleware/chat client seguem como defesa em profundidade |

O `OrchestratorContextFactory` mantém AI context, quality gates, logging e OpenTelemetry na montagem do hosted orchestrator. A reflection final do fluxo principal deixou de depender do middleware específico do orquestrador hosted.

#### Step 13 — Migrar `AgentCollaborationWorkflow` para `AgentWorkflowBuilder` ✅ Implementado

O fluxo planner-executor-reviewer foi migrado incrementalmente. O projeto agora monta um `BuildSequential` com planner, executor e reviewer e executa o workflow via `InProcessExecution`. O fallback transitório pertence ao snapshot histórico da migração e já não descreve o runtime atual. O MAF segue oferecendo `BuildSequential` e `BuildConcurrent` como mecanismos nativos de orquestração multi-agent:

```csharp
// ANTES — Custom workflow
// AgentCollaborationWorkflow.cs
var plan = await _planner.PlanAsync(input);
foreach (var step in plan.Steps)
    await _executor.ExecuteStep(step);
await _reviewer.ReviewAsync(plan);

// DEPOIS — MAF AgentWorkflowBuilder
builder.AddWorkflow("collaboration", workflowBuilder => {
    workflowBuilder
        .BuildSequential([plannerAgent, executorAgent, reviewerAgent])
        .AddAsAIAgent();  // ← workflow exposto como agent tool do orquestrador
});
```

**Detalhamento:**

```
Antes:                                  Depois:
AgentCollaborationWorkflow              AgentWorkflowBuilder
  ├─ Planner.PlanAsync()                  ├─ BuildSequential([
  ├─ Executor.ExecuteStep()               │     plannerAgent,    ← ChatClientAgent
  └─ Reviewer.ReviewAsync()               │     executorAgent,   ← ChatClientAgent
                                          │     reviewerAgent    ← ChatClientAgent
                                          │   ])
                                          └─ .AddAsAIAgent()     ← expõe como tool do supervisor
```

**Benefícios vs tool calls manuais:**
- **Checkpointing** entre steps (resume em caso de falha)
- **Streaming nativo** (output de cada agent é streamado)
- **Paralelismo tipado** (`BuildConcurrent` para steps independentes)
- **Graph visualizável** (edges tipados entre agents)
- **HITL nativo** via `RequestInfoExecutor` (human-in-the-loop)

**Diferença conceitual — agent-as-tool vs workflow:**

| | Agent-as-tool (`AsAIFunction()`) | Workflow (`AgentWorkflowBuilder`) |
|---|---|---|
| Padrão | Supervisor + especialistas | Pipeline sequencial/paralelo |
| Decisão | LLM decide qual tool chamar | Grafo define a sequência |
| Melhor para | Orquestrador → especialistas (open-ended) | Planner → executor → reviewer (determinístico) |
| Usado em | Fases 1-2: supervisor-with-tools | Fase 3: collaboration pipeline |

**Recomendação:** manter `AsAIFunction()` para orquestrador → especialistas (supervisor-with-tools). Usar `AgentWorkflowBuilder.BuildSequential` para planner → executor → reviewer (fluxo determinístico). O workflow pode ser exposto como tool do orquestrador via `.AddAsAIAgent()`.

#### Step 14 — Unificar Sessão via `ISessionStore` Nativo

O MAF oferece `ISessionStore` como interface nativa para persistência de sessões, com `.WithInMemorySessionStore()` e suporte a stores customizados.

**Implementar `PostgresSessionStore` como `ISessionStore`:**

```csharp
public class PostgresSessionStore : ISessionStore
{
    private readonly IDbConnectionFactory _db;

    public async Task<AgentSession?> GetSessionAsync(string sessionId, CancellationToken ct)
    {
        // Busca sessão serializada no PostgreSQL
    }

    public async Task SaveSessionAsync(string sessionId, AgentSession session, CancellationToken ct)
    {
        // Persiste sessão serializada no PostgreSQL
    }
}

// Registro:
agentBuilder.WithSessionStore<PostgresSessionStore>();
```

| Aspecto | Antes | Depois |
|---|---|---|
| Sessão principal de conversa | `ISessionManager` (negócio) | `AgentSession` do framework via `ISessionStore` |
| Persistência de sessão framework | `AgentSessionBridge` (sync bidirecional) | `PostgresSessionStore` nativo (elimina bridge) |
| Persistência de negócio | `ISessionManager` | `ISessionManager` (mantido para eventos e consolidação) |
| Thread de chat history | Custom | Framework como fonte primária |

**Resultado:** `AgentSessionBridge` é **eliminada**. A persistência de sessões do framework é feita diretamente pelo `PostgresSessionStore` via `ISessionStore`. O `ISessionManager` continua existindo para lógica de negócio (eventos, consolidação, metadados).

**Importante:** doc oficial do MAF: "Sessions are agent/service-specific. Reusing a session with a different agent configuration or provider can lead to invalid context." → manter sessões separadas por agente no store.

#### Step 15 — Remover `IAgentFactory` como cérebro de seleção

- `IAgentFactory.ResolveAgentAsync` perde o papel de "escolher agente baseado em analysis"
- Passa a ser apenas "criar agente dado nome/spec" — sem lógica de seleção
- A seleção é feita pelo LLM do orquestrador via tool bindings
- `HierarchicalAgentFactory` pode ser simplificado

### Arquivos Impactados — Fase 3

| Arquivo | Ação |
|---|---|
| `Infrastructure/AgentFramework/AgentFrameworkDirectExecutionService.cs` | ✅ Serviço direto final do path explícito |
| `Infrastructure/AgentFramework/ReflectionMiddleware.cs` | **Criar** — middleware wrapper para `ReflectionEngine` |
| `Infrastructure/AgentFramework/QualityGateMiddleware.cs` | **Criar** — middleware de quality gates |
| `Infrastructure/AgentFramework/SimpleSessionStoreAdapter.cs` | **Criado** — adapter local para `WithSessionStore(...)` do hosting nativo |
| `Infrastructure/AI/AgentCollaborationWorkflow.cs` | ✅ Migrado para `AgentWorkflowBuilder`; o fallback transitório ficou apenas como contexto histórico |
| `Core/Services/DirectAgentRequestExecutor.cs` | ✅ Convergido para `IDirectAgentExecutionService` no path direto |
| `Core/Services/CorrectionLoopService.cs` | Reposicionar como AIFunction complementar |

---

## 8. Fase 4 — Protocol Hosting e Interoperabilidade

> **Status: ✅ Completa**
> 
> **Notas de implementação (2026-05):**
> - MAF atualizado para 1.4.0 (de 1.3.0) + `Microsoft.Agents.AI.Workflows` 1.4.0 adicionado
> - **A2A: implementado** — `Microsoft.Agents.AI.Hosting.A2A.AspNetCore` 1.4.0-preview. `AddAIAgent()` + `AddA2AServer()` para DI, `MapA2AHttpJson()` para endpoint `/a2a`
> - **AG-UI: implementado** — `Microsoft.Agents.AI.Hosting.AGUI.AspNetCore` 1.4.0-preview. `AddAGUI()` para DI, `MapAGUI()` para endpoint `/agui`
> - **OpenAI-compatible: implementado como controller custom** — `OpenAIChatCompletionController` expõe `POST /v1/chat/completions` e `GET /v1/models` com autenticação via Bearer token
> - **`AddOpenAIChatCompletionServer()` não existe no MAF .NET** — endpoint implementado manualmente
> - Workflows API disponível para uso futuro: `AgentWorkflowBuilder.BuildSequential`, `BuildConcurrent`, `CreateHandoffBuilderWith`, `ChatProtocolExecutor`, checkpointing
> - Config adicionada em `appsettings.json` seção `ProtocolHosting` com flags A2A/AG-UI/OpenAI (todos habilitados)
> - CS8765 nullability warnings corrigidos em `ReflectionDelegatingAgent` e `QualityGateDelegatingAgent`
> - Target framework atualizado para .NET 10 (net10.0)

### Objetivo

Expor agentes via protocolos padronizados (A2A, AG-UI, OpenAI-compatible), permitindo que sistemas externos interajam com os agentes sem depender da API HTTP interna.

### Steps

#### Step 16 — Protocol Hosting (A2A, AG-UI, OpenAI-compatible)

O MAF oferece protocol hosting nativo via:

```csharp
// Program.cs ou ServiceCollectionExtensions.cs

// A2A (Agent-to-Agent protocol)
builder.Services.AddA2AServer();
app.MapA2AServer();

// AG-UI (Agent-UI protocol)
builder.Services.AddAgentUIServer();
app.MapAgentUIServer();

// OpenAI-compatible endpoints
builder.Services.AddOpenAIChatCompletionServer();
app.MapOpenAIChatCompletionServer();
```

**Benefícios:**
- Agentes do Agentic System acessíveis por outros sistemas via A2A
- Frontend pode interagir via AG-UI (streaming nativo, typed events)
- Compatibilidade com ferramentas que usam OpenAI API format
- Zero mudança na lógica dos agentes — apenas exposição de endpoints

**Requisitos:**
- Fase 3 completa (agents registrados via `AddAIAgent()`)
- `ISessionStore` implementado (sessões persistidas nativamente)
- Middleware pipeline configurado

### Arquivos Impactados — Fase 4

| Arquivo | Ação |
|---|---|
| `Api/Program.cs` | **Evoluir** — registrar e mapear protocol servers |
| `Api/appsettings.json` | **Evoluir** — configuração de endpoints de protocolo |
| `Infrastructure/Extensions/ServiceCollectionExtensions.cs` | **Evoluir** — `AddA2AServer()`, `AddAgentUIServer()` |

---

## 9. Grafo de Dependências

```
Step 1 — IFrameworkOrchestratorService interface
  │
  ▼
Step 2 — OrchestratorContextFactory ◄── Step 3 — FrameworkOrchestratorService
  │
  ▼
Step 4 — Reduzir AgentExecutionWorkflow
  │
  ▼
Step 5 — DI Registration
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━ Fase 1 completa ━━━
  │
  ▼
Step 5b — Migrar para AddAIAgent() hosting nativo
  │
  ▼
Step 6 — RAG via `MessageAIContextProvider`/`RAGContextProvider` ◄──► Step 7 — Handoff via tool binding  (paralelos)
  │
  ▼
Step 8 — Router/Analyzer como tools
  │
  ▼
Step 9 — Remover enrichedInput
  │
  ▼
Step 10 — Registrar tools no builder
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━ Fase 2 completa ━━━
  │
  ▼
Step 11 — Consolidar o caminho direto nativo
  │
  ▼
Step 12 — Reflection/QualityGates via middleware
  │
  ▼
Step 13 — Collaboration via AgentWorkflowBuilder ◄── Step 12
  │
  ▼
Step 14 — PostgresSessionStore (ISessionStore nativo) ← elimina AgentSessionBridge
  │
  ▼
Step 15 — Simplificar IAgentFactory
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━ Fase 3 completa ━━━
  │
  ▼
Step 16 — Protocol Hosting (A2A, AG-UI, OpenAI-compatible)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━ Fase 4 completa ━━━
```

---

## 10. Análise contra Documentação Oficial do MAF

### Pontos Validados ✅

| Conceito | Validação |
|---|---|
| `AsAIFunction()` para agent-as-tool | Documentado em "Using an Agent as a Function Tool" |
| `ChatClientAgent` como tipo base | Agente padrão para qualquer `IChatClient` |
| `AgentSession` serialização/restauração | `SerializeSession` / `DeserializeSessionAsync` documentados |
| Pipeline `AsBuilder().UseLogging().UseOpenTelemetry().Build()` | Middleware pattern suportado |
| Especialistas como tool bindings do supervisor | "The inner agent is converted to a function tool and provided to the outer agent" |
| `RunAsync(string input, AgentSession session)` — 2 args | Confirmado: sem CancellationToken |
| `AddAIAgent()` + `IHostedAgentBuilder` | Hosting nativo que resolve DI, session store e tools |
| `MessageAIContextProvider` | First-class concept para injeção automática de contexto; o projeto o usa via `RAGContextProvider` |
| Session store de hosting | `.WithInMemorySessionStore()` / `.WithSessionStore(...)` documentados |
| `AgentWorkflowBuilder` | `.BuildSequential` / `.BuildConcurrent` para orquestração multi-agent |
| Protocol hosting (A2A, AG-UI) | `AddA2AServer()` / `MapA2AServer()` documentados |

### Correções Aplicadas (vs versão inicial do plano) 🔧

#### 1. Steps 2-3 ignoravam `AddAIAgent()` + `IHostedAgentBuilder`

- **Problema:** Fase 1 construiu o orquestrador manualmente via `new ChatClientAgent(...).AsBuilder().Build()`
- **Correção:** Documentado como débito técnico da Fase 1. Step 5b (Fase 2) migra para hosting nativo via `AddAIAgent()`
- **Impacto:** lifecycle do agent, session store de hosting e tools passam a poder ser resolvidos pelo framework; middleware de reflection/quality gates segue custom

#### 2. Step 6 (RAG) não considerava `MessageAIContextProvider`

- **Problema:** RAG estava planejado apenas como `AIFunction` (tool), onde o LLM decide quando buscar contexto
- **Correção:** `MessageAIContextProvider`, concretizado no projeto por `RAGContextProvider`, passou a ser a opção primária. O provider injeta contexto automaticamente (determinístico); a `AIFunction` foi mantida como complemento para buscas ad-hoc
- **Trade-off:** Tool = LLM controla (pode esquecer); Provider = sempre injeta (mais confiável para RAG reranqueado)

#### 3. Step 12 (Reflection) planejado como pós-processamento manual

- **Problema:** Reflection seria chamado manualmente no `FrameworkOrchestratorService` pós-resposta
- **Correção:** Usar middleware custom sobre o pipeline do agent (`.UseReflection()`, `.UseQualityGates()`) via extensões da aplicação, já que o MAF 1.4 não expõe essas APIs nativamente
- **Impacto:** `FrameworkOrchestratorService` fica mais limpo; a dependência remanescente é apenas da camada de middleware local

#### 4. Step 13 (Collaboration) planejado como tool calls do supervisor

- **Problema:** Planner/executor/reviewer seriam convertidos em tool calls do orquestrador
- **Correção:** Usar `AgentWorkflowBuilder.BuildSequential([planner, executor, reviewer])` com `.AddAsAIAgent()` para expor o workflow como tool
- **Impacto:** Checkpointing, streaming nativo, paralelismo tipado, graph visualizável

#### 5. Step 14 (Sessão) mantinha `AgentSessionBridge` simplificada

- **Problema:** Bridge seria simplificada para forward-only, mas continuava existindo
- **Correção:** Reclassificado: o MAF 1.4 já oferece session store de hosting via `WithSessionStore(...)`; o adapter local já está integrado no fluxo principal e o trabalho restante é drenar a bridge dos paths residuais
- **Impacto:** a eliminação da bridge deixou de ser bloqueio do framework e virou backlog local de integração

#### 6. Protocol hosting não existia no plano

- **Problema:** Não havia previsão para expor agentes via protocolos padronizados
- **Correção:** Adicionada Fase 4 com A2A (`AddA2AServer()`), AG-UI, e OpenAI-compatible endpoints
- **Impacto:** Agentes acessíveis por sistemas externos sem depender da API HTTP interna

#### 7. Clarificação agent-as-tool vs workflow

- **Problema:** Não estava claro quando usar `AsAIFunction()` vs `AgentWorkflowBuilder`
- **Correção:** Documentado explicitamente:
  - `AsAIFunction()` → supervisor + especialistas (open-ended, LLM decide)
  - `AgentWorkflowBuilder` → planner → executor → reviewer (determinístico, grafo define)
  - Workflow pode ser exposto como tool do supervisor via `.AddAsAIAgent()`

### Pontos Críticos de Atenção ⚠️

#### 1. `AgentGroupChat` e `AgentOrchestrator` NÃO EXISTEM no MAF

O `AI_Capabilities_Gaps.md` referencia como backlog, mas são abstrações do Semantic Kernel/AutoGen não portadas. A decisão de usar agent-as-tool é a abordagem correta para o MAF atual.

#### 2. MAF Workflows é o mecanismo nativo de orquestração multi-agent

`AgentWorkflowBuilder` + executors + edges formam grafos tipados com checkpointing, human-in-the-loop (via `RequestInfoExecutor`), streaming e parallel execution. No estado atual do projeto, essa capacidade já foi integrada ao `AgentCollaborationWorkflow` no caminho planner → executor → reviewer; os ganhos incrementais restantes estão em ampliar paralelismo e checkpointing onde fizer sentido.

#### 3. Sessões são agent-specific

Doc oficial: *"Sessions are agent/service-specific. Reusing a session with a different agent configuration or provider can lead to invalid context."* Confirma que sessões devem ser separadas por agente no `PostgresSessionStore`.

#### 4. Agent vs Workflow — trade-off fundamental

| | Agent (supervisor-with-tools) | Workflow (AgentWorkflowBuilder) |
|---|---|---|
| Melhor para | Open-ended, conversational, autonomous | Well-defined steps, explicit control |
| Decisão | LLM decide (não-determinístico) | Grafo decide (determinístico) |
| Uso no plano | Orquestrador → especialistas (Fases 1-2) | Planner → executor → reviewer (Fase 3) |
| Composição | Agentes expostos via `AsAIFunction()` | Agents em `BuildSequential`, workflow via `.AddAsAIAgent()` |

### Recomendação Consolidada

- **Implementar agora:** simplificar a composição manual que ainda existe sobre `AddAIAgent()` + `IHostedAgentBuilder` e cortar o código anterior já bypassado assim que cada slice estiver validado.
- **Implementado no corte atual:** o path direto converge em `AgentFrameworkDirectExecutionService`, o protocolo aponta direto para o hosted orchestrator e o código transitório de sessão saiu do runtime.
- **Manter como KEEP:** `FinalResponseApprovalService` e `SmartRouter`/`PersistentSmartRouter`, pois seguem em uso e agregam valor no desenho atual.
- **Adiar por bloqueio real do framework:** substituição por middleware nativo de reflection/quality gates; hoje isso continua dependendo de extensões da aplicação.

---

## 11. Riscos e Mitigações

| # | Risco | Probabilidade | Impacto | Mitigação |
|:---:|---|:---:|:---:|---|
| 1 | System prompt do orquestrador mal calibrado → seleção incorreta de tools | Média | Alto | Testar com cenários existentes (domain mismatch, multi-domain, planning required) |
| 2 | Latência extra por camada de LLM na decisão de roteamento | Média | Médio | Cachear decisões de routing; usar model menor para orquestrador |
| 3 | Muitos tool bindings confundem o LLM | Baixa | Alto | Limitar tools visíveis por domínio; descriptions claras e concisas |
| 4 | Perda de determinismo no pipeline | Média | Médio | Supervisor-with-tools nas Fases 1-2; `AgentWorkflowBuilder` na Fase 3 para fluxos determinísticos |
| 5 | Sessão do orquestrador cresce demais | Baixa | Médio | Context budget management; truncar histórico do orquestrador |
| 6 | Reflection/CorrectionLoop perdem eficácia fora do workflow | Baixa | Baixo | Middleware nativo (`.UseReflection()`, `.UseQualityGates()`) na Fase 3 |
| 7 | `MessageAIContextProvider` / `RAGContextProvider` injeta contexto excessivo | Média | Médio | Implementar budget/relevance filter no provider; monitorar token usage |
| 8 | Migração `AddAIAgent()` quebra construção manual existente | Baixa | Alto | Step 5b deve conviver por pouco tempo com a construção manual; validar por slice e cortar o builder manual em seguida |
| 9 | `ISessionStore` PostgreSQL performance com sessões grandes | Baixa | Médio | Serialização compacta; TTL para sessões inativas; índice por `sessionId` |
| 10 | Protocol hosting (A2A) expõe superfície de ataque | Média | Alto | Autenticação obrigatória em endpoints de protocolo; rate limiting; audit logging |

---

## 12. Critérios de Sucesso por Fase

### Fase 1 ✅

- [x] `AgentExecutionWorkflow.ExecuteAsync` não chama mais `_contextAnalyzer.AnalyzeAsync` nem `_agentFactory.ResolveAgentAsync` diretamente (quando orquestrador disponível)
- [x] Toda requisição passa por `IFrameworkOrchestratorService.ExecuteAsync`
- [x] `ExecuteAsync` do runtime principal já não mantém fallback imperativo para o caminho anterior
- [x] `ExecuteDirectAsync` inalterado
- [x] Build compila sem erros

### Fase 2

- [x] Não existe mais `enrichedInput` montado manualmente no workflow
- [x] `MessageAIContextProvider` injeta contexto RAG automaticamente antes de cada request
- [x] `AIFunction` RAG disponível como complemento para buscas ad-hoc
- [x] Handoff acontece por tool binding; o `HandoffManager` legado foi removido do runtime
- [x] SmartRouter e ContextAnalyzer disponíveis como tools auxiliares
- [x] Orquestrador registrado via `AddAIAgent()` (hosting nativo, Step 5b), ainda com composição local via `OrchestratorContextFactory`
- [x] Tests existentes continuam passando

> **Revalidação 2026-05:** o item de hosting nativo do orquestrador principal não está mais bloqueado pelo MAF 1.4 e já está no runtime; a dívida local remanescente ficou restrita à composição por sessão do orquestrador.

### Fase 3

- [x] O workflow não escolhe mais agente
- [x] A sessão principal de conversa é a do framework via `ISessionStore` (`PostgresSessionStore` quando configurado), usando `SimpleSessionStoreAdapter` no hosting nativo
- [x] `AgentSessionBridge` eliminada do runtime
- [x] Especialistas são chamados pelo framework no caminho principal; `ExecuteDirectAsync` permanece como escape hatch explícito
- [ ] Reflection via middleware nativo (`.UseReflection()`)
- [ ] Quality gates via middleware nativo (`.UseQualityGates()`)
- [x] `AgentCollaborationWorkflow` migrado para `AgentWorkflowBuilder.BuildSequential`
- [x] `AgentFrameworkDirectExecutionService` concentra a execução nativa do `ExecuteDirectAsync`

> **Revalidação 2026-05:** `AgentWorkflowBuilder` e `WithSessionStore(...)` existem no MAF 1.4 e já estão no runtime principal. Os únicos itens realmente abertos desta fase são `UseReflection()` / `UseQualityGates()` nativos, ainda ausentes no framework.

### Fase 4

- [x] Agentes acessíveis via A2A protocol — `AddA2AServer("AgenticSystem")` + `MapA2AHttpJson("/a2a")` via `Microsoft.Agents.AI.Hosting.A2A.AspNetCore` 1.4.0-preview
- [x] AG-UI endpoints ativos para frontend — `AddAGUI()` + `MapAGUI("/agui")` via `Microsoft.Agents.AI.Hosting.AGUI.AspNetCore` 1.4.0-preview
- [x] OpenAI-compatible endpoints para interoperabilidade — `POST /v1/chat/completions` + `GET /v1/models`
- [x] Autenticação em endpoints de protocolo — Bearer token validado contra `AdminApiKey` + `.RequireAuthorization()` em A2A/AG-UI
- [x] Rate limiting em endpoints de protocolo — policy `ProtocolEndpoints` aplicada em A2A, AG-UI e OpenAI-compatible
- [ ] Tests de integração para cada protocolo
- [x] ✅ Agent do protocolo integrado com o hosted orchestrator via alias direto de `AddAIAgent("AgenticSystem")` + reuso do `AgentSessionStore` do orquestrador
- [x] MAF atualizado para 1.4.0 + Workflows package adicionado
- [x] A2A + AG-UI packages (1.4.0-preview.260505.1) instalados
- [x] Config `ProtocolHosting` no appsettings.json (todos habilitados)
- [x] Target framework .NET 10 (net10.0)
- [x] Build compila sem erros

> **Pendências locais desta fase:** testes de integração dos endpoints de protocolo. Nenhuma dessas pendências depende de limitação atual do MAF.

---

## 13. Decisões Globais

| Decisão | Detalhes |
|---|---|
| **Feature flag para cutover seguro** | Foi usada apenas durante a transição. O runtime principal atual exige `IFrameworkOrchestratorService` e não mantém mais o caminho anterior em `ExecuteAsync` |
| **Escopo excluído** | Autenticação, controllers, telemetria cross-cutting, persistência EF — continuam fora do framework |
| **Interface pública inalterada** | `IAgentExecutionWorkflow` não muda — consumidores (controllers, hubs) não são afetados |
| **Testes devem continuar passando** | Behavior externo não muda em cada fase; apenas orquestração interna |
| **`ExecuteDirectAsync` preservado** | Escape hatch para seleção manual de agente pelo frontend |
| **Supervisor-with-tools para Fases 1-2** | Padrão documentado no MAF; simples de implementar e reverter internamente durante o desenvolvimento |
| **`AddAIAgent()` como target de hosting** | Disponível no MAF 1.4 e já usado no protocolo e no fluxo principal; a dívida local remanescente é só a composição por sessão no `OrchestratorContextFactory` |
| **`MessageAIContextProvider` para RAG** | Injeção automática e determinística de contexto; `RAGContextProvider` é a concretização usada no projeto, com `AIFunction` como complemento |
| **Middleware de reflection/quality gates** | Hoje continua custom sobre o builder; o MAF 1.4 não expõe `.UseReflection()` / `.UseQualityGates()` nativos |
| **`AgentWorkflowBuilder` para collaboration** | Disponível no MAF 1.4 e já integrado ao fluxo planner-executor-reviewer |
| **Session store de hosting** | `WithSessionStore(...)` existe no MAF 1.4; o projeto já usa `SimpleSessionStoreAdapter` + store da aplicação |
| **Protocol hosting na Fase 4** | A2A, AG-UI, OpenAI-compatible para interoperabilidade externa |

---

## 14. Análise de Dead Code, Duplicidades e Fluxos Não Utilizados

> **Data da análise:** 2026-05-05 (Pós-Fase 4)  
> **Status:** ✅ Revalidação completa — 9 de 11 findings resolvidos  
> **Análise anterior:** 2026-05-05 (Pós-Fase 1) — 10 findings originais
>
> ✅ **Findings 1, 2, 3, 4, 5 (parcial), 8, 9, 10, 11 resolvidos.** Findings 6-7 (FinalResponseApprovalService, SmartRouter) mantidos como ativamente usados.
>
> **Nota de leitura:** quando esta seção mencionar wrappers, adapters ou factories já removidos, trate isso como nomenclatura histórica do finding original. A situação atual do runtime está refletida nas colunas de resolução e nas revalidações abaixo.

### Resumo Executivo

| # | Item | Tipo | Severidade | Status | Resolvido por |
|---|------|------|:----------:|:------:|:------------:|
| 1 | `EfSessionStore` + `UseEntityFramework()` | Dead code | 🔴 Alta | ✅ Resolvido | Remoção direta — `EfSessionStore.cs`, `UseEntityFramework()` e `EfSessionStoreTests.cs` deletados |
| 2 | `AgentSessionBridge` vs `ISessionStore` | Duplicidade ativa | 🟡 Média | ✅ Resolvido | Runtime migrado para `SimpleSessionStoreAdapter`; bridge removida |
| 3 | `ContextAnalyzer`, `SmartRouter`, `HandoffManager` (caminho anterior) | Dead code com framework ativo | 🔴 Alta | ✅ Resolvido | Dependências do caminho anterior removidas do `AgentExecutionWorkflow`; `ExecuteAsync` delega diretamente ao framework e o `HandoffManager` legado foi deletado |
| 4 | Integration Providers (Calendar, Email, Notes, Storage, Vision) | Dead code — nunca injetados | 🔴 Alta | ✅ Resolvido | Remoção completa — 5 providers + interfaces + models + config + testes deletados |
| 5 | `ILLMManager` paralelo a `IChatClient` | Duplicidade de abstração LLM | 🟢 Baixa ⬇️ | ✅ Resolvido | Runtime migrou para `IChatClient`; a superfície administrativa foi separada em `ILLMAdministrationService`, consumida pelo `LLMController` |
| 6 | `FinalResponseApprovalService` | Dead code com framework ativo | 🟠 Média-Alta | ⬜ Pendente | KEEP — ativamente usado no `AgentController` e no `AgentExecutionPostProcessingPipeline` |
| 7 | `PersistentSmartRouter` + `SmartRouter` | Dead code com framework ativo | 🟠 Média-Alta | ⬜ Pendente | KEEP — ambos ativamente usados no pipeline de roteamento |
| 8 | `InMemory*` stores sem equivalente PostgreSQL | Gap de persistência | 🟡 Média | ✅ Resolvido | `PostgresMigrationJobStore` e `PostgresEmbeddingModelStore` criados + migração SQL V002 + registro em `UseLocalExecutionStorageMode()` |
| 9 | `IRuntimeEvaluator` null em dev | Gap de ambiente | 🟢 Baixa ⬇️ | ✅ Resolvido | `InMemoryRuntimeEvaluator` criado + registro incondicional em `AddAgenticSystemCore()` |
| 10 | `InMemorySkillManager` / `InMemoryToolManager` | Sem persistência | 🟡 Média | ✅ Resolvido (análise) | InMemory É o design correto — runtime services re-seeded via `SeedAgenticDefaults()` |
| **11** | **`AddAIAgent` vs composição local do orquestrador** | **Duplicidade funcional** | **🟠 Média-Alta** | **✅ Resolvido** | **`AddAIAgent("AgenticSystem")` agora aponta direto para o hosted orchestrator com reuso do session store do protocolo** |

### Classificação Atual — Bloqueio Real do MAF vs Dívida Local

> **Base da revalidação:** pacote `Microsoft.Agents.AI.Hosting` 1.4.0-preview.260505.1 e pacote `Microsoft.Agents.AI.Workflows` 1.4.0 instalados no ambiente local.

| Item | Classificação | Evidência / motivo | Situação atual |
|---|---|---|---|
| `UseReflection()` / `UseQualityGates()` nativos | **Bloqueio real do MAF** | APIs nativas não aparecem no MAF 1.4 instalado | Continuar com extensões custom da aplicação |
| Hosting principal via `AddAIAgent()` + `IHostedAgentBuilder` | **✅ Resolvido no runtime** | APIs existem e já estão no fluxo principal | A resolução scoped foi absorvida pelo `OrchestratorContextFactory`; resta apenas enxugar a composição por sessão dentro dele |
| `AgentWorkflowBuilder` / `BuildSequential` | **✅ Resolvido** | Package 1.4 instalado com APIs documentadas | `AgentCollaborationWorkflow` já usa `BuildSequential` no path principal |
| Session store de hosting via `WithSessionStore(...)` | **✅ Resolvido** | Capability existe no MAF 1.4 e já está no fluxo principal | `SimpleSessionStoreAdapter` atende fluxo principal e tool bindings |
| Path direto nativo / obsolete paths anteriores | **✅ Resolvido** | O path explícito converge em `AgentFrameworkDirectExecutionService` e o catálogo continua via `ResolveAgentAsync` | Os wrappers e paths obsoletos foram removidos do runtime |
| Superfície administrativa do LLM | **✅ Resolvido** | Não dependia de nova API do MAF | `LLMController` agora consome `ILLMAdministrationService`; o contrato público `ILLMManager` foi removido |
| `FinalResponseApprovalService` | **KEEP** | Continua em uso no controller e no `AgentExecutionPostProcessingPipeline` compartilhado | Manter enquanto o fluxo de aprovação humana continuar válido |
| `SmartRouter` / `PersistentSmartRouter` | **KEEP** | Continua em uso como tool auxiliar do orquestrador | Manter enquanto continuar agregando valor no roteamento e nas métricas |
| Rate limiting e testes de integração de protocolo | **Backlog local** | Checklist da Fase 4 ainda aberto | Não depende do MAF |

### Tech Debts já Implementáveis com o MAF 1.4 Atual

1. **Continuar enxugando a composição por sessão do orquestrador**, hoje concentrada no `OrchestratorContextFactory`.
2. **Manter o hosting principal em `AddAIAgent()`**, agora já revalidado no runtime, e evitar recriar uma cadeia paralela de resolução scoped.
3. **Manter `LLMManager` como detalhe concreto de infraestrutura**; a antiga interface pública de runtime já foi removida.
4. **Apagar código anterior já bypassado** no `AgentExecutionWorkflow` e nas integrações associadas assim que cada fatia estiver revalidada.

### Recomendação Objetiva

1. **Vale implementar agora:** enxugar a composição por sessão do orquestrador, backlog de protocolo (rate limiting + testes) e qualquer limpeza documental residual do legado já removido.
2. **Já implementado:** `AgentFrameworkDirectExecutionService` concentra o `ExecuteDirectAsync`, o protocolo aponta direto para o hosted orchestrator e o código transitório de sessão saiu do runtime.
3. **Deve continuar como KEEP:** `FinalResponseApprovalService` e `SmartRouter`/`PersistentSmartRouter`.
4. **Deve continuar como workaround local:** reflection e quality gates, até o MAF oferecer middleware nativo para isso.

### Detalhamento dos Findings — Estado Atual Corrigido

#### Finding 1 — 🔴 `EfSessionStore` + `UseEntityFramework()`

**Status atual: ✅ RESOLVIDO**

`EfSessionStore`, o registro `UseEntityFramework()` e seus testes foram removidos. O pipeline real permanece em `UseLocalExecutionStorageMode()` com store PostgreSQL raw.

#### Finding 2 — 🟡 `AgentSessionBridge` vs `ISessionStore`

**Status atual: ✅ RESOLVIDO**

O runtime passou a usar apenas `SimpleSessionStoreAdapter`. `AgentSessionBridge` foi removida depois que o fluxo principal, tool bindings e o path direto deixaram de depender dela.

#### Finding 3 — 🔴 Serviços do caminho anterior bypassed pelo framework

**Status atual: ✅ RESOLVIDO**

`AgentExecutionWorkflow.ExecuteAsync()` delega diretamente ao framework. O `HandoffManager`, sua interface pública e seus testes foram removidos do runtime; o restante da dívida local está concentrado na composição hosted do orquestrador, não mais no fluxo imperativo antigo.

#### Finding 4 — 🔴 Integration Providers sem consumidores

**Status atual: ✅ RESOLVIDO**

Os providers de Calendar, Email, Storage, Notes e Vision foram removidos junto das interfaces/configurações associadas.

#### Finding 5 — 🟠 `ILLMManager` vs `IChatClient`

**Status atual: ✅ RESOLVIDO**

`ContextAnalyzer`, `SessionConsolidator`, `BaseAgent`, `DynamicAgentService` e `HierarchicalAgentFactory` já migraram para `IChatClient`. O `LLMController` agora consome `ILLMAdministrationService`, e o contrato público `ILLMManager` foi removido do código.

#### Finding 6 — 🟠 `FinalResponseApprovalService`

**Status atual: KEEP**

Não é dead code. Continua em uso no `AgentController`. Quando o path anterior do workflow for cortado, a ligação restante deve ficar limitada ao uso real de controller.

#### Finding 7 — 🟠 `PersistentSmartRouter` + `SmartRouter`

**Status atual: KEEP**

Não é dead code. O roteador segue útil como tool auxiliar do orquestrador e como camada de persistência de métricas.

#### Finding 8 — 🟡 Stores in-memory sem equivalente PostgreSQL

**Status atual: ✅ RESOLVIDO**

`PostgresMigrationJobStore` e `PostgresEmbeddingModelStore` foram criados, registrados e suportados por migração SQL própria.

#### Finding 9 — 🟢 `IRuntimeEvaluator` null em dev

**Status atual: ✅ RESOLVIDO**

`InMemoryRuntimeEvaluator` foi criado e passou a ser registrado incondicionalmente como fallback.

#### Finding 10 — 🟡 `InMemorySkillManager` / `InMemoryToolManager`

**Status atual: ✅ RESOLVIDO (por análise)**

O comportamento in-memory foi mantido por decisão de design. Skills e tools built-in são re-seeded em startup, e não há necessidade técnica imediata de persistência para esse par de managers.

#### Finding 11 — ✅ RESOLVIDO — `AddAIAgent` integrado diretamente ao hosted orchestrator

**Status: ✅ Resolvido (Fase 4b)**

**Problema original:** a surface de protocolo não reutilizava o hosted orchestrator completo. Requests A2A/AG-UI podiam cair em uma montagem parcial sem tools, RAG ou especialistas.

**Solução implementada:** `AddAIAgent("AgenticSystem")` passou a resolver diretamente o `AIAgent` hospedado do orquestrador via DI, reutilizando o `AgentSessionStore` keyed do runtime principal. Assim, A2A/AG-UI usam o mesmo pipeline completo (tools, RAG, especialistas e middleware) sem wrapper intermediário dedicado.

```
A2A/AG-UI request
  → MAF ChatClientAgent (AddAIAgent("AgenticSystem"))
    → hosted orchestrator resolvido via DI
      → AgentSessionStore keyed do runtime principal
        → OrchestratorContextFactory (tools, RAG, specialists, middleware)
          → Resposta com capacidades completas
```

**Arquivos criados/modificados:**
- `Api/Program.cs` — `AddAIAgent("AgenticSystem")` agora resolve o hosted orchestrator nativo
- `Infrastructure/Extensions/ServiceCollectionExtensions.cs` — remoção do keyed `IChatClient` transitório e consolidação do runtime no agente hospedado
- `Infrastructure/AgentFramework/SimpleSessionStoreAdapter.cs` — reuso do store do framework no protocolo e nos tool bindings

### Mapa de Resolução por Fase

```
Fase 2 — Mover Cross-Cutting Concerns
├─ Finding 3: ContextAnalyzer → tool "analyze_request"
├─ Finding 3: SmartRouter → tool "route_to_best_agent"
├─ Finding 4: Integration Providers → AIFunction tools (decisão)
├─ Finding 6: FinalResponseApprovalService → KEEP (fluxo de aprovação humana continua válido)
└─ Finding 7: PersistentSmartRouter → KEEP (métricas preservadas via tool)

Fase 3 — Remover Duplicidade Arquitetural
├─ Finding 1: EfSessionStore → ✅ REMOVIDO
├─ Finding 2: AgentSessionBridge → ✅ resolvido via `WithSessionStore(...)` + `SimpleSessionStoreAdapter`
├─ Finding 3: HandoffManager → ✅ removido do runtime e dos testes
├─ ConfidenceScoreCalculator → KEEP no `AgentExecutionPostProcessingPipeline`
├─ Finding 5: runtime migrado para IChatClient + superfície administrativa separada em ILLMAdministrationService
├─ Finding 8: InMemoryMigrationJobStore, InMemoryEmbeddingModelStore → ✅ PostgreSQL criado
├─ Finding 9: IRuntimeEvaluator → ✅ registro incondicional
└─ Finding 10: InMemorySkillManager, InMemoryToolManager → ✅ sem ação (design validado)

Fase 4b — Integração Protocol Agent ✅
└─ Finding 11: ✅ RESOLVIDO — `AddAIAgent("AgenticSystem")` aponta direto para o hosted orchestrator

Manual (qualquer momento):
└─ Backlog local: rate limiting e testes de integração dos endpoints de protocolo
```

### Checklist de Revalidação

> ✅ **Revalidação Fase 4 concluída** (2026-05-05) — 10 findings originais revalidados, 1 novo finding identificado
> ✅ **Resolução em lote** (2026-05-06) — Findings 1, 3, 4, 5 (parcial), 8, 9, 10 resolvidos. 12 arquivos deletados, 5 criados.
> ✅ **Revalidação de suíte** (2026-05-06) — 553 de 553 testes passando após remoção do `HandoffManager` legado e correções do slice final da suíte.
> ✅ **Simplificação local do hosting principal** (2026-05-06) — a resolução scoped foi absorvida pelo `OrchestratorContextFactory`, e a composição local ficou concentrada em `OrchestratorContextFactory` + `OrchestratorHostBuilder`.
> ✅ **Rate limiting de protocolo** (2026-05-06) — policy `ProtocolEndpoints` aplicada em A2A, AG-UI e OpenAI-compatible com configuração em `ProtocolHosting:RateLimiting`.
> 🔲 **Pendentes:** Findings 6-7 (KEEP — ativamente usados)
> ✅ **Fase 4b concluída** (Finding 11 resolvido) — `AddAIAgent("AgenticSystem")` integra o protocol agent diretamente ao hosted orchestrator

---

## Changelog

| Data | Fase | Ação |
|---|---|---|
| 2026-05-05 | Fase 1 | Implementação completa — 3 arquivos criados, 3 modificados, build ok |
| 2026-05-05 | Todas | Revisão completa contra doc oficial MAF 1.3.0+ — 7 correções aplicadas: `AddAIAgent()` hosting, `MessageAIContextProvider` para RAG, middleware para reflection, `AgentWorkflowBuilder` para collaboration, `ISessionStore` nativo, protocol hosting (Fase 4), clarificação agent-vs-workflow |
| 2026-05-05 | Seção 14 | Análise de dead code, duplicidades e fluxos não utilizados — 10 findings identificados com severidade e mapa de resolução por fase |
| 2026-05-05 | Fase 4b | Finding 11 resolvido — `AddAIAgent("AgenticSystem")` passou a apontar direto para o hosted orchestrator, reusando o pipeline completo sem wrapper transitório |
| 2026-05-06 | Seção 14 | Resolução em lote dos findings: F1 (EfSessionStore removido), F3 (workflow simplificado), F4 (providers removidos), F5 parcial (ContextAnalyzer/SessionConsolidator → IChatClient), F8 (PostgresMigrationJobStore/PostgresEmbeddingModelStore criados + V002 SQL), F9 (InMemoryRuntimeEvaluator), F10 (validado como design correto). 12 arquivos deletados, 5 criados, 8+ modificados. Build: 0 errors. |
| 2026-05-06 | Seções 10, 12 e 14 | Revalidação contra MAF 1.4: separação explícita entre bloqueio real do framework e dívida local; lista de tech debts já implementáveis; recomendação objetiva do que vale fazer agora e do que deve permanecer como KEEP. |
| 2026-05-06 | Fase 3 | `AgentCollaborationWorkflow` migrado para `AgentWorkflowBuilder.BuildSequential` com execução via `InProcessExecution`; o path hosted do orquestrador deixou de depender de wrappers transitórios para o caminho direto. |
| 2026-05-06 | Fase 3 | `HandoffManager` legado removido do runtime e dos testes; `AgentExecutionWorkflow` segue fino, a suíte completa ficou verde (553/553), e o legado remanescente ficou concentrado na composição local do orquestrador hosted. |
| 2026-05-06 | Fase 3 | A resolução scoped do `OrchestratorContext` ficou concentrada no `OrchestratorContextFactory`, com a composição local apoiada pelo `OrchestratorHostBuilder`; os checks da Fase 3 mantiveram em aberto apenas os middlewares nativos ainda ausentes no MAF. |
| 2026-05-06 | Fases 3 e 4 | `OrchestratorHostedAgentFactory` foi absorvido pelo `OrchestratorContextFactory`, e os endpoints de protocolo passaram a usar rate limiting nativo (`ProtocolEndpoints`) em A2A, AG-UI e OpenAI-compatible. |
