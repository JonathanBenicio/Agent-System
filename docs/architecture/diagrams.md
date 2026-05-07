# Diagramas de Arquitetura — Sistema Agentic

> Diagramas alinhados ao runtime atual. Para a narrativa canônica da arquitetura backend, use [backend-architecture-explained.md](backend-architecture-explained.md).

## 1. Visão Geral (C4 — Container)

```mermaid
graph TB
    subgraph User["👤 Usuário"]
        WEB[Dashboard Web]
        API_CLIENT[API Client]
        PROTOCOL_CLIENT[Protocol Client]
    end

    subgraph System["Sistema Agentic (.NET 10)"]
        subgraph Api["AgenticSystem.Api"]
            REST[REST Controllers]
            VOICE[VoiceController<br/>POST /api/voice/ask]
            CHAT_HUB[ChatHub<br/>/hubs/chat]
            GATEWAY_HUB[GatewayHub<br/>/hubs/gateway]
            PROTOCOLS[A2A / AG-UI / OpenAI-compatible]
            DASH[Dashboard UI]
        end

        subgraph Core["AgenticSystem.Core"]
            META[MetaAgentOrchestrator<br/>session + streaming facade]
            WORKFLOW[AgentExecutionWorkflow<br/>thin execution shell]
        end

        subgraph Infra["AgenticSystem.Infrastructure"]
            ORCH[FrameworkOrchestratorService]
            HOSTED[Hosted Orchestrator<br/>AIAgent + OrchestratorContext]
            COLLAB[Collaboration Workflow<br/>planner → executor → reviewer]
            GW[ServiceGateway<br/>Control Plane]
            PRE[Pre-Processing Pipeline]
            POST[Post-Processing Pipeline]

            subgraph AI["AI Providers"]
                LLM["LLM<br/>OpenAI · Gemini · Claude · Ollama"]
                EMB["Embeddings<br/>OpenAI · Google · Ollama · ONNX"]
            end

            subgraph Integrations["Integrations"]
                CAL["Calendar<br/>Google · Outlook"]
                PROD["Productivity<br/>Workflows · HTTP · MCP"]
                KNOW["Knowledge<br/>Obsidian · Files · RAG"]
            end

            subgraph Persistence["Persistence"]
                PG[(PostgreSQL<br/>+ pgvector<br/>Sessions + Vectors)]
                OBS[(Obsidian Vault<br/>Markdown)]
                MCP[MCP Plugins]
                FRAME_SESS[Agent Framework Session Store]
            end
        end
    end

    WEB --> CHAT_HUB
    WEB --> GATEWAY_HUB
    API_CLIENT --> REST
    API_CLIENT --> VOICE
    PROTOCOL_CLIENT --> PROTOCOLS
    REST --> META
    VOICE --> META
    CHAT_HUB --> META
    GATEWAY_HUB --> DASH
    PROTOCOLS --> ORCH

    META --> WORKFLOW
    WORKFLOW --> ORCH
    ORCH --> PRE
    PRE --> HOSTED
    HOSTED --> COLLAB
    HOSTED --> POST
    HOSTED --> GW

    GW --> LLM
    GW --> EMB
    GW --> CAL
    GW --> PROD
    GW --> KNOW

    HOSTED --> PG
    HOSTED --> OBS
    HOSTED --> MCP
    HOSTED --> FRAME_SESS
    FRAME_SESS --> PG

    style GW fill:#ff6b35,stroke:#333,stroke-width:3px,color:#fff
    style META fill:#1a73e8,stroke:#333,stroke-width:2px,color:#fff
    style PG fill:#336791,stroke:#333,color:#fff
    style OBS fill:#7c3aed,stroke:#333,color:#fff
```

## 2. Pipeline de Request (Sequência)

```mermaid
sequenceDiagram
    actor User
    participant API as REST/SignalR/API Protocol
    participant Meta as MetaAgentOrchestrator
    participant WF as AgentExecutionWorkflow
    participant Orch as FrameworkOrchestratorService
    participant Pre as Pre-Processing Pipeline
    participant Hosted as Hosted AIAgent
    participant Tools as Specialists + Aux Tools
    participant Post as Post-Processing Pipeline
    participant GW as ServiceGateway
    participant Sess as AgentSessionStore
    participant Mem as Persistence (PG + Obsidian)

    User->>API: POST /api/chat {message}

    rect rgb(255, 230, 230)
        Note over API: Rate Limiting per-Tenant
        API->>API: Sliding window check (MaxRequestsPerMinute)
        alt Limite excedido
            API-->>User: 429 Too Many Requests + Retry-After
        end
    end

    API->>Meta: ProcessRequestAsync(request)
    Meta->>WF: ExecuteAsync(sessionId, input, context)

    rect rgb(230, 240, 255)
        Note over WF,Pre: Fase 1 — Escopo + Pre-Processing
        WF->>Orch: ExecuteAsync(sessionId, input, context)
        Orch->>Sess: GetSessionAsync(orchestrator, sessionId)
        Orch->>Pre: ProcessAsync(request)
        Pre-->>Orch: effectiveInput + metadata
    end

    rect rgb(230, 255, 230)
        Note over Hosted,Tools: Fase 2 — Orquestração Hosted
        Orch->>Hosted: RunAsync(effectiveInput, session)
        Hosted->>Tools: especialistas via AsAIFunction()
        Tools->>GW: provider/tool/RAG calls
        GW-->>Tools: results + metrics
        Tools-->>Hosted: tool outputs + specialist responses
        Hosted-->>Orch: framework response
    end

    rect rgb(255, 245, 230)
        Note over Orch,Mem: Fase 3 — Pós-Processamento
        Orch->>Sess: SaveSessionAsync(orchestrator, sessionId, session)
        Orch->>Post: ProcessAsync(response)
        Post->>Mem: session + artifacts + agent memory
        Post-->>WF: AgentResponse
    end

    WF-->>API: AgentResponse{content, agent, cost}
    API-->>User: JSON Response
```

## 3. External Service Gateway (Detalhe)

```mermaid
graph TB
    subgraph Gateway["🎛️ External Service Gateway"]
        direction TB
        CTRL[Runtime Controls<br/>Enable · Disable · Switch · Failover]

        subgraph Guards["Pre-execution Guards"]
            CB[Circuit Breaker<br/>Polly]
            RL[Rate Limiter<br/>Per-provider]
            BG[Budget Guard<br/>Cost alerts]
        end

        EXEC[Execute Request]

        subgraph PostExec["Post-execution"]
            MET[Metrics Collector<br/>Latency · Tokens · Cost]
            HC[Health Monitor<br/>Liveness · Readiness]
            EVT[Event Emitter<br/>SignalR → Dashboard]
        end
    end

    REQ([Agent Request]) --> CTRL
    CTRL --> CB
    CB -->|Closed| RL
    CB -->|Open| FALLBACK([Fallback Provider])
    RL -->|OK| BG
    RL -->|Throttled| FALLBACK
    BG -->|Under budget| EXEC
    BG -->|Over budget| ALERT([Budget Alert]) --> FALLBACK

    EXEC --> MET
    MET --> HC
    HC --> EVT
    EVT --> DASH([Dashboard<br/>Real-time])
    EVT --> SIGNALR([SignalR Hub])

    EXEC -->|Success| RES([Response + Metadata])
    EXEC -->|Failure| CB

    style Gateway fill:#1e1e2e,stroke:#ff6b35,stroke-width:2px,color:#cdd6f4
    style CB fill:#f38ba8,stroke:#333,color:#1e1e2e
    style RL fill:#fab387,stroke:#333,color:#1e1e2e
    style BG fill:#a6e3a1,stroke:#333,color:#1e1e2e
    style EXEC fill:#89b4fa,stroke:#333,color:#1e1e2e
```

## 4. Tier System & Agent Routing

```mermaid
graph TD
    INPUT([User Input]) --> META

    subgraph Tier0["Tier 0 — Chief"]
        META[MetaAgent<br/>temp: 0.2<br/>Context Analysis + Routing]
    end

    subgraph Tier1["Tier 1 — Master"]
        PERSONAL[PersonalAgent<br/>temp: 0.4]
        WORK[WorkAgent<br/>temp: 0.3]
        LEARNING[LearningAgent<br/>temp: 0.6]
    end

    subgraph Tier2["Tier 2 — Specialist"]
        CALENDAR[CalendarAgent<br/>temp: 0.0]
        CREATIVE[CreativeAgent<br/>temp: 0.9]
        ANALYSIS[AnalysisAgent<br/>temp: 0.1]
    end

    subgraph Tier3["Tier 3 — Support"]
        NOTIF[NotificationAgent<br/>temp: 0.2]
        APIAGENT[APIAgent<br/>temp: 0.3]
    end

    META -->|"personal context"| PERSONAL
    META -->|"work context"| WORK
    META -->|"learning context"| LEARNING

    PERSONAL -->|"schedule"| CALENDAR
    LEARNING -->|"creative"| CREATIVE
    LEARNING -->|"data"| ANALYSIS

    CALENDAR --> NOTIF
    WORK --> NOTIF
    WORK --> APIAGENT

    style Tier0 fill:#e8f0fe,stroke:#1a73e8,stroke-width:2px
    style Tier1 fill:#fef7e0,stroke:#f9ab00,stroke-width:2px
    style Tier2 fill:#e6f4ea,stroke:#34a853,stroke-width:2px
    style Tier3 fill:#fce8e6,stroke:#ea4335,stroke-width:2px
```

## 5. Memory Architecture

```mermaid
graph LR
    subgraph Input
        SESSION[Session Events]
        AGENT[Agent Outputs]
        USER[User Feedback]
    end

    subgraph Processing["Event Processing"]
        CONSOLIDATOR[Session Consolidator<br/>Paperclip-like]
        EMBEDDER[Embedding Provider<br/>Multi-provider]
    end

    subgraph Storage["Dual Storage"]
        OBS[(Obsidian Vault<br/>📝 Human-readable<br/>Markdown files)]
        PG[(PostgreSQL + pgvector<br/>🔍 Semantic search<br/>Vector embeddings)]
    end

    subgraph Retrieval
        RAG[RAG Pipeline<br/>Similarity search]
        BROWSE[Obsidian Browse<br/>Manual navigation]
    end

    SESSION --> CONSOLIDATOR
    AGENT --> CONSOLIDATOR
    USER --> CONSOLIDATOR

    CONSOLIDATOR --> EMBEDDER
    CONSOLIDATOR --> OBS
    EMBEDDER --> PG

    PG --> RAG
    OBS --> BROWSE
    RAG --> AGENT
    BROWSE --> USER

    style OBS fill:#7c3aed,stroke:#333,color:#fff
    style PG fill:#336791,stroke:#333,color:#fff
    style CONSOLIDATOR fill:#ff6b35,stroke:#333,color:#fff
```

## 6. Deployment Architecture

```mermaid
graph TB
    subgraph Cloud["Cloud / On-Premise"]
        subgraph K8s["Kubernetes Cluster"]
            API_POD[AgenticSystem.Api<br/>Replicas: 2+]
            WORKER[Background Workers<br/>Memory consolidation]
        end

        PG_SVC[(PostgreSQL<br/>+ pgvector)]
        OBSIDIAN_VOL[(Obsidian Vault<br/>Persistent Volume)]

        subgraph Monitoring
            LOGS[Structured Logs]
            AI_DASH[Dashboard<br/>Service Gateway]
        end
    end

    subgraph External["External Services (via Gateway)"]
        OPENAI[OpenAI API]
        GEMINI[Gemini API]
        CLAUDE[Claude API]
        OLLAMA[Ollama<br/>Local]
        GRAPH[MS Graph]
        GOOGLE[Google APIs]
        MCP_EXT[MCP Servers]
    end

    API_POD --> PG_SVC
    API_POD --> OBSIDIAN_VOL
    WORKER --> PG_SVC
    WORKER --> OBSIDIAN_VOL

    API_POD --> OPENAI
    API_POD --> GEMINI
    API_POD --> CLAUDE
    API_POD --> OLLAMA
    API_POD --> GRAPH
    API_POD --> GOOGLE
    API_POD --> MCP_EXT

    API_POD --> LOGS
    API_POD --> AI_DASH

    style K8s fill:#326ce5,stroke:#333,color:#fff
    style PG_SVC fill:#336791,stroke:#333,color:#fff
```

## 7. Voice Pipeline (ML18)

```mermaid
sequenceDiagram
    actor Voice as Alexa / Google Assistant
    participant VC as VoiceController
    participant Meta as MetaAgent
    participant LLM as LLM Provider
    participant Strip as StripMarkdown

    Voice->>VC: POST /api/voice/ask<br/>{query, userId?, maxTokens?}
    Note over VC: Timeout = 7s (VoiceTimeout)

    VC->>Meta: ProcessAsync(request)
    Meta->>LLM: GenerateAsync(prompt)
    LLM-->>Meta: AgentResponse (markdown)
    Meta-->>VC: AgentResponse{Content, AgentUsed}

    VC->>Strip: StripMarkdown(content)
    Note over Strip: Remove: headers, bold, italic,<br/>code blocks, links, images, lists

    Strip-->>VC: Plain text (TTS-friendly)
    VC-->>Voice: VoiceResponse{text, agentUsed, timestamp}
```

## 8. Session Store Architecture (ML16)

```mermaid
graph TB
    subgraph Core["AgenticSystem.Core"]
        ISTORE[ISessionStore<br/>SaveAsync · GetAsync<br/>GetByUserAsync · DeleteAsync]
        INMEM[InMemorySessionStore<br/>ConcurrentDictionary]
    end

    subgraph Infra["AgenticSystem.Infrastructure"]
        POSTGRES[PostgresSessionStore<br/>JSON + PostgreSQL]
    end

    subgraph Consumers
        SM[SessionManager]
        SC[SessionConsolidator]
    end

    SM --> ISTORE
    SC --> ISTORE
    ISTORE -.->|default| INMEM
    ISTORE -.->|"UsePostgresSessionStore()"| POSTGRES
    POSTGRES --> PG[("PostgreSQL<br/>session_data")]

    style ISTORE fill:#1a73e8,stroke:#333,color:#fff
    style INMEM fill:#34a853,stroke:#333,color:#fff
    style POSTGRES fill:#336791,stroke:#333,color:#fff
```

## 9. IChatClient Compatibility (ML17)

```mermaid
graph LR
    subgraph Runtime["Contextual runtime"]
        MGR[LLMManager<br/>provider catalog]
        CTX[ContextAwareChatClient<br/>resolve provider/model]
    end

    subgraph Compatibility["Optional compatibility"]
        BACK[ProviderBackedChatClient<br/>ILLMProvider → IChatClient]
    end

    subgraph System["AgenticSystem"]
        CHAT[IChatClient]
        ILLM[ILLMProvider]
    end

    MGR --> CTX
    CTX --> CHAT
    ILLM --> BACK
    BACK --> CHAT

    style Runtime fill:#ff6b35,stroke:#333,color:#fff
    style Compatibility fill:#0078d4,stroke:#333,color:#fff
    style ILLM fill:#1a73e8,stroke:#333,color:#fff
```
