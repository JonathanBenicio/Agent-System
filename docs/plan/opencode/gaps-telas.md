# Plano de Refinamento — Gaps de Implementação nas Telas

## Diagnóstico

| Área | Frontend | Backend API | Banco |
|------|----------|-------------|-------|
| **RAG (métricas)** | Hardcoded | ❌ Não existe | ✅ Tables existem |
| **KnowledgeRooms** | Zustand (client-only) | ❌ Não existe | ❌ Sem tabela |
| **Workflows** | ReactFlow (visual only) | ❌ Não existe | ✅ Tables existem |

---

## Tarefa 1: RAG — Métricas Reais (Baixa Complexidade) - ✅ CONCLUÍDO

**Problema:** `RAGPage.tsx` usa valores hardcoded para `totalIndexedChunks`, `searchCount24h` e status do ONNX.

**Solução:**

1. **Backend** — Criar endpoint `GET /api/document/stats` que retorna:
   - `totalChunks`: `SELECT COUNT(*) FROM vector_documents`
   - `searchCount24h`: Contagem de buscas nas últimas 24h
   - `onnxStatus`: Status do runtime ONNX (carregado/não, modelo ativo, latência média)

2. **Frontend** — Adicionar método `getStats()` no hook `useRAG.ts` e substituir valores hardcoded no `RAGPage.tsx`.

**Arquivos afetados:**
- `src/AgenticSystem.Api/Controllers/DocumentController.cs` (novo endpoint)
- `frontend/src/hooks/useRAG.ts` (novo método)
- `frontend/src/components/rag/RAGPage.tsx` (remover hardcoded)

**Estimativa:** 1-2 horas

---

## Tarefa 2: Knowledge Rooms — Persistência (Média Complexidade) - ✅ CONCLUÍDO

**Problema:** `useKnowledgeStore.ts` usa Zustand com `persist` (localStorage). Dados não são compartilhados entre usuários/sessões. Sem API backend.

**Solução:**

1. **Backend** — Criar entidade + API CRUD:
   - Entidade `KnowledgeRoomEntity` (Id, TenantId, Name, Description, Color, Icon, DocumentCount, Tags, CreatedAt, UpdatedAt)
   - Migration para tabela `knowledge_rooms`
   - Controller `KnowledgeRoomController` com endpoints:
     - `GET /api/knowledge/rooms` — lista por tenant
     - `POST /api/knowledge/rooms` — cria
     - `PUT /api/knowledge/rooms/{id}` — atualiza
     - `DELETE /api/knowledge/rooms/{id}` — deleta
     - `GET /api/knowledge/rooms/{id}/documents` — lista documentos da room

2. **Frontend** — Refatorar `useKnowledgeStore.ts`:
   - Remover `persist` do Zustand
   - Usar `react-query` para fetch/mutation
   - Criar hook `useKnowledgeRooms.ts`
   - Conectar `KnowledgeRooms.tsx` ao hook

3. **API** — Adicionar `knowledgeRoomApi` ao `api.ts`

**Arquivos afetados:**
- `src/AgenticSystem.Infrastructure/Persistence/Entities/PersistenceEntities.cs` (nova entidade)
- `src/AgenticSystem.Infrastructure/Persistence/Configurations/PersistenceConfigurations.cs` (nova config)
- Nova migration
- `src/AgenticSystem.Core/Services/KnowledgeRoomService.cs` (novo serviço)
- `src/AgenticSystem.Api/Controllers/KnowledgeRoomController.cs` (novo controller)
- `frontend/src/store/useKnowledgeStore.ts` (refatorar)
- `frontend/src/hooks/useKnowledgeRooms.ts` (novo hook)
- `frontend/src/components/rag/KnowledgeRooms.tsx` (conectar ao hook)
- `frontend/src/lib/api.ts` (novo namespace)
- `frontend/src/types/api.ts` (novos tipos)

**Estimativa:** 4-6 horas

---

## Tarefa 3: Workflow Builder — Persistência + Execução (Alta Complexidade) - ✅ CONCLUÍDO

**Problema:** `WorkflowBuilder.tsx` é puramente visual. Sem listagem de workflows, sem persistência, sem edição de nós, botão "Run" sem ação.

### Fase 3A: Persistência de Workflows (Média) - ✅ CONCLUÍDO

1. **Backend** — Criar controller `WorkflowController`:
   - `GET /api/workflows` — lista definições por tenant
   - `GET /api/workflows/{id}` — detalhes
   - `POST /api/workflows` — salva definição (recebe `WorkflowDefinition` do frontend)
   - `PUT /api/workflows/{id}` — atualiza
   - `DELETE /api/workflows/{id}` — deleta
   - `POST /api/workflows/{id}/execute` — executa workflow
   
   *(As entities WorkflowDefinitionEntity, WorkflowExecutionEntity já existem)*

2. **Frontend** — Refatorar `WorkflowBuilder.tsx`:
   - Adicionar painel lateral com lista de workflows salvos
   - Conectar `handleSave` à API `POST /api/workflows`
   - Adicionar `workflowApi` ao `api.ts` e hook `useWorkflows.ts`

### Fase 3B: Edição de Nós (Média) - ✅ CONCLUÍDO

1. **Frontend** — Adicionar painel de propriedades do nó:
   - Ao clicar em um nó, abrir painel lateral com campos editáveis
   - Para nó Agent: nome do agent, descrição, input params
   - Para nó Tool: nome da tool, input params
   - Salvar alterações no store (`useWorkflowStore`)

2. **Frontend** — Adicionar tipos de nó:
   - `Decision` (condição/branch)
   - `Wait` (timer/evento)
   - Mapear para `WorkflowStepType` do backend

### Fase 3C: Execução (Alta) - ✅ CONCLUÍDO

1. **Backend** — Implementar `WorkflowExecutionService`:
   - Parser do `DefinitionJson` → `WorkflowDefinition`
   - Executor sequencial/parcial baseado em `DependsOn`
   - Persistir `WorkflowExecutionEntity` + `WorkflowStepExecutionEntity`
   - SignalR para streaming de progresso (opcional)

2. **Frontend** — Conectar botão "Run":
   - `POST /api/workflows/{id}/execute`
   - Mostrar status de execução em tempo real
   - Histórico de execuções

**Arquivos afetados:**
- `src/AgenticSystem.Core/Services/WorkflowExecutionService.cs` (novo)
- `src/AgenticSystem.Api/Controllers/WorkflowController.cs` (novo)
- `frontend/src/store/useWorkflowStore.ts` (expandir)
- `frontend/src/components/workflows/WorkflowBuilder.tsx` (refatorar)
- `frontend/src/hooks/useWorkflows.ts` (novo)
- `frontend/src/lib/api.ts` (novo namespace)
- `frontend/src/types/api.ts` (novos tipos)

**Estimativa:** 12-18 horas total (3A: 4h, 3B: 4h, 3C: 6-8h)

---

## Ordem de Execução Recomendada

1. **Tarefa 1 (RAG métricas)** → 1-2h → Quick win, baixo risco
2. **Tarefa 2 (Knowledge Rooms)** → 4-6h → Nenhuma dependência
3. **Tarefa 3A (Workflow CRUD)** → 4h → Nenhuma dependência
4. **Tarefa 3B (Workflow Editor)** → 4h → Depende de 3A
5. **Tarefa 3C (Workflow Exec)** → 6-8h → Depende de 3A+3B
