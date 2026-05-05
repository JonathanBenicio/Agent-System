# 🤖 Sistema Agentic Generalista

> .NET 8 + Microsoft.Extensions.AI + OpenAI SDK — multi-provider LLM, memória Obsidian + pgvector, orquestração inteligente. Inspirado no Baianinho-Labs.

## Atualização Maio/2026 — Runtime V2

- Execução centralizada em `AgentExecutionWorkflow` (orquestração operacional fora do `MetaAgentOrchestrator`)
- Streaming fim a fim via SignalR (`ChatHub`) e SSE (`POST /api/chat/stream`)
- MCP server HTTP autenticado em `/mcp` com tools para listar agents, consultar RAG, inventariar tools e executar o MetaAgent
- Governança de tools com políticas de risco, aprovação e auditoria
- Artefatos operacionais persistidos por sessão (plan, steps, review, handoff, tool outputs)
- Human-in-the-loop para resposta final sensível (`final-approvals`)

Referências:
- [docs/USER-STORIES.md](docs/USER-STORIES.md)
- [docs/TECHNICAL_ARCHITECTURE_GUIDE.md](docs/TECHNICAL_ARCHITECTURE_GUIDE.md)

## 🚀 Quick Start

```bash
cp appsettings.example.json appsettings.json   # Configurar API keys
dotnet restore
dotnet run --project src/AgenticSystem.Api      # https://localhost:5001
dotnet test
```

```bash
curl -X POST https://localhost:5001/api/chat \
  -H "Content-Type: application/json" \
  -d '{"message": "Crie um lembrete para amanhã às 14h"}'
```

**MCP server**: `https://localhost:5001/mcp` via Streamable HTTP/SSE, protegido pela autenticação padrão da API.

## 🧠 O que este sistema faz?

Um **Meta-Agent** analisa o contexto da solicitação e roteia para o agent especialista mais adequado, cada um com parâmetros LLM otimizados:

- 📅 **Produtividade** — Calendário, tarefas, lembretes
- 💼 **Trabalho** — Email, documentos, reuniões
- 📚 **Aprendizado** — Pesquisa, resumos, explicações
- 🎨 **Criatividade** — Brainstorming, escrita, ideação
- 📊 **Análise** — Dados, insights, relatórios

## 🏗️ Arquitetura

> Diagramas Mermaid detalhados: [docs/architecture/diagrams.md](docs/architecture/diagrams.md)

```mermaid
graph TD
    User(["👤 User"]) --> API["API (ASP.NET + SignalR)"]
    Voice(["🎙️ Voice Assistant"]) --> VoiceAPI["VoiceController\n/api/voice/ask"]
    API --> Meta["🧠 MetaAgent — Tier 0\nContext Analyzer + Smart Router"]
    VoiceAPI --> Meta

    Meta --> T1["Tier 1 — Masters\nPersonal · Work · Learning"]
    Meta --> T2["Tier 2 — Specialists\nCreative · Analysis · Calendar"]
    Meta --> T3["Tier 3 — Support\nNotification · API"]

    T1 --> GW["🛡️ External Service Gateway\nCircuit Breaker · Rate Limiter · Cost Tracker · Health Monitor"]
    T2 --> GW
    T3 --> GW

    GW --> LLM["LLM\nOpenAI · Gemini · Claude · Ollama"]
    GW --> MEAI["M.E.AI Adapter\nIChatClient → ILLMProvider"]
    GW --> Embed["Embeddings"]
    GW --> Vision["Vision"]
    GW --> Cal["Calendar"]
    GW --> Int["Integrations"]

    Embed --> Memory[("💾 Memory\nObsidian + PostgreSQL/pgvector")]
    Meta --> SessionStore[("📦 Session Store\nInMemory · PostgreSQL")]

    style Meta fill:#1a1a2e,stroke:#e94560,color:#fff
    style GW fill:#16213e,stroke:#0f3460,color:#fff
    style Memory fill:#0f3460,stroke:#533483,color:#fff
    style VoiceAPI fill:#ff6b35,stroke:#333,color:#fff
    style MEAI fill:#0078d4,stroke:#333,color:#fff
    style SessionStore fill:#336791,stroke:#333,color:#fff
```

## 🛠️ Stack Tecnológica

| Camada | Tecnologias |
|--------|-------------|
| **Core** | .NET 8, ASP.NET Core, SignalR, Microsoft.Extensions.AI |
| **LLM** | OpenAI SDK 2.10 (GPT-4o), Google Gemini, Anthropic Claude, Ollama, M.E.AI.OpenAI (IChatClient bridge) |
| **Embeddings** | OpenAI (text-embedding-3-small), Google (text-embedding-004), Ollama (nomic-embed-text), ML.NET+ONNX |
| **Vision** | OpenAI Vision, Google Cloud Vision, Azure CV, Ollama llava |
| **Memory** | Obsidian vault (human-readable), PostgreSQL + pgvector (semantic search) |
| **Integrations** | Microsoft Graph, Google APIs, Notion, Todoist/TickTick, MCP Plugins |
| **Document Pipeline** | Parsers (Markdown, PlainText, HTML), Hybrid Chunking, RAG + Re-Ranking |
| **Gateway** | Circuit Breaker (pure C#), Rate Limiter, Cost Tracker, Health Monitor |

## � Segurança & Resiliência

| Proteção | Implementação |
|----------|---------------|
| **Prompt Injection Protection** | Delimitadores `<user_input>` no ContextAnalyzer isolam input do usuário do system prompt |
| **Rate Limiting per-Tenant** | Sliding window no `/api/chat` — retorna 429 Too Many Requests quando excedido |
| **Correlation ID** | Header `X-Correlation-Id` em error responses para rastreabilidade de incidentes |
| **Retry com Jitter Exponencial** | PostgresVectorStore e PostgresSessionStore — evita thundering herd |
| **JSON Corrupted Data Safety** | try/catch `JsonException` em `GetAsync`/`ReadSessionsAsync` — dados corrompidos não crasham o sistema |
| **Named Constants (Re-Ranker)** | `VectorScoreWeight`, `TfScoreWeight`, `ExactPhraseBonus`, `SectionMatchBonus`, `TagMatchBonus`, `OverlapPenalty` |
| **Token Estimation Multilingual** | `CharsPerToken = 3.5` (otimizado para conteúdo multilingual, era 4.0) |

## �📂 Estrutura do Projeto

```
src/
├── AgenticSystem.Api/              # Web API + SignalR
│   ├── Auth/                       # API Key authentication
│   ├── Controllers/                # REST endpoints (Chat, Agent, LLM, Voice...)
│   ├── Hubs/                       # SignalR real-time (ChatHub, GatewayHub)
│   └── Program.cs                  # Startup + DI
├── AgenticSystem.Core/             # Business Logic
│   ├── Interfaces/                 # Contracts (ISkill, ITool, ISessionStore)
│   ├── Models/                     # Domain models (SessionData, AgentResponse...)
│   ├── Services/                   # Core services (InMemorySessionStore...)
│   └── LLM/                       # LLM abstraction layer
└── AgenticSystem.Infrastructure/   # External Services
    ├── LLM/Providers/              # OpenAI, Gemini, Claude, Ollama
    ├── LLM/Adapters/               # ChatClientProviderAdapter (M.E.AI → ILLMProvider)
    ├── Embeddings/Providers/       # OpenAI, Google, Ollama, ONNX
    ├── Vision/Providers/           # OpenAI, Google, Azure, Ollama
    ├── Persistence/                # PostgresSessionStore (produção)
    ├── Documents/                  # Parsers (Markdown, PlainText, HTML)
    ├── Chunking/                   # Hybrid chunking strategy
    ├── RAG/                        # RAG service + Heuristic Re-Ranker
    ├── Integrations/               # Google, Microsoft, Notion, Tasks
    ├── Gateway/                    # External Service Gateway
    ├── Memory/                     # pgvector
    └── MCP/                        # MCP plugins
tests/
└── AgenticSystem.Tests/            # 549 testes (Unit + Integration)
docs/architecture/                  # Diagramas, pipeline, RAG flow
data/obsidian-vault/                # Obsidian notes
```

## 🤖 Agents

> Registry completo: [docs/architecture/agent-registry.md](docs/architecture/agent-registry.md) | Schema: [agent-registry.schema.json](docs/architecture/agent-registry.schema.json)

| Agent | Tier | Domínio | Temp | Função |
|-------|:----:|---------|:----:|--------|
| MetaAgent | 0 Chief | orchestration | 0.2 | Análise de contexto e roteamento |
| PersonalAgent | 1 Master | personal | 0.4 | Produtividade pessoal, calendário |
| WorkAgent | 1 Master | work | 0.3 | Email, documentos, reuniões |
| LearningAgent | 1 Master | learning | 0.6 | Pesquisa, ensino, explicações |
| CreativeAgent | 2 Specialist | creative | 0.9 | Brainstorming, escrita criativa |
| AnalysisAgent | 2 Specialist | analysis | 0.1 | Análise de dados, relatórios |
| CalendarAgent | 2 Specialist | scheduling | 0.0 | Agendamentos específicos |
| NotificationAgent | 3 Support | notifications | 0.2 | Alertas e lembretes |
| APIAgent | 3 Support | api-integration | 0.3 | Chamadas a APIs externas |

### 🔧 Skills vs Tools

> Contrato completo: [docs/architecture/skills-vs-tools.md](docs/architecture/skills-vs-tools.md)

- **Skill** = conhecimento passivo injetado no prompt (ex: `creative-writing`, `data-analysis`)
- **Tool** = capability ativa executável via Gateway (ex: `calendar-provider`, `email-sender`)

## ⚙️ Configuração LLM Providers

### 1. Configurar appsettings.json

```json
{
  "LLMProviders": {
    "DefaultProvider": "OpenAI",
    "FallbackEnabled": true,
    "Providers": {
      "OpenAI": {
        "ApiKey": "sk-proj-...",
        "DefaultModel": "gpt-4o",
        "IsEnabled": true,
        "Priority": 1,
        "DefaultParameters": {
          "Temperature": 0.7,
          "MaxTokens": 2000
        }
      },
      "Gemini": {
        "ApiKey": "AIza...",
        "DefaultModel": "gemini-1.5-pro",
        "IsEnabled": true,
        "Priority": 2
      },
      "Claude": {
        "ApiKey": "sk-ant-...",
        "DefaultModel": "claude-3-5-sonnet-20241022",
        "IsEnabled": false,
        "Priority": 3
      }
    }
  },
  
  "AgentLLMProfiles": {
    "MetaAgent": {
      "PreferredModel": "gpt-4o",
      "DefaultParameters": {
        "Temperature": 0.2,
        "MaxTokens": 800,
        "ResponseFormat": "Json"
      },
      "TaskParameters": {
        "context-analysis": { "Temperature": 0.1 },
        "agent-routing": { "Temperature": 0.0 }
      }
    },
    "CreativeAgent": {
      "PreferredModel": "gpt-4o",
      "DefaultParameters": {
        "Temperature": 0.9,
        "MaxTokens": 3000,
        "PresencePenalty": 0.3
      },
      "TaskParameters": {
        "brainstorming": { "Temperature": 1.1 },
        "writing": { "Temperature": 0.8 }
      }
    }
  },

  "EmbeddingProviders": {
    "DefaultProvider": "OpenAI",
    "Providers": {
      "OpenAI": {
        "ApiKey": "sk-proj-...",
        "Model": "text-embedding-3-small",
        "Dimensions": 1536,
        "IsEnabled": true,
        "Priority": 1
      },
      "Google": {
        "ApiKey": "AIza...",
        "Model": "text-embedding-004",
        "Dimensions": 768,
        "IsEnabled": true,
        "Priority": 2
      },
      "Ollama": {
        "BaseUrl": "http://localhost:11434",
        "Model": "nomic-embed-text",
        "Dimensions": 768,
        "IsEnabled": true,
        "Priority": 3
      }
    }
  },

  "VisionProviders": {
    "DefaultProvider": "OpenAI",
    "Providers": {
      "OpenAI": { "Model": "gpt-4o", "IsEnabled": true, "Priority": 1 },
      "GoogleVision": { "ApiKey": "AIza...", "IsEnabled": false, "Priority": 2 },
      "AzureVision": { "Endpoint": "https://...", "ApiKey": "...", "IsEnabled": false, "Priority": 3 },
      "Ollama": { "BaseUrl": "http://localhost:11434", "Model": "llava", "IsEnabled": true, "Priority": 4 }
    }
  },

  "Integrations": {
    "MicrosoftGraph": {
      "IsEnabled": true,
      "TenantId": "...",
      "ClientId": "..."
    },
    "Google": {
      "IsEnabled": true,
      "CredentialsFile": "./credentials/google-service-account.json",
      "Scopes": ["calendar", "drive", "gmail.readonly"]
    },
    "Notion": {
      "IsEnabled": false,
      "ApiKey": "ntn_..."
    },
    "Todoist": {
      "IsEnabled": false,
      "ApiKey": "..."
    }
  },

  "ObsidianSync": {
    "VaultPath": "./data/obsidian-vault",
    "AutoSync": true,
    "IndexOnStartup": true
  },

  "ConnectionStrings": {
    "SessionStore": "Host=localhost;Database=agentic;Username=postgres;Password=..."  // Se omitido → InMemorySessionStore (dev/test)
  },

  "PostgreSQL": {
    "ConnectionString": "Host=localhost;Database=agentic;Username=postgres;Password=...",
    "VectorDimensions": 1536,
    "CollectionPrefix": "agentic"
  },

  "ServiceGateway": {
    "DefaultCircuitBreaker": {
      "FailureThreshold": 5,
      "SamplingDuration": "00:01:00",
      "BreakDuration": "00:00:30",
      "MinimumThroughput": 10
    },
    "DefaultRateLimits": {
      "RequestsPerMinute": 60,
      "RequestsPerHour": 1000,
      "TokensPerDay": 100000
    },
    "CostTracking": {
      "Enabled": true,
      "DefaultDailyBudget": 10.00,
      "AlertThresholdPercent": 80,
      "PersistToDatabase": true
    },
    "HealthChecks": {
      "IntervalSeconds": 30,
      "TimeoutSeconds": 5,
      "UnhealthyThreshold": 3,
      "AutoFailover": true
    },
    "Dashboard": {
      "MetricsRetentionDays": 30,
      "SnapshotIntervalSeconds": 10,
      "SignalREnabled": true
    },
    "ServiceOverrides": {
      "OpenAI": {
        "RateLimits": { "RequestsPerMinute": 100, "TokensPerDay": 500000 },
        "DailyBudget": 5.00
      },
      "Ollama": {
        "CircuitBreaker": { "FailureThreshold": 10, "BreakDuration": "00:00:15" },
        "RateLimits": { "RequestsPerMinute": 0 }
      }
    }
  }
}
```

### 2. Variáveis de Ambiente (alternativa)

```bash
export OPENAI_API_KEY="sk-proj-..."
export GEMINI_API_KEY="AIza..."
export CLAUDE_API_KEY="sk-ant-..."
```

## 🎛️ External Service Gateway

Todo serviço externo passa pelo Gateway unificado com proteção e telemetria.

> Diagrama detalhado: [diagrams.md — External Service Gateway](docs/architecture/diagrams.md#3-external-service-gateway)

| Componente | Função |
|-----------|--------|
| **Circuit Breaker** (pure C#) | Proteção contra falhas em cascata |
| **Rate Limiter** | Controle de throughput por provider |
| **Cost Tracker** | Custo por serviço/agent/sessão + alertas de budget |
| **Health Monitor** | Health checks + auto-failover |

### Serviços Controlados

| Categoria | Providers | Interface |
|-----------|-----------|-----------|
| LLM | OpenAI SDK 2.10, Gemini, Claude, Ollama, M.E.AI (IChatClient) | `ILLMProvider` |
| Embedding | OpenAI, Google, Ollama, ONNX | `IEmbeddingProvider` |
| Vision | OpenAI, Google CV, Azure CV, Ollama | `IVisionProvider` |
| Calendar | Google Calendar, Outlook | `ICalendarProvider` |
| Email | Gmail, Outlook | `IEmailProvider` |
| Storage | Google Drive, OneDrive | `IStorageProvider` |
| Notes | Notion | `INotesProvider` |
| Tasks | Todoist, TickTick | `ITaskProvider` |
| Database | PostgreSQL + pgvector | `IVectorStore` |

### Admin API

> Todos os endpoints `/api/admin/*` requerem autenticação via header `X-Api-Key`. Configure a chave em `AgenticSystem:AdminApiKey` no `appsettings.json`.

```bash
# Gateway
GET  /api/admin/gateway/dashboard              # Snapshot completo
GET  /api/admin/gateway/services?category=LLM  # Por categoria
POST /api/admin/gateway/services/OpenAI/enable  # Toggle runtime
POST /api/admin/gateway/categories/LLM/switch   # Trocar provider
GET  /api/admin/gateway/costs?range=7d          # Custos
GET  /api/admin/gateway/health                  # Health

# Voice (Alexa / Google Assistant ready)
POST /api/voice/ask                            # Text-in → clean text-out (7s timeout)

# LLM Providers
GET  /api/admin/llm/providers                  # Listar providers
PUT  /api/admin/llm/providers/{name}           # Atualizar provider (apiKey, model, enabled, priority)

# Settings (runtime)
GET  /api/admin/settings                       # Todas as configurações
PUT  /api/admin/settings/gateway               # Atualizar GatewaySettings
PUT  /api/admin/settings/memory                # Atualizar MemorySettings

# MCP Plugins
GET  /api/admin/mcp/plugins                    # Listar plugins
POST /api/admin/mcp/plugins                    # Registrar plugin
```

**MCP Server**: `/mcp` — expõe `list_agents`, `search_knowledge`, `list_runtime_tools` e `execute_agent`

**SignalR Hub**: `/hubs/gateway` — eventos: `ServiceStatusChanged`, `CostAlertTriggered`, `CircuitStateChanged`, `RateLimitWarning`

**Dashboard Web**: `https://localhost:5001/dashboard`

## 🗺️ Roadmap

### Implementado

| Conceito | Status |
|----------|--------|
| Tier System (hierarquia 0-3) | ✅ |
| Quality Gates (validação pré/pós) | ✅ |
| Agent LLM Profiles (temp/model por agent) | ✅ |
| NoWait Pattern | ✅ |
| Memory (Obsidian + pgvector) | ✅ |
| MCP Plugin System | ✅ |
| MCP Server Mode (HTTP `/mcp`) | ✅ |
| External Service Gateway | ✅ |
| Document Ingestion Pipeline | ✅ |
| Hybrid Chunking (structural + semantic + size) | ✅ |
| Agentic RAG + Heuristic Re-Ranking | ✅ |
| ML11 — Dynamic Agent Creation (agents via chat + LLM) | ✅ |
| ML12 — Dynamic Handoffs (SingleDelegate / FanOut / Chain) | ✅ |
| ML13 — Session Consolidation (LLM summarization + insights) | ✅ |
| ML14 — Smart Routing (performance + user preferences) | ✅ |
| ML15 — Setup Flow (conversational onboarding wizard) | ✅ |
| ML16 — Session Persistence (ISessionStore + PostgreSQL) | ✅ |
| ML17 — M.E.AI Adapter (IChatClient → ILLMProvider) | ✅ |
| ML18 — Voice Interface (Alexa-ready endpoint) | ✅ |
| ML19 — Multi-Tenant Foundation (ITenantStore + TenantContext) | ✅ |
| ML20 — Tool Availability Guard (IToolAvailabilityGuard + ToolDiscoveryService) | ✅ |

### Documentação

- [**Agentic Design Manifesto**](docs/agentic-design-manifesto.md) — Os 10 princípios que guiam o design do sistema
- [Extension Examples](docs/extension-examples.md) — Guia para criar novos Agents, Tools, Skills e Maturity Levels
- [Design Philosophy](docs/architecture/design-philosophy.md) — Pilares arquiteturais do sistema (8 pilares + ML1-15)
- [Obsidian Vault](docs/obsidian-vault.md) — Memória episódica: interface, implementação, configuração e limitações

## 📄 Document Ingestion + RAG Pipeline

> Detalhes: [docs/architecture/document-pipeline.md](docs/architecture/document-pipeline.md) | [docs/architecture/rag-flow.md](docs/architecture/rag-flow.md)

Pipeline completo para ingestão de documentos, chunking inteligente e retrieval-augmented generation:

```
RawDocument → Parser → ParsedDocument → Chunking → Embedding → VectorStore
                                                                    ↓
                                          User Query → Search → Re-Rank → RAGContext
```

| Componente | Implementação | Função |
|-----------|---------------|--------|
| **Parsers** | `MarkdownParser`, `PlainTextParser`, `HtmlParser` | Extração estrutural por tipo de documento |
| **Chunking** | `HybridChunkingStrategy` | Chunking por seções com overlap, merge de chunks pequenos |
| **Re-Ranker** | `HeuristicReRanker` | TF + phrase match + metadata scoring via named constants (sem LLM) |
| **RAG Service** | `RAGService` | Retrieval agentic com query variants, merge distinto e compressão semântica sob pressão de contexto |
| **Pipeline** | `DocumentIngestionPipeline` | Orquestra parse → chunk → embed → upsert |

### Estratégias de Retrieval

| Strategy | Filtros | Uso |
|----------|---------|-----|
| `Default` | — | Busca geral |
| `RecentMemory` | `type=conversation` | Memória recente do agent |
| `DomainKnowledge` | `type=knowledge` | Base de conhecimento |
| `DecisionHistory` | `type=decision` | Histórico de decisões |
| `Episodic` | `session_id` | Contexto da sessão |
| `Targeted` | `agent_id` | Documentos do agent |

## 🎯 Casos de Uso Práticos

### 📅 Produtividade Pessoal
```bash
# "Agende reunião com João amanhã às 14h sobre projeto X"
# → Roteia para CalendarAgent (temp: 0.0) → Cria evento preciso
```

### 🎨 Brainstorming Criativo  
```bash
# "Ideias inovadoras para app de fitness"
# → Roteia para CreativeAgent (temp: 0.9) → Gera ideias variadas
```

### 📊 Análise de Dados
```bash
# "Analise o relatório em anexo e extraia insights"
# → Roteia para AnalysisAgent (temp: 0.1) → Análise precisa
```

### 🤔 Aprendizado
```bash
# "Explique machine learning de forma simples"
# → Roteia para LearningAgent (temp: 0.6) → Explicação didática
```

## 🚀 Deployment

### Docker

```dockerfile
# Dockerfile já configurado
docker build -t agentic-system .
docker run -p 8080:8080 \
  -e OPENAI_API_KEY="sk-..." \
  -e GEMINI_API_KEY="AIza..." \
  agentic-system
```

### Kubernetes

```yaml
# k8s/deployment.yaml incluído
kubectl apply -f k8s/
```

### Azure Container Apps

```bash
# Scripts de deploy incluídos
./scripts/deploy-azure.sh
```

## 📈 Monitoramento

- **Application Insights**: Telemetria automática
- **Prometheus**: Métricas custom (requests, latency, costs)
- **Grafana**: Dashboards de uso por agent
- **Quality Score**: Score contínuo 0-1 com baseline histórico e AI evaluation (`Fluency` + `RTC`) quando há contexto de sessão

## 🧪 Testes

```bash
# Unit tests
dotnet test tests/AgenticSystem.Tests/Unit/

# Integration tests  
dotnet test tests/AgenticSystem.Tests/Integration/

# LLM provider tests
dotnet test tests/AgenticSystem.Tests/LLM/

# Load testing (K6)
k6 run tests/load/agent-load-test.js
```

## 🤝 Contribuindo

1. **Fork** o repositório
2. **Branch** feature (`git checkout -b feature/nova-funcionalidade`)
3. **Commit** (`git commit -m 'feat: adiciona nova funcionalidade'`)
4. **Push** (`git push origin feature/nova-funcionalidade`)
5. **Pull Request**

### Adicionando Novo Agent

1. Criar classe agent em `src/Core/Services/`
2. Configurar perfil LLM em `appsettings.json`
3. Registrar no DI em `Program.cs`
4. Adicionar testes em `tests/`

### Adicionando Novo Provider LLM

1. Implementar `ILLMProvider` em `src/Infrastructure/LLM/Providers/`
2. Configurar HttpClient e auth
3. Mapear request/response formats
4. Adicionar configuração em `appsettings.json`

**Via Microsoft.Extensions.AI**: Registre qualquer `IChatClient` no DI e o `ChatClientProviderAdapter` o expõe automaticamente como `ILLMProvider` — zero código adicional.

## 🧬 Maturity Levels

O sistema implementa 10 níveis de maturidade que elevam o agente de um "chatbot com memória" para um sistema autônomo com auto-reflexão, correção, governança e personalização:

| Level | Nome | Serviço | Responsabilidade |
|:-----:|------|---------|------------------|
| ML1 | Chunk Lifecycle | `IChunkLifecycleManager` | Aging, decay e promoção de chunks — gerencia o ciclo New → Active → Consolidated → Archived |
| ML2 | Context Budget | `IContextBudgetManager` | Orçamento semântico de tokens — aloca contexto entre memória recente, domínio, episódica e histórico de decisões |
| ML3 | Task Planning | `ITaskPlanManager` | Decomposição multi-step — cria planos com steps, avança/falha etapas, pausa e cancela execuções |
| ML4 | Reflection | `IReflectionEngine` | Auto-reflexão pós-resposta — analisa qualidade, identifica gaps e gera insights acionáveis |
| ML5 | Correction Loop | `ICorrectionLoop` | Aprendizado com correções humanas — registra correções, extrai regras e aplica em respostas futuras |
| ML6 | Knowledge Freshness | `IKnowledgeFreshnessService` | Detecção de drift — monitora freshness de chunks e gera relatórios de conhecimento desatualizado |
| ML7 | Confidence Score | `IConfidenceScoreCalculator` | Score de confiança multi-fator — calcula confiança baseado em RAG, tools, reflexões e qualidade da resposta |
| ML8 | Semantic Compression | `ISemanticCompressor` | Compressão semântica — consolida sessões e chunks em sumários com insights e princípios-chave |
| ML9 | Query Compression | `IQueryCompressor` | Compressão de queries antes do search — remove redundância, extrai key terms, normaliza intent semântico |
| ML10 | User Personalization | `IUserPreferenceEngine` | Perfis de preferência por usuário — estilo de comunicação, tolerância a risco, agentes preferidos, EMA de satisfação |
| ML11 | Dynamic Agent Creation | `IDynamicAgentService` | Criação de agents via linguagem natural — detecção de intent, geração de spec via LLM, fallback por keywords, registro automático |
| ML12 | Dynamic Handoffs | `IHandoffManager` | Delegação mid-conversation entre agents — SingleDelegate, FanOut (paralelo), Chain (sequencial), histórico de handoffs |
| ML13 | Session Consolidation | `ISessionConsolidator` | Sumarização de sessão via LLM — extração de fatos, decisões, preferências, action items. Memória de longo prazo |
| ML14 | Smart Routing | `ISmartRouter` | Roteamento multi-critério — preferências do usuário, histórico de performance, EMA de latência e qualidade |
| ML15 | Setup Flow | `ISetupFlowManager` | Onboarding conversacional — wizard step-by-step (Welcome→Identity→Workspace→Jira→Profile→Team→Projects→Complete) |
| ML16 | Session Persistence | `ISessionStore` | Abstração de persistência de sessões — InMemory (default) e PostgreSQL (produção). Swap transparente via DI |
| ML17 | M.E.AI Adapter | `ChatClientProviderAdapter` | Bridge Microsoft.Extensions.AI `IChatClient` → `ILLMProvider` — integração zero-config com qualquer IChatClient |
| ML18 | Voice Interface | `VoiceController` | Endpoint voice-friendly `/api/voice/ask` — timeout 7s, StripMarkdown para TTS, Alexa/Google Assistant ready |
| ML19 | Multi-Tenant | `ITenantStore` · `ITenantResolver` · `TenantContext` | Isolamento por tenant — resolução via header/token, store in-memory (default), contexto propagado por middleware |
| ML20 | Tool Availability Guard | `IToolAvailabilityGuard` · `IToolDiscoveryService` | Validação pré-execução de tools requeridas — discovery de MCPs/plugins ausentes, penalização no ConfidenceScore, sugestões sem auto-install |

### Exemplo de Uso

```csharp
// ML7 — Calcular confiança de uma resposta
var confidence = confidenceCalculator.Calculate(response, ragContext, reflections);
// → { Score: 0.82, Level: High, RequiresConfirmation: false, Factors: [...] }

// ML3 — Criar plano multi-step
var plan = await taskPlanner.CreatePlanAsync("user1", "Deploy to prod", steps);
await taskPlanner.AdvanceStepAsync(plan.Id, "Step 1 done");

// ML5 — Registrar correção humana
await correctionLoop.RecordCorrectionAsync(new HumanCorrection {
    OriginalResponse = "X custa R$10",
    CorrectedResponse = "X custa R$15",
    Reason = "Preço atualizado"
});

// ML9 — Comprimir query antes do search
var compressed = await queryCompressor.CompressAsync(
    "como que eu faço para criar um novo serviço no sistema?",
    QueryCompressionStrategy.HybridCompression);
// → { CompressedText: "criar serviço sistema", CompressionRatio: 0.35, ... }

// ML10 — Personalizar prompt para o usuário
var adjustment = await preferenceEngine.PersonalizePromptAsync("user1", prompt);
// → Aplica estilo, risco, idioma e preferência de code examples
```

Todos os serviços são registrados via DI como Singleton e cobertos por **344 testes unitários** (xUnit + FluentAssertions + NSubstitute).

## 📜 Licença

MIT License - veja [LICENSE](LICENSE) para detalhes.

## 🙏 Inspiração

Baseado nos conceitos e arquitetura do **Baianinho-Labs** (Casas Bahia Tech):
- Tier System para hierarquia de agents
- Quality Gates para confiabilidade  
- Context Instructions para especialização
- Memory Architecture para conhecimento persistente

---

**"Automatizar o repetitivo para focar no criativo."** — Filosofia Baianinho-Labs

                         IMPLEMENTADO (ML1-ML10)          IMPLEMENTADO (ML11-ML15)
                    ┌──────────────────┐     ┌──────────────────────────┐
MetaAgent           │ Análise + Route  │────▶│ ✅ Handoffs + Multi-agent│
                    │ (1 agent por vez)│     │ (SingleDelegate/FanOut)  │
                    └──────────────────┘     └──────────────────────────┘

Agent Factory       ┌──────────────────┐     ┌──────────────────────────┐
                    │ 9 agents fixos   │────▶│ ✅ N agents dinâmicos    │
                    │ + CustomAgent API │     │ (criados por chat + LLM) │
                    └──────────────────┘     └──────────────────────────┘

Sessão              ┌──────────────────┐     ┌──────────────────────────┐
                    │ Events tracking  │────▶│ ✅ Consolidation + Recall│
                    │ (in-memory)      │     │ (resumo → memória LP)    │
                    └──────────────────┘     │ + ISessionStore + Postgres│
                                             └──────────────────────────┘

Routing             ┌──────────────────┐     ┌──────────────────────────┐
                    │ LLM context      │────▶│ ✅ + Performance history │
                    │ analysis         │     │ + User preferences       │
                    └──────────────────┘     └──────────────────────────┘

Onboarding          ┌──────────────────┐     ┌──────────────────────────┐
                    │ Manual config    │────▶│ ✅ Setup conversacional  │
                    │ (appsettings)    │     │ (wizard guiado 8 steps)  │
                    └──────────────────┘     └──────────────────────────┘

LLM Bridge          ┌──────────────────┐     ┌──────────────────────────┐
                    │ ILLMProvider     │────▶│ ✅ + M.E.AI IChatClient  │
                    │ (manual impl)    │     │ (ChatClientProviderAdptr)│
                    └──────────────────┘     └──────────────────────────┘

Voice               ┌──────────────────┐     ┌──────────────────────────┐
                    │ REST API only    │────▶│ ✅ Voice endpoint        │
                    │ (text/json)      │     │ (Alexa/Google ready, 7s) │
                    └──────────────────┘     └──────────────────────────┘

Multi-Tenant        ┌──────────────────┐     ┌──────────────────────────┐
                    │ Single tenant    │────▶│ ✅ ITenantStore +        │
                    │ (hardcoded)      │     │ TenantResolver + Context │
                    └──────────────────┘     └──────────────────────────┘