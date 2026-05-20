# Project Plan: Session Chat History & Long-Term Memory

## 1. Background & Motivation

O sistema atual **já possui** infraestrutura de sessão e memória implementada, mas com gaps de persistência e integração frontend. O `ChatHub` transmite eventos em tempo real via SignalR, o `SessionManager` gerencia o ciclo de vida, e o `SessionConsolidator` extrai insights via LLM.

**Problema real:** O frontend não carrega histórico de sessões anteriores, os resumos do consolidator ficam apenas em memória (`ConcurrentBag<SessionSummary>`), e não há API para listar/recuperar sessões persistidas.

Este plano aborda a **evolução** da infraestrutura existente, não uma reescrita:

1. **Histórico de Chat da Sessão (Foco em UX)** — Conectar frontend ao backend existente
2. **Memória de Longo Prazo do Agente (Foco em Capacidade)** — Persistir resumos e injetar contexto

---

## 2. Current State Analysis

### O que já existe e funciona

| Componente | Arquivo | Status |
|------------|---------|--------|
| **SignalR ChatHub** | `src/AgenticSystem.Api/Hubs/ChatHub.cs` | ✅ Funcionando |
| **SessionData model** | `src/AgenticSystem.Core/Models/SessionData.cs` | ✅ Com Events, Summary, Insights |
| **SessionManager** | `src/AgenticSystem.Core/Services/SessionManager.cs` | ✅ Gerencia ciclo de vida |
| **SessionConsolidator** | `src/AgenticSystem.Core/Services/SessionConsolidator.cs` | ✅ Sumariza + extrai insights |
| **ISessionStore** | `src/AgenticSystem.Core/Interfaces/ISessionStore.cs` | ✅ Interface com GetByUserAsync |
| **PostgresSessionStore** | `src/AgenticSystem.Infrastructure/Persistence/PostgresSessionStore.cs` | ✅ Salva JSONB na tabela `sessions` |
| **EfAgentMemoryStore** | `src/AgenticSystem.Infrastructure/Persistence/EfAgentMemoryStore.cs` | ✅ Memória por agente |
| **PostgresMemoryLifecycleStore** | `src/AgenticSystem.Infrastructure/Persistence/PostgresMemoryLifecycleStore.cs` | ✅ Enhanced memories |
| **PostgresVectorStore** | `src/AgenticSystem.Infrastructure/Persistence/PostgresVectorStore.cs` | ✅ pgvector + hybrid search |
| **Tabela `sessions`** | Migration InitialCreate | ✅ Com JSONB (`data`) |
| **Tabela `agent_memories`** | Migration InitialCreate | ✅ |
| **Tabela `enhanced_memories`** | Migration InitialCreate | ✅ |
| **Tabela `vector_documents`** | Migration InitialCreate + AddFtsGinIndex | ✅ Com pgvector + FTS |

### Gaps identificados

| Gap | Descrição | Severidade |
|-----|-----------|------------|
| **GAP-1** | `SessionConsolidator._summaries` é `ConcurrentBag` (in-memory) — perde ao reiniciar | 🔴 Alta |
| **GAP-2** | `ChatPage.tsx` não carrega histórico de sessões existentes | 🔴 Alta |
| **GAP-3** | `ChatSession` type definido em `chat.ts` mas não usado | 🟡 Média |
| **GAP-4** | Nenhuma API REST para listar/recuperar sessões por usuário | 🔴 Alta |
| **GAP-5** | `SessionSummary` e `SessionInsights` não persistidos no DB | 🟡 Média |
| **GAP-6** | Sem sidebar de sessões no frontend | 🟡 Média |
| **GAP-7** | Insights extraídos não são injetados como contexto em novas sessões | 🟠 Média-Alta |

---

## 3. Proposed Features & Architecture Changes

### Feature 1: Persistência de Sessão e Mensagens (Caso 1)

**Objetivo:** Garantir que o chat não seja perdido e seja acessível.

**Estratégia:** Evoluir a infraestrutura existente, não recriar.

#### Fase 1A: Backend — API de Sessões (2-3h)

**Arquivos afetados:**
- `src/AgenticSystem.Api/Controllers/SessionController.cs` (novo)
- `src/AgenticSystem.Core/Interfaces/ISessionStore.cs` (já existe — apenas usar)
- `src/AgenticSystem.Api/Hubs/ChatHub.cs` (pequeno ajuste para retornar sessionId)

**Endpoints a criar:**
```
GET    /api/sessions                    — Lista sessões do usuário (usa ISessionStore.GetByUserAsync)
GET    /api/sessions/{id}               — Detalhes de uma sessão (usa ISessionStore.GetAsync)
GET    /api/sessions/{id}/messages      — Extrai mensagens do JSONB (converte AgentEvent[] → ChatMessage[])
DELETE /api/sessions/{id}               — Deleta sessão (usa ISessionStore.DeleteAsync)
PUT    /api/sessions/{id}/title         — Renomeia sessão (atualiza RuntimeSettings["title"])
```

**DTOs necessários:**
```csharp
// src/AgenticSystem.Core/Models/SessionDtos.cs (novo)
public record SessionListItemDto(string Id, string Title, DateTime LastActivity, int MessageCount, string? Summary);
public record SessionDetailDto(string Id, string Title, DateTime StartedAt, DateTime? EndedAt, List<ChatMessageDto> Messages, SessionSummaryDto? Summary, SessionInsightsDto? Insights);
public record ChatMessageDto(string Id, string Role, string Content, string? AgentName, int? AgentTier, List<string>? Actions, List<string>? Tools, bool? Success, DateTime Timestamp);
public record SessionSummaryDto(string Summary, List<string> TopicsDiscussed, List<string> AgentsUsed, int EventCount);
public record SessionInsightsDto(List<string> Facts, List<string> Decisions, List<string> Preferences, List<string> ActionItems);
```

#### Fase 1B: Frontend — Sidebar de Sessões (3-4h)

**Arquivos afetados:**
- `frontend/src/hooks/useSessions.ts` (novo — react-query hook)
- `frontend/src/components/chat/SessionSidebar.tsx` (novo)
- `frontend/src/components/chat/ChatPage.tsx` (modificar — adicionar sidebar)
- `frontend/src/lib/api.ts` (adicionar `sessionApi` namespace)
- `frontend/src/types/chat.ts` (já tem `ChatSession` — reutilizar)

**Componente `SessionSidebar.tsx`:**
- Lista de sessões do usuário (busca via `GET /api/sessions`)
- Clique em sessão carrega histórico (`GET /api/sessions/{id}/messages`)
- Botão "Nova Conversa" limpa mensagens e cria nova sessão
- Renomear sessão (duplo clique ou botão de editar)
- Deletar sessão (com confirmação)

**Hook `useSessions.ts`:**
```typescript
// Pseudocode
const useSessions = () => {
  const { data: sessions, isLoading } = useQuery({ queryKey: ['sessions'], queryFn: sessionApi.list })
  const loadSession = async (id: string) => { ... }
  const renameSession = async (id: string, title: string) => { ... }
  const deleteSession = async (id: string) => { ... }
  const createNewSession = () => { ... }
}
```

#### Fase 1C: ChatHub — Persistir SessionId no Contexto (1h)

**Arquivo:** `src/AgenticSystem.Api/Hubs/ChatHub.cs`

- O `ChatHub` já recebe `sessionId` dos eventos `SessionCompleted`
- Garantir que o `sessionId` seja retornado no primeiro evento para o frontend poder associar
- Adicionar método `JoinSession(sessionId)` no hub para carregar histórico existente

---

### Feature 2: Memória de Longo Prazo e Sumarização (Caso 2)

**Objetivo:** Permitir que o agente lembre de fatos importantes de conversas passadas.

**Estratégia:** Persistir o que já existe (`SessionConsolidator`) e injetar contexto.

#### Fase 2A: Persistir SessionConsolidator Summaries (2-3h)

**Arquivos afetados:**
- `src/AgenticSystem.Core/Interfaces/ISessionConsolidator.cs` (adicionar método)
- `src/AgenticSystem.Core/Services/SessionConsolidator.cs` (modificar)
- `src/AgenticSystem.Infrastructure/Persistence/Configurations/PersistenceConfigurations.cs` (nova config)
- `src/AgenticSystem.Infrastructure/Persistence/Entities/PersistenceEntities.cs` (nova entidade)
- Nova migration EF Core

**Nova entidade:**
```csharp
// src/AgenticSystem.Infrastructure/Persistence/Entities/PersistenceEntities.cs
public class SessionSummaryEntity
{
    public Guid Id { get; set; }
    public string SessionId { get; set; }
    public string UserId { get; set; }
    public string TenantId { get; set; }
    public string Summary { get; set; }
    public string TopicsJson { get; set; }
    public string AgentsJson { get; set; }
    public int EventCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public TimeSpan? SessionDuration { get; set; }
}

public class SessionInsightEntity
{
    public Guid Id { get; set; }
    public string SessionId { get; set; }
    public string UserId { get; set; }
    public string TenantId { get; set; }
    public string FactsJson { get; set; }
    public string DecisionsJson { get; set; }
    public string PreferencesJson { get; set; }
    public string ActionItemsJson { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

**Mudança no `SessionConsolidator`:**
- Adicionar `ISessionSummaryStore` como dependência
- Após `SummarizeSessionAsync`, persistir summary no DB
- Após `ExtractInsightsAsync`, persistir insights no DB
- `GetRelevantSummariesAsync` deve buscar do DB, não do `ConcurrentBag`

#### Fase 2B: Vetorizar Insights para Recuperação RAG (3-4h)

**Arquivos afetados:**
- `src/AgenticSystem.Core/Services/MemoryInjectionService.cs` (novo)
- `src/AgenticSystem.Infrastructure/Persistence/PostgresVectorStore.cs` (já suporta upsert)
- `src/AgenticSystem.Core/Services/SessionManager.cs` (modificar — injetar contexto no início)

**Fluxo:**
1. Após `ExtractInsightsAsync`, converter `SessionInsights` em `VectorDocumentEntity`
2. Cada insight (fact, decision, preference, action item) vira um documento vetorial
3. Metadata: `{ type: "memory", userId, sessionId, insightType, createdAt }`
4. No início de uma nova sessão, `SessionManager` busca memórias relevantes via `IVectorStore.SearchWithFiltersAsync`
5. Injeta memórias no `systemPrompt` como contexto adicional

**Prompt injection example:**
```
RELEVANT CONTEXT FROM PREVIOUS INTERACTIONS:
- Facts: [user prefers Python, working on project X]
- Decisions: [chose PostgreSQL for vector storage]
- Preferences: [likes concise code, no comments]
```

#### Fase 2C: Background Job para Consolidação Automática (2-3h)

**Arquivos afetados:**
- `src/AgenticSystem.Core/Services/SessionAutoConsolidator.cs` (novo)
- `src/AgenticSystem.Api/Program.cs` (registrar hosted service)

**Lógica:**
- `BackgroundService` que roda a cada N minutos
- Busca sessões com `EndedAt != null` e `IsConsolidated == false`
- Chama `SessionConsolidator.SummarizeSessionAsync` + `ExtractInsightsAsync`
- Persiste resultados
- Marca sessão como consolidada

---

## 4. Prioritization Matrix

| Fase | Feature | Esforço | Impacto | Prioridade | Dependências |
|------|---------|---------|---------|------------|--------------|
| **1A** | Backend API de Sessões | 2-3h | Alto | 🔴 P0 | Nenhuma |
| **1B** | Frontend Sidebar de Sessões | 3-4h | Alto | 🔴 P0 | 1A |
| **1C** | ChatHub SessionId Context | 1h | Médio | 🟡 P1 | Nenhuma |
| **2A** | Persistir Summaries | 2-3h | Alto | 🔴 P0 | Nenhuma |
| **2B** | Vetorizar Insights (RAG) | 3-4h | Alto | 🟠 P1 | 2A |
| **2C** | Auto-Consolidação Background | 2-3h | Médio | 🟡 P1 | 2A |

**Estimativa total:** 13-20 horas

---

## 5. Architecture Decision: JSONB vs Tabelas Relacionais

### Contexto Atual
A tabela `sessions` armazena `SessionData` como JSONB na coluna `data`. Isso inclui todos os `AgentEvent` objects (mensagens do chat).

### Decisão: Manter JSONB + Adicionar APIs de Extração

**Razões:**
1. **Simplicidade** — Não requer migração de dados existente
2. **Performance** — JSONB no PostgreSQL é otimizado para queries
3. **Flexibilidade** — `AgentEvent` pode evoluir sem alterar schema
4. **Já funciona** — `PostgresSessionStore` já salva/recupera corretamente

**Quando reconsiderar:**
- Se precisar de busca full-text em mensagens individuais
- Se o JSONB crescer > 10MB por sessão
- Se precisar de queries complexas cruzando mensagens de sessões diferentes

**Mitigação:** As APIs de extração (`GET /api/sessions/{id}/messages`) convertem JSONB → DTOs relacionais para o frontend.

---

## 6. Implementation Order

```
1A → 1B → 1C  (Feature 1: Histórico de Chat)
  ↓
2A → 2B → 2C  (Feature 2: Memória de Longo Prazo)
```

**Sprint 1 (1 semana):** 1A + 1B + 1C — Histórico de chat funcional
**Sprint 2 (1 semana):** 2A + 2B + 2C — Memória persistente e inteligente

---

## 7. Testing Strategy

### Backend
- Unit tests para `SessionController` endpoints
- Integration tests para `ISessionStore` com PostgreSQL real
- Test de consolidação com mock `IChatClient`

### Frontend
- E2E test: criar sessão → enviar mensagem → recarregar → histórico persiste
- E2E test: listar sessões → clicar → carrega histórico
- Unit test: `useSessions` hook com mock API

### Performance
- Testar carga de `GET /api/sessions` com 1000+ sessões
- Testar extração de JSONB com 500+ eventos por sessão

---

## 8. Risks & Mitigations

| Risco | Probabilidade | Impacto | Mitigação |
|-------|--------------|---------|-----------|
| JSONB muito grande (>10MB) | Baixa | Alto | Implementar paginação de eventos |
| LLM de consolidação falhar | Média | Médio | Fallback summary já implementado |
| Frontend sidebar complexa | Média | Médio | Usar componentes existentes (Badge, etc.) |
| Conflito com multi-tenancy | Baixa | Alto | Seguir padrão `ITenantEntity` |

---

## 9. Success Criteria

- [x] Usuário abre chat → vê lista de sessões anteriores
- [x] Clique em sessão → carrega histórico completo
- [x] Nova mensagem → persistida e visível após reload
- [x] Sessão encerrada → automaticamente consolidada
- [x] Nova conversa → injeta memórias relevantes do usuário
- [x] Todos os 600+ testes existentes passam
- [x] Coverage acima de 80% em componentes críticos

---

## 10. Known Gaps & Future Work

1. **SignalR UI Sync:** A sidebar de sessões não atualiza em tempo real quando uma sessão é criada ou excluída via API (requer refresh ou polling).
2. **Memory Context Caching:** Chamadas frequentes ao `IVectorStore` para buscar memórias podem ser otimizadas com cache por usuário/sessão.
3. **Integration Tests:** Faltam testes de integração fim-a-fim para o `PostgresSessionSummaryStore`.

