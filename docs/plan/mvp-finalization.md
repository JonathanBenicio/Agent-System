# Plano de Implementação: Finalização do MVP (Gaps @docs/plan/mvp-scope.md)

> **Status:** ✅ CONCLUÍDO (revisado em 2026-05-20)

## Goal
Finalizar os gaps remanescentes do MVP: Autenticação Supabase, Criação de Workflows via Chat (Prompt-to-Resource) e Consolidação da Memória de Longo Prazo.

## Tasks

### Fase 1: Autenticação Supabase ✅ CONCLUÍDO
- [x] Criar `frontend/src/lib/supabase.ts` → Cliente inicializa sem erros.
- [x] Refatorar `authStore.ts` para Supabase Auth → Login/Logout via UI funcional.
- [x] Implementar `ProtectedRoute` e proteção de rotas → Redirecionamento para login funciona.
- [x] Backend valida JWT Supabase e extrai `tenant_id` do `app_metadata`.

> **Nota:** `AuthGuard` e `AuthInitializer` do plano original foram implementados como `ProtectedRoute` + `useEffect` no `App.tsx`.

### Fase 2: Prompt-to-Workflow ✅ CONCLUÍDO
- [x] `WorkflowSpecialist` no MetaAgent → Gera JSON de workflow via `json-workflow` blocks.
- [x] `DetectAndRecordWorkflowsAsync` extrai workflows e salva via `IWorkflowStore`.
- [x] `ArtifactRecorded` via SignalR → Evento chega no frontend com `definition` (bug corrigido: agora inclui `artifact.Data` no evento).
- [x] Frontend detecta `StreamEvent` com `artifactType: 'Plan'` e popula `useWorkflowStore`.
- [x] Toast notification com link para workflows gerados.

### Fase 3: Memória e Contexto ✅ CONCLUÍDO
- [x] `SessionInsights` entidade persistida no Postgres (`session_insights`).
- [x] `SessionConsolidator.ExtractInsightsAsync` extrai fatos, decisões, preferências e action items.
- [x] `InjectMemoryContextAsync` no `MetaAgentOrchestrator` injeta contexto de memória via RAG.
- [x] `MemoryInjectionService.VectorizeInsightsAsync` vetoriza insights para busca semântica.
- [x] `SessionInsights` component exibe insights na `ChatPage` (botão "Insights").
- [x] `JoinSession` envia insights via SignalR para restauração de contexto.

### RAG com Citações ✅ CONCLUÍDO
- [x] `CitationEngine.GenerateWithCitationsAsync` gera marcadores `[1]`, `[2]` no texto.
- [x] Citações incluídas no `SessionCompleted` event (bug corrigido: agora envia `citations` metadata).
- [x] Frontend renderiza citações como badges clicáveis com tooltip de detalhes.
- [x] `AgentExecutionPostProcessingPipeline` aplica citações quando RAG context está presente.

## Done When
- [x] Sistema de login Supabase integrado e multi-tenant.
- [x] Chat capaz de gerar e abrir workflows visuais automaticamente.
- [x] Agente demonstra continuidade entre sessões usando memórias de longo prazo.
- [x] RAG em PDFs validado e com citação de fontes.

## Bugs Corrigidos Durante Revisão

| Bug | Arquivo | Descrição |
|-----|---------|-----------|
| ArtifactRecorded sem dados | `AgentRuntimeCoordinator.cs:290-301` | `artifact.Data` não era incluído no evento SignalR |
| Citações não enviadas ao frontend | `AgentRuntimeCoordinator.cs:143-158` | `citations` metadata não era incluída no `SessionCompleted` |
| Citações não recebidas no frontend | `ChatHub.cs:61-72` | `citations` não era mapeado no `ReceiveMessage` |
| Frontend sem rendering de citações | `MessageBubble.tsx` | Componente não renderizava badges de fontes |
