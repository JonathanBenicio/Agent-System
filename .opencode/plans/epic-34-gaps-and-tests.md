# Epic #34 - Correção de Gaps + Testes

## Tarefa 1: Corrigir Gaps

### 1A. Frontend — Handler SessionJoined + isHistory

**Arquivo:** `frontend/src/types/chat.ts`
- Adicionar `isHistory?: boolean` em `ChatMessage` e `SignalRMessage`

**Arquivo:** `frontend/src/hooks/useChat.tsx`
- Adicionar handler `conn.on('SessionJoined', ...)` — log informativo
- Adicionar handler `conn.on('JoinSessionError', ...)` — exibe erro como system message
- No handler `ReceiveMessage`, propagar `msg.isHistory` para `chatMsg.isHistory`

**Arquivo:** `frontend/src/components/chat/ChatPage.tsx`
- Corrigir ordem em `handleSelectSession`: `onClearMessages()` → `setActiveSessionId(id)` → `conn.invoke('JoinSession', id)`

### 1B. Backend — Integrar BuildMemoryContextAsync

**Arquivo:** `src/AgenticSystem.Core/Interfaces/ISessionManager.cs`
- Adicionar método: `Task<string> GetMemoryContextAsync(string userQuery, string userId, string tenantId, CancellationToken ct = default)`

**Arquivo:** `src/AgenticSystem.Core/Services/SessionManager.cs`
- Implementar `GetMemoryContextAsync` delegando para `_memoryInjection.BuildMemoryContextAsync`

**Arquivo:** `src/AgenticSystem.Core/Services/MetaAgentOrchestrator.cs`
- Em `ProcessRequestCoreAsync` e `ProcessDirectRequestCoreAsync`:
  - Chamar `_sessionManager.GetMemoryContextAsync(input, ...)` antes do processamento
  - Prependar contexto de memória ao input: `effectiveInput = memoryContext + "\n\n" + input`

## Tarefa 2: Adicionar Testes

### 2A. SessionControllerTests.cs
- `GetSessions_ReturnsOk_WithSessions`
- `GetSessions_ReturnsUnauthorized_WhenNoUserId`
- `GetSession_ReturnsOk_WhenFound`
- `GetSession_ReturnsNotFound_WhenOtherUser`
- `GetSessionMessages_ReturnsMessages`
- `DeleteSession_DeletesAndReturnsNoContent`
- `UpdateSessionTitle_UpdatesTitle`
- `UpdateSessionTitle_RejectsEmptyTitle`

### 2B. MemoryInjectionServiceTests.cs
- `VectorizeInsights_CreatesDocuments_ForAllTypes`
- `VectorizeInsights_ReturnsZero_WhenNoInsights`
- `BuildMemoryContext_ReturnsEmpty_WhenNoMatches`
- `BuildMemoryContext_GroupsByType`
- `BuildMemoryContext_HandlesVectorStoreFailure`

### 2C. SessionAutoConsolidatorTests.cs
- `ProcessPending_ConsolidatesEndedSessions`
- `ProcessPending_SkipsActiveSessions`
- `ProcessPending_SkipsAlreadyConsolidated`
- `ProcessPending_VectorizesInsights`
- `ProcessPending_HandlesConsolidationFailure`

## Tarefa 3: Marcar Completo + Issues

### Plano
- Atualizar `docs/plan/session-chat-history-memory.md` com status final
- Criar GitHub issues para gaps remanescentes

### Issues a criar
1. `bug(frontend): SessionSidebar não reage a mudanças em tempo real`
2. `enhancement(memory): Cache de memória context para evitar chamadas repetidas ao vector store`
3. `test: Testes de integração para PostgresSessionSummaryStore`
