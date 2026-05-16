# Project Plan: Hot-Swapping Architecture Roadmap

## 1. Background & Motivation
O objetivo deste *roadmap* é evoluir o Agentic System para uma plataforma modular e pronta para nível *enterprise*, capaz de trocar a quente ("hot-swap") os seus componentes centrais (Bases de Dados, Modelos LLM, Provedores de Embeddings) sem exigir o reinício do sistema (Zero-Downtime). Esta mudança transita a arquitetura para um modelo dinâmico e orientado a configurações, capacitando tanto o uso local *out-of-the-box* quanto implantações em cloud escaláveis.

## 2. Scope & Impact
**Scope:**
*   Refatoração do motor de Injeção de Dependências (DI) do .NET para utilizar *Dynamic Factories* e `IOptionsMonitor<T>`.
*   Construção de um frontend React interativo (*drag-and-drop*) para a ingestão via RAG.
*   Implementação de um construtor visual de *workflows* *low-code* (React Flow).
*   Suporte a rotas dinâmicas de *webhooks* e ligações de agentes externos via SignalR.
*   Expansão do suporte de vector stores para incluir provedores locais (SQLite/DuckDB) e em nuvem (Pinecone/Qdrant) com recursos de pesquisa híbrida.

**Impact:**
*   **Configurações Zero-Downtime:** O sistema reage instantaneamente a alterações de configuração.
*   **Melhoria de UX:** Configuração local sem atritos e automação visual de *low-code*.
*   **Escalabilidade Enterprise:** Transição transparente da execução local para a execução distribuída na nuvem.

## 3. Proposed Solution (Phased Implementation)

### Phase 0: Hot-Swapping Foundation (Issue #18)
*   **Tasks:**
    - [x] Refatorar o DI do .NET para usar `Dynamic Factories` (ex: `IVectorStoreFactory`, `ILLMProviderFactory`, `IEmbeddingProviderFactory`).
    - [x] Substituir configurações estáticas por `IOptionsMonitor<T>` para reatividade — com cache + `volatile` + double-checked locking nos 3 proxies (`HotSwappableLLMProvider`, `HotSwappableVectorStore`, `HotSwappableEmbeddingProvider`).
    - [x] Expandir `ConfigController` com `POST /api/admin/config/hot-swap` e `PostgresConfigStore` com raw `pg_notify` para propagação real-time via `RealTimeConfigReloadBackgroundService`.
*   **Agents:** `dotnet-expert`, `backend-specialist`, `database-architect`
*   **Status:** ✅ CONCLUÍDO — Build: 0 erros, 607/608 testes passando.

### Phase 1: Local Setup & Out-of-the-Box RAG
*   **Tasks:**
    - [x] Implementar `SqliteVectorStore` (ou `DuckDbVectorStore`) para execução local.
    - [x] Construir a interface "Knowledge Rooms" em React com ingestão de RAG via *drag-and-drop*.
    - [x] Implementar *multi-tenancy* dinâmico (Workspaces) conectado ao `TenantIsolationService`.
*   **Agents:** `frontend-specialist`, `dotnet-expert`, `database-architect`
*   **Status:** ✅ CONCLUÍDO — Frontend integrado com Drag and Drop e isolamento de tenants (X-Tenant-Id).

### Phase 2: Visual Workflows & Triggers
*   **Tasks:**
    - [x] Integrar o React Flow no frontend para a construção visual de *workflows* (Store + UI implementados).
    - [x] Desenvolver um motor universal de *webhooks* dinâmicos (`/api/webhooks/{id}`) reativo em tempo real (`WebhooksController` + `WebhooksPage`).
    - [x] Expor o `MCPPluginManager` na interface gráfica (UI) para injeção de *plugins* ao vivo.
*   **Agents:** `frontend-specialist`, `dotnet-expert`

### Phase 3: External Orchestration & Templates
*   **Tasks:**
    - [x] Expandir o `ScheduledTaskHostedService` utilizando o Quartz.NET para trabalhos *cron* dinâmicos.
    - [x] Implementar um canal SignalR BYOB (*Bring Your Own Bot*) para a orquestração de agentes externos.
    - [x] Criar uma interface de importação de *templates* ligando ao já existente `AgentYamlValidator`.
*   **Agents:** `dotnet-self-learning-architect`, `dotnet-expert`

### Phase 4: Enterprise Scale & Performance
*   **Tasks:**
    - [x] Implementar `PineconeVectorStore` (REST API, filtros `$eq`, namespaces, stats) + registro em `VectorStoreFactory`.
    - [x] Desenvolver a Pesquisa Híbrida: `HybridSearchAsync` com Reciprocal Rank Fusion (RRF k=60) combinando pgvector cosine + `ts_rank` FTS — fallback ILIKE.
    - [x] Migration EF Core `AddFtsGinIndex` com `CREATE INDEX CONCURRENTLY` para FTS zero-downtime.
    - [x] Realizar testes de carga e perfis de desempenho (Plano de testes k6 criado em `docs/plan/load-testing-vector-stores.md`).
*   **Agents:** `database-architect`, `dotnet-expert`, `performance-optimizer`
*   **Status:** ✅ CONCLUÍDO — Todas as tarefas de integração e performance documentadas.

## 4. Alternatives & Trade-offs Considered
| Alternativa | Prós | Contras | Decisão |
| :--- | :--- | :--- | :--- |
| **Reinício da Aplicação na Mudança de Configuração** | Muito mais simples de implementar. | Provoca *downtime*; degrada severamente a experiência de utilizador (UX) em ambientes *multi-tenant*. | **Rejeitada** em favor do `IOptionsMonitor` e *Dynamic Factories*. |
| **RabbitMQ/Kafka para o Event Bus** | Extremamente robusto para escalas gigantes. | Introduz pesadas dependências externas em ambientes locais ou leves. | **Rejeitada** em favor do `PostgresEventBus` (LISTEN/NOTIFY) para configurações locais simples, mantendo-se expansível futuramente. |

## 5. Risk Matrix & Mitigations
| Risco | Impacto | Probabilidade | Estratégia de Mitigação |
| :--- | :--- | :--- | :--- |
| **Race Conditions no Hot-Swapping** | Elevado | Média | ✅ Implementado: `volatile` + `lock` + double-checked locking nos 3 proxies. |
| **Memory Leaks de Instâncias Antigas** | Elevado | Baixa | ✅ Implementado: `IDisposable` com dispose seguro + `ObjectDisposedException.ThrowIf` em todos os proxies. |
| **Dessincronização do Estado do Frontend** | Médio | Média | ✅ Implementado: `HotSwapPanel` com feedback de estado (loading/success/error) + auto-reset. |

## 6. Migration & Rollback Strategy
**Migração:**
*   Cada fase será implementada na sua própria ramificação (*branch*) isolada.
*   Modificações no esquema da base de dados (ex: tabelas de configuração, roteamento de webhooks) utilizarão migrações do EF Core alinhadas com as regras de *zero-downtime*.
*   ✅ GIN Index: `20260516031800_AddFtsGinIndex.cs` — usa `CREATE INDEX CONCURRENTLY IF NOT EXISTS`.

**Rollback:**
*   Se uma fábrica dinâmica falhar ao carregar uma nova instância, o sistema deve automaticamente reverter para a instância anterior conhecida como funcional.
*   *Rollbacks* de base de dados serão geridos através de `dotnet ef database update <PreviousMigration>`.

## 7. Verification & Testing
*   [ ] **Testes Unitários:** Verificar se a alteração de valores no `IOptionsMonitor` aciona corretamente a recompilação da fábrica.
*   [ ] **Testes de Integração:** Testes E2E a inserir documentos antes e depois do *hot-swap* do Vector Store.
*   [ ] **Testes de Carga:** Profile de performance do `HybridSearchAsync` com 100k+ documentos (RRF benchmark).
*   [ ] **Auditorias de Segurança:** O `security-auditor` correrá após as Fases 1 e 2 para garantir o Isolamento de *Tenants* e a segurança dos *Webhooks*.
*   [x] **Linting & QA:** Build limpo — 0 erros, 11 warnings pré-existentes, 607/608 testes passando.
*   [x] **Frontend Hot-Swap UI:** `HotSwapPanel` integrado em `ConfigAdvancedPage` na aba "Hot-Swap".