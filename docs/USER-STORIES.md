# User Stories — Agentic System

> Catálogo consolidado de User Stories do backend (.NET 8) e frontend (React 19).
> Gerado via pipeline Spec→Code em maio/2026.

---

## Índice

- [Backend — Maturity Levels (ML1–ML33)](#backend--maturity-levels-ml1ml33)
- [Frontend — Épicos e User Stories (US-01–US-30)](#frontend--épicos-e-user-stories-us-01us-30)

---

## Backend — Maturity Levels (ML1–ML33)

Cada Maturity Level é um capability flag independente — pode ser ativado/desativado isoladamente.

### Foundation (ML1–ML2)

#### ML1 — Chunk Lifecycle

**Como** sistema de memória,
**quero** gerenciar o ciclo de vida de chunks (New → Active → Consolidated → Archived),
**para que** o conhecimento seja envelhecido, promovido e descartado de forma controlada.

| Item | Detalhe |
|------|---------|
| Serviço | `IChunkLifecycleManager` |
| Responsabilidade | Aging, decay e promoção de chunks |
| Testes | Unitários (xUnit) |
| Status | ✅ Implementado |

**Critérios de Aceite:**
- [x] Chunks novos entram como `New` e transitam para `Active` após uso
- [x] Chunks sem acesso por N dias transitam para `Archived`
- [x] Consolidação agrupa chunks similares em um único chunk resumido
- [x] Métricas de aging são rastreáveis (lastAccessed, accessCount)

---

#### ML2 — Context Budget

**Como** orquestrador de agentes,
**quero** controlar o orçamento de tokens por contexto injetado,
**para que** o custo de LLM seja previsível e o contexto seja alocado por prioridade.

| Item | Detalhe |
|------|---------|
| Serviço | `IContextBudgetManager` |
| Responsabilidade | Orçamento semântico de tokens — aloca entre memória recente, domínio, episódica e histórico de decisões |
| Testes | Unitários (xUnit) |
| Status | ✅ Implementado |

**Critérios de Aceite:**
- [x] Token budget é configurável por agent/tier
- [x] Alocação prioriza: memória recente > domínio > episódica > histórico
- [x] Excedentes são truncados sem quebrar semântica
- [x] Relatório de uso de budget por request

---

### Intelligence (ML3–ML5)

#### ML3 — Task Planning

**Como** usuário que faz solicitações complexas,
**quero** que o sistema decomponha minha tarefa em etapas executáveis,
**para que** tarefas multi-step sejam rastreadas e executadas com controle.

| Item | Detalhe |
|------|---------|
| Serviço | `ITaskPlanManager` |
| Responsabilidade | Criação de planos com steps, avanço/falha de etapas, pausa e cancelamento |
| Testes | Unitários (xUnit) |
| Status | ✅ Implementado |

**Critérios de Aceite:**
- [x] Plano é criado com N steps ordenados
- [x] Cada step pode ser avançado, pausado ou falhado individualmente
- [x] Status do plano reflete progresso (InProgress, Completed, Failed, Cancelled)
- [x] Histórico de execução é persistido por sessão

---

#### ML4 — Reflection

**Como** sistema de qualidade,
**quero** auto-reflexão pós-resposta para avaliar qualidade,
**para que** gaps e inconsistências sejam identificados proativamente.

| Item | Detalhe |
|------|---------|
| Serviço | `IReflectionEngine` |
| Responsabilidade | Análise de qualidade pós-resposta, identificação de gaps, geração de insights |
| Testes | Unitários (xUnit) |
| Status | ✅ Implementado |

**Critérios de Aceite:**
- [x] Cada resposta é avaliada em dimensões: completude, precisão, relevância
- [x] Gaps identificados geram insights acionáveis
- [x] Insights são persistidos e consultáveis por sessão
- [x] Reflexão não bloqueia resposta ao usuário (async)

---

#### ML5 — Correction Loop

**Como** usuário que corrige respostas incorretas,
**quero** que o sistema aprenda com minhas correções,
**para que** erros similares não se repitam em interações futuras.

| Item | Detalhe |
|------|---------|
| Serviço | `ICorrectionLoop` |
| Responsabilidade | Registro de correções, extração de regras, aplicação em respostas futuras |
| Testes | Unitários (xUnit) |
| Status | ✅ Implementado |

**Critérios de Aceite:**
- [x] Correção humana é registrada (original vs corrigido + motivo)
- [x] Sistema extrai regra genérica a partir da correção
- [x] Regras são aplicadas automaticamente em respostas futuras (TimesApplied++)
- [x] Regras sem uso expiram automaticamente
- [x] Regras são escopadas por agent/domínio

---

### Quality (ML6–ML7)

#### ML6 — Knowledge Freshness

**Como** sistema de conhecimento,
**quero** detectar drift e conhecimento desatualizado,
**para que** respostas não sejam baseadas em informações obsoletas.

| Item | Detalhe |
|------|---------|
| Serviço | `IKnowledgeFreshnessService` |
| Responsabilidade | Monitoramento de freshness de chunks, relatórios de drift |
| Testes | Unitários (xUnit) |
| Status | ✅ Implementado |

**Critérios de Aceite:**
- [x] Chunks têm timestamp de criação e última validação
- [x] Sistema gera relatório de chunks potencialmente desatualizados
- [x] Threshold de freshness é configurável por domínio
- [x] Alerta quando % de chunks stale ultrapassa limite

---

#### ML7 — Confidence Score

**Como** usuário que precisa confiar nas respostas,
**quero** um score de confiança transparente em cada resposta,
**para que** eu saiba quando a resposta é confiável vs. quando preciso validar.

| Item | Detalhe |
|------|---------|
| Serviço | `IConfidenceScoreCalculator` |
| Responsabilidade | Score multi-fator baseado em RAG coverage, tools, reflexões e qualidade |
| Testes | Unitários (xUnit) |
| Status | ✅ Implementado |

**Critérios de Aceite:**
- [x] Score 0.0–1.0 em cada resposta
- [x] > 0.85 → resposta direta
- [x] 0.6–0.85 → resposta com caveats
- [x] 0.3–0.6 → disclaimers explícitos
- [x] < 0.3 → recusa, pede intervenção humana
- [x] Fatores do score são expostos ao usuário

---

### Compression (ML8–ML9)

#### ML8 — Semantic Compression

**Como** sistema de memória de longo prazo,
**quero** consolidar sessões e chunks em sumários comprimidos,
**para que** memória seja eficiente sem perda de semântica.

| Item | Detalhe |
|------|---------|
| Serviço | `ISemanticCompressor` |
| Responsabilidade | Consolidação de sessões/chunks em summaries com insights e princípios-chave |
| Testes | Unitários (xUnit) |
| Status | ✅ Implementado |

**Critérios de Aceite:**
- [x] Sessões longas são comprimidas em sumários estruturados
- [x] Insights e princípios-chave são preservados
- [x] Ratio de compressão é mensurável
- [x] Sumário mantém referências aos chunks originais

---

#### ML9 — Query Compression

**Como** pipeline de RAG,
**quero** comprimir queries antes do vector search,
**para que** o retrieval tenha maior precisão com menor custo.

| Item | Detalhe |
|------|---------|
| Serviço | `IQueryCompressor` |
| Responsabilidade | Remoção de redundância, extração de key terms, normalização de intent |
| Testes | Unitários (xUnit) |
| Status | ✅ Implementado |

**Critérios de Aceite:**
- [x] Query comprimida mantém intent semântico
- [x] Compression ratio mensurável (ex: 0.35 = 65% redução)
- [x] Suporta estratégias: KeyTermExtraction, HybridCompression
- [x] Latência de compressão < 50ms

---

### Personalization (ML10)

#### ML10 — User Personalization

**Como** usuário recorrente,
**quero** que o sistema adapte respostas ao meu perfil,
**para que** a experiência seja personalizada sem configuração manual.

| Item | Detalhe |
|------|---------|
| Serviço | `IUserPreferenceEngine` |
| Responsabilidade | Perfis por usuário — estilo, risco, agents preferidos, EMA de satisfação |
| Testes | Unitários (xUnit) |
| Status | ✅ Implementado |

**Critérios de Aceite:**
- [x] Perfil de preferência é criado automaticamente a partir de interações
- [x] Estilo de comunicação é adaptado (formal/casual, verbose/conciso)
- [x] Agents preferidos recebem prioridade no routing
- [x] Satisfação é rastreada via EMA (Exponential Moving Average)
- [x] Personalização é opt-out (nunca mandatória)

---

### Autonomy (ML11–ML15)

#### ML11 — Dynamic Agent Creation

**Como** usuário avançado,
**quero** criar agentes especializados via linguagem natural,
**para que** o catálogo de agentes cresça com o uso real.

| Item | Detalhe |
|------|---------|
| Serviço | `IDynamicAgentService` |
| Responsabilidade | Detecção de intent, geração de spec via LLM, registro automático em runtime |
| Testes | Unitários (xUnit) |
| Status | ✅ Implementado |

**Critérios de Aceite:**
- [x] Comando natural: "Crie um agente especialista em compliance"
- [x] LLM gera spec (tier, domain, keywords, temperature)
- [x] Agent é registrado em runtime via Factory
- [x] SmartRouter automaticamente delega para novo agent
- [x] Fallback por keywords quando LLM indisponível

---

#### ML12 — Dynamic Handoffs

**Como** sistema de orquestração,
**quero** delegação mid-conversation entre agents,
**para que** cada subtarefa seja resolvida pelo agent mais qualificado.

| Item | Detalhe |
|------|---------|
| Serviço | `IHandoffManager` |
| Responsabilidade | Estratégias de delegação: SingleDelegate, FanOut (paralelo), Chain (sequencial) |
| Testes | Unitários (xUnit) |
| Status | ✅ Implementado |

**Critérios de Aceite:**
- [x] SingleDelegate: um agent sabe quem é melhor para a subtarefa
- [x] FanOut: múltiplas perspectivas em paralelo
- [x] Chain: pipeline sequencial (output → input)
- [x] Contexto é preservado entre delegações (HandoffManager)
- [x] Histórico de handoffs é rastreável

---

#### ML13 — Session Consolidation

**Como** sistema de memória,
**quero** consolidar sessões longas em summaries estruturados,
**para que** memória de longo prazo seja útil sem consumo excessivo.

| Item | Detalhe |
|------|---------|
| Serviço | `ISessionConsolidator` |
| Responsabilidade | Sumarização via LLM — extração de fatos, decisões, preferências, action items |
| Testes | Unitários (xUnit) |
| Status | ✅ Implementado |

**Critérios de Aceite:**
- [x] Sessão com > N mensagens é elegível para consolidação
- [x] Summary extrai: tópicos, agents usados, insights, action items
- [x] Histórico bruto pode ser descartado após consolidação
- [x] Consolidação é batch (não bloqueia interação ativa)

---

#### ML14 — Smart Routing

**Como** MetaAgent,
**quero** routing multi-critério inteligente,
**para que** cada request vá para o agent com melhor fit.

| Item | Detalhe |
|------|---------|
| Serviço | `ISmartRouter` |
| Responsabilidade | Análise de intent, confidence scoring, capability match, load awareness, fallback chain |
| Testes | Unitários (xUnit) |
| Status | ✅ Implementado |

**Critérios de Aceite:**
- [x] Combina: intent analysis + confidence + capability + load + fallback
- [x] Preferências do usuário (ML10) influenciam routing
- [x] Histórico de performance (EMA latência/qualidade) é considerado
- [x] Fallback chain garante que nenhuma request fica sem resposta
- [x] Routing decision é logado para auditoria
- [x] `PersistentSmartRouter` — decorator write-through que persiste métricas no PostgreSQL
- [x] Warm-up automático: carrega 7 dias de métricas no startup (`EnsureWarmedUpAsync`)
- [x] Double-check locking no warm-up para evitar race conditions
- [x] Fallback gracioso: se PostgreSQL indisponível, opera cold (in-memory only)
- [x] `AgentPerformanceMetric` persistido com: domain, latency, success, user satisfaction
- [x] `AgentPerformanceMetricEntity` com `IEntityTypeConfiguration` para EF Core
- [x] `AgentRanking` calculado por domínio a partir de métricas persistidas

---

#### ML15 — Setup Flow

**Como** novo usuário,
**quero** um wizard de onboarding conversacional,
**para que** a primeira experiência seja guiada e não hostil.

| Item | Detalhe |
|------|---------|
| Serviço | `ISetupFlowManager` |
| Responsabilidade | Wizard step-by-step: Welcome → Identity → Workspace → Jira → Profile → Team → Projects → Complete |
| Testes | Unitários (xUnit) |
| Status | ✅ Implementado |

**Critérios de Aceite:**
- [x] 8 steps com progresso persistente
- [x] Cada step tem validação e rollback
- [x] Pode ser retomado se interrompido
- [x] Completion rate rastreável
- [x] Complexidade interna é invisível ao usuário

---

### Infrastructure (ML16–ML19)

#### ML16 — Session Persistence

**Como** sistema em produção,
**quero** persistência de sessões em PostgreSQL,
**para que** sessões sobrevivam a restarts e sejam escaláveis.

| Item | Detalhe |
|------|---------|
| Serviço | `ISessionStore` (abstração) |
| Implementações | `InMemorySessionStore` (dev) · `PostgresSessionStore` (produção) |
| Testes | Unitários + Integração (xUnit) |
| Status | ✅ Implementado |

**Critérios de Aceite:**
- [x] Abstração `ISessionStore` com CRUD completo
- [x] InMemory para dev/test (default)
- [x] PostgreSQL para produção (swap via DI/config)
- [x] Sessões suportam multi-tenant (ML19)
- [x] TTL configurável para expiração automática
- [x] `AgenticDbContext : DbContext` — contexto EF Core centralizado para todas as entidades
- [x] Entidades persistidas: `SessionData`, `Tenant`, `VectorDocumentEntity`, `CostBudgetEntity`, `CostEntryEntity`, `AgentPerformanceMetricEntity`
- [x] Cada entidade com `IEntityTypeConfiguration<T>` para mapeamento explícito
- [x] `EfSessionStore : ISessionStore` — implementação alternativa via EF Core
- [x] `PostgresCostTracker : ICostTracker` — tracking de custo por provider/model em PostgreSQL
- [x] `PostgresVectorStore : IVectorStore` — armazenamento de vetores com pgvector

---

#### ML17 — M.E.AI Adapter

**Como** integrador de LLM providers,
**quero** bridge automático entre `IChatClient` (M.E.AI) e `ILLMProvider`,
**para que** qualquer `IChatClient` seja utilizável sem código adicional.

| Item | Detalhe |
|------|---------|
| Serviço | `ChatClientProviderAdapter` |
| Responsabilidade | Bridge `Microsoft.Extensions.AI.IChatClient` → `ILLMProvider` |
| Testes | Unitários (xUnit) |
| Status | ✅ Implementado |

**Critérios de Aceite:**
- [x] Registrar `IChatClient` no DI é suficiente
- [x] Adapter expõe como `ILLMProvider` automaticamente
- [x] Mapeamento de request/response é transparente
- [x] Fallback mantém funcionalidade se IChatClient falhar
- [x] `EmbeddingProviderAdapter` — bridge `IEmbeddingProvider` → `IEmbeddingGenerator<string, Embedding<float>>`
- [x] `AgenticVectorStoreAdapter` — bridge `IVectorStore` → `IVectorStore` (M.E.AI)
- [x] 3 adapters distintos cobrem: Chat, Embedding e Vector Store
- [x] `HttpEmbeddingGenerator : IEmbeddingGenerator` — geração de embeddings via HTTP para providers remotos

---

#### ML18 — Voice Interface

**Como** usuário de assistentes de voz,
**quero** interagir com o sistema via endpoint voice-friendly,
**para que** eu possa usar Alexa, Google Assistant ou TTS customizado.

| Item | Detalhe |
|------|---------|
| Serviço | `VoiceController` |
| Endpoint | `POST /api/voice/ask` |
| Testes | Unitários (xUnit) |
| Status | ✅ Implementado |

**Critérios de Aceite:**
- [x] Endpoint `/api/voice/ask` com timeout de 7s
- [x] Response strip markdown para compatibilidade TTS
- [x] Formato compatível com Alexa/Google Assistant
- [x] Fallback text quando processamento excede timeout

---

#### ML19 — Multi-Tenant

**Como** sistema multi-empresa,
**quero** isolamento completo por tenant,
**para que** dados e configurações de cada tenant sejam segregados.

| Item | Detalhe |
|------|---------|
| Serviços | `ITenantStore` · `ITenantResolver` · `TenantContext` |
| Middleware | `TenantMiddleware` (resolução por header/JWT) |
| Testes | Unitários (xUnit) |
| Status | ✅ Implementado |

**Critérios de Aceite:**
- [x] Tenant resolvido via header `X-Tenant-Id` ou claim JWT `tenant_id`
- [x] `TenantContext` propagado por middleware a todo pipeline
- [x] Store in-memory (default) com interface para persistência
- [x] Sessões, preferências e agents isolados por tenant
- [x] Request sem tenant → default tenant ou rejeição (configurável)
- [x] `JwtTenantAuthenticationHandler : AuthenticationHandler<JwtTenantAuthenticationOptions>` — autenticação JWT com extração automática de tenant
- [x] `TenantMiddleware` — intercepta toda request, resolve tenant e popula `TenantContext`
- [x] `TenantResolver : ITenantResolver` — lógica de resolução: JWT claim → header → default
- [x] `Tenant` persistido via EF Core com `TenantConfiguration : IEntityTypeConfiguration<Tenant>`
- [x] `TenantLimits` — rate limiting e quotas por tenant (requests, tokens, storage)

---

### Infraestrutura Transversal (Backend)

> 10 componentes cross-cutting que sustentam toda a stack do AgenticSystem.

#### T1 — Gateway de Serviços Externos

**Como** sistema que consome APIs externas,
**quero** um gateway com resiliência e governança,
**para que** falhas externas não derrubem o sistema.

| Item | Detalhe |
|------|---------|
| Serviço | `ServiceGateway` · `CircuitBreaker` · `RateLimiter` · `CostTracker` |
| Diretório | `Infrastructure/Gateway/` |
| DI | `IServiceGateway` → `ServiceGateway`; `ICostTracker` → `CostTracker` (ou `PostgresCostTracker`) |
| Controllers | `GatewayController` (admin dashboard REST) |
| Hub | `GatewayHub` — eventos real-time: `DashboardUpdate`, `ServiceStatusChanged` |
| Testes | Unitários (xUnit) |
| Status | ✅ Implementado |

**Critérios de Aceite:**
- [x] Circuit Breaker: abre após N falhas consecutivas, half-open após cooldown (configurável via `GatewaySettings`)
- [x] Rate Limiter: controle por provider (ex: 60 req/min OpenAI, configurável em `DefaultRequestsPerMinute`)
- [x] Cost Tracker: rastreamento de custo por provider/model/tenant com budget diário (`DefaultDailyBudget`)
- [x] Cost Tracker: dual implementation — `CostTracker` (in-memory) ou `PostgresCostTracker` (persistente)
- [x] Health Monitor: health check periódico de cada serviço externo
- [x] Dashboard de saúde via REST API (`GatewayController`) e SignalR (`GatewayHub`)
- [x] SignalR subscribe/unsubscribe por serviço individual (`SubscribeToService`)

---

#### T2 — Document Pipeline (RAG)

**Como** sistema de RAG,
**quero** pipeline completo de ingestão, chunking e re-ranking,
**para que** documentos alimentem o contexto dos agents com alta precisão.

| Item | Detalhe |
|------|---------|
| Serviços | `DocumentIngestionPipeline` · `MarkdownParser` · `PlainTextParser` · `HtmlParser` · `HybridChunkingStrategy` · `HeuristicReRanker` |
| Diretórios | `Infrastructure/Documents/` · `Infrastructure/Chunking/` · `Infrastructure/RAG/` |
| DI | `IDocumentIngestionPipeline`, `IDocumentParser` (3 impl), `IChunkingStrategy`, `IReRanker`, `IRAGService` |
| Testes | Unitários (xUnit) |
| Status | ✅ Implementado |

**Critérios de Aceite:**
- [x] Parsers suportam Markdown, PlainText e HTML via `IDocumentParser` multi-registration
- [x] Chunking híbrido (`HybridChunkingStrategy`) preserva estrutura semântica
- [x] ReRanker heurístico: < 5ms/query (vs. 200-500ms cross-encoder)
- [x] Interface `IReRanker` permite swap para cross-encoder futuro
- [x] Pipeline completo: parse → chunk → embed → upsert em VectorStore

---

#### T3 — Hierarquia de Agents

**Como** sistema de agentes,
**quero** hierarquia por tiers com especialização,
**para que** cada nível tenha responsabilidades claras.

| Item | Detalhe |
|------|---------|
| Serviços | `MetaAgentOrchestrator` · `HierarchicalAgentFactory` · `AgentFrameworkAgentFactory` (decorator) |
| DI | `IMetaAgent`, `IAgentFactory`, `IContextAnalyzer` |
| Pattern | Factory + Decorator (Agent Framework wraps HierarchicalAgentFactory) |

| Tier | Papel | Agents |
|:----:|-------|--------|
| 0 | Chief | MetaAgent (análise + roteamento) |
| 1 | Master | PersonalAgent, WorkAgent, LearningAgent |
| 2 | Specialist | CreativeAgent, AnalysisAgent, CalendarAgent |
| 3 | Support | NotificationAgent, APIAgent |

**Critérios de Aceite:**
- [x] MetaAgent nunca executa — apenas analisa e delega
- [x] Cada agent tem `CanHandle()` claro (nunca aceita `*`)
- [x] Agents são intercambiáveis via Factory pattern
- [x] Dynamic agents (ML11) herdam o mesmo tier system
- [x] Agent Framework decorator aplica pipeline M.E.AI (telemetry, function invocation, logging)

---

#### T4 — Multi-Auth (ApiKey + JWT)

**Como** API que atende clientes internos e tenants,
**quero** dual authentication (API Key para admin, JWT Bearer para tenants),
**para que** cada perfil tenha credenciais e claims adequados.

| Item | Detalhe |
|------|---------|
| Handlers | `ApiKeyAuthenticationHandler` (header `X-Api-Key`) · `JwtTenantAuthenticationHandler` (Bearer JWT) |
| Diretório | `Api/Auth/` |
| Scheme | `MultiAuth` — PolicyScheme que roteia: `Authorization` header → JWT, senão → ApiKey |
| Segurança | `CryptographicOperations.FixedTimeEquals` (timing-safe comparison) para API Key |
| JWT Claims | `tenant_id` obrigatório; validação de issuer/audience/lifetime; `ClockSkew: 2min` |
| Swagger | Ambos schemes documentados em OpenAPI (ApiKey + Bearer) |
| Status | ✅ Implementado |

**Critérios de Aceite:**
- [x] ApiKey handler valida contra `AgenticSystem:AdminApiKey` com comparação timing-safe
- [x] ApiKey gera claims: Name=admin, Role=Admin, tenant_id=default
- [x] JWT handler valida signing key, issuer, audience e lifetime
- [x] JWT exige claim `tenant_id` — rejeita token sem ele
- [x] PolicyScheme `MultiAuth` roteia automaticamente pelo header presente
- [x] Dev mode: key JWT default gerada se `SecretKey` não configurado; Produção: exige key explícita

---

#### T5 — Multi-Tenant Middleware

**Como** sistema multi-tenant,
**quero** resolver o tenant em cada request via JWT claim ou header,
**para que** serviços downstream operem no contexto do tenant correto.

| Item | Detalhe |
|------|---------|
| Middleware | `TenantMiddleware` |
| Diretório | `Api/Middleware/` |
| DI | `TenantContext` (scoped) · `ITenantResolver` · `ITenantStore` |
| Resolução | 1º JWT claim `tenant_id` → 2º header `X-Tenant-Id` → fallback "default" |
| Contexto | `TenantContext.TenantId`, `.TenantName`, `.Plan`, `.Limits`, `.IsAuthenticated` |
| Status | ✅ Implementado |

**Critérios de Aceite:**
- [x] Middleware extrai tenantId de JWT claim ou header `X-Tenant-Id`
- [x] `ITenantResolver.ResolveAsync()` popula `TenantContext` scoped completo (id, name, plan, limits)
- [x] Endpoints sem `[Authorize]` não exigem tenant (health, swagger, etc.)
- [x] Tenant não encontrado em endpoint autenticado → 403 Forbidden com JSON error
- [x] Request autenticado sem tenant context → 403 bloqueado
- [x] Rate limit por tenant no chat endpoint (sliding window, `MaxRequestsPerMinute` do plano)

---

#### T6 — SignalR Real-Time

**Como** frontend que interage com agents,
**quero** comunicação bidirecional em tempo real via SignalR,
**para que** o usuário receba respostas e eventos sem polling.

| Item | Detalhe |
|------|---------|
| Hubs | `ChatHub` (`/hubs/chat`) · `GatewayHub` (`/hubs/gateway`) |
| Diretório | `Api/Hubs/` |
| ChatHub | `SendMessage(userId, message, targetAgent?)` → `ReceiveMessage` · `ProcessingStarted` · `ReceiveError` |
| GatewayHub | `GetDashboard` · `GetServiceStatus` · `SubscribeToService` · `UnsubscribeFromService` |
| Eventos | `Connected`, `DashboardUpdate`, `ServiceStatusChanged` |
| Status | ✅ Implementado |

**Critérios de Aceite:**
- [x] ChatHub: envia `ProcessingStarted` antes de processar e `ReceiveMessage` com metadata (agentName, tier, tools, actions, sessionId)
- [x] ChatHub: suporta `targetAgent` para direct request a agent específico
- [x] GatewayHub: subscribe/unsubscribe por serviço via SignalR Groups
- [x] GatewayHub: push de status em tempo real para clientes subscribed
- [x] Ambos hubs logam connect/disconnect com ConnectionId

---

#### T7 — Persistence Layer (PostgreSQL + EF Core + pgvector)

**Como** sistema que precisa persistir sessões, vetores e custos,
**quero** camada de persistência dual (InMemory para dev, PostgreSQL para produção),
**para que** o sistema funcione sem infraestrutura externa em dev mas seja durável em produção.

| Item | Detalhe |
|------|---------|
| DbContext | `AgenticDbContext` — DbSets: `Sessions`, `Tenants`, `VectorDocuments`, `CostEntries`, `CostBudgets`, `AgentPerformanceMetrics` |
| Stores | `PostgresSessionStore` · `PostgresVectorStore` · `PostgresCostTracker` · `PersistentSmartRouter` · `EfSessionStore` |
| InMemory | `InMemorySessionStore` · `InMemoryVectorStore` · `InMemoryTenantStore` (defaults para dev) |
| Diretório | `Infrastructure/Persistence/` (entities, configurations, stores) |
| Pattern | Decorator — `PersistentSmartRouter` wraps `SmartRouter` (write-through + warm-up) |
| Entidades | `VectorDocumentEntity`, `CostEntryEntity`, `CostBudgetEntity`, `AgentPerformanceMetricEntity` |
| Config EF | Fluent API em `Configurations/` — tabelas snake_case, indexes compostos, JSONB para metadata |
| Status | ✅ Implementado |

**Critérios de Aceite:**
- [x] Swap transparente via DI: `UsePostgresSessionStore`, `UsePostgresVectorStore`, `UsePostgresCostTracker`, `UsePostgresSmartRouter`
- [x] PostgresVectorStore: full-text search com `ts_rank` + `plainto_tsquery` (SQL nativo via Npgsql)
- [x] PostgresVectorStore: upsert via `ON CONFLICT DO UPDATE` (idempotente)
- [x] PersistentSmartRouter: write-through decorator com warm-up no startup
- [x] EF Core configurations: snake_case columns, JSONB, indexes compostos (`tenant_service_date`)
- [x] pgvector ready: coluna `embedding float[]` pronta para cosine similarity SQL

---

#### T8 — Obsidian Vault Sync

**Como** sistema que precisa persistir eventos de sessão em formato legível,
**quero** sincronizar eventos de agents com um vault Obsidian (file-based),
**para que** sessões sejam navegáveis como Markdown e indexadas no VectorStore.

| Item | Detalhe |
|------|---------|
| Serviço | `FileObsidianSync` |
| Diretório | `Infrastructure/Sync/` |
| Interface | `IObsidianSync` |
| Formato | Markdown com YAML frontmatter (id, session, agent, tier, timestamp, tags) |
| Path | Configurável via `AgenticSystem:Memory:ObsidianVaultPath` (default: `{AppDir}/vault`) |
| Status | ✅ Implementado |

**Critérios de Aceite:**
- [x] Gera arquivo Markdown por evento: `{timestamp}_{agentName}.md` em `vault/sessions/{sessionId}/`
- [x] YAML frontmatter com id, session, agent, tier, timestamp e tags
- [x] Seções: Input (code block), Response, Actions, Tools Used
- [x] Indexa conteúdo no VectorStore automaticamente após salvar (tipo `session_event`)
- [x] Cria diretórios automaticamente se não existirem

---

#### T9 — Structured Logging (Serilog)

**Como** sistema que precisa de observabilidade,
**quero** logging estruturado com contexto rico,
**para que** logs sejam consultáveis e correlacionáveis em produção.

| Item | Detalhe |
|------|---------|
| Framework | Serilog via `builder.Host.UseSerilog()` |
| Sinks | Console + File (rolling diário: `logs/agentic-system-{date}.log`) |
| Enrichers | `ApplicationName: "AgenticSystem"` · `FromLogContext` |
| Exception | Global exception handler com `X-Correlation-Id` header (= `TraceIdentifier`) |
| Config | Serilog lê de `appsettings.json` via `ReadFrom.Configuration` |
| Status | ✅ Implementado |

**Critérios de Aceite:**
- [x] Serilog configurado via `Host.UseSerilog` com enrichers ApplicationName e LogContext
- [x] Console sink para dev, File sink com rolling diário para produção
- [x] Exception handler global: retorna JSON com `correlationId` e status 500
- [x] Correlation ID via `TraceIdentifier` propagado em header `X-Correlation-Id`
- [x] Configuração extensível via `appsettings.json` (ReadFrom.Configuration)

---

#### T10 — DI Bootstrapping Modular

**Como** sistema com múltiplas camadas (Core, Infrastructure, Api),
**quero** registro de DI modular e extensível,
**para que** cada camada registre seus serviços de forma isolada com overrides opcionais.

| Item | Detalhe |
|------|---------|
| Core | `AddAgenticSystemCore()` — agents, sessions, ML services, tools, schedulers, config |
| Infrastructure | `AddAgenticSystemInfrastructure(config)` — LLM providers, Gateway, RAG, MCP, Persistence, Sync, Vision |
| Seeds | `SeedAgenticDefaults()` — tools built-in (DateTime, Calculator, FileSearch, WebSearch, etc.) |
|  | `SeedInfrastructureTools()` — tools de infra (MCP, RAG, etc.) |
| Overrides | `UsePostgresSessionStore`, `UsePostgresVectorStore`, `UsePostgresCostTracker`, `UsePostgresSmartRouter`, `UseEntityFramework` |
| Pattern | Remove + re-Add para swap transparente; Decorator para Agent Framework |
| Status | ✅ Implementado |

**Critérios de Aceite:**
- [x] `AddAgenticSystemCore()`: 20+ serviços Core (agents, sessions, ML1-ML23, MediatR)
- [x] `AddAgenticSystemInfrastructure()`: LLM multi-provider (OpenAI, Ollama, Gemini, Claude), Gateway, RAG, MCP, Persistence
- [x] Microsoft.Extensions.AI pipeline: `ChatClientBuilder` com OpenTelemetry + FunctionInvocation + Logging
- [x] M.E.AI `IEmbeddingGenerator<string, Embedding<float>>` com OpenTelemetry
- [x] Overrides por ambiente: InMemory (dev) → PostgreSQL (produção) via métodos `UsePostgres*`
- [x] Agent Framework decorator: `AgentFrameworkAgentFactory` wraps `HierarchicalAgentFactory` condicionalmente
- [x] Health endpoint: `/health` (anonymous) + `/version` (anonymous)
- [x] CORS: permissivo em dev (`SetIsOriginAllowed(_ => true)`), restrito em produção (AllowedOrigins obrigatório)

---

### Resilience (ML20)

#### ML20 — Tool Availability Guard

**Como** sistema de orquestração,
**quero** verificar se as tools requeridas por uma solicitação estão disponíveis antes de executar,
**para que** o sistema recuse ou redirecione em vez de dar respostas incompletas sem capabilities.

| Item | Detalhe |
|------|---------|
| Serviços | `IToolAvailabilityGuard` · `IToolDiscoveryService` |
| Responsabilidade | Validação pré-execução de tools requeridas + discovery de MCPs/plugins externos |
| Testes | Unitários (xUnit) |
| Status | ✅ Implementado |

**Contexto do Problema:**

O `ContextAnalyzer` já identifica `requiredTools` via LLM, mas nenhum serviço valida se essas tools existem no `IToolManager` antes da execução. O `ConfidenceScoreCalculator` penaliza "sem tools" com 0.5 (vs 0.8 com tools), mas como `success=true` contribui +1.0, o score mínimo real é ~0.60 — jamais atingindo o threshold < 0.3 para recusa.

**Critérios de Aceite:**

- [x] Antes da execução, `requiredTools` do AnalysisResult são validados contra `IToolManager`
- [x] Se ≥ 1 tool crítica ausente → resposta inclui disclaimer e score penalizado
- [x] Se **todas** tools requeridas ausentes → recusa com sugestão de extensão
- [x] `ToolDiscoveryService` busca MCPs/plugins compatíveis em registros conhecidos
- [x] Sugestões de tools são apresentadas ao usuário (nunca auto-instaladas sem consentimento)
- [x] Integração no `MetaAgentOrchestrator` entre análise de contexto e seleção de agent
- [x] `ConfidenceScoreCalculator` recebe fator adicional: "required tools coverage" (0.0–1.0)
- [x] Score com 0% coverage de tools: penalidade severa (fator 0.1 no lugar de 0.5)

**Fluxo:**

```
ContextAnalyzer → requiredTools: ["finance-api", "calendar"]
     ↓
ToolAvailabilityGuard.CheckAsync(requiredTools)
     ↓
┌─ Todas disponíveis → prosseguir normalmente
├─ Parcialmente → prosseguir com disclaimer + score reduzido
└─ Nenhuma → ToolDiscoveryService.SearchAsync(missingTools)
              ↓
         Sugestões de MCPs/plugins → resposta ao usuário
```

**Registros de Discovery (fontes):**
- npm registry (MCPs publicados como `@modelcontextprotocol/*`)
- GitHub Topics (`mcp-server`, `mcp-plugin`)
- Catálogo interno (Baianinho-Labs `Ferramentas/`)
- VS Code Marketplace (extensões com tools)

---

#### ML21 — Scheduled Tasks & Trigger Engine

**Como** operador do sistema agêntico,
**quero** agendar tarefas recorrentes com regras condicionais e receber notificações quando condições forem satisfeitas,
**para que** o sistema execute verificações autônomas sem intervenção manual e me alerte via canal configurado.

| Item | Detalhe |
|------|---------|
| Serviços | `IScheduledTaskManager` · `ITriggerEngine` · `IDeliveryChannel` |
| Responsabilidade | Background jobs .NET (Hosted Service/Worker), avaliação de regras periódicas, entrega de notificações multi-canal |
| Testes | Unitários (xUnit) + Integração (in-memory scheduler) |
| Status | ✅ Implementado |

**Contexto do Problema:**

Hoje o sistema agêntico é puramente reativo — responde apenas a solicitações síncronas do usuário. Não há mecanismo para:
- Executar verificações periódicas (health checks, SLA monitors, data freshness)
- Avaliar condições e disparar ações automaticamente (alertas, notificações)
- Entregar resultados por canais assíncronos (email, SMS, push, webhook)

**Componentes:**

1. **Scheduled Task Manager** — CRON-based scheduling via `IHostedService` / .NET Worker
2. **Trigger Engine** — Motor de regras: condição + ação + frequência
3. **Delivery Channel** — Abstração multi-canal para entrega de notificações

**Critérios de Aceite:**

- [x] `IScheduledTaskManager` permite registrar tarefas com expressão CRON ou intervalo (ex: `TimeSpan`, `"0 */4 * * *"`)
- [x] Tarefas executam como `BackgroundService` / Hosted Service no ASP.NET
- [x] `ITriggerEngine` avalia regras no formato: `{ source, condition, action, schedule }`
- [x] Regras suportam: HTTP GET em endpoint → avaliar resposta (status, body JSONPath, threshold)
- [x] Quando condição satisfeita → `ITriggerEngine` invoca `IDeliveryChannel.SendAsync()`
- [x] `IDeliveryChannel` é interface com implementações plugáveis:
  - [x] `WebhookDeliveryChannel` (POST para URL configurada) — obrigatório na v1
  - [x] `EmailDeliveryChannel` (via SMTP/SendGrid)
  - [x] `PushDeliveryChannel` (via Firebase/APNS)
- [x] Payload da notificação inclui: trigger name, timestamp, condition result, suggested action
- [x] Retry com backoff exponencial em caso de falha de entrega (max 3 tentativas)
- [x] Logs estruturados para cada execução de task e trigger evaluation
- [x] Health check endpoint expõe status dos scheduled tasks ativos

**Modelo de Dados — Trigger Rule:**

```csharp
public record TriggerRule(
    string Name,
    string Description,
    string Schedule,           // CRON expression ou intervalo
    TriggerSource Source,      // HTTP endpoint, DB query, metric threshold
    TriggerCondition Condition,// JSONPath match, status code, threshold comparison
    TriggerAction Action,      // Notify, ExecuteAgent, Webhook
    string[] DeliveryChannels, // ["webhook", "email"]
    bool Enabled
);

public record TriggerSource(
    TriggerSourceType Type,    // HttpGet, HttpPost, DatabaseQuery, MetricQuery
    string Endpoint,           // URL or connection string
    Dictionary<string, string> Headers,
    string? Body
);

public record TriggerCondition(
    ConditionType Type,        // JsonPathMatch, StatusCode, ThresholdAbove, ThresholdBelow
    string Expression,         // "$.status == 'unhealthy'" ou "response.time > 5000"
    string? ExpectedValue
);
```

**Fluxo:**

```
ScheduledTaskManager (BackgroundService)
     ↓ a cada tick (CRON)
TriggerEngine.EvaluateAsync(rule)
     ↓
┌─ Source: HTTP GET https://api.example.com/health
│       ↓
├─ Condition: $.status != "healthy"
│       ↓
├─ Condition TRUE → Action: Notify
│       ↓
└─ DeliveryChannel.SendAsync(webhook, payload)
        ↓
   POST https://hooks.slack.com/... { "trigger": "health-check", "result": "unhealthy" }
```

**Exemplos de Regras:**

| Nome | Schedule | Source | Condition | Action |
|------|----------|--------|-----------|--------|
| API Health Monitor | `*/5 * * * *` (5min) | GET /health | status != 200 | Webhook Slack |
| SLA Response Time | `0 * * * *` (1h) | GET /metrics/p99 | value > 3000ms | Email + Webhook |
| Data Freshness | `0 0 * * *` (24h) | GET /data/last-update | age > 48h | Notify team |
| Certificate Expiry | `0 8 * * 1` (seg 8h) | GET /certs/status | daysLeft < 30 | Email admin |

**Decisões Técnicas:**

- Scheduler in-process via `IHostedService` (sem dependência externa tipo Hangfire na v1)
- Persistência de state via `IScheduledTaskStore` (in-memory default, PostgreSQL opcional)
- Idempotência: cada execução gera um `executionId` para dedup
- Circuit breaker no delivery channel (Polly) para evitar flood em caso de falha do destino
- Timezone-aware: regras CRON respeitam timezone configurado no tenant

### Configuration & Embedding (ML22–ML23)

#### ML22 — Gerenciamento de Credenciais, Caminhos e Configurações

**Como** administrador do sistema,
**quero** gerenciar credenciais e configurações sensíveis com encriptação AES-256, audit trail e hot-reload,
**para que** segredos nunca fiquem expostos em plaintext e mudanças sejam rastreáveis.

| Item | Detalhe |
|------|---------|
| Serviço | `IConfigManager` |
| Infraestrutura | `IConfigStore`, `IConfigEncryptionService`, `IConfigReloadNotifier` |
| API | `ConfigManagementController` (CRUD + validação + audit) |
| Frontend | `ConfigAdvancedPage.tsx` — CRUD completo com indicação de secrets |
| Testes | Unitários (xUnit): ConfigManagerTests, AesConfigEncryptionServiceTests |
| Status | ✅ Implementado |

**Critérios de Aceite:**

- [x] Valores sensíveis são encriptados com AES-256 antes do armazenamento
- [x] API nunca retorna plaintext de secrets — sempre retorna "********"
- [x] Audit trail registra toda criação, atualização e deleção com hash do valor anterior
- [x] Hot-reload notifica listeners quando uma configuração muda
- [x] Validação detecta: key não encontrada, expirada e secrets sem valor encriptado
- [x] Suporte a categorias: Credentials, Paths, Connection, Provider, General
- [x] Frontend com ícone de cadeado para secrets, badge de categoria, busca e filtros

---

#### ML23 — Trocar Dimensionalidade de Banco e Embeddings — Re-indexação

**Como** engenheiro de ML,
**quero** migrar embeddings de um modelo/dimensionalidade para outro com zero-downtime,
**para que** o sistema evolua sem perda de dados ou interrupção de serviço.

| Item | Detalhe |
|------|---------|
| Serviço | `IEmbeddingMigrationManager` |
| Infraestrutura | `IEmbeddingModelStore`, `IMigrationJobStore` |
| API | `EmbeddingMigrationController` (modelos CRUD + jobs + status + cancel/retry/switch) |
| Frontend | `EmbeddingMigrationWizard.tsx` — Wizard de 3 etapas (modelo → migração → status) |
| Testes | Unitários (xUnit): EmbeddingMigrationManagerTests, InMemoryEmbeddingModelStoreTests |
| Status | ✅ Implementado |

**Critérios de Aceite:**

- [x] Registro de múltiplos modelos de embedding (OpenAI, Google, Ollama, Cohere, Custom)
- [x] Migração cria job com status: Pending → InProgress → Completed/Failed/Cancelled
- [x] Progresso granular: total documents, processed, failed, percentual calculado
- [x] Cancel interrompe job (rejeita cancel em jobs já finalizados)
- [x] Retry re-executa jobs Failed
- [x] Switch collection alterna coleção ativa (blue-green)
- [x] Frontend wizard: Step 1 (selecionar modelos) → Step 2 (configurar migração) → Step 3 (acompanhar status)
- [x] API retorna `MigrationStatusSummary` com elapsed time e ETA

---

### Observability & Self-Healing (ML24–ML25)

#### ML24 — Quality Gates Pipeline

**Como** sistema de orquestração,
**quero** um pipeline de quality gates extensível que valide entrada e saída de cada interação,
**para que** requests malformadas sejam rejeitadas cedo e respostas de baixa qualidade sejam detectadas antes de chegar ao usuário.

| Item | Detalhe |
|------|---------|
| Serviços | `IQualityGateService` · `IQualityGate` |
| Implementações | `InputValidationGate` (pré-execução) · `ResponseQualityGate` (pós-execução) |
| Integração | `MetaAgentOrchestrator` — gate pipeline entre análise e execução |
| Registro | DI via `IEnumerable<IQualityGate>` — extensível sem alterar orquestrador |
| Testes | Unitários (xUnit) |
| Status | ✅ Implementado |

**Critérios de Aceite:**
- [x] `IQualityGate` define contrato: `EvaluateAsync(QualityContext) → QualityResult`
- [x] `InputValidationGate` valida: input não vazio, tamanho dentro do budget, sem injection patterns
- [x] `ResponseQualityGate` valida: resposta não vazia, confidence acima de threshold, coerência semântica
- [x] `QualityGateService` orquestra N gates em sequência e agrega `QualityReport`
- [x] `RegisterGate()` permite adicionar gates em runtime sem recompilação
- [x] `GetRegisteredGates()` expõe gates ativos para diagnóstico
- [x] `QualityContext` carrega: input, output, analysis result, session context
- [x] `QualityReport` consolida: all passed, failures list, gate execution times
- [x] Integração no `MetaAgentOrchestrator` entre steps 1 (análise) e 2 (routing) — GAP-02

---

#### ML25 — Agent Cleanup (Self-Healing)

**Como** sistema com agents dinâmicos (ML11),
**quero** limpeza automática de agents inativos via background service,
**para que** recursos de memória e conexões sejam liberados proativamente.

| Item | Detalhe |
|------|---------|
| Serviço | `AgentCleanupHostedService : BackgroundService` |
| Dependência | `IMetaAgent.CleanupInactiveAgentsAsync()` |
| Intervalo | 5 minutos (configurável) |
| Lifecycle | Registrado como Hosted Service — inicia com a app, para com graceful shutdown |
| Testes | Unitários (xUnit) |
| Status | ✅ Implementado |

**Critérios de Aceite:**
- [x] `BackgroundService` executa tick de cleanup a cada 5 minutos
- [x] Delega a `IMetaAgent.CleanupInactiveAgentsAsync()` para decisão de quais agents remover
- [x] Tolerante a falhas: exceptions não param o loop (catch + log + continua)
- [x] Respeita `CancellationToken` para shutdown gracioso
- [x] Logs estruturados: startup, cada tick, errors, shutdown

---

### Vision (ML26)

#### ML26 — Vision (Análise de Imagens)

**Como** usuário que precisa analisar imagens,
**quero** enviar imagens ao sistema e receber análise via LLM multimodal,
**para que** o sistema suporte interações visuais além de texto.

| Item | Detalhe |
|------|---------|
| Serviço | `IVisionProvider` |
| Implementação | `OpenAIVisionProvider` (gpt-4o / gpt-4o-mini) |
| Modelos | `VisionRequest` · `VisionResponse` |
| Input | Imagem via URL ou Base64 |
| Testes | Unitários (xUnit) |
| Status | ✅ Implementado |

**Critérios de Aceite:**
- [x] Interface `IVisionProvider` com contrato: `AnalyzeImageAsync(VisionRequest) → VisionResponse`
- [x] Suporte a imagem via URL (http/https) e Base64 (inline)
- [x] Multi-model: default `gpt-4o-mini`, configurável por request
- [x] `VisionRequest` inclui: image source, prompt, model override, max tokens
- [x] `VisionResponse` inclui: description, tokens used, model, latency
- [x] Health check: `IsEnabled` valida API key + settings antes de aceitar requests
- [x] Priority system para fallback entre providers (expansível para Google Vision, Azure CV)
- [x] Provider registrado via DI com `HttpClient` factory (resiliência Polly aplicável)

---

### MCP & Extensibility (ML27–ML28)

#### ML27 — MCP Plugin System

**Como** operador do sistema,
**quero** integrar Model Context Protocol (MCP) servers como plugins gerenciáveis,
**para que** o sistema estenda suas capabilities dinamicamente via servidores MCP externos.

| Item | Detalhe |
|------|---------|
| Serviços | `IMCPPluginManager` · `IMCPPlugin` |
| Implementações | `MCPPluginManager` · `McpClientPlugin` (IAsyncDisposable) |
| Adapter | `McpToolsAIFunctionAdapter` — bridge MCP tools → AI Functions (M.E.AI) |
| API | `MCPPluginController` (load, unload, list, discover, execute) |
| Frontend | `PluginsPage.tsx` · `PluginDetailModal.tsx` · `PluginLoadModal.tsx` |
| Testes | Unitários (xUnit) |
| Status | ✅ Implementado |

**Critérios de Aceite:**
- [x] `MCPPluginManager` gerencia lifecycle completo: load → connect → discover → execute → unload
- [x] `McpClientPlugin : IMCPPlugin, IAsyncDisposable` — encapsula conexão com MCP server
- [x] Discover automático de tools, resources e prompts do MCP server
- [x] `McpToolsAIFunctionAdapter` converte MCP tools em `AIFunction` para uso no pipeline M.E.AI
- [x] API REST completa: `POST /load`, `DELETE /unload`, `GET /list`, `GET /tools`, `POST /execute`
- [x] Frontend com UI para carregar plugins (URL + config), visualizar tools e resources
- [x] Modelos: `MCPPluginConfig`, `MCPToolInfo`, `MCPToolDetail`, `MCPResourceInfo`, `MCPPromptInfo`, `MCPResponse`
- [x] Cleanup automático via `IAsyncDisposable` quando plugin é descarregado

---

#### ML28 — Storage Abstraction

**Como** sistema que gera e consome arquivos (documentos RAG, exports, attachments),
**quero** uma abstração de storage desacoplada do filesystem,
**para que** seja possível trocar entre local, S3, Azure Blob ou outro provider sem mudar código de negócio.

| Item | Detalhe |
|------|---------|
| Serviço | `IStorageProvider` |
| Modelo | `StorageFile` |
| Testes | Unitários (xUnit) |
| Status | ✅ Implementado |

**Critérios de Aceite:**
- [x] Interface `IStorageProvider` com operações: Save, Get, Delete, List, Exists
- [x] `StorageFile` encapsula: path, content stream, metadata, content type
- [x] Implementação local (filesystem) como default
- [x] Interface preparada para swap para cloud (S3, Azure Blob, GCS)
- [x] Integração com Document Pipeline (RAG) para armazenamento de documentos ingeridos

---

### Agent Runtime Platform (ML29–ML33)

#### ML29 — Agent Execution Workflow

**Como** arquitetura de execução,
**quero** centralizar o pipeline operacional em um workflow dedicado,
**para que** o MetaAgent atue como fachada de sessão/streaming/governança e não como orquestrador monolítico.

| Item | Detalhe |
|------|---------|
| Serviço | `IAgentExecutionWorkflow`, `AgentExecutionWorkflow` |
| Responsabilidade | Fluxo principal e direto (análise, routing, handoff, execução, reflexão, persistência) |
| Status | ✅ Implementado |

**Critérios de Aceite:**
- [x] `MetaAgentOrchestrator` delega execução para `IAgentExecutionWorkflow`
- [x] Fluxo principal e fluxo direto usam o mesmo contrato operacional
- [x] Consolidação de sessão e persistência de artefatos ficam no workflow

---

#### ML30 — End-to-End Streaming Runtime

**Como** consumidor de API em tempo real,
**quero** streaming fim a fim por SignalR e SSE,
**para que** eu acompanhe status, tokens e eventos operacionais durante a execução.

| Item | Detalhe |
|------|---------|
| Serviços | `IAgentRuntimeCoordinator`, `AgentRuntimeCoordinator`, `ChatHub`, `/api/chat/stream` |
| Responsabilidade | Eventos de sessão, planejamento, steps, tools, RAG, revisão, aprovação e término |
| Status | ✅ Implementado |

**Critérios de Aceite:**
- [x] `ProcessRequestStreamAsync` e `ProcessDirectRequestStreamAsync` disponíveis no `IMetaAgent`
- [x] SSE `/api/chat/stream` transmite eventos estruturados
- [x] SignalR `StreamEvent` transmite o mesmo contrato de evento

---

#### ML31 — Governed Capabilities

**Como** plataforma de agentes em produção,
**quero** governança de capabilities por risco e escopo,
**para que** chamadas sensíveis tenham proteção operacional e auditoria.

| Item | Detalhe |
|------|---------|
| Serviços | `IToolGovernanceService`, `ToolGovernanceService`, `InMemoryToolManager` |
| Responsabilidade | Whitelist por agent scope, timeout, retry, idempotência, cache, aprovação de tool |
| Status | ✅ Implementado |

**Critérios de Aceite:**
- [x] Avaliação de política por tool/action antes da execução
- [x] Aprovação humana para operações de maior risco
- [x] Metadados e artefatos de auditoria registrados no runtime

---

#### ML32 — Operational Artifacts & Runtime Metrics

**Como** time de operação,
**quero** observabilidade semântica do ciclo de execução,
**para que** debugging, resume e governança sejam objetivos e rastreáveis.

| Item | Detalhe |
|------|---------|
| Modelos/Serviços | `AgentExecutionArtifact`, `AgentRuntimeMetricsSnapshot`, `RuntimeEvaluationResult`, `AgentRuntimeCoordinator`, `IRuntimeEvaluator` |
| Responsabilidade | Persistir plano, step, review, handoff, tool output, approvals, métricas de runtime e scores contínuos de avaliação |
| Status | ✅ Implementado |

**Critérios de Aceite:**
- [x] Endpoint para artefatos de sessão
- [x] Endpoint para métricas de runtime
- [x] Endpoint para avaliações e regressões de runtime
- [x] Scores de avaliação persistidos no operational store
- [x] Eventos persistidos para replay operacional

---

#### ML33 — Human-in-the-Loop Final Approval

**Como** governança de produção,
**quero** aprovação humana antes da resposta final em cenários sensíveis,
**para que** operações de alto impacto não sejam publicadas automaticamente.

| Item | Detalhe |
|------|---------|
| Serviços | `IFinalResponseApprovalService`, `FinalResponseApprovalService` |
| API | `GET /api/agent/sessions/{sessionId}/final-approvals`, `POST /api/agent/final-approvals/{approvalId}/approve`, `POST /api/agent/final-approvals/{approvalId}/reject` |
| Status | ✅ Implementado |

**Critérios de Aceite:**
- [x] Resposta final pode ser bloqueada e marcada como pending approval
- [x] Aprovação/rejeição gera evento e artefato operacional
- [x] Estado de aprovação final é consultável por sessão

---

## Backend — Resumo de Cobertura

| Camada | MLs | Serviços | Testes |
|--------|:---:|:--------:|:------:|
| Foundation | ML1–ML2 | 2 | ✅ |
| Intelligence | ML3–ML5 | 3 | ✅ |
| Quality | ML6–ML7 | 2 | ✅ |
| Compression | ML8–ML9 | 2 | ✅ |
| Personalization | ML10 | 1 | ✅ |
| Autonomy | ML11–ML15 | 5 | ✅ |
| Infrastructure | ML16–ML19 | 5 | ✅ |
| Resilience | ML20–ML21 | 5 | ✅ |
| Config & Embedding | ML22–ML23 | 4 | ✅ |
| Observability & Self-Healing | ML24–ML25 | 3 | ✅ |
| Vision | ML26 | 1 | ✅ |
| MCP & Extensibility | ML27–ML28 | 3 | ✅ |
| Agent Runtime Platform | ML29–ML33 | 5 | ✅ |
| Transversal | T1–T10 | 10 | ✅ |
| **Total** | **33 MLs + 10 Transversais** | **53 serviços** | **549+ testes** |

---

## Frontend — Épicos e User Stories (US-01–US-30)

Stack: **React 19 + TypeScript + Vite + Tailwind CSS + SignalR**

### Épico 1: Chat Interface

#### US-01 — Enviar mensagem de texto

**Como** usuário do sistema,
**quero** enviar mensagens de texto no chat,
**para que** eu possa interagir com os agentes de IA.

| Item | Detalhe |
|------|---------|
| Componente | `ChatPage` · `ChatInput` · `useChat` |
| Hub | SignalR `ChatHub` (`/hubs/chat`) |
| Status | ✅ Implementado |

**Critérios de Aceite:**
- [x] Campo de input com Enter para enviar e Shift+Enter para nova linha
- [x] Mensagem enviada via SignalR com fallback REST
- [x] Rate limiting de 500ms entre envios
- [x] Guard contra envio duplo via `sendingRef`

---

#### US-02 — Receber resposta do agente em tempo real

**Como** usuário,
**quero** ver a resposta do agente aparecer em tempo real,
**para que** a experiência seja fluida e responsiva.

| Item | Detalhe |
|------|---------|
| Componente | `MessageList` · `MessageBubble` |
| Hub | SignalR `ReceiveMessage` event |
| Status | ✅ Implementado |

**Critérios de Aceite:**
- [x] Mensagens renderizadas com Markdown (react-markdown)
- [x] Proteção XSS: `disallowedElements` bloqueia script/iframe/object/embed/form
- [x] Indicador de "digitando" enquanto agente processa
- [x] Auto-scroll para última mensagem

---

#### US-03 — Identificar agente que respondeu

**Como** usuário,
**quero** saber qual agente respondeu minha mensagem,
**para que** eu entenda quem está me ajudando e o nível de especialização.

| Item | Detalhe |
|------|---------|
| Componente | `MessageBubble` (tierColors, tierLabels) |
| Status | ✅ Implementado |

**Critérios de Aceite:**
- [x] Badge colorido com tier do agente (Chief, Master, Specialist, Support)
- [x] Nome do agente exibido na mensagem
- [x] Cores diferenciadas por tier

---

#### US-04 — Gerenciar sessões de chat

**Como** usuário,
**quero** criar, alternar e encerrar sessões de chat,
**para que** conversas sejam organizadas por contexto.

| Item | Detalhe |
|------|---------|
| Componente | `useChat` · API `sessionApi` |
| Status | ✅ Implementado |

**Critérios de Aceite:**
- [x] ID de sessão gerado com `crypto.randomUUID()` (fallback seguro)
- [x] Sessão persistida via API `/api/sessions`
- [x] Histórico de mensagens por sessão

---

### Épico 2: Gateway Dashboard

#### US-05 — Visualizar métricas do dashboard

**Como** administrador,
**quero** ver métricas consolidadas do sistema,
**para que** eu monitore saúde e capacidade dos agentes.

| Item | Detalhe |
|------|---------|
| Componente | `DashboardPage` |
| API | `GET /api/admin/gateway/dashboard` |
| Status | ✅ Implementado |

**Critérios de Aceite:**
- [x] Cards com: Total Agents, Total Tools, Total Plugins, Active Services
- [x] Dados carregados automaticamente no mount
- [x] Loading skeleton durante carregamento
- [x] Tratamento de erro com retry

---

#### US-06 — Listar serviços do gateway

**Como** administrador,
**quero** ver todos os serviços registrados no gateway,
**para que** eu gerencie quais serviços estão ativos.

| Item | Detalhe |
|------|---------|
| Componente | `ServicesPage` |
| API | `GET /api/admin/gateway/services` |
| Status | ✅ Implementado |

**Critérios de Aceite:**
- [x] Tabela com nome, status, categoria e toggle enable/disable
- [x] Filtro por categoria
- [x] Ação de habilitar/desabilitar serviço individual

---

#### US-07 — Monitorar saúde dos serviços

**Como** SRE,
**quero** ver o health status de cada serviço,
**para que** eu identifique rapidamente serviços degradados.

| Item | Detalhe |
|------|---------|
| Componente | `HealthPage` |
| API | `GET /api/admin/gateway/health` |
| Status | ✅ Implementado |

**Critérios de Aceite:**
- [x] Status geral: Healthy / Degraded / Unhealthy
- [x] Lista de checks individuais por serviço
- [x] Cores semafóricas (verde/amarelo/vermelho)
- [x] Timestamp da última verificação

---

#### US-08 — Consultar custos por provider

**Como** gestor de custos,
**quero** ver o breakdown de custos por provider e modelo,
**para que** eu controle o orçamento de LLM.

| Item | Detalhe |
|------|---------|
| Componente | `CostsPage` |
| API | `GET /api/admin/gateway/costs` |
| Status | ✅ Implementado |

**Critérios de Aceite:**
- [x] Custo total e breakdown por provider
- [x] Período de consulta
- [x] Valores formatados em moeda

---

### Épico 3: Agent Management

#### US-09 — Listar agentes com filtro por tier

**Como** administrador,
**quero** listar todos os agentes com filtro por tier,
**para que** eu gerencie a hierarquia de especialistas.

| Item | Detalhe |
|------|---------|
| Componente | `AgentsPage` |
| API | `GET /api/agent/agents` |
| Status | ✅ Implementado |

**Critérios de Aceite:**
- [x] Grid de agents com busca por nome
- [x] Filtro por tier (0-Chief, 1-Master, 2-Specialist, 3-Support)
- [x] Contador de resultados filtrados
- [x] Badge colorido por tier

---

#### US-10 — Criar novo agente

**Como** administrador,
**quero** criar um novo agente via formulário,
**para que** eu expanda o catálogo de especialistas.

| Item | Detalhe |
|------|---------|
| Componente | `AgentFormModal` |
| API | `POST /api/agent/agents` |
| Status | ✅ Implementado |

**Critérios de Aceite:**
- [x] Modal com campos: nome, tier, domínio, temperatura, capabilities
- [x] Validação de campos obrigatórios
- [x] Temperatura entre 0.0 e 2.0
- [x] Toast de sucesso/erro após criação

---

#### US-11 — Editar agente existente

**Como** administrador,
**quero** editar configurações de um agente,
**para que** eu ajuste comportamento sem recriar.

| Item | Detalhe |
|------|---------|
| Componente | `AgentFormModal` (modo edição) |
| API | `PUT /api/agent/agents/{name}` |
| Status | ✅ Implementado |

**Critérios de Aceite:**
- [x] Formulário pré-preenchido com dados atuais
- [x] Agente mantém mesmo ID após edição
- [x] Validação idêntica à criação

---

#### US-12 — Excluir agente com confirmação

**Como** administrador,
**quero** excluir um agente com confirmação,
**para que** exclusões acidentais sejam prevenidas.

| Item | Detalhe |
|------|---------|
| Componente | `ConfirmModal` (variant danger) |
| API | `DELETE /api/agent/agents/{name}` |
| Status | ✅ Implementado |

**Critérios de Aceite:**
- [x] Modal de confirmação com variante "danger"
- [x] Nome do agente exibido na confirmação
- [x] Toast de sucesso após exclusão
- [x] Lista atualizada automaticamente

---

#### US-13 — Ver detalhes do agente

**Como** usuário,
**quero** ver detalhes completos de um agente,
**para que** eu entenda capabilities, tools e skills associadas.

| Item | Detalhe |
|------|---------|
| Componente | `AgentDetailModal` |
| Status | ✅ Implementado |

**Critérios de Aceite:**
- [x] Modal read-only com informações completas
- [x] Lista de capabilities
- [x] Tools e skills associadas
- [x] Parâmetros LLM (temperatura, modelo)

---

#### US-14 — Buscar agentes por nome

**Como** administrador com muitos agentes,
**quero** buscar agentes por nome,
**para que** eu encontre rapidamente o que preciso.

| Item | Detalhe |
|------|---------|
| Componente | `AgentsPage` (search input) |
| Status | ✅ Implementado |

**Critérios de Aceite:**
- [x] Input de busca com filtro em tempo real
- [x] Busca case-insensitive
- [x] Combinável com filtro de tier

---

### Épico 4: LLM Providers

#### US-15 — Gerenciar providers de LLM

**Como** administrador,
**quero** ver e gerenciar providers de LLM configurados em uma área dedicada,
**para que** eu controle quais modelos estão disponíveis e qual IA abre pré-selecionada no chat.

| Item | Detalhe |
|------|---------|
| Componente | `ProvidersPage` (rota `/ai`) |
| API | `GET /api/admin/llm/configuration` + `PUT /api/admin/llm/default-selection` |
| Status | ✅ Implementado |

**Critérios de Aceite:**
- [x] Lista de providers com status (enabled/disabled)
- [x] Informações: nome, modelo default, prioridade, disponibilidade e flag de default
- [x] Ação de editar configuração
- [x] Área para definir provider + modelo default do chat
- [x] Rota legada `/providers` redireciona para `/ai`

---

#### US-16 — Testar conexão com provider

**Como** administrador,
**quero** testar a conexão com um provider,
**para que** eu valide que a API key e configuração estão corretas.

| Item | Detalhe |
|------|---------|
| Componente | `ProvidersPage` (botão testar) |
| API | `POST /api/admin/llm/providers/{name}/test` |
| Status | ✅ Implementado |

**Critérios de Aceite:**
- [x] Botão "Testar" por provider
- [x] Feedback visual: sucesso/falha
- [x] Mensagem de erro detalhada em caso de falha

---

#### US-01A — Selecionar IA no chat

**Como** usuário,
**quero** escolher provider e modelo diretamente no topo do chat,
**para que** eu altere a IA da conversa sem abrir telas técnicas.

| Item | Detalhe |
|------|---------|
| Componentes | `ChatPage` · `AgentChatPage` · `AISelectorBar` |
| APIs | `GET /api/admin/llm/configuration` + `POST /api/chat` / `ChatHub.SendMessage(...)` |
| Status | ✅ Implementado |

**Critérios de Aceite:**
- [x] Chat principal e chat dedicado exibem seletor de provider e modelo
- [x] Seleção é persistida localmente para reaproveitar a última IA usada
- [x] Envio REST e SignalR propagam provider/model selecionados
- [x] Ação "Configurar IA" leva da conversa para a rota `/ai`

---

### Épico 5: Settings

#### US-17 — Configurar parâmetros do gateway

**Como** administrador,
**quero** configurar parâmetros gerais do gateway,
**para que** eu ajuste comportamento global do sistema.

| Item | Detalhe |
|------|---------|
| Componente | `SettingsPage` (tab Gateway) |
| API | `GET/PUT /api/admin/settings/gateway` |
| Status | ✅ Implementado |

**Critérios de Aceite:**
- [x] Formulário com campos do gateway settings
- [x] Salvar com validação
- [x] Toast de confirmação

---

#### US-18 — Configurar parâmetros de memória

**Como** administrador,
**quero** configurar parâmetros de memória e RAG,
**para que** eu ajuste chunking, embedding e retrieval.

| Item | Detalhe |
|------|---------|
| Componente | `SettingsPage` (tab Memory) |
| API | `GET/PUT /api/admin/settings/memory` |
| Status | ✅ Implementado |

**Critérios de Aceite:**
- [x] Formulário com configurações de memória
- [x] Tabs separadas: Gateway | Memory
- [x] Persistência das configurações

---

#### US-19 — Alternar entre tabs de configuração

**Como** administrador,
**quero** navegar entre seções de configuração por tabs,
**para que** a interface seja organizada por domínio.

| Item | Detalhe |
|------|---------|
| Componente | `SettingsPage` (tab system) |
| Status | ✅ Implementado |

**Critérios de Aceite:**
- [x] Tabs: Gateway, Memory
- [x] Estado da tab ativa persiste durante a sessão
- [x] Transição suave entre tabs

---

### Épico 6: MCP Plugins

#### US-20 — Listar plugins carregados

**Como** administrador,
**quero** ver todos os plugins MCP carregados,
**para que** eu gerencie extensões do sistema.

| Item | Detalhe |
|------|---------|
| Componente | `PluginsPage` |
| API | `GET /api/admin/plugins` |
| Status | ✅ Implementado |

**Critérios de Aceite:**
- [x] Grid de plugins com nome, tipo (stdio/sse) e status
- [x] Contadores por tipo
- [x] Ações: ver detalhes, excluir

---

#### US-21 — Carregar novo plugin

**Como** administrador,
**quero** carregar um novo plugin MCP,
**para que** eu adicione capabilities externas ao sistema.

| Item | Detalhe |
|------|---------|
| Componente | `PluginLoadModal` |
| API | `POST /api/admin/plugins/load` |
| Status | ✅ Implementado |

**Critérios de Aceite:**
- [x] Modal com tipo (stdio/sse), comando, argumentos
- [x] Validação de campos obrigatórios
- [x] Feedback de sucesso/erro
- [x] Plugin aparece na lista após carregamento

---

#### US-22 — Ver detalhes de plugin

**Como** administrador,
**quero** ver tools e resources de um plugin,
**para que** eu saiba o que cada plugin oferece.

| Item | Detalhe |
|------|---------|
| Componente | `PluginDetailModal` |
| API | `GET /api/admin/plugins/{id}` |
| Status | ✅ Implementado |

**Critérios de Aceite:**
- [x] Lista de tools disponíveis no plugin
- [x] Lista de resources disponíveis
- [x] Informações do plugin (tipo, comando, status)

---

### Épico 7: Real-time (SignalR)

#### US-23 — Conexão SignalR com ChatHub

**Como** aplicação frontend,
**quero** conexão persistente com o ChatHub via SignalR,
**para que** mensagens sejam trocadas em tempo real.

| Item | Detalhe |
|------|---------|
| Componente | `lib/signalr.ts` · `useChat` |
| Hub | `/hubs/chat` |
| Status | ✅ Implementado |

**Critérios de Aceite:**
- [x] Conexão singleton com auto-reconnect
- [x] Events: ReceiveMessage, ProcessingStarted, ReceiveError, Connected
- [x] Cleanup de listeners no unmount
- [x] Fallback REST quando SignalR indisponível

---

#### US-24 — Conexão SignalR com GatewayHub

**Como** dashboard de administração,
**quero** receber atualizações em tempo real do gateway,
**para que** métricas e status reflitam estado atual.

| Item | Detalhe |
|------|---------|
| Componente | `lib/signalr-gateway.ts` |
| Hub | `/hubs/gateway` |
| Status | ✅ Implementado |

**Critérios de Aceite:**
- [x] Conexão singleton separada do ChatHub
- [x] Auto-reconnect configurado
- [x] Start/stop controlado por lifecycle de componentes

---

#### US-25 — Indicador de processamento

**Como** usuário,
**quero** ver um indicador quando o agente está processando,
**para que** eu saiba que minha mensagem foi recebida.

| Item | Detalhe |
|------|---------|
| Componente | `MessageList` · `useChat` |
| Event | SignalR `ProcessingStarted` |
| Status | ✅ Implementado |

**Critérios de Aceite:**
- [x] Indicador visual (typing animation) durante processamento
- [x] Indicador desaparece quando resposta chega
- [x] Timeout para indicador (não fica infinito)

---

### Épico 8: Transversal (Shell / Auth / UX)

#### US-26 — Navegação por sidebar

**Como** usuário,
**quero** navegar entre páginas por sidebar lateral,
**para que** todas as funcionalidades sejam acessíveis.

| Item | Detalhe |
|------|---------|
| Componente | `Sidebar` |
| Router | 16 rotas em `App.tsx` |
| Status | ✅ Implementado |

**Critérios de Aceite:**
- [x] 16 itens: Chat, Dashboard, Agents, AgentChat, Tools, Skills, RAG, Gateway, GatewayHealth, Costs, IAs, Plugins, ScheduledTasks, Config, ConfigAdvanced, EmbeddingMigration
- [x] Ícones (lucide-react) por item
- [x] Item ativo destacado visualmente
- [x] Navegação via react-router-dom

---

#### US-27 — Feedback visual com Toast

**Como** usuário,
**quero** notificações toast para ações importantes,
**para que** eu receba feedback sem bloquear a interface.

| Item | Detalhe |
|------|---------|
| Componente | `Toast` · `ToastProvider` · `useToast` |
| Status | ✅ Implementado |

**Critérios de Aceite:**
- [x] Variantes: success, error, warning, info
- [x] Auto-dismiss após N segundos
- [x] Empilhamento de múltiplos toasts
- [x] Provider wrapping no `main.tsx`

---

#### US-28 — Confirmação de ações destrutivas

**Como** usuário,
**quero** modal de confirmação antes de ações destrutivas,
**para que** exclusões acidentais sejam prevenidas.

| Item | Detalhe |
|------|---------|
| Componente | `ConfirmModal` |
| Variantes | `default` · `danger` |
| Status | ✅ Implementado |

**Critérios de Aceite:**
- [x] Modal com título, mensagem e botões Confirmar/Cancelar
- [x] Variante danger com cor vermelha
- [x] Esc e click fora para cancelar

---

#### US-29 — Estados de loading e erro

**Como** usuário,
**quero** feedback visual durante carregamento e em erros,
**para que** eu saiba o estado de cada operação.

| Item | Detalhe |
|------|---------|
| Componentes | `Loading` · `PageLoading` · `PageError` |
| Status | ✅ Implementado |

**Critérios de Aceite:**
- [x] Spinner animado durante carregamento
- [x] PageLoading: spinner centralizado em full-page
- [x] PageError: mensagem + botão retry
- [x] Consistente em todas as páginas

---

#### US-30 — Tema dark e design system

**Como** usuário,
**quero** interface com tema dark e componentes consistentes,
**para que** a experiência visual seja profissional e agradável.

| Item | Detalhe |
|------|---------|
| Componentes | `Badge` · `index.css` (theme) · `lib/utils.ts` (cn) |
| Status | ✅ Implementado |

**Critérios de Aceite:**
- [x] Tema dark com cores customizadas (zinc-850, zinc-925)
- [x] Badge com variantes: default, success, warning, danger, violet
- [x] Utility `cn()` para composição de classes (clsx + tailwind-merge)
- [x] Scrollbar customizado
- [x] Tipografia e espaçamento consistentes

---

## Frontend — Resumo de Cobertura

| Épico | Stories | IDs | Componentes | Status |
|-------|:-------:|-----|:-----------:|:------:|
| Chat Interface | 5 | US-01, US-01A, US-02 a US-04 | 6 | ✅ |
| Gateway Dashboard | 4 | US-05 a US-08 | 4 | ✅ |
| Agent Management | 6 | US-09 a US-14 | 4 | ✅ |
| LLM Providers | 2 | US-15, US-16 | 1 | ✅ |
| Settings | 3 | US-17 a US-19 | 1 | ✅ |
| MCP Plugins | 3 | US-20 a US-22 | 3 | ✅ |
| Real-time (SignalR) | 3 | US-23 a US-25 | 2 | ✅ |
| Transversal | 5 | US-26 a US-30 | 5 | ✅ |
| Chat Dedicado | 3 | US-31 a US-33 | 2 | ✅ |
| **Total** | **34** | | **28 componentes** | **✅** |

---

## Artefatos de Teste (QA)

| Tipo | Quantidade | Localização |
|------|:----------:|-------------|
| Cenários BDD | 17 + 6 features (chat dedicado) | Documentados nesta spec + `docs/bdd/` |
| Cypress API tests | 3 suítes (14 testes) | `frontend/cypress/e2e/` |
| K6 performance | 1 script | `frontend/k6/gateway-load-test.js` |
| xUnit (backend) | 408 testes | `tests/AgenticSystem.Tests/` |

---

## Build Status

| Camada | Ferramenta | Resultado |
|--------|-----------|-----------|
| Backend (.NET) | `dotnet build` | ✅ 0 errors, 0 warnings |
| Backend testes | `dotnet test` | ✅ 408 testes passando |
| Frontend (TS) | `npx tsc --noEmit` | ✅ 0 errors |
| Frontend (Vite) | `npx vite build` | ✅ 1964 modules, 521KB JS |

---

## US-31 — Chat dedicado via lista de agents

**Como** usuário do AgenticSystem  
**Quero** abrir um chat direto com um agent específico a partir da lista  
**Para** enviar mensagens diretamente ao agent sem roteamento automático

### Critérios de Aceite

- [x] Botão "Chat direto" (ícone MessageSquare) visível em cada card de agent na `/agents`
- [x] Ao clicar, navega para `/chat/{agentName}`
- [x] Página dedicada exibe header com nome do agent e botão de voltar
- [x] Placeholder do input indica o agent alvo: "Envie uma mensagem para {agentName}..."
- [x] Subtítulo indica "Mensagens vão direto para este agent"

### Impacto Técnico

| Camada | Alteração |
|--------|-----------|
| Frontend | Rota `/chat/:agentName`, componente `AgentChatPage`, botão em `AgentsPage` |
| Frontend | `useChat` aceita `targetAgent?: string` |

---

## US-32 — Mensagem vai direto ao agent selecionado

**Como** usuário no chat dedicado  
**Quero** que minhas mensagens sejam processadas diretamente pelo agent alvo  
**Para** obter respostas sem análise de contexto intermediária

### Critérios de Aceite

- [x] SignalR `SendMessage` envia `targetAgent` como terceiro argumento
- [x] REST `POST /api/chat` inclui `targetAgent` no body
- [x] Backend `ProcessDirectRequestAsync` é invocado quando `targetAgent` presente
- [x] Agent é localizado por nome (case-insensitive)
- [x] Análise de contexto é bypassed (não executa ContextAnalysis)
- [x] Sessão registra evento com `directRequest = true`
- [x] Se agent não encontrado, retorna erro: "Agent '{name}' não encontrado."

### Impacto Técnico

| Camada | Alteração |
|--------|-----------|
| SignalR | `ChatHub.SendMessage` ganha parâmetro `string? targetAgent = null` |
| Backend | `IMetaAgent.ProcessDirectRequestAsync(input, context, targetAgent)` |
| Backend | `MetaAgentOrchestrator` implementa lookup + delegação direta |
| API | `ChatRequest` record ganha `TargetAgent` opcional |

---

## US-33 — Histórico separado e retorno ao roteamento automático

**Como** usuário  
**Quero** que o histórico do chat dedicado seja independente do chat genérico  
**Para** manter contexto separado e poder voltar ao roteamento automático quando quiser

### Critérios de Aceite

- [x] Mensagens no chat dedicado não aparecem no chat genérico (rota `/`)
- [x] Cada chat dedicado tem histórico independente
- [x] Ao navegar para `/`, o roteamento automático é restaurado (targetAgent = null)
- [x] Footer do chat genérico mantém texto sobre seleção automática
- [x] Chat genérico continua funcionando normalmente sem targetAgent

### Impacto Técnico

| Camada | Alteração |
|--------|-----------|
| Frontend | `useChat` instanciado separadamente por page (App.tsx vs AgentChatPage) |
| Frontend | Estado de mensagens isolado por instância do hook |
| Backend | Compatibilidade mantida — sem targetAgent = comportamento original |
