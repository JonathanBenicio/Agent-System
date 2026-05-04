# User Stories — Agentic System

> Catálogo consolidado de User Stories do backend (.NET 8) e frontend (React 19).
> Gerado via pipeline Spec→Code em maio/2026.

---

## Índice

- [Backend — Maturity Levels (ML1–ML23)](#backend--maturity-levels-ml1ml23)
- [Frontend — Épicos e User Stories (US-01–US-30)](#frontend--épicos-e-user-stories-us-01us-30)

---

## Backend — Maturity Levels (ML1–ML23)

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

---

### Infraestrutura Transversal (Backend)

#### Gateway de Serviços Externos

**Como** sistema que consome APIs externas,
**quero** um gateway com resiliência e governança,
**para que** falhas externas não derrubem o sistema.

| Item | Detalhe |
|------|---------|
| Serviço | `ExternalServiceGateway` |
| Capabilities | Circuit Breaker, Rate Limiter, Cost Tracker, Health Monitor |
| Testes | Unitários (xUnit) |
| Status | ✅ Implementado |

**Critérios de Aceite:**
- [x] Circuit Breaker: abre após N falhas consecutivas, half-open após cooldown
- [x] Rate Limiter: controle por provider (ex: 60 req/min OpenAI)
- [x] Cost Tracker: rastreamento de custo por provider/model
- [x] Health Monitor: health check periódico de cada serviço externo
- [x] Dashboard de saúde via API admin

---

#### Document Pipeline (RAG)

**Como** sistema de RAG,
**quero** pipeline completo de ingestão, chunking e re-ranking,
**para que** documentos alimentem o contexto dos agents com alta precisão.

| Item | Detalhe |
|------|---------|
| Serviços | Parsers (Markdown, PlainText, HTML) · Hybrid Chunking · Heuristic ReRanker |
| Testes | Unitários (xUnit) |
| Status | ✅ Implementado |

**Critérios de Aceite:**
- [x] Parsers suportam Markdown, PlainText e HTML
- [x] Chunking híbrido preserva estrutura semântica
- [x] ReRanker heurístico: < 5ms/query (vs. 200-500ms cross-encoder)
- [x] Interface `IReRanker` permite swap para cross-encoder futuro

---

#### Hierarquia de Agents

**Como** sistema de agentes,
**quero** hierarquia por tiers com especialização,
**para que** cada nível tenha responsabilidades claras.

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
| Transversal | — | 3 | ✅ |
| **Total** | **23** | **34 serviços** | **432+ testes** |

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
**quero** ver e gerenciar providers de LLM configurados,
**para que** eu controle quais modelos estão disponíveis.

| Item | Detalhe |
|------|---------|
| Componente | `ProvidersPage` |
| API | `GET /api/admin/llm/providers` |
| Status | ✅ Implementado |

**Critérios de Aceite:**
- [x] Lista de providers com status (enabled/disabled)
- [x] Informações: nome, modelo default, prioridade
- [x] Ação de editar configuração

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
| Router | 12 rotas em `App.tsx` |
| Status | ✅ Implementado |

**Critérios de Aceite:**
- [x] 12 itens: Chat, Dashboard, Agents, Tools, Skills, RAG, Gateway, Saúde, Custos, Providers, Plugins, Config
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
| Chat Interface | 4 | US-01 a US-04 | 5 | ✅ |
| Gateway Dashboard | 4 | US-05 a US-08 | 4 | ✅ |
| Agent Management | 6 | US-09 a US-14 | 4 | ✅ |
| LLM Providers | 2 | US-15, US-16 | 1 | ✅ |
| Settings | 3 | US-17 a US-19 | 1 | ✅ |
| MCP Plugins | 3 | US-20 a US-22 | 3 | ✅ |
| Real-time (SignalR) | 3 | US-23 a US-25 | 2 | ✅ |
| Transversal | 5 | US-26 a US-30 | 5 | ✅ |
| Chat Dedicado | 3 | US-31 a US-33 | 2 | ✅ |
| **Total** | **33** | | **27 componentes** | **✅** |

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
