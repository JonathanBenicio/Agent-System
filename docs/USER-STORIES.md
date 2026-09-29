# User Stories — Agentic System

## BACK-FIX-111–117 — Núcleo seguro e operacional
Como usuário de um tenant, quero executar conversas e consumir recursos autorizados sem acesso cruzado ou falhas de persistência, para operar o núcleo com isolamento e contabilização confiáveis.

Issues: [#111](https://github.com/JonathanBenicio/Agent-System/issues/111)–[#117](https://github.com/JonathanBenicio/Agent-System/issues/117) • [ADR-035](architecture/adr/035-backend-core-isolation-and-reliability.md) • [Plano](plan/backend-core-remediation.md) • [Validação](backend/validation/backend-core-remediation.md). Status: cenários funcionais validados; release ainda depende do gate de cobertura e dos limites descritos no relatório.

- BACK-FIX-111: chave Viewer conserva Viewer e não muda tenant sem vínculo/concessão; Admin legado permanece scoped.
- BACK-FIX-112: header/query/claim seguem a mesma política; tenants desconhecidos/inativos são negados; eventos dos cinco hubs permanecem no tenant autorizado; Gateway global exige Platform Admin.
- BACK-FIX-113: RAG vazio nega, SQL funciona no PostgreSQL real e sala permitida é encontrada mesmo com mais de 55 candidatos proibidos.
- BACK-FIX-114: chat REST/SSE/SignalR gera conteúdo/sessionId, retoma somente para o dono e persiste após restart com isolamento MAF configurado.
- BACK-FIX-115: incrementos confirmados não se perdem sob concorrência; consumo real de tokens/custo é persistido e volta a ser bloqueado após restart; janela RPM é local ao processo.
- BACK-FIX-116: dois tenants têm defaults completos e isolados após restart; seeding concorrente/idempotente conserva IDs e customizações antigas.
- BACK-FIX-117: memberships por tenant aplicam papéis independentes; Platform Admin não tem conteúdo implícito; suporte expira/revoga com auditoria; gateway global exige Platform Admin; planos limitam quotas e recursos, ACL de sala sempre obrigatória.

Evidência unitária não substitui integração; critérios falhos/não executados permanecem abertos.

## BACK-MAF-120 — MAF atualizado e providers integrados ao Gateway

Como mantenedor do backend multi-tenant, quero atualizar o Microsoft Agent Framework com compatibilidade comprovada e encaminhar providers ativos pelo Gateway, para que o runtime de produção tenha execução e telemetria reais sob os controles existentes.

Issue: [#120](https://github.com/JonathanBenicio/Agent-System/issues/120) · [ADR-036](architecture/adr/036-maf-122-protocols-and-gateway.md) · [Plano](plan/maf-122-protocols-gateway.md).
Status: implementação MAF/Gateway validada; suíte 755 aprovados, 1 skip vetorial, 0 falhas; build limpo. PostgreSQL validou store global/NOTIFY, dois LLMManagers/Gateways com inferência Ollama, sessão MAF reaberta após reinício real da API e Wait recuperado após encerramento forçado. Banner gerou arquivo com client determinístico e skills reais; inferência vision/editor ainda não foi executada. Handler externo precisa deduplicar após crash. A2A/AG-UI preview está separado em #121. [Evidência](backend/validation/maf-122-workflow-runtime-2026-09-29.md).

- Dada sessão pertencente a usuário/tenant, quando criada, serializada, retomada ou restaurada após restart, então seu owner, tenant, ID e estado MAF permanecem iguais; identidade de outro tenant recebe negação sem dados.
- Dado provider de infraestrutura habilitado na configuração do host, quando a aplicação inicia e chama o modelo, então ele aparece no Gateway e chamadas completas/streaming atualizam status/circuito/limite; provider desabilitado não é registrado.
- Dada API key BYOK pertencente a um tenant, quando esse tenant escolhe o provider, então sua rota e quota por tenant permanecem isoladas, sem compartilhar circuit/rate state global via Gateway.
- Dado Platform Admin que atualiza provider global, quando dois tenants usam o runtime após restart ou em nós diferentes, então ambos veem os mesmos limites/modelos/chave global cifrada; mudança fica auditada pelo ator e a API retorna apenas presença da credencial.
- Dado tenant com configuração BYOK existente, quando a configuração global é alterada, então o valor legado continua tenant-scoped e não é promovido nem sobrescrito; a chamada usa credencial do tenant antes da global.
- Dado falha/cancelamento durante stream, quando ocorre, então recursos são liberados, falha é contabilizada e fallback só é usado antes de conteúdo ter sido entregue; quotas persistidas por tenant continuam aplicadas.
- Dada mudança de pacote/API MAF ou migration explícita do store global, quando build/testes/EF são executados, então incompatibilidade é corrigida sem migrar dados tenant para globais, ou fica registrada como bloqueio verificável.
- Dado workflow dinâmico compilado por tenant/request no modo PostgreSQL, quando iniciado, então existe um worker compatível e todas as instâncias conhecem a mesma versão da definição; caso contrário, o backend não deve retornar uma execução pendente como se a tivesse enfileirado com sucesso.

## BACK-ORCH-122 — Orquestrar agentes dinamicos pelo supervisor MAF

Como usuário da plataforma de agentes personalizáveis, quero que o orquestrador identifique e delegue a solicitação aos especialistas ativos configurados para meu tenant, para receber resposta consolidada sem perder o estado das sessões.

Issue: [#122](https://github.com/JonathanBenicio/Agent-System/issues/122) · [ADR-038](architecture/adr/038-dynamic-supervisor-orchestrator.md) · [Plano separado](plan/dynamic-orchestrator-implementation.md). Dependência: API de sessões MAF 1.22 em [#120](https://github.com/JonathanBenicio/Agent-System/issues/120).
Status: implementação funcional do supervisor concluída; regressões cobrem multi-tool, binding, identidade, catálogo, persistência seletiva de sessões e cache tenant-scoped. Sessão MAF do supervisor reaberta após reinício real da API; especialistas só foram reabertos com novos adapters/contextos. Suíte: 755 aprovados/1 skip.

- Dada lista de specialists com bindings válidos, quando o modo “Intelligent Router” recebe input, então `ChatClientAgent` do MAF pode invocar um ou mais `AIFunction`s correspondentes e consolidar resposta útil.
- Dado agente ativo cuja tool/binding falhou ou agente inativo, quando o prompt supervisor é construído, então ele não é apresentado como candidato delegável.
- Dada alteração de descrição/domínio/tier/tools no catálogo, quando o próximo request constrói supervisor, então a instrução/cache reflete a nova configuração sem restart.
- Dada execução com delegação, quando termina, então a sessão MAF do supervisor e de cada specialist invocado é persistida com partição tenant+usuário e retomável após restart.
- Dada chamada direta por `targetAgent`, quando executada, então continua bypassando o orquestrador; respostas SignalR/REST continuam informando `agentName` real e `sessionId`.
- Dada execução sem especialista aplicável, quando o supervisor não delega, então a resposta direta continua válida; quota, erro e cancelamento não produzem sucesso vazio.

## BACK-PROTO-121 — Validar A2A e AG-UI sob hosting preview

Como integrador de protocolos, quero validar A2A e AG-UI de ponta a ponta depois da atualização do core MAF, para saber se autenticação, tenant, sessão e streaming funcionam antes de tratar esses endpoints preview como suportados.

Issue: [#121](https://github.com/JonathanBenicio/Agent-System/issues/121) · [ADR-037](architecture/adr/037-a2a-agui-preview-validation.md) · [Plano](plan/a2a-agui-preview-validation.md). Dependência: [#120](https://github.com/JonathanBenicio/Agent-System/issues/120).
Status: planejada; prioridade secundária, não bloqueia MAF core/Gateway.

- Dado hosting preview compatível com o core atualizado, quando a flag habilita A2A/AG-UI, então endpoint e contrato básico iniciam no host de validação.
- Dada identidade sem auth, tenant desconhecido/inativo ou sem membership, quando invoca qualquer protocolo, então a chamada é negada sem emitir conteúdo.
- Dada sessão em tenant A, quando identidade de tenant B tenta criar/retomar ou subscrever stream, então recebe negação sem conteúdo de A.
- Dada execução válida em cada protocolo, quando resposta ou stream ocorre e é cancelado, então formato esperado chega ao solicitante e recursos são encerrados.
- Dada evidência com modelo local, fixture, skip ou indisponibilidade externa, quando registrada, então cada categoria fica distinguida e nenhuma é descrita como validação mais ampla.

## BACK-DOC-001 — Contratos claros e validação do núcleo

Como mantenedor, quero contratos rastreáveis de endpoints, acesso e recursos, para distinguir funcionalidades comprovadas de lacunas.

Issue: [#110](https://github.com/JonathanBenicio/Agent-System/issues/110) • ADR: [034](architecture/adr/034-backend-contracts-and-access-target.md) • Plano: [execução](plan/backend-documentation-validation.md).
Status: documentação e diagnóstico entregues; falhas de produto no [backlog](backend/backlog.md). Evidência: [relatório](backend/validation/2026-09-28.md).

- Dado o código da baseline, quando consultar o hub, então encontrar rotas e fontes, regras atuais, alvo desejado e limites de validação.
- Dado dois tenants e usuários sem ACL, quando executar diagnóstico, então registrar aprovação ou reprodução de vazamento/negação incorreta sem alterar produção.
- Dado falha ou cenário não executado, quando entregar o PR, então informar resultado e backlog sem declarar estabilidade.
- Dado nova iniciativa, quando usar templates, então obter issue → ADR → story → plano → commits → PR e evidências adequadas.

IDs novos usam domínio e número únicos; IDs históricos duplicados permanecem como legado, sem renumeração destrutiva.

> Catálogo consolidado de User Stories do backend (.NET 10, runtime framework-first hospedado) e frontend (React 19).
> Gerado via pipeline Spec→Code em maio/2026.

---

## Índice

- [Backend — Maturity Levels (ML1–ML35)](#backend--maturity-levels-ml1ml35)
- [Frontend — Épicos e User Stories (US-01–US-30)](#frontend--épicos-e-user-stories-us-01us-30)

---

## Backend — Platform Capabilities (Integrated Architecture)

As capacidades abaixo compõem a baseline unificada do Agentic System. O modelo de "Maturity Levels" (ML) evoluiu para uma **Arquitetura de Capacidades Nativas**, onde cada funcionalidade é integrada via Microsoft Agent Framework (MAF).

### Core Foundation

#### Memory & Chunk Lifecycle

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

#### ML3 — Task Planning & Native Workflow Orchestration

**Como** usuário que faz solicitações complexas,
**quero** que o sistema orquestre tarefas usando os workflows nativos do MAF 1.6+,
**para que** tarefas multi-step sejam executadas de forma padrão e observável.

| Item | Detalhe |
|------|---------|
| Serviço | `ITaskPlanManager` / `WorkflowBuilder` Nativo |
| Responsabilidade | Criação de planos e roteamento entre agentes usando primitivas nativas (`Microsoft.Agents.AI.Workflows`) |
| Testes | Unitários (xUnit) |
| Status | ✅ Em Migração (MAF Nativo) |

**Critérios de Aceite:**
- [x] O workflow deve ser instanciado via `Microsoft.Agents.AI.Workflows.WorkflowBuilder`.
- [x] Estados de transição e roteamento são manipulados via `RouteBuilder` e `WorkflowSession`.

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

#### ML35 — Smart Triage & Fast Path

**Como** arquiteto do sistema,
**quero** um pipeline de triagem em 3 camadas (Regex -> ML.NET -> LLM),
**para que** consultas simples sejam respondidas instantaneamente com custo zero e consultas complexas sejam roteadas para o agente especialista correto.

| Item | Detalhe |
|------|---------|
| Serviço | `TriageService`, `MlFastPathInterceptor`, `ConversationalFastPathInterceptor` |
| Responsabilidade | Classificação de intenção de baixa latência, interceptação de saudações e roteamento inteligente para agentes especialistas |
| Testes | Integração (xUnit) e ML.NET Model Validation |
| Status | ✅ Implementado |

**Critérios de Aceite:**
- [x] Layer 0 (Regex): Intercepta saudações e comandos triviais com < 5ms de latência
- [x] Layer 0.5 (ML.NET/ONNX): Classifica intenções (Agent_Capabilities, System_Status, SmallTalk) usando modelo local exportado para ONNX para máxima portabilidade e performance
- [x] Layer 1 (LLM): Realiza triagem profunda e decomposição de tarefas para consultas complexas
- [x] Roteamento para `DotNetExpertAgent` quando detectado domínio técnico de backend
- [x] Resiliência: se o modelo local falhar, o sistema faz fallback gracioso para LLM

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

#### ML12 — Dynamic Delegation

**Como** sistema de orquestração,
**quero** delegação mid-conversation entre agents,
**para que** cada subtarefa seja resolvida pelo agent mais qualificado.

| Item | Detalhe |
|------|---------|
| Serviço | `IFrameworkOrchestratorService` + `IAgentChannelService` + `AgentCollaborationWorkflow` |
| Responsabilidade | Delegação por tool bindings, canais estruturados e workflow colaborativo (planner → executor → reviewer), com suporte a topologias `SingleDelegate`, `FanOut` e `Chain` |
| Testes | Unitários (xUnit) |
| Status | ✅ Implementado |

**Critérios de Aceite:**
- [x] SingleDelegate: um agent sabe quem é melhor para a subtarefa
- [x] FanOut: múltiplas perspectivas em paralelo
- [x] Chain: pipeline sequencial (output → input)
- [x] Contexto é preservado entre delegações via sessão estruturada e bindings
- [x] Histórico de delegações é rastreável

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
| Implementações | `InMemorySessionStore` (dev/local) · `PostgresSessionStore` (persistência principal) |
| Testes | Unitários + Integração (xUnit) |
| Status | ✅ Implementado |

**Critérios de Aceite:**
- [x] Abstração `ISessionStore` com CRUD completo
- [x] InMemory para dev/test e execuções locais leves
- [x] PostgreSQL para persistência durável do runtime
- [x] Sessões suportam multi-tenant (ML19)
- [x] TTL configurável para expiração automática
- [x] `AgenticDbContext : DbContext` — contexto EF Core centralizado para todas as entidades
- [x] Entidades persistidas: `SessionData`, `Tenant`, `VectorDocumentEntity`, `CostBudgetEntity`, `CostEntryEntity`, `AgentPerformanceMetricEntity`
- [x] Cada entidade com `IEntityTypeConfiguration<T>` para mapeamento explícito
- [x] `PostgresCostTracker : ICostTracker` — tracking de custo por provider/model em PostgreSQL
- [x] `PostgresVectorStore : IVectorStore` — armazenamento de vetores com pgvector
- [x] `SimpleSessionStoreAdapter` integra a persistência ao runtime hospedado do Agent Framework

---

#### ML17 — IChatClient Compatibility & Native MAF Integration

**Como** integrador de LLM providers,
**quero** utilizar os clientes nativos do Microsoft Agent Framework (`Microsoft.Agents.AI.OpenAI`),
**para que** a comunicação com o LLM possua telemetria oficial e binding otimizado de ferramentas.

| Item | Detalhe |
|------|---------|
| Serviço | `LLMManager` + `ContextAwareChatClient` + `ProviderBackedChatClient` (Legado) / Clientes MAF Nativos |
| Responsabilidade | Seleção dinâmica de provider/modelo no runtime e interoperabilidade nativa com MAF 1.6+ |
| Testes | Unitários (xUnit) |
| Status | ✅ Em Migração (MAF Nativo) |

**Critérios de Aceite:**
- [x] O pipeline principal deve instanciar LLMs utilizando bibliotecas oficiais (`Microsoft.Agents.AI.OpenAI`).
- [x] `ContextAwareChatClient` resolve provider/modelo a partir do contexto runtime atual
- [x] `LLMManager` mantém catálogo administrativo e registro de chat clients por provider
- [x] `ProviderBackedChatClient` oferece compatibilidade reversa quando um fluxo precisa expor `ILLMProvider` como `IChatClient`
- [x] Mapeamento de request/response é transparente para chamadas do pipeline principal
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

#### ML19.1 — Auto-Bootstrap e Remoção do Tenant Default

**Como** arquiteto do sistema,  
**quero** que a plataforma gerencie credenciais dinamicamente via banco de dados e auto-provisione o tenant inicial admin,  
**para que** o fallback inseguro "default" seja eliminado e haja isolamento multi-tenant real e estrito.

| Item | Detalhe |
|------|---------|
| Serviços | `SystemBootstrapService` · `ApiKeyAuthenticationHandler` · `TenantMiddleware` |
| Responsabilidade | Auto-bootstrap de tenant/chaves no startup, validação de chaves hashed SHA-256 e remoção de referências hardcoded a "default" |
| Testes | Unitários (xUnit) e Integração/E2E |
| Status | ⏳ Proposto |

**Critérios de Aceite:**
- [ ] O banco de dados reflete o tenant `admin` e `access_api_keys` populados automaticamente no primeiro boot se a tabela estiver vazia.
- [ ] Chaves de API são armazenadas exclusivamente como hash SHA-256 de via única, protegendo as chaves contra vazamento físico de banco.
- [ ] Requisições com chaves válidas (enviadas por cookie ou header) são resolvidas para o `tenant_id` correto associado no banco.
- [ ] Requisições autenticadas sem um `tenant_id` final explícito são rejeitadas pelo `TenantMiddleware` com HTTP 403 Forbidden.
- [ ] Nenhuma constante estática ou string `"default"` permanece como fallback implícito no Core ou Api do sistema.

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
| Serviços | `DocumentIngestionPipeline` · `MarkdownParser` · `PlainTextParser` · `HtmlParser` · `HybridChunkingStrategy` · `RAGService` · `LlmReRanker` · `QueryCompressorService` · `SemanticCompressorService` |
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
| Serviços | `MetaAgentOrchestrator` · `HierarchicalAgentFactory` · `AgentFrameworkDirectExecutionService` (path direto explícito) |
| DI | `IMetaAgent`, `IAgentFactory`, `IContextAnalyzer` |
| Pattern | Factory + serviço de execução direta (o path direto aciona o runtime do framework sem wrapper transitório de `IAgent`) |

| Tier | Papel | Agents |
|:----:|-------|--------|
| 0 | Chief | MetaAgent (análise + roteamento) |
| 1 | Master | PersonalAgent, WorkAgent, LearningAgent |
| 2 | Specialist | CreativeAgent, AnalysisAgent, CalendarAgent, DotNetExpertAgent |
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
- [x] Direct execution service: `AgentFrameworkDirectExecutionService` executa o agent cru pelo runtime do framework só no `ExecuteDirectAsync`
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
- Catálogo interno (Labs `Ferramentas/`)
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
- Circuit breaker no delivery channel (`CircuitBreaker` local, sem dependência externa específica) para evitar flood em caso de falha do destino
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
- [x] Provider registrado via DI com `HttpClient` factory (resiliência configurável sem acoplar biblioteca específica ao contrato)

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
| Adapter | `McpToolsAIFunctionAdapter` — bridge MCP tools → `AIFunction` (M.E.AI) |
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

### Agent Runtime Platform (ML29–ML34)

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

#### ML34 — Protocol Hosting e Interoperabilidade

**Como** plataforma extensível,
**quero** hospedar os agentes via protocolos padronizados (A2A, AG-UI e OpenAI-compatible),
**para que** sistemas externos, UIs especializadas e ferramentas do ecossistema interajam com o orquestrador nativamente.

| Item | Detalhe |
|------|---------|
| Serviços | `A2A Protocol`, `AG-UI Protocol`, `OpenAIChatCompletionController` |
| Endpoints | `POST /a2a`, `POST /agui`, `POST /v1/chat/completions`, `GET /v1/models` |
| Status | ✅ Implementado |

**Critérios de Aceite:**
- [x] Hospedagem de endpoints via `Microsoft.Agents.AI.Hosting`
- [x] O orquestrador hospedado reaproveita a sessão nativa do framework
- [x] Compatibilidade total com requisições formato OpenAI (`/v1/chat/completions`)
- [x] Rate limiting e autenticação centralizados via middlewares de protocolo

---

#### ML38 — RAG Avançado (Reranker)

**Como** orquestrador que precisa de alta precisão na recuperação de documentos,
**quero** utilizar um Cross-Encoder ReRanker local (ONNX) após a busca vetorial,
**para que** os documentos mais relevantes sejam priorizados no contexto enviado ao LLM, reduzindo alucinações.

| Item | Detalhe |
|------|---------|
| Serviços | `LocalOnnxCrossEncoderReRankerProvider` |
| Responsabilidade | Re-ranqueamento de chunks recuperados via modelo ONNX local |
| Status | ✅ Implementado |

**Critérios de Aceite:**
- [x] Carregamento do modelo ONNX e vocabulário na inicialização.
- [x] Processamento de pares (query, chunk) para atribuição de score de relevância.
- [x] Filtragem e reordenação dos Top-K resultados antes de passar para o gerador.

---

#### ML39 — FinOps & Auto-Melhoria

Issue [#16](https://github.com/JonathanBenicio/Agent-System/issues/16) · decisão de aprovação humana: [ADR-040](architecture/adr/040-self-improvement-human-approval.md) · [especificação/status](plan/open-issues-specification-audit-2026-09-29.md#issue-16).

**Como** administrador do sistema,
**quero** cotas proativas de uso de LLM e processamento em batch para auto-melhoria,
**para que** os custos sejam controlados e o sistema aprenda sem impactar a latência das respostas em tempo real.

| Item | Detalhe |
|------|---------|
| Serviços | `ProactiveQuotaManager` · `SelfImprovementService` |
| Responsabilidade | Controle de custos e limites de tokens; execução assíncrona de rotinas de auto-melhoria |
| Status | ⏳ Planejado |

**Critérios de Aceite:**
- [ ] Bloqueio de requisições que excedam a quota diária de tokens/custo.
- [ ] Processamento diário de reflexões em background (Hosted Service).
- [ ] Mudanças sugeridas são propostas tenant-scoped, persistidas e versionadas; `confidence` informa prioridade, mas nunca autoriza aplicação automática.
- [ ] Owner/Admin do tenant revisa e aprova/rejeita a proposta antes de aplicar; aprovação, ator, versão anterior/nova, avaliação e rollback ficam auditáveis. `confidence` não substitui aprovação humana.

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
| Agent Runtime Platform | ML29–ML34 | 6 | ✅ |
| Advanced Capabilities | ML35–ML39 | 3 | ⏳ |
| Transversal | T1–T10 | 10 | ✅ |
| **Total** | **39 MLs + 10 Transversais** | **57 serviços** | **549+ testes** |

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

#### US-11 — Ingestão RAG via Drag and Drop de arquivos

**Como** analista de conhecimento,
**quero** arrastar e soltar arquivos na área de chat,
**para que** eles sejam processados e indexados instantaneamente no Vector Store (da sessão ou de uma sala ativa).

| Item | Detalhe |
|------|---------|
| Componente | `ChatPage` · `ragApi` |
| Status | ✅ Implementado |

**Critérios de Aceite:**
- [x] Overlay visual (backdrop blur) com mensagem "Ingestão RAG Contextual" ao arrastar arquivos sobre o chat.
- [x] Spinner "Processando Documentos" bloqueia interações temporariamente durante a ingestão.
- [x] Roteamento de contexto de destino para a Sala de Conhecimento ativa ou para a Sessão temporária.
- [x] Exibe feedback visual via Toast de sucesso/erro e adiciona mensagem especial de sistema informando o status da indexação.

---

#### US-12 — Rastreabilidade e Citações de Fontes RAG

**Como** usuário exigente,
**quero** auditar as fontes e trechos de documentos que embasaram a resposta do agente,
**para que** eu possa evitar alucinações e verificar a exatidão das respostas.

| Item | Detalhe |
|------|---------|
| Componente | `MessageBubble` (citations) |
| Status | ✅ Implementado |

**Critérios de Aceite:**
- [x] Seção "Fontes (n)" com ícone de livro em respostas baseadas em RAG.
- [x] Exibição de pílulas bibliográficas resumindo o nome do documento.
- [x] Popover interativo exibido ao clicar na pílula da citação.
- [x] Popup contendo o trecho exato citado (`relevantExcerpt`), porcentagem de confiança e página do documento original.

---

#### US-13 — Visualização de Workflows, Ações e Metadados

**Como** operador de sistema,
**quero** monitorar a execução de fluxos, ferramentas e metadados diretamente no fluxo do chat,
**para que** eu compreenda a tomada de decisões e a orquestração do agente.

| Item | Detalhe |
|------|---------|
| Componente | `MessageBubble` · `WorkflowExecutionCard` |
| Status | ✅ Implementado |

**Critérios de Aceite:**
- [x] Renderização inteligente do cartão interativo `WorkflowExecutionCard` quando a resposta do agente contiver `workflowExecutionId`.
- [x] Badges visuais identificadores para ferramentas (`🔧 tool`) e ações executadas (`⚡ action`).
- [x] Badge indicador de "Memória Recuperada" nas mensagens enviadas do usuário quando contextualizadas via memória episódica.
- [x] Mensagens de sistema (erros/conexão) formatadas com borda avermelhada e ícone de perigo `AlertTriangle`.

---


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
| Workflow Orchestration | 2 | US-34, US-35 | 1 | ⏳ |
| Webhooks Integration | 2 | US-36, US-37 | 1 | ⏳ |
| Alerts History | 2 | US-38, US-39 | 1 | ⏳ |
| Specialized Context & Evolution | 4 | US-41 a US-44 | 1 | 🚧 |
| Dynamic ONNX Inference Engine | 3 | US-45 a US-47 | 4 | ✅ |
| Dynamic Customization & No-Code | 3 | US-48 a US-50 | 2 | ⏳ |
| **Total** | **50** | | **38 componentes** | **⏳** |


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

---

## US-34 — Visualizar e manipular Canvas de Workflows

**Como** administrador  
**Quero** visualizar e manipular um canvas interativo de workflows  
**Para** orquestrar visualmente tarefas entre agentes e ferramentas

### Critérios de Aceite

- [x] Canvas interativo com suporte a drag and drop de nós e conexões
- [x] Tipos de nós suportados: Agent Node e Tool Node
- [x] Toolbar com ações de adicionar nós, salvar e executar
- [x] Painel de status do motor exibindo nós ativos e conexões

---

## US-35 — Salvar e Executar Workflow

**Como** administrador  
**Quero** salvar a definição do workflow e executá-lo  
**Para** automatizar processos complexos no sistema

### Critérios de Aceite

- [x] Botão "Save Workflow" gera a definição do workflow (JSON) e envia para a API
- [x] Botão "Run" dispara a execução do workflow no backend
- [x] Feedback visual de salvamento e execução

---

## US-36 — Gerenciar Webhooks (CRUD)

**Como** administrador  
**Quero** listar, criar e excluir webhooks  
**Para** permitir que sistemas externos disparem ações no AgenticSystem

### Critérios de Aceite

- [x] Lista de webhooks com nome, status (Ativo/Inativo), data de criação e último disparo
- [x] Formulário para criar webhook com nome, agente alvo (opcional) e workflow alvo (opcional)
- [x] Ação de excluir webhook com confirmação
- [x] Copiar URL do webhook para a área de transferência

---

## US-37 — Receber Webhook e disparar ação

**Como** sistema externo  
**Quero** enviar um payload para a URL do webhook  
**Para** disparar um agente ou workflow automaticamente

### Critérios de Aceite

- [x] Endpoint `/api/webhooks/receive/{id}` recebe requisições POST
- [x] Execução é encaminhada para o agente ou workflow configurado
- [x] Retorno de sucesso ou erro apropriado para o chamador

---

## US-38 — Visualizar Histórico de Alertas

**Como** administrador  
**Quero** visualizar o histórico de alertas de cota e saldo  
**Para** monitorar o consumo e saúde financeira do sistema

### Critérios de Aceite

- [x] Lista de alertas exibindo provider, tipo, mensagem, percentual restante e data
- [x] Alertas não lidos destacados visualmente
- [x] Botão para atualizar a lista de alertas

---

## US-39 — Marcar Alerta como Lido

**Como** administrador  
**Quero** marcar um alerta como lido  
**Para** organizar meu histórico e focar nos alertas pendentes

### Critérios de Aceite

- [x] Botão de check para marcar alerta como lido
- [x] Atualização do estado do alerta na interface sem recarregar a página

---

### Épico 9: Specialized Context & Evolution (Roadmap Q2 2026)

#### US-41 — Associar Agente a Knowledge Rooms

**Como** administrador de segurança,
**quero** selecionar quais Knowledge Rooms um agente pode acessar,
**para que** o escopo de busca semântica seja restrito a contextos específicos e seguros.

| Item | Detalhe |
|------|---------|
| Componente | `AgentFormModal` (seletor múltiplo) |
| API | `PUT /api/agent/agents/{name}/rooms` |
| Status | ✅ Implementado (ADR-019) |

**Critérios de Aceite:**
- [x] Lista de salas disponíveis carregada no modal de criação/edição de agente.
- [x] Persistência da associação em tabela junction `AgentKnowledgeRoomAssignment`.
- [x] O `KnowledgeSpecialist` filtra a busca vetorial automaticamente pelas salas associadas ao agente.

---

#### US-42 — Dashboard de FinOps e Previsão de Custos

**Como** gestor financeiro,
**quero** visualizar o consumo detalhado de tokens e custos por tenant/agente,
**para que** eu possa prever gastos e ajustar quotas proativamente.

| Item | Detalhe |
|------|---------|
| Componente | `FinOpsPage` (rota `/admin/finops`) |
| API | `GET /api/admin/gateway/metrics/finops` |
| Status | ⏳ Planejado (ADR-008) |

**Critérios de Aceite:**
- [ ] Gráficos de barra: Consumo por Provider (OpenAI, Gemini, Claude).
- [ ] Tabela de Top-Agents por custo.
- [ ] Alertas visuais quando um tenant atinge 80% da quota.

---

#### US-43 — Publicar Agentes via A2A/AgUI

**Como** desenvolvedor de ecossistema,
**quero** expor meus agentes internos via protocolos padronizados,
**para que** eles possam ser consumidos por sistemas externos (ex: Copilot Studio).

| Item | Detalhe |
|------|---------|
| Componente | `ProtocolsPage` |
| API | `GET /a2a` · `GET /agui` |
| Status | ⏳ Planejado (ADR-020) |

**Critérios de Aceite:**
- [ ] Flag "Publicly Exportable" na configuração do agente.
- [ ] Endpoint `/agui` retorna manifesto JSON válido do protocolo.
- [ ] Logs de auditoria mostram chamadas originadas via protocolo.

---

#### US-44 — Executar Bateria de Avaliação de Qualidade

**Como** arquiteto de prompts,
**quero** rodar um Golden Set contra um agente após mudanças no sistema,
**para que** eu valide scores de Relevância e Grounding (Grounding).

| Item | Detalhe |
|------|---------|
| Componente | `EvaluationPage` |
| Engine | `Microsoft.Extensions.AI.Evaluation` |
| Status | 🚧 CRUD backend implementado; interface e métricas pendentes (ADR-032) |

**Critérios de Aceite:**
- [ ] Upload/Edição de Golden Sets (Query vs Expected) na interface.
- [ ] Relatório de comparação entre versões do agente.
- [ ] Scores automáticos (0-1) para Grounding e Fluência.

O backend oferece CRUD e execução de Golden Sets via REST. Esses endpoints não concluem, por si só, os critérios da interface e das métricas acima.

---

### Épico 10: Dynamic ONNX In-Process Inference Engine (Roadmap Q2 2026)

#### US-45 — Upload e Gerenciamento Dinâmico de Modelos ONNX

**Como** administrador do sistema,  
**quero** fazer upload e configurar modelos ONNX pela interface web,  
**para que** novas capacidades de IA local sejam incorporadas sem a necessidade de novos deploys de código C#.

| Item | Detalhe |
|------|---------|
| Componente | `OnnxModelsPage` · `OnnxModelUploadModal` · `OnnxModelInspectModal` · `OnnxModelTestModal` |
| API | `GET/POST/PUT/DELETE /api/onnx/models` · `POST /api/onnx/models/{id}/inspect` · `POST /api/onnx/models/{id}/test` |
| Status | ✅ Implementado (ADR-010) |

**Critérios de Aceite:**
- [x] Interface de upload aceita o arquivo `.onnx` principal e opcionalmente o arquivo secundário de pesos (`.data` / `.bin`) para modelos split.
- [x] Formulário de upload com validações para metadados de inferência (Input/Output Nodes, Width, Height, Channels, Scale Factor, Mean R/G/B, Output Format).
- [x] Exibição de aviso visual claro e progresso de upload caso a soma dos arquivos exceda 50MB, indicando salvamento físico em disco.
- [x] Rota de deleção física e lógica que limpa registros no PostgreSQL e diretórios físicos correspondentes no disco.
- [x] Interface de testes rápidos (`TestModal`) que permite upload de imagem de teste local e exibe o resultado da inferência lado a lado com métricas de latência e shape.

---

#### US-46 — Execução Genérica via DynamicOnnxProcessorTool (ITool)

**Como** construtor de workflows,  
**quero** utilizar uma tool genérica do processador ONNX como bloco em meu fluxo,  
**para que** eu possa aplicar inferências de IA em dados de imagem encadeados de forma transparente.

| Item | Detalhe |
|------|---------|
| Componente | `DynamicOnnxProcessorTool` (`ITool`) · `WorkflowBuilder.tsx` (Properties Panel) |
| API | SignalR `hubs/chat` · REST execution APIs |
| Status | ✅ Implementado (ADR-010) |

**Critérios de Aceite:**
- [x] Registro correto da tool `onnx_processor` no `IToolManager` com a categoria `AI`.
- [x] Properties Panel do Workflow Builder exibe dropdown populado dinamicamente com os modelos ONNX ativos ao selecionar o nó `onnx_processor`.
- [x] A execução do processador decodifica a imagem base64 de entrada, realiza o pré-processamento de canais/normalização, cria a `InferenceSession`, executa a inferência e pós-processa o output de volta para base64.
- [x] Tratamento de erros gracioso: falhas internas do runtime ONNX retornam uma descrição legível de erro no `ToolResult` em vez de crashar a thread.

---

#### US-47 — Isolamento Multi-Tenant e Segurança Físico-Lógica dos Modelos ONNX

**Como** cliente/tenant da plataforma,  
**quero** garantia absoluta de que meus modelos ONNX e arquivos de pesos carregados estão isolados física e logicamente,  
**para que** meus ativos intelectuais e de dados nunca vazem para outros tenants.

| Item | Detalhe |
|------|---------|
| Componente | `TenantMiddleware` · `AgenticDbContext` · `OnnxModelController` |
| Segurança | Isolamento Físico e Lógico (T5, T7) |
| Status | ✅ Implementado (ADR-010) |

**Critérios de Aceite:**
- [x] Aplicação de filtro global EF Core (`TenantId`) na entidade `CustomOnnxModelEntity`.
- [x] Modelos armazenados fisicamente são salvos estritamente sob a estrutura `wwwroot/onnx-models/{tenantId}/{modelId}/` com nomes originais preservados.
- [x] Resolução de arquivos secundários (`.data` / `.bin`) via path absoluto restrita estritamente ao diretório do respectivo `tenantId`, bloqueando acessos transversais de diretório (Directory Traversal).
- [x] Validação no `DeleteModel` para impedir que um tenant delete arquivos pertencentes a outro através da manipulação do `modelId`.

---

### Épico 11: Dynamic Customization & No-Code Orchestration (Future Roadmap)

#### US-48 — No-Code Skills (Dynamic Custom Skills via UI)

**Como** construtor de agentes ou administrador do sistema,  
**quero** criar, persistir de forma relacional e fiar dinamicamente Skills personalizadas diretamente pela interface de usuário (sem precisar codificar C#),  
**para que** eu possa estender o comportamento dos agentes rapidamente usando instruções declarativas, parâmetros de inputs/outputs e prompts estruturados.

| Item | Detalhe |
|------|---------|
| Componente | `CustomSkillsPage` · `SkillCreatorWizard` |
| API / Serviço | `ICustomSkillManager` · `GET/POST/PUT/DELETE /api/skills/custom` |
| Status | ⏳ Planejado (Future Roadmap) |

**Critérios de Aceite:**
- [ ] Interface visual para criação de Skills (Nome, Descrição, System Prompt/Instruções e Variáveis de Entrada/Saída).
- [ ] Persistência relacional em banco de dados das custom skills com isolamento multi-tenant (`TenantId`).
- [ ] Associação dinâmica a agentes existentes com fiação em tempo real (runtime reflection).
- [ ] Validação de schema e tipos das variáveis de entrada/saída declaradas.
- [ ] Suporte a importação/exportação de definições de Skills em formato YAML/JSON.

---

#### US-49 — Agent Constructor (Visual Agent Builder)

**Como** administrador do sistema,  
**quero** uma interface visual de construção de agentes que me permita arrastar ou selecionar via checkboxes as capabilities, tools, salas de RAG e skills de forma dinâmica,  
**para que** novos agentes especializados possam ser montados em minutos sem qualquer deploy de código.

| Item | Detalhe |
|------|---------|
| Componente | `AgentConstructorPage` · `AgentBuilderCanvas` |
| API / Serviço | `IDynamicAgentFactory` · `PUT /api/agent/agents/{name}/wire` |
| Status | ⏳ Planejado (Future Roadmap) |

**Critérios de Aceite:**
- [ ] Form Wizard visual premium com etapas claras para definição do perfil do Agente (Nome, Avatar, Modelo de LLM, Temperatura, Max Tokens).
- [ ] Painel de Checkboxes / Multi-select interativo para Capabilities (Web Search, File Search, Advanced Math).
- [ ] Painel para fiação de Tools de infraestrutura e plugins MCP registrados.
- [ ] Seletor de Knowledge Rooms autorizadas para o agente (RAG).
- [ ] Seletor de Custom Skills criadas declarativamente pela interface.
- [ ] Visualização ao vivo do "Prompt Consolidado" resultante e testes rápidos integrados antes de salvar.

---

#### US-50 — Auto-Triage Pipeline (Semantic Router & Ingest Pipeline)

**Como** arquiteto do sistema agêntico,  
**quero** um classificador semântico em background que avalie e roteie de forma inteligente uploads de arquivos e mensagens no chat entre RAG (Knowledge Rooms) e Memória Episódica (Histórico/Conhecimento Pessoal do Usuário),  
**para que** o armazenamento seja otimizado e a recuperação de contexto seja extremamente relevante e rápida.

| Item | Detalhe |
|------|---------|
| Componente | Background Ingest Monitor |
| API / Serviço | `IAutoTriageService` · `SemanticTriageWorker` (Background Service) |
| Status | ⏳ Planejado (Future Roadmap) |

**Critérios de Aceite:**
- [ ] Pipeline assíncrono em background (HostedService ou Worker) ativado após uploads ou interações significativas.
- [ ] Classificador semântico que determina a natureza do dado (ex: manual/documento estático -> RAG Room; decisão/fato pessoal -> Memória Episódica).
- [ ] Execução assíncrona em background que não bloqueia a interface do usuário nem o envio inicial de mensagens.
- [ ] Mecanismo de re-indexação inteligente que move chunks stale ou consolidados entre as camadas de memória.
- [ ] Painel de monitoramento visual do pipeline de triagem com status do routing e estatísticas de destinação.

---

### Épico 12: Multi-Provider LLM Sychronization & Integrity (Issue #94)

#### US-51 — Inspeção Automática de Modelos LLM no Login

**Como** usuário autenticado do sistema,  
**quero** que a plataforma execute automaticamente em background a inspeção e descoberta de modelos LLM das chaves ativas associadas ao meu tenant,  
**para que** a lista de modelos disponíveis na interface esteja sempre atualizada com as capacidades reais de cada provedor no momento do acesso.

| Item | Detalhe |
|------|---------|
| Componente | `AuthController` (Backend Trigger) · `LlmCatalogUpdated` (SignalR Hub Notification) |
| API / Serviço | `ILLMAdministrationService` · `ILLMProviderApiKeyService` · `IHubContext<ChatHub>` / `IHubContext<GatewayHub>` |
| Status | ⏳ Planejado (ADR-021, Issue #94) |

**Critérios de Aceite:**
- [ ] O serviço `ILLMProviderApiKeyService` deve ser registrado no DI em `ServiceCollectionExtensions.cs` como Scoped.
- [ ] O controller `AuthController.Login` deve injetar `IServiceScopeFactory` e disparar a descoberta em segundo plano via `Task.Run` sem bloquear o login HTTP.
- [ ] A varredura de chaves armazenadas em banco deve ser restrita apenas ao **tenant do usuário logado**, mantendo o isolamento de dados entre os inquilinos.
- [ ] As chaves ativas de infraestrutura global em `AgenticSystemSettings` devem ser inspecionadas se seus respectivos provedores estiverem ativos.
- [ ] Notificar erros e falhas nas chamadas a APIs de LLM externas de forma isolada nos logs do Serilog, impedindo que a falha de um provedor afete os demais.
- [ ] Disparar um evento SignalR `LlmCatalogUpdated` direcionado ao grupo do tenant no sucesso da varredura, notificando o frontend para atualizar o catálogo de modelos disponíveis dinamicamente em tempo real.

---

### Épico 13: Resilient Workflows & Durable Orchestration

#### US-52 — Execução recuperável de workflows dinâmicos multi-tenant

**Rastreabilidade:** [Issue original #108 (fechada)](https://github.com/JonathanBenicio/Agent-System/issues/108) · reavaliação do backend em [#120](https://github.com/JonathanBenicio/Agent-System/issues/120) · [ADR-036](architecture/adr/036-maf-122-protocols-and-gateway.md) · [plano](plan/maf-122-protocols-gateway.md).

**Como** usuário de uma plataforma de agentes personalizáveis,<br>
**quero** executar definições de workflow do meu tenant, acompanhar o mesmo ID até o resultado e retomar execuções após falha do worker,<br>
**para que** automações dinâmicas preservem estado, autorização e efeitos rastreáveis sem depender de um grafo global estático.

| Item | Detalhe |
|------|---------|
| Runtime | `IWorkflowEngine`/`IWorkflowStore` como orquestrador da aplicação; MAF 1.22 como runtime de agentes e ferramentas |
| Persistência | PostgreSQL com versão/hash imutável da definição, execução, etapas, aprovação/espera e lease de worker |
| Status | Engine dinâmico canônico implementado: ID/status, snapshot/hash, approval/reject, Agent, Wait persistido, retries, RBAC de tool e lease/recovery. Wait concluiu após encerramento forçado antes do prazo. Banner compartilha start/status com o store e gerou arquivo final com client determinístico/skills reais; modelos vision/editor não foram exercitados. Handler externo precisa deduplicar a chave. Suíte 755 aprovados/1 skip. |

O Issue #108 exigia especificamente `Microsoft.Agents.AI.DurableTask`. Essa escolha foi substituída na análise atual: a extensão agenda por nome com registry criado no startup, enquanto as definições do produto são tenant/request-specific e a API atual consulta `IWorkflowStore`. O valor de produto continua; o mecanismo não é requisito.

**Critérios de aceite:**
- [x] O start cria um execution ID persistido e o mesmo ID serve à consulta/cancelamento/eventos; PostgreSQL validou leitura negada por outro tenant.
- [x] Cada execução fixa versão/hash e snapshot imutável; editar a definição viva não altera a retomada.
- [x] Claims concorrentes, fencing e recovery após lease expirado foram testados no PostgreSQL; Wait persiste prazo e retoma após recriar engine/store. Restart abrupto de API no meio de efeito externo continua follow-up.
- [x] Etapas Agent/Action executam; Action exige `Permission.Execute`, Approval restringe papéis, Wait retoma no prazo e Subworkflow falha explicitamente quando não suportado.
- [x] `MaxRetries` é aplicado e a tool recebe chave idempotente estável por execução/etapa; handler externo precisa deduplicar. Semântica at-least-once, sem exactly-once.
- [x] Testes PostgreSQL no Compose isolado cobrem claims concorrentes, lease expirado, Wait após reinício real, start/status de Banner, aprovação, isolamento e fencing. Efeito externo interrompido e imagem final de Banner seguem abertos.
## BACK-CHAT-123 — Chat, sessões e configurações efetivamente usadas

**Issue:** [#123](https://github.com/JonathanBenicio/Agent-System/issues/123) · [ADR-039](architecture/adr/039-chat-session-user-tenant-settings.md) · [Plano](plan/chat-session-user-settings.md).

**Como** membro de um tenant, **quero** conversar, retomar minhas sessões e selecionar configurações permitidas, **para que** meu histórico e minhas escolhas sejam preservados e realmente governem a próxima resposta. Owner/Admin pode gerir chaves BYOK e ativação das skills do tenant.

- [x] Chat REST/SignalR devolve conteúdo, erro e `sessionId`; Cypress escolheu o agente direto e persistiu provider/modelo que o provider local recebeu.
- [x] Sessões são criadas, listadas, abertas, retomadas e encerradas; Cypress recarregou o browser e abriu o mesmo histórico; usuário/tenant cruzados foram negados.
- [x] Chave BYOK cadastrada, atualizada, validada no endpoint de modelos configurado, removida; DTOs não expuseram segredo; atualização/default foi confirmada no header do chat; Viewer recebeu 403.
- [x] Catálogo e preferência provider/modelo por usuário/tenant foram salvos e a próxima execução recebeu a seleção efetiva; modelos de chave default descobertos integram as opções.
- [x] Skills do tenant listadas e alternadas por Owner/Admin; a instrução ativa chegou ao provider e não chegou a uma sessão nova após desativar; tools continuam sujeitas a ACL própria.
- [x] Frontend, API e PostgreSQL comprovaram em separado configuração persistida e usada, browser reload/session resume e isolamento. [Relatório](backend/validation/chat-session-settings-2026-09-29.md).

**Validação:** build Release; suíte 763 aprovados/1 skip; Cypress 1/1, incluindo agente direto, provider/modelo, sessão e telas de chave/skills. ESLint global mantém 22 erros e 1 warning em arquivos fora da história; os arquivos alterados passam lint. Issue segue aberta até resolver ou separar o gate global.

