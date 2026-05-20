# Plano de Implementação: Finalização do MVP (Gaps @docs/plan/mvp-scope.md)

## Goal
Finalizar os gaps remanescentes do MVP: Autenticação Supabase, Criação de Workflows via Chat (Prompt-to-Resource) e Consolidação da Memória de Longo Prazo.

## Tasks
- [ ] **Fase 1: Autenticação Supabase**
    - Criar `frontend/src/lib/supabase.ts` → Verify: Cliente inicializa sem erros.
    - Refatorar `authStore.ts` para Supabase Auth → Verify: Login/Logout via UI funcional.
    - Implementar `AuthGuard` e proteção de rotas → Verify: Redirecionamento para login funciona.
- [ ] **Fase 2: Prompt-to-Workflow**
    - Implementar `WorkflowSpecialist` no MetaAgent (Backend) → Verify: Capaz de gerar JSON de workflow.
    - Emitir `ArtifactRecorded` via SignalR → Verify: Evento chega no frontend com os dados.
    - Sincronizar com `useWorkflowStore` → Verify: WorkflowBuilder popula automaticamente após comando no chat.
- [ ] **Fase 3: Memória e Contexto**
    - Persistir Insights no Postgres após consolidação → Verify: Tabela `session_insights` populada.
    - Aprimorar `InjectMemoryContextAsync` com RAG → Verify: Logs mostram memórias injetadas.
    - Exibir sumário na `SessionSidebar` → Verify: UI exibe resumo da conversa.

## Done When
- [ ] Sistema de login Supabase integrado e multi-tenant.
- [ ] Chat capaz de gerar e abrir workflows visuais automaticamente.
- [ ] Agente demonstra continuidade entre sessões usando memórias de longo prazo.
- [ ] RAG em PDFs validado e com citação de fontes.
