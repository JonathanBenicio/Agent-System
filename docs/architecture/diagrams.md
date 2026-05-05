# Diagramas de Arquitetura — Sistema Agentic

## 1. Visão Geral (C4 — Container)

```mermaid
graph TB
    subgraph User["👤 Usuário"]
        CLI[CLI / Terminal]
        WEB[Dashboard Web]
        API_CLIENT[API Client]
    end

    subgraph System["Sistema Agentic (.NET 8)"]
        subgraph Api["AgenticSystem.Api"]
            REST[REST Controllers]
            VOICE[VoiceController<br/>POST /api/voice/ask]
            HUB[SignalR Hub<br/>/hubs/gateway]
            DASH[Dashboard UI]
        end

        subgraph Core["AgenticSystem.Core"]
            META[MetaAgent<br/>Tier 0 — Chief]
            CTX[ContextAnalyzer]
            ROUTER[SmartRouter<br/>ML14]
            QG[QualityGates]

            subgraph Agents["Specialized Agents"]
                T1["Tier 1 — Master<br/>Personal · Work · Learning"]
                T2["Tier 2 — Specialist<br/>Calendar · File · Research · Creative · Analysis"]
                T3["Tier 3 — Support<br/>Notification · API"]
                DYN["Dynamic Agents<br/>ML11 — Runtime-created"]
            end

            subgraph Maturity["Maturity Services (ML11-18)"]
                HANDOFF[HandoffManager<br/>ML12]
                SESSION[SessionConsolidator<br/>ML13]
                SETUP[SetupFlowManager<br/>ML15]
                SESSSTORE[ISessionStore<br/>ML16]
                MEAI_ADAPT[ChatClientProviderAdapter<br/>ML17]
                VOICEML[VoiceInterface<br/>ML18]
            end
        end

        subgraph Infra["AgenticSystem.Infrastructure"]
            GW[ServiceGateway<br/>Control Plane]

            subgraph AI["AI Providers"]
                LLM["LLM<br/>OpenAI · Gemini · Claude · Ollama"]
                EMB["Embeddings<br/>OpenAI · Google · Ollama · ONNX"]
                VIS["Vision<br/>OpenAI · Google CV · Azure CV · Ollama"]
            end

            subgraph Integrations["Integrations"]
                CAL["Calendar<br/>Google · Outlook"]
                PROD["Productivity<br/>Todoist · TickTick · MS Graph"]
                KNOW["Knowledge<br/>Notion · Obsidian · Drive"]
            end

            subgraph Persistence["Persistence"]
                PG[(PostgreSQL<br/>+ pgvector<br/>Sessions + Vectors)]
                OBS[(Obsidian Vault<br/>Markdown)]
                MCP[MCP Plugins]
            end
        end
    end

    CLI --> REST
    WEB --> HUB
    API_CLIENT --> REST
    API_CLIENT --> VOICE
    REST --> META
    VOICE --> META
    HUB --> GW
    DASH --> HUB

    META --> CTX
    CTX --> ROUTER
    ROUTER --> QG
    QG --> T1
    QG --> T2
    QG --> T3
    QG --> DYN

    T1 --> HANDOFF
    T2 --> HANDOFF
    HANDOFF --> T1
    HANDOFF --> T2
    HANDOFF --> T3

    T1 --> GW
    T2 --> GW
    T3 --> GW

    GW --> LLM
    GW --> EMB
    GW --> VIS
    GW --> CAL
    GW --> PROD
    GW --> KNOW

    T1 --> PG
    T2 --> PG
    META --> OBS
    T2 --> MCP
    SESSION --> SESSSTORE
    SESSSTORE --> PG
    MEAI_ADAPT --> LLM

    style GW fill:#ff6b35,stroke:#333,stroke-width:3px,color:#fff
    style META fill:#1a73e8,stroke:#333,stroke-width:2px,color:#fff
    style PG fill:#336791,stroke:#333,color:#fff
    style OBS fill:#7c3aed,stroke:#333,color:#fff
```

## 2. Pipeline de Request (Sequência)

```mermaid
sequenceDiagram
    actor User
    participant API as REST API
    participant Meta as MetaAgent
    participant Ctx as ContextAnalyzer
    participant Router as SmartRouter
    participant QG as QualityGates
    participant Agent as Specialized Agent
    participant GW as ServiceGateway
    participant LLM as LLM Provider
    participant Mem as Memory (PG + Obsidian)

    User->>API: POST /api/chat {message}

    rect rgb(255, 230, 230)
        Note over API: Rate Limiting per-Tenant
        API->>API: Sliding window check (MaxRequestsPerMinute)
        alt Limite excedido
            API-->>User: 429 Too Many Requests + Retry-After
        end
    end

    API->>Meta: ProcessAsync(request)

    rect rgb(230, 240, 255)
        Note over Meta,Router: Fase 1 — Análise & Routing
        Meta->>Ctx: AnalyzeAsync(userInput)
        Ctx->>GW: ExecuteAsync<ILLMProvider>(analysis prompt)
        GW->>LLM: Send (temp: 0.1)
        LLM-->>GW: Intent + Domain + Complexity
        GW-->>Ctx: Result + Metrics
        Ctx-->>Meta: RoutingDecision{agent, confidence}
        Meta->>Router: RouteAsync(decision)
        Router->>QG: ValidatePreExecution(request)
        QG-->>Router: ✅ Approved
    end

    rect rgb(230, 255, 230)
        Note over Agent,LLM: Fase 2 — Execução
        Router->>Agent: ProcessAsync(request, context)
        Agent->>GW: ExecuteAsync<ILLMProvider>(task prompt)
        Note over GW: Circuit Breaker → Rate Limit → Budget → Execute
        GW->>LLM: Send (agent-specific params)
        LLM-->>GW: Response + tokens
        GW-->>Agent: Result + metadata{cost, latency}
    end

    rect rgb(255, 245, 230)
        Note over Agent,Mem: Fase 3 — Persistência
        Agent->>QG: ValidatePostExecution(response)
        QG-->>Agent: ✅ Quality OK
        Agent->>Mem: IndexAsync(session, embeddings)
        Agent->>Mem: SyncToVault(insights)
    end

    Agent-->>API: AgentResponse{content, agent, cost}
    API-->>User: JSON Response

    Note over GW: Emite métricas via SignalR → Dashboard
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
        FILE[FileAgent<br/>temp: 0.3]
        RESEARCH[ResearchAgent<br/>temp: 0.5]
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
    PERSONAL -->|"files"| FILE
    WORK -->|"research"| RESEARCH
    LEARNING -->|"creative"| CREATIVE
    LEARNING -->|"data"| ANALYSIS

    CALENDAR --> NOTIF
    WORK --> NOTIF
    RESEARCH --> APIAGENT

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
            PROM[Prometheus]
            GRAF[Grafana]
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
        NOTION_EXT[Notion API]
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
    API_POD --> NOTION_EXT

    API_POD --> PROM
    PROM --> GRAF
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
        SQLITE[SqliteSessionStore<br/>File-based JSON]
    end

    subgraph Consumers
        SM[SessionManager]
        SC[SessionConsolidator]
    end

    SM --> ISTORE
    SC --> ISTORE
    ISTORE -.->|default| INMEM
    ISTORE -.->|"UseSqliteSessionStore()"| SQLITE
    SQLITE --> FS[("File System<br/>sessions/#123;id#125;.json")]

    style ISTORE fill:#1a73e8,stroke:#333,color:#fff
    style INMEM fill:#34a853,stroke:#333,color:#fff
    style SQLITE fill:#336791,stroke:#333,color:#fff
```

## 9. M.E.AI Adapter (ML17)

```mermaid
graph LR
    subgraph External["Microsoft.Extensions.AI"]
        ICLIENT[IChatClient<br/>Any registered client]
    end

    subgraph Adapter["ChatClientProviderAdapter"]
        MAP_IN["LLMRequest → ChatMessage#91;#93;"]
        EXEC[CompleteAsync]
        MAP_OUT[ChatResponse → LLMResponse]
    end

    subgraph System["AgenticSystem"]
        ILLM[ILLMProvider<br/>Name · GenerateAsync]
        GW[ServiceGateway]
    end

    ICLIENT --> MAP_IN
    MAP_IN --> EXEC
    EXEC --> MAP_OUT
    MAP_OUT --> ILLM
    ILLM --> GW

    style Adapter fill:#ff6b35,stroke:#333,color:#fff
    style ICLIENT fill:#0078d4,stroke:#333,color:#fff
    style ILLM fill:#1a73e8,stroke:#333,color:#fff
```
