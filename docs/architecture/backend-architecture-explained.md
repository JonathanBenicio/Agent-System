# Backend & Frontend AgenticSystem — Arquitetura de Referência Consolidada

> **Documento canônico de arquitetura de software (SST - Single Source of Truth)**. Este arquivo consolida todas as decisões arquiteturais, topologias, fluxos de execução do backend e frontend, substituindo e unificando o antigo `TECHNICAL_ARCHITECTURE_GUIDE.md`.
>
> O sistema opera em modo **framework-first** no fluxo principal, usando o **Microsoft Agent Framework (MAF) 1.5.0** como runtime nativo consolidado (transição 100% concluída), com suporte a fluxos colaborativos e múltiplos canais de interface.

---

## Sumário

1. [Visão Geral da Arquitetura](#1-visão-geral-da-arquitetura)
2. [Governança de Escopo (Core x Laboratório)](#2-governança-de-escopo-core-x-laboratório)
3. [Stack Tecnológico](#3-stack-tecnológico)
4. [Camadas e Responsabilidades](#4-camadas-e-responsabilidades)
5. [Inicialização, Registro de Dependências e Pipeline LLM](#5-inicialização-registro-de-dependências-e-pipeline-llm)
6. [Padrão de Orquestração — Supervisor-with-Tools](#6-padrão-de-orquestração--supervisor-with-tools)
7. [Fluxos de Request (REST & Real-Time)](#7-fluxos-de-request-rest--real-time)
8. [RAG Pipeline — RAGContextProvider + retrieve_context](#8-rag-pipeline--ragcontextprovider--retrieve_context)
9. [Middleware Pipeline e Auto-Ajuste (Correction Loop)](#9-middleware-pipeline-e-auto-ajuste-correction-loop)
10. [Workflow de Colaboração — AgentWorkflowBuilder](#10-workflow-de-colaboração--agentworkflowbuilder)
11. [Gestão de Sessões — ISessionStore + ISessionManager](#11-gestão-de-sessões--isessionstore--isessionmanager)
12. [Sistema de Tools — MCP, Built-in e Tool Versioning](#12-sistema-de-tools--mcp-built-in-e-tool-versioning)
13. [Ciclo de Vida dos Agentes](#13-ciclo-de-vida-dos-agentes)
14. [Multi-Tenant](#14-multi-tenant)
15. [Autenticação e Segurança](#15-autenticação-e-segurança)
16. [Service Gateway — Resiliência e Circuit Breaker](#16-service-gateway--resiliência-e-circuit-breaker)
17. [SignalR — Comunicação Real-Time](#17-signalr--comunicação-real-time)
18. [Protocol Hosting — A2A, AG-UI e OpenAI-Compatible](#18-protocol-hosting--a2a-ag-ui-e-openai-compatible)
19. [Funcionalidades Transversais e Scheduler DAG-lite](#19-funcionalidades-transversais-e-scheduler-dag-lite)
20. [Observabilidade e Monitoramento](#20-observabilidade-e-monitoramento)
21. [Frontend — SPA React & Vite](#21-frontend--spa-react--vite)
22. [Over-Engineering Check & Simplificações](#22-over-engineering-check--simplificações)
23. [Padrões Arquiteturais Utilizados](#23-padrões-arquiteturais-utilizados)
24. [Apêndice A: Mapa Geral de Arquivos](#apêndice-a-mapa-geral-de-arquivos)
25. [Apêndice B: Platform Capabilities](#apêndice-b-platform-capabilities)
26. [Smart Routing & Triage — N-Tier Pipeline](smart-routing-triage.md)
27. [Glossário](#glossário)

---

## 1. Visão Geral da Arquitetura

O AgenticSystem é uma plataforma corporativa multi-agent construída sobre o **.NET 10** e o **Microsoft Agent Framework (MAF) 1.5.0**. O sistema expõe agentes de inteligência artificial especializados por domínio (Personal, Work, Learning, Creative, Finance, Health, etc.) que são coordenados por um orquestrador central usando o padrão **Supervisor-with-Tools**.

O LLM do orquestrador decide dinamicamente para qual especialista delegar a tarefa com base no input do usuário, eliminando as antigas regras imperativas de roteamento do fluxo principal. Cada especialista é encapsulado e exposto como uma `AIFunction` do orquestrador por meio do método nativo `.AsAIFunction()`.

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
│       ├─ OrchestratorContextFactory → OrchestratorHostBuilder    │
│       ├─ WithAITool(specialist_1)    ← AsAIFunction()            │
│       ├─ WithAITool(specialist_N)    ← AsAIFunction()            │
│       ├─ Tools auxiliares (RAG / Router / Analyzer)             │
│       ├─ UseAIContextProviders(RAGContextProvider)               │
│       ├─ WithSessionStore(SimpleSessionStoreAdapter)             │
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

## 2. Governança de Escopo (Core x Laboratório)

Para manter o controle de complexidade do sistema, adotamos uma separação rígida entre as capacidades estáveis de produção e as trilhas de experimentação.

### 2.1 Core de Produto
O núcleo estável do produto deve passar por validações severas de CI, testes unitários de regressão e possuir acoplamento limpo. É composto por:
*   Interface de Chat Principal (SignalR) com streaming ponta a ponta.
*   Gestão de sessões corporativas e controle de ciclo de vida.
*   Um único caminho principal de execução orquestrada (`FrameworkOrchestratorService`).
*   Observabilidade essencial de custos, latência e traces de execução.

### 2.2 Trilhas de Laboratório
Capacidades em fase experimental ou protótipos de pesquisa não devem alterar o Core. Elas são isoladas usando:
*   **Feature Flags**: Ativação explícita em arquivos de configuração.
*   **Módulos Separados**: Classes em namespaces ou subpastas dedicadas.
*   **Rollout Opcional & Fallbacks**: Se a capacidade falhar ou estiver desabilitada, o comportamento padrão do Core assume imediatamente.

*Exemplos típicos*: Protocolos extras experimentais, fluxos concorrentes avançados de colaboração (`BuildConcurrent`), loops de auto-aperfeiçoamento profundo (`Self-Improvement`) e dashboards administrativos dedicados.

---

## 3. Stack Tecnológico

| **Camada** | **Tecnologia** | **Escopo / Papel** |
|---|---|---|
| **Runtime** | .NET 10 | ASP.NET Core Runtime para o Backend |
| **Framework de Agentes** | Microsoft Agent Framework 1.5.0 | `Microsoft.Agents.AI`, `Microsoft.Agents.AI.Hosting`, `Microsoft.Agents.AI.Workflows` |
| **Abstração LLM** | `IChatClient` (M.E.AI) | Abstração comum de chat (Microsoft.Extensions.AI) |
| **Geração de Embeddings**| `IEmbeddingGenerator<string, Embedding<float>>` | Abstração comum de vetores (Microsoft.Extensions.AI) |
| **Vector Store** | In-Memory / PostgreSQL (pgvector) | Armazenamento de embeddings semânticos |
| **Base de Dados** | PostgreSQL | Armazenamento relacional e operacional via EF Core |
| **Comunicação Real-Time**| SignalR | Hubs WebSocket para Chat e Dashboard Gateway |
| **Frontend** | React 19 + Vite 8 + TS 6 | Single Page Application moderna |
| **MCP** | Model Context Protocol | Servidor interno com HttpTransport e integrações clientes |
| **Estilização** | TailwindCSS v4 | Interface moderna zinc-950 com acentos violetas |
| **Autenticação** | JWT Bearer + API Keys | Middleware customizado com `PolicyScheme "MultiAuth"` |

---

## 4. Camadas e Responsabilidades

### 4.1 `AgenticSystem.Api` — Apresentação
Camada externa que gerencia a entrada de requests, canais de comunicação e infraestrutura HTTP/WebSocket:
*   **Controllers**: REST endpoints para CRUD de agentes, controle de sessões, upload de documentos e gateway administrativo.
*   **Hubs SignalR**: `ChatHub` (streaming de tokens, eventos de execução) e `GatewayHub` (status de microsserviços em tempo real).
*   **Middlewares**: `TenantMiddleware` (resolução dinâmica de tenant por JWT ou Header) e `ApiKeyAuthHandler`.
*   **Protocol Hosting**: Mapeamentos HTTP para interoperabilidade via A2A, AG-UI e controllers OpenAI-compatible (`/v1/chat/completions`).

### 4.2 `AgenticSystem.Core` — Domínio e Regras de Negócio
Independente de frameworks externos de orquestração. **Não referencia o MAF diretamente**, trabalhando sobre interfaces:
*   **Domain Agents**: Agentes base (`BaseAgent`) e as especializações (Work, Personal, Learning, etc.).
*   **Business Workflows**: `MetaAgentOrchestrator` (fachada central), `SmartRouter` (roteamento semântico), `TriageService` (classificação de urgência e intenção) e `AgentExecutionWorkflow`. Detalhes em [Smart Routing & Triage](smart-routing-triage.md).
*   **Services**: `ConfidenceScoreCalculator`, `SessionManager`, `SessionConsolidator` (compactação e sumarização de histórico), `DirectAgentRequestExecutor` (fast-path de execução direta), `ScheduledTaskManager` (Scheduler), e `ReflectionEngine`.

### 4.3 `AgenticSystem.Infrastructure` — Implementações e Conectores
Camada que implementa as interfaces do Core usando tecnologias e frameworks específicos:
*   **AgentFramework**: Integração real com o Microsoft Agent Framework. Contém `FrameworkOrchestratorService`, `PostgresSessionStore`, `OrchestratorContextFactory` e `FrameworkAgentChannelService`.
*   **LLM Pipeline**: Abstrações baseadas em `Microsoft.Extensions.AI`, contendo decorators como `GovernedChatClient`, `ContextAwareChatClient` e `SemanticCacheChatClient`.
*   **RAG / VectorStore**: Mecanismo de busca semântica, contendo o `RAGService`, `LlmReRanker`, ONNX CrossEncoder local, gerenciadores de arquivo e o `FileObsidianSync` para alimentação reativa de conhecimento.
*   **Gateway**: Controle operacional de dependências via `ServiceGateway`, agregando Circuit Breaker, Rate Limiter e contadores de custos.

---

### 5.1 Host Scoped Agents (ScopedAgentProxy Pattern)

Para resolver o conflito entre a natureza `Singleton` do `Microsoft.Agent.AI` (A2A/AG-UI hosting) e a necessidade de `Scoped` services (DbContext/TenantContext) no Orquestrador, implementamos o **ScopedAgentProxy**.

- **Problema**: O framework tenta resolver o agente no *Root Provider* durante o setup do protocolo, causando erros de *Captive Dependency*.
- **Solução**:
  1. Registramos um `ScopedAgentProxy` como `KeyedSingleton("AgenticSystem")`.
  2. Este proxy recebe o `IServiceProvider` raiz.
  3. Ao ser invocado pelo protocolo, o proxy cria um novo `AsyncScope` e resolve o Orquestrador Scoped real, delegando a execução.
  4. Isso garante que o contexto de Tenant e o DBContext sejam perfeitamente preservados em cada requisição de protocolo.

```csharp
// Exemplo de registro no Program.cs
builder.Services.AddKeyedSingleton<AIAgent>("AgenticSystem", (sp, key) =>
{
    return new ScopedAgentProxy(rootServiceProvider: sp, targetAgentKey: "OrchestratorName", ...);
});
```

> **Nota de Infraestrutura**: Containers Linux que utilizam clientes Npgsql requerem a biblioteca `libgssapi-krb5-2` instalada no `Dockerfile` para evitar erros de biblioteca compartilhada em tempo de execução.
> **Nota de Compatibilidade**: A versão atual do `ModelContextProtocol.AspNetCore` apresenta `TypeLoadException` com `Microsoft.Extensions.AI` 10.6.0. O MCP está temporariamente desabilitado aguardando alinhamento de versões.

### 5.1 Registro no Program.cs
A inicialização do sistema resolve os microsserviços do Core e Infrastructure de forma estruturada:

```csharp
var builder = WebApplication.CreateBuilder(args);

// 1. Registro de Dependências Básicas de Negócio e Banco
builder.Services.AddAgenticSystemCore();
builder.Services.AddAgenticSystemInfrastructure();

// 2. Registro do Microsoft Agent Framework (Hosting Nativo)
builder.Services.AddScoped(sp => sp.GetRequiredService<OrchestratorContextFactory>().Resolve());

builder.Services.AddAIAgent(
    OrchestratorMetadata.Default.Name,
    static (sp, _) => sp.GetRequiredService<OrchestratorContext>().OrchestratorAgent,
    ServiceLifetime.Scoped)
    .WithSessionStore(
        static (sp, _) => sp.GetRequiredService<SimpleSessionStoreAdapter>(),
        ServiceLifetime.Singleton);

// 3. Registro de Protocolos e Portas de Entrada
builder.Services.AddAuthentication("MultiAuth").AddPolicyScheme(...);
builder.Services.AddSignalR();
builder.Services.AddA2AServer("AgenticSystem");
builder.Services.AddAGUI();
```

### 5.2 IChatClient Pipeline (Microsoft.Extensions.AI)
Para garantir governança, custos previsíveis e flexibilidade de provedores, o `IChatClient` é registrado como um pipeline decorator de múltiplas camadas:

```
┌───────────────────────────────────────────────┐
│              GovernedChatClient               │  <-- Concurrency Cap, Queue Timeout & Quality Gates
└──────────────────────┬────────────────────────┘
                       ▼
┌───────────────────────────────────────────────┐
│            SemanticCacheChatClient            │  <-- Interceptação rápida via pgvector Cosine Distance (95%)
└──────────────────────┬────────────────────────┘
                       ▼
┌───────────────────────────────────────────────┐
│            ContextAwareChatClient             │  <-- Resolução dinâmica de Model/Provider por request
└──────────────────────┬────────────────────────┘
                       ▼
┌───────────────────────────────────────────────┐
│            Provider-Specific Client           │  <-- OpenAI, Azure OpenAI, Claude, Ollama (via LLMManager)
└───────────────────────────────────────────────┘
```

*   **GovernedChatClient**: Controla limites de chamadas simultâneas (semaphore) e protege o sistema contra estouro de concorrência. Também valida inputs em borda (`ValidateRequestAsync`) e buffers de saída para checar as regras antes do retorno.
*   **SemanticCacheChatClient**: Verifica hits de cache semântico antes de acionar o LLM. Faz bypass automático caso a requisição envolva chamadas de ferramentas.
*   **ContextAwareChatClient**: Inspeciona o escopo atual (via `ILLMRuntimeContextAccessor`) e escolhe o provedor/modelo correto associado ao Tenant ou Sessão de chat.
*   **ToolAIFunctionFactory**: Fábrica na infraestrutura responsável por mapear qualquer `ITool` do Core para um `AIFunction` nativo da Microsoft, permitindo que os agentes chamem ferramentas do Core de forma uniforme.

---

## 6. Padrão de Orquestração — Supervisor-with-Tools

O AgenticSystem utiliza o padrão **Supervisor-with-Tools**. O Orquestrador Central é um `ChatClientAgent` do MAF enriquecido com instruções que descrevem as competências de cada especialista registrado.

```
                  ┌───────────────────────────────┐
                  │      Orquestrador Central     │
                  │   ("Supervisor-with-Tools")   │
                  └──────┬─────────┬─────────┬────┘
                         │         │         │
      ┌──────────────────┘         │         └──────────────────┐
      ▼ (AsAIFunction)             ▼ (AsAIFunction)             ▼ (AsAIFunction)
┌───────────┐                ┌───────────┐                ┌───────────┐
│ Personal  │                │   Work    │                │ Learning  │
│   Agent   │                │   Agent   │                │   Agent   │
└───────────┘                └───────────┘                └───────────┘
```

### 6.1 Funcionamento Dinâmico
1.  O input do usuário ("Organize minhas tarefas e veja as novidades de ontem") chega ao Orquestrador.
2.  O prompt do Orquestrador lista os agentes disponíveis como ferramentas executáveis por meio de bindings criados via `AsAIFunction()`.
3.  O LLM do orquestrador emite chamadas de ferramenta (`FunctionCallContent`) para os agentes que deseja consultar.
4.  O runtime do MAF intercepta, executa os agentes especialistas informando seus respectivos históricos persistidos, e retorna o feedback para o orquestrador consolidar o retorno final.

---

## 7. Fluxos de Request (REST & Real-Time)

### 7.1 Fluxo Orquestrado via SignalR (Streaming)
Este é o principal fluxo do produto, unindo o tempo real do SignalR com a orquestração hospedada do MAF:

```
Frontend (React) ──[SendMessage]──> ChatHub
  │
  ├─ 1. Resolve ClaimsPrincipal & TenantContext
  │
  ├─ 2. Invoca MetaAgentOrchestrator.ProcessRequestStreamAsync()
  │       │
  │       ▼
  │     AgentRuntimeCoordinator.StreamAsync()
  │       │
  │       ▼
  │     AgentExecutionWorkflow.ExecuteAsync() (Abre escopo de telemetria)
  │       │
  │       ▼
  │     FrameworkOrchestratorService.ExecuteAsync()
  │       │
  │       ├─ 1. Resolve OrchestratorContext scoped via OrchestratorContextFactory
  │       ├─ 2. Injeta RAGContextProvider (MAF MessageAIContextProvider)
  │       ├─ 3. Carrega histórico de chat via ISessionStore (SimpleSessionStoreAdapter)
  │       │
  │       ├─ 4. Executa OrchestratorAgent.RunAsync(input, session)
  │       │      ├─ RAGContextProvider executa busca semântica em lote
  │       │      ├─ LLM avalia e invoca os Specialists agentes via tool calling
  │       │      └─ Middleware local: UseReflection() & UseQualityGates()
  │       │
  │       ├─ 5. Extrai conteúdo textual consolidado das mensagens de retorno
  │       ├─ 6. Identifica qual agente foi invocado para atualizar o frontend
  │       └─ 7. Salva a sessão atualizada via ISessionStore
  │
  └─ 3. Envia eventos em real-time para o frontend via Hub (ProcessingStarted, AgentSelected, StreamEvent, ReceiveMessage)
```

### 7.2 Fluxo Direto (Direct Chat)
Quando o usuário seleciona explicitamente um agente na barra lateral ("Conversar com o especialista de Trabalho"), o sistema ignora o Orquestrador Central:

```
User ──> ChatHub.SendMessage(targetAgent: "WorkAgent")
          │
          ▼
        MetaAgentOrchestrator.ProcessDirectRequestStreamAsync()
          │
          ▼
        AgentExecutionWorkflow.ExecuteDirectAsync()
          │
          ├─ 1. Executa Pré-Processamento (Validações, Correction Rules)
          ├─ 2. Resolve o agente cru via HierarchicalAgentFactory
          ├─ 3. Delega para AgentFrameworkDirectExecutionService
          │       └─ Invoca ChatClientAgent.RunAsync() diretamente (sem ferramentas de supervisão)
          ├─ 4. Executa Pós-Processamento (Reflection, auto-ajuste, Confidence, memórias)
          └─ 5. Persiste a sessão dedicada do agente
```

---

## 8. RAG Pipeline — RAGContextProvider + retrieve_context

Para maximizar a precisão contextual sem estourar a janela de contexto dos modelos, o AgenticSystem opera um modelo dual de RAG com **Contextual Retrieval**:

| Modo de Ativação | Mecanismo | Momento de Execução | Propósito |
|---|---|---|---|
| **RAGContextProvider** | Provider nativo do MAF (`MessageAIContextProvider`) | Executado deterministicamente antes de qualquer `RunAsync` do orquestrador | Garante que dados básicos sobre a pergunta do usuário estejam no prompt inicial |
| **`retrieve_context`** | Ferramenta explícita do orquestrador (`AIFunction`) | Executado sob demanda, apenas se o LLM do orquestrador decidir chamá-lo | Permite realizar buscas adicionais ou aprofundadas com queries modificadas |
| **Contextual Retrieval** | Enriquecimento por IA no Ingestion Pipeline | Executado no momento do parse e chunking de documentos (`DocumentIngestionPipeline`) | Gera um resumo via LLM prefixado ao chunk antes do embedding no pgvector para preservar escopo semântico |
| **Obsidian Sync** | Alimentação Reativa de Conhecimento | `FileObsidianSync` monitorando diretório local em background | Sincroniza bidirecionalmente as notas markdown do usuário com o vector store em tempo real |

### 8.1 O Pipeline Semântico de Busca
```
                      Query do Usuário
                             │
                             ▼
              [ 1. Query Compressor Service ]
                             │  (Normaliza e reduz ruído da query)
                             ▼
              [ 2. VectorStore.SearchAsync ]
                             │  (Busca vetorial pgvector com chunks enriquecidos)
                             ▼
              [ 3. LlmReRanker / ONNX CrossEncoder ]
                             │  (Reordena e seleciona os Top-K mais relevantes)
                             ▼
              [ 4. Knowledge Freshness Service ]
                             │  (Aplica decaimento temporal em registros velhos)
                             ▼
              [ 5. Semantic Compressor Service ]
                             │  (Resume e limpa os trechos para caber no budget)
                             ▼
                      Contexto RAG Final
```

---

## 9. Middleware Pipeline e Auto-Ajuste (Correction Loop)

### 9.1 Middleware do Microsoft Agent Framework
A configuração de comportamento dos agentes no MAF é feita por middlewares anexados ao builder na infraestrutura:

```csharp
agentBuilder
    .UseLogging()
    .UseOpenTelemetry()
    .UseReflection()     // Extensão local para auto-avaliação pós-execução
    .UseQualityGates();  // Extensão local de regras de qualidade
```

*   **UseQualityGates()**: Valida se a resposta final cumpre critérios corporativos como confiança semântica mínima, presença de citações caso tenha ocorrido RAG e ausência de termos ofensivos.
*   **UseReflection()**: Executa uma análise assíncrona pós-resposta (`ReflectionEngine`) gerando metadados sobre desvios da instrução e aprendizados práticos.

### 9.2 Pipeline de Pré e Pós-Processamento Integrados
Para garantir que as correções operem tanto em chamadas diretas quanto hospedadas, o processamento se consolida em dois barramentos do Core:

```
                      ┌───────────────────────────────┐
                      │    Request do Usuário / API   │
                      └──────────────┬────────────────┘
                                     ▼
                [ AgentExecutionPreProcessingPipeline ]
                   ├─ Executa validações básicas de input
                   └─ Aplica Correction Rules do banco
                                     ▼
                      [ Execução do Agente (MAF) ]
                                     ▼
                [ AgentExecutionPostProcessingPipeline ]
                   ├─ Calcula ConfidenceScore final
                   ├─ Armazena interações na Agent Memory (Episódica)
                   ├─ Executa auto-reflexão via ReflectionEngine
                   └─ Se houver falha persistente:
                        AddRuleAsync(CorrectionLoop) -> Auto-Ajuste
```

---

## 10. Workflow de Colaboração — AgentWorkflowBuilder

Quando uma tarefa é identificada como de alta complexidade ou envolve múltiplos domínios (ex: planejar um projeto de marketing, gerar o código e revisar o plano), o sistema ativa o **Workflow de Colaboração** (`AgentCollaborationWorkflow`).

```
                              Orquestrador Central
                                       │
                         (Tarefa complexa detectada)
                                       ▼
                       Invoca tool "collaboration_workflow"
                                       │
                                       ▼
                     [ AgentWorkflowBuilder.BuildSequential ]
                                       │
                                       ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│ 1. Planner Agent                                                            │
│    └─ Cria TaskPlan (lista de passos ordenados e agrupa em ITaskPlanManager)│
├─────────────────────────────────────────────────────────────────────────────┤
│ 2. Executor Agent                                                           │
│    └─ Para cada passo: resolve o Specialist ideal e executa via canal nativo │
├─────────────────────────────────────────────────────────────────────────────┤
│ 3. Reviewer Agent                                                           │
│    └─ Analisa o compilado de saídas e faz correções ou aprova a versão final │
└─────────────────────────────────────────────────────────────────────────────┘
```

*   **FrameworkAgentChannelService**: Canal de comunicação estruturado entre agentes, que permite a troca de eventos (`AgentEvent`) e dados de contexto nativos sem poluir o histórico de chat direto do usuário.
*   **Controle de Rollout**: Recursos de colaboração avançados (como `BuildConcurrent` ou Human-in-the-Loop em frentes paralelas) permanecem como trilhas de laboratório sob feature flag, garantindo o funcionamento do fluxo sequencial padrão do Core.

---

## 11. Gestão de Sessões — ISessionStore + ISessionManager

O AgenticSystem trabalha com uma arquitetura de sessões em duas camadas independentes:

```
┌───────────────────────────────────────────────────────────────────────────┐
│                           SESSÃO DE NEGÓCIO                               │
│                         (ISessionManager)                                 │
│                                                                           │
│ - Controla metadados gerais de auditoria corporativa.                     │
│ - Consolida sessões antigas com resumos gerados por LLM.                  │
│ - Gerencia status, tempo de vida útil e métricas operacionais globais.    │
└─────────────────────────────────────┬─────────────────────────────────────┘
                                      │  (Vínculo por SessionId)
                                      ▼
┌───────────────────────────────────────────────────────────────────────────┐
│                         SESSÃO DO FRAMEWORK                               │
│                   (ISessionStore -> PostgresSessionStore)                 │
│                                                                           │
│ - Armazena o AgentSession nativo do MAF.                                  │
│ - Mantém o histórico de mensagens exato de cada agente isoladamente.      │
│ - Isolamento por "agentId + sessionId" no banco de dados relacional.      │
└───────────────────────────────────────────────────────────────────────────┘
```

*   **SimpleSessionStoreAdapter**: Wrapper na infraestrutura que adapta o `ISessionStore` nativo do MAF para as chamadas de banco do AgenticSystem, integrando o controle de tempo de expiração de contexto (TTL).

---

## 12. Sistema de Tools — MCP, Built-in e Tool Versioning

O sistema de execução de funções externas do AgenticSystem unifica as ferramentas em três frentes:

```
                              UnifiedAIToolProvider
                                       │
        ┌──────────────────────────────┼──────────────────────────────┐
        ▼                              ▼                              ▼
  [ MCP Tools ]                [ Built-in Tools ]            [ Agent-as-Tool ]
  - Servidores externos        - Data, hora, calculadora     - Especialistas expostos
  - Servidor local (SSE)       - Busca de arquivos local     - Workflow colaborativo
```

### 12.1 Tool Versioning / A-B Testing
O `InMemoryToolManager` suporta múltiplas versões para as ferramentas registradas (ex: v1.0.0 e v1.1.0 de uma ferramenta de busca no calendário). 
*   **Roteamento de Versão**: No momento de resolver a execução de uma ferramenta, o manager avalia o ID do usuário ou o contexto da sessão para aplicar um rollout percentual gradual (ex: 90% dos usuários usam v1.0, 10% testam a v1.1).
*   **Garantia de Fallback**: Caso uma chamada falhe em uma variante experimental, o sistema re-executa a chamada na versão estável (v1.0) de forma automática e transparente.

### 12.2 Baseline Local de Tools
Para ambientes de desenvolvimento que não possuem servidores MCP externos configurados, o Core inicializa ferramentas locais de prontidão:
*   `DateTimeTool` (Data e hora local sincronizada).
*   `CalculatorTool` (Motor básico matemático isolado).
*   `FileSearchTool` (Busca superficial local e indexação do workspace).
*   `FileObsidianSync` (Atualizador do vector store com base em pastas markdown).

---

## 13. Ciclo de Vida dos Agentes

Os agentes no AgenticSystem são categorizados pelo seu ciclo de vida e escopo de criação:

| Tipo de Agente | Criação / Registro | Ciclo de Vida e Escopo | Exemplo de Uso |
|---|---|---|---|
| **Built-in (Nativo)** | Inicializado no startup via `HierarchicalAgentFactory` | Singleton (está ativo durante toda a execução da aplicação) | PersonalAgent, WorkAgent, GeneralAgent |
| **Custom (Dinâmico)** | Criado por prompt do usuário via `DynamicAgentService` | Scoped ou In-Memory Pool (persiste as configurações em banco relacional) | "Agente de Direito Trabalhista" |
| **Framework-hosted** | Registrado via `AddAIAgent()` no arquivo `Program.cs` | Scoped por requisição de chat (gerenciado pelo container de DI) | OrchestratorAgent |

*   **Cleanup de Inativos**: Para evitar vazamentos de memória e sobrecarga do banco de dados, o `AgentCleanupHostedService`  executa rotinas em background limpando agentes customizados e dados temporários inativos há mais de 24 horas.

---

## 14. Multi-Tenant

O AgenticSystem foi desenhado para ser multi-inquilino (multi-tenant) desde as camadas mais baixas:

```
                 Request HTTP / SignalR Connection
                                │
                                ▼
                       [ TenantMiddleware ]
                                ├─ Resolve JWT claim: "tenantId"
                                └─ Fallback: Header "X-Tenant-Id"
                                │
                                ▼
                   Registra scoped TenantContext
                                │
                                ▼
           [ Serviços de Infraestrutura Resolvidos via DI ]
  ├─ LLMManager: Resolve as chaves de API específicas do Tenant
  ├─ Vector Store: Filtra os resultados de busca semântica por "tenant_id"
  ├─ Settings: Armazena e expõe configurações personalizadas
  └─ SessionManager: Isola as auditorias de negócio de cada empresa
```

---

## 15. Autenticação e Segurança

### 15.1 Multi-Scheme Authentication
O sistema de autenticação unifica requisições do frontend e integrações de microsserviços por meio do esquema inteligente de policiamento `MultiAuth`:

```csharp
// Camada de borda Api
builder.Services.AddAuthentication("MultiAuth")
    .AddPolicyScheme("MultiAuth", "ApiKey ou JWT Bearer", options => {
        options.ForwardDefaultSelector = context => {
            if (context.Request.Headers.ContainsKey("X-Api-Key"))
                return "ApiKey"; // Validação de chave administrativa estática
            return "Bearer"; // Validação padrão de Token JWT OAuth/OIDC
        };
    });
```

### 15.2 API Key Masking nos Logs
Para estar em conformidade com as melhores práticas de auditoria e segurança, chaves secretas e tokens de provedores LLM informados pelos Tenants passam por um serviço de higienização de string (`ApiKeyMaskingService`) antes de serem escritos em qualquer log persistente ou trace do OpenTelemetry.
*   *Formato de mascaramento*: `sk-proj-ab...c3de` (preserva apenas caracteres iniciais e finais, ocultando o miolo).

---

## 16. Service Gateway — Resiliência e Circuit Breaker

Para conexões com APIs externas (OpenAI, Anthropic, Claude, Jina, etc.), o sistema de infraestrutura centraliza as chamadas em um **Service Gateway** (`ServiceGateway`).

```
                              ServiceGateway.ExecuteAsync<T>()
                                             │
                                             ▼
                             [ 1. Valida Registro de Serviço ]
                                             │
                                             ▼
                             [ 2. CircuitBreaker.AllowRequest() ]
                                             ├─ Closed (Permite requisição)
                                             ├─ Open (Barra chamada imediata se falhou N vezes)
                                             └─ Half-Open (Testa 1 requisição para auto-recuperar)
                                             │
                                             ▼
                             [ 3. RateLimiter.AllowRequest() ]
                                             │  (Garante limite de requisições por minuto)
                                             ▼
                             [ 4. Executa Action & Mede Custo ]
                                             └─ Grava métricas e custos no CostTracker
```

*   **CostTracker**: Subsistema que monitora tokens consumidos, custos de API por provedor/modelo de forma consolidada, gerando alertas quando o consumo diário do Tenant se aproxima de 90% do budget configurado.

---

## 17. SignalR — Comunicação Real-Time

O backend expõe dois Hubs SignalR para garantir dinamismo e monitoramento em tempo real de longo prazo.

### 17.1 ChatHub (`/hubs/chat`)
Gerencia o canal principal de interações do chat.

*Métodos expostos pelo Servidor:*
*   `SendMessage(string message, string? targetAgent, string? provider, string? model, string? apiKey)`: Inicia o processamento orquestrado de IA com parâmetros opcionais de infraestrutura.

*Eventos emitidos para os Clientes:*

| Evento | Payload | Descrição |
|---|---|---|
| `ProcessingStarted` | `{ DateTime timestamp }` | Indica ao frontend para ligar o spinner de "IA pensando" |
| `AgentSelected` | `{ string name, string tier }` | Informa qual agente foi escolhido pelo supervisor |
| `StreamEvent` | `{ string token }` | Envia tokens de texto parciais em streaming |
| `ReceiveMessage` | `{ string content, string agentName, string sessionId, bool success }` | Envia a mensagem consolidada final e fecha o ciclo de resposta |
| `ReceiveError` | `{ string error }` | Notifica o frontend sobre falhas de execução |

### 17.2 GatewayHub (`/hubs/gateway`)
Canal real-time reservado para dashboards de monitoramento operacional.

*Métodos expostos pelo Servidor:*
*   `GetDashboard()`: Devolve o relatório de saúde completo e acumulados de latência.
*   `SubscribeToService(string serviceName)`: Inscreve o cliente em um grupo do SignalR correspondente àquele serviço externo específico.
*   `UnsubscribeFromService(string serviceName)`: Cancela a inscrição naquele grupo de atualizações.

*Eventos emitidos para os Clientes:*
*   `DashboardUpdate`: Notifica alterações consolidadas de estatísticas globais do gateway.
*   `ServiceStatusChanged`: Alerta imediato se um Circuit Breaker de serviço mudou de estado (ex: de Closed para Open).

---

## 18. Protocol Hosting — A2A, AG-UI e OpenAI-Compatible

Para permitir interoperabilidade de sistemas externos com a rede de agentes, o AgenticSystem implementa três superfícies de comunicação:

```
                            Portas de Comunicação (Hosting)
                                           │
         ┌─────────────────────────────────┼─────────────────────────────────┐
         ▼                                 ▼                                 ▼
   [ A2A Server ]                    [ AGUI Server ]                [ OpenAI Controller ]
   - /a2a                            - /agui                        - /v1/chat/completions
   - Comunicação JSON                - Stream de eventos tipados     - Emula a API oficial
   - Agent-to-Agent                  - Chat rico para frontends      - Plugável em ferramentas
```

*   **A2A (Agent-to-Agent)**: Endereçamento padronizado para que agentes de outros sistemas façam chamadas a agentes especialistas do AgenticSystem usando formatos semânticos puros.
*   **AGUI (Agentic UI)**: Superfície focada em simplificar conexões de frontends com controle robusto de metadados, actions recomendadas e diagramação dinâmica.
*   **OpenAI-Compatible**: Controller customizado que traduz requests JSON padrão da OpenAI e converte internamente para chamadas ao `MetaAgentOrchestrator`, devolvendo respostas formatadas no padrão do SDK oficial da OpenAI.

---

## 19. Funcionalidades Transversais e Scheduler DAG-lite

### 19.1 Scheduler — Task Chaining DAG-lite
O gerenciador de tarefas agendadas (`ScheduledTaskManager`) estende as execuções clássicas por período ou CRON, implementando um motor simples de grafo acíclico dirigido de tarefas (**DAG-lite**):

```
                        [ Tarefa A (Cron Trigger) ]
                                     │  (Conclui com Sucesso)
                                     ▼
                        [ Tarefa B (Continuation) ]
                                     │  (Conclui com Sucesso)
                                     ▼
                        [ Tarefa C (Continuation) ]
```

*   **DependencyTaskIds**: Coleção de IDs de tarefas das quais o registro atual depende. O scheduler impede que tarefas com dependências sejam executadas, mantendo seu agendamento pausado (`NextRunAt = null`) até que os predecessores terminem com sucesso.
*   **ContinuationTaskIds**: Lista de tarefas sucessoras que devem ser desatilhadas e agendadas para execução imediata no momento em que a tarefa pai atual for concluída com sucesso.
*   **Garantia de Ciclos**: O método `LinkTasksAsync` executa uma busca em profundidade (DFS) de alcançabilidade no grafo de conexões para impedir que relações cíclicas (como `A -> B -> A`) sejam salvas no banco de dados.

### 19.2 Confidence Score
A classe `ConfidenceScoreCalculator` compõe dinamicamente um veredito numérico e qualitativo de confiança para cada retorno de agente com base em quatro eixos:
1.  **Status Operacional** (1.0 se executou sem falhas de timeout ou erros internos; 0.1 se houve queda de rede).
2.  **Relevância Semântica RAG** (média aritmética de score de proximidade cossexual dos chunks recuperados de banco).
3.  **Análise de Ferramentas** (reforça a nota se ferramentas precisas como MCPs foram acionadas ao invés de mera opinião direta do LLM).
4.  **Histórico de Auto-Reflexão** (penaliza se a auto-reflexão indicou desvios anteriores graves de prompt naquela sessão).

O resultado é encapsulado como `ConfidenceScore` (High, Medium, Low, ou RequiresHumanReview).

### 19.3 Final Response Approval (Human-in-the-Loop)
Se a nota de confiança obtida for menor que o limite definido para o Tenant ou se a ação envolver comandos financeiros/alterações estruturais de dados, o `IFinalResponseApprovalService` intercepta o fluxo.
*   **Pendente de Aprovação**: A mensagem é persistida no banco com a tag `pendingFinalApproval` e não é retornada ao usuário final.
*   **Liberação**: Uma requisição POST no endpoint administrativo aprova ou rejeita a mensagem, liberando o envio para o chat do usuário caso aprovado.

---

## 20. Observabilidade e Monitoramento

*   **OpenTelemetry**: Rastreamento estruturado adicionando tags detalhadas nos spans (ex: `agent.name`, `agent.tier`, `session.id`, `rag.chunks_count`, `llm.total_tokens`).
*   **Markers Temáticos de Logs**: Para facilitar o rastreamento em terminais ou servidores de log:
    *   🎯 `[Routing]` — Decisões de direcionamento e mapeamento de agentes.
    *   🔍 `[RAG]` — Chamadas e consultas na base semântica de conhecimentos.
    *   ✅ `[Success]` — Sucesso na execução operacional do pipeline.
    *   ❌ `[Error]` — Exceções tratadas e não-tratadas do runtime.
    *   🔄 `[Handoff]` — Passagem de contexto entre especialistas de domínio.
    *   🏗️ `[Creation]` — Inicialização de agentes dinâmicos via linguagem natural.
    *   🧹 `[Cleanup]` — Processos em background de remoção de agentes inativos.

---

## 21. Frontend — SPA React & Vite

O frontend do AgenticSystem é uma aplicação de página única (SPA) rica e moderna.

### 21.1 Arquitetura de Fluxo do Frontend
```
                              ┌───────────────────────────────┐
                              │          App Router           │  (App.tsx)
                              └──────────────┬────────────────┘
                                             ▼
                              ┌───────────────────────────────┐
                              │      Main Layout (Sidebar)    │
                              └──────────────┬────────────────┘
                                             ▼
                               ┌─────────────────────────────┐
                               │         Active Page         │
                               └─────────────┬───────────────┘
                                             ▼
                              ┌───────────────────────────────┐
                              │    Custom Hooks (useChat...)  │
                              └──────────────┬────────────────┘
                                             ▼
                              ┌───────────────────────────────┐
                              │    Lib Layer (signalr.ts...)  │
                              └───────────────────────────────┘
```

*   **signalr.ts**: Gerencia a conexão Singleton com o `/hubs/chat` utilizando reconexão automática e gerenciamento de buffers para mensagens pendentes.
*   **api.ts**: Exporta serviços tipados para cada domínio do backend (ex: `agentApi` para gerenciar agentes, `llmApi` para gerenciar as IAs e providers).

### 21.2 Componentes de Destaque
*   **MessageBubble**: Renderizador de mensagens do chat com suporte completo a Markdown formatado de forma segura e badges customizados por Tier de agente. Mostra tags visuais indicando quais ferramentas (🔧) e ações (⚡) a IA disparou no backend.
*   **DashboardPage**: Tela operacional que consome dados em tempo real do `/hubs/gateway`. Mostra gráficos de latência, consumo de créditos, alertas ativos do Circuit Breaker e um painel de status de microsserviços.
*   **PluginsPage**: Tela administrativa para gerenciar servidores MCP, conectando novos endpoints STDIO ou SSE.

---

## 22. Over-Engineering Check & Simplificações

Durante a revalidação da arquitetura, mapeamos pontos de complexidade acidental que foram simplificados na evolução do sistema:

| Ponto de Complexidade | Evidência Identificada | Decisão de Simplificação Aplicada |
|---|---|---|
| **Excesso de Construtores** | `MetaAgentOrchestrator` possuía acoplamento com mais de 15 dependências de suporte | Agrupamento de dependências afins em objetos estruturados (`ExecutionPolicies`, `ExecutionObservability`) |
| **Duplicidade de Fluxos** | O fluxo de streaming possuía regras de persistência diferentes do fluxo síncrono REST | Consolidação de toda a lógica central de pós-processamento de borda no `AgentExecutionPostProcessingPipeline` |
| **Boundaries de ML** | Dezenas de pequenas interfaces redundantes na pasta `Interfaces/` | Fusão de interfaces correlatas com alta coesão e simplificação do registro de DI |

---

## 23. Padrões Arquiteturais Utilizados

1.  **Clean Architecture (Onion)**: Divisão de responsabilidade nítida entre Api, Core e Infrastructure.
2.  **Supervisor-with-Tools**: Roteador central inteligente coordenando agentes especialistas por meio de chamadas de funções.
3.  **Pipeline (Chain of Responsibility)**: Pipelines de pré e pós-processamento interceptando requests de chat para validar dados, aplicar correção em lote e monitorar qualidade.
4.  **Decorator**: Múltiplos decorators envolvendo o `IChatClient` padrão para incluir concorrência, governança de chaves e logs de telemetria de forma transparente.
5.  **Circuit Breaker & Rate Limiter**: Mecanismos de resiliência corporativa para conexões e integrações de borda externas.
6.  **DAG (Grafo Acíclico Dirigido)**: Implementação DAG-lite no scheduler para controle de tarefas com relação de dependência e gatilhos de continuação.

---

## Apêndice A: Mapa Geral de Arquivos

| Camada | Pasta / Namespace | Atribuição Técnica |
|---|---|---|
| **Api** | `Controllers/` | Endpoints REST públicos e administrativos |
| **Api** | `Hubs/` | Hubs SignalR para comunicação bidirecional com frontends |
| **Api** | `Auth/` | Manipulador do esquema composto de segurança `MultiAuth` |
| **Core**| `Agents/` | Definições básicas de Agente (`BaseAgent`) e Tiers |
| **Core**| `Interfaces/` | Contratos abstratos de memória, RAG, sessões e execução |
| **Core**| `Services/` | Lógicas centrais: orquestração, loops de correção, scheduler |
| **Infra**| `AgentFramework/` | Acoplamentos e adaptadores para o Microsoft Agent Framework |
| **Infra**| `LLM/` | Decorators de governança (`GovernedChatClient`, `ContextAwareChatClient`) |
| **Infra**| `RAG/` | Mecanismos de vetorização, busca semântica, rerankers ONNX/Jina |
| **Infra**| `Persistence/`| Implementações EF Core com PostgreSQL para banco relacional |
| **Front**| `frontend/src/components/` | Páginas visuais do chat, dashboards e componentes unificados |
| **Front**| `frontend/src/hooks/` | Hooks React customizados conectando APIs REST e SignalR |

---

## Apêndice B: Platform Capabilities

| Capacidade | Descrição | Serviços de Referência | Domínio de Impacto |
|---|---|---|---|
| **Quality Gates** | Pipeline de validação | `IQualityGateService`, `InputValidationGate`, `ResponseQualityGate` | Qualidade & Filtros de Entrada/Saída |
| **Agent Cleanup** | Daemon de higienização | `AgentCleanupHostedService` | Ciclo de Vida & Higienização de Banco |
| **Vision** | Integração Multimodal | `IVisionProvider`, `OpenAIVisionProvider` | Análise Multimodal de Imagens |
| **MCP Gateway** | Extensibilidade | `IMCPPluginManager`, `McpToolsAIFunctionAdapter` | Extensibilidade por Protocolo MCP |
| **Storage** | Abstração de arquivos | `IStorageProvider`, `StorageFile` | Gerenciamento de Arquivos e Uploads |
| **Execution Workflow** | Orquestração Nativa | `IAgentExecutionWorkflow`, `AgentExecutionWorkflow` | Infraestrutura de Orquestração no Core |
| **Streaming Runtime** | Real-Time | `IAgentRuntimeCoordinator`, `ChatHub` | Comunicação Dinâmica em Real-Time |
| **Governance** | Controle de Ferramentas | `IToolGovernanceService`, Approvals de ferramentas | Controle de Acesso e Governança de Tools |
| **Ops Artifacts** | Telemetria & Traces | `AgentExecutionArtifact`, `AgentRuntimeMetricsSnapshot` | Armazenamento de Planos e Métricas de Traces |
| **Human-in-the-Loop** | Aprovação Final | `IFinalResponseApprovalService`, endpoint `final-approvals` | Mecanismo de Borda |

*Nota*: As capacidades base (Core Foundation, Memory, Autonomy) estão detalhadas no documento de especificações [USER-STORIES.md](../USER-STORIES.md).

---

## Glossário

*   **MAF (Microsoft Agent Framework)**: Framework corporativo de agentes e workflows distribuído pela Microsoft para ecossistema .NET.
*   **AsAIFunction()**: Extension method nativo do MAF que transforma qualquer agente em um tool formatado em JSONSchema compatível com chamada de função de LLMs.
*   **RAG (Retrieval-Augmented Generation)**: Técnica de busca e injeção de conhecimentos corporativos para diminuir alucinações e atualizar o contexto da IA.
*   **Cross-Encoder ReRanker**: Modelo de rede neural especializado que avalia a relevância de pares (pergunta, resposta) com precisão superior aos embeddings tradicionais.
*   **Circuit Breaker**: Padrão de design de software que intercepta chamadas de rede externas e abre o circuito (bloqueia requisições) se o servidor de destino estiver apresentando falhas seguidas, protegendo o sistema de travamentos.
*   **Model Context Protocol (MCP)**: Protocolo padronizado que unifica a forma como agentes de IA descobrem e chamam ferramentas em servidores locais ou remotos.
*   **DAG-lite**: Grafo acíclico direcionado de tarefas com checagens de ciclo simplificadas, embarcado no motor do agendador do AgenticSystem.
