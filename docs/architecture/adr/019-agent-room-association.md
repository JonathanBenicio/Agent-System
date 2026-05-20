# ADR-019: Associação Granular Agente-Sala (Contexto Restrito)

**Status:** Proposto  
**Data:** 2026-05-18  
**Decisores:** Equipe de Arquitetura  
**Contexto:** Track 1 — Specialized Context (Roadmap Q2 2026)

## Contexto

Atualmente, os agentes do sistema possuem acesso irrestrito a todos os documentos indexados no vetor de conhecimento (RAG). Em ambientes multi-tenant e com múltiplos domínios de conhecimento (Knowledge Rooms), isso representa:

1. **Risco de segurança:** Um agente de RH pode acessar documentos financeiros confidenciais.
2. **Degradação de qualidade:** Buscas semânticas retornam resultados irrelevantes de domínios não relacionados.
3. **Falta de governança:** Não há controle granular sobre qual agente acessa qual contexto de conhecimento.

## Decisão

Implementar uma associação N:N entre agentes e Knowledge Rooms através de:

### 1. Tabela Junction: `agent_knowledge_room_assignments`

```sql
CREATE TABLE agent_knowledge_room_assignments (
    id VARCHAR(64) PRIMARY KEY,
    tenant_id VARCHAR(64) NOT NULL,
    agent_name VARCHAR(128) NOT NULL,
    room_id VARCHAR(64) NOT NULL REFERENCES knowledge_rooms(id),
    created_at TIMESTAMP DEFAULT NOW(),
    UNIQUE(agent_name, room_id, tenant_id)
);
```

### 2. API Endpoints

| Método | Rota | Descrição |
|--------|------|-----------|
| `GET` | `/api/agent/agents/{name}/rooms` | Lista salas associadas a um agente |
| `PUT` | `/api/agent/agents/{name}/rooms` | Atualiza associações (replace all) |
| `POST` | `/api/agent/agents/{name}/rooms/{roomId}` | Adiciona uma sala |
| `DELETE` | `/api/agent/agents/{name}/rooms/{roomId}` | Remove uma sala |

### 3. Filtragem no `KnowledgeSpecialist`

O `KnowledgeSpecialist` (responsável pela busca vetorial RAG) receberá o contexto do agente e filtrará os documentos pelas rooms associadas:

```csharp
// Antes: busca em todas as collections
var results = await _vectorStore.SearchAsync(query, collection: "all", topK: 5);

// Depois: busca apenas nas rooms associadas ao agente
var roomIds = await _assignmentStore.GetRoomIdsForAgentAsync(agentName, tenantId);
var results = await _vectorStore.SearchAsync(query, collections: roomIds, topK: 5);
```

### 4. Frontend: Room Selector no `AgentFormModal`

- Multi-select com busca (react-select ou similar)
- Carrega salas disponíveis via `GET /api/knowledge/rooms`
- Salva associações via `PUT /api/agent/agents/{name}/rooms`

## Alternativas Consideradas

### A. Tag-based filtering nos documentos
Marcar cada documento com tags e filtrar por tags do agente.
- **Prós:** Mais flexível, um documento pode pertencer a múltiplos contextos.
- **Contras:** Complexidade adicional no ingest, risco de inconsistência de tags.

### B. Collections separadas por room
Criar uma collection vetorial por Knowledge Room.
- **Prós:** Isolamento físico total.
- **Contras:** Degradação de performance com muitas collections, complexidade operacional.

### C. Hierarquia de tenants
Usar sub-tenants para isolamento.
- **Prós:** Isolamento nativo.
- **Contras:** Overkill para o caso de uso, quebra o modelo atual de tenant único por organização.

## Consequências

### Positivas
- Isolamento de conhecimento por agente (segurança)
- Melhoria na relevância das respostas RAG (qualidade)
- Governança granular e auditável
- Compatível com modelo multi-tenant existente

### Negativas
- Complexidade adicional no `KnowledgeSpecialist`
- Necessidade de migração para agentes existentes (default: todas as rooms)
- Query de busca vetorial mais complexa (IN clause nas collections)

## Implementação

1. **Backend:**
   - Entidade `AgentKnowledgeRoomAssignmentEntity` + migration
   - `IAgentKnowledgeRoomStore` + implementação PostgreSQL
   - `AgentRoomController` com endpoints CRUD
   - Atualizar `KnowledgeSpecialist` para filtrar por rooms

2. **Frontend:**
   - Room selector no `AgentFormModal`
   - Hook `useAgentRooms.ts` para gerenciamento de estado

3. **Migração:**
   - Agentes existentes recebem associação com todas as rooms do tenant (backward compatibility)

## Referências

- US-41: Associar Agente a Knowledge Rooms
- ADR-012: Multi-Tenant Agent Memory Schema
- ADR-005: pgvector + Graph RAG
