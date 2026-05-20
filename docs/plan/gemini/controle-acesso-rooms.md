# Plano: Controle de Acesso e Permissões em Knowledge Rooms

**Objetivo:** Implementar Role-Based Access Control (RBAC) para gerenciar quem pode visualizar ou editar as Knowledge Rooms, com integração ponta a ponta (Banco, API e UI).

**Fases de Implementação e Futuras Github Sub-issues:**

1. **Fase 1: Entidade de Permissão e Migração (Backend)**
   - Criar `KnowledgeRoomPermissionEntity` associando `RoomId`, `UserId` e `Role` (Admin, Editor, Reader).
   - Configurar o mapeamento no EF Core e aplicar a migration no PostgreSQL.

2. **Fase 2: API para Gerenciamento de Acessos (Backend)**
   - Atualizar o `KnowledgeRoomService` para que a listagem (`ListRoomsAsync`) filtre as salas de acordo com as permissões do usuário atual.
   - Garantir que a criação de uma sala atribua automaticamente o papel de `Admin` ao criador.
   - Criar os endpoints CRUD de permissões (`GET`, `POST`, `DELETE` em `/api/knowledge/rooms/{id}/permissions`).

3. **Fase 3: Modal de Gestão de Acessos (Frontend)**
   - Dar vida ao botão "Manage Access" na tela de Knowledge Rooms.
   - Construir o componente `RoomAccessModal.tsx` para listar membros da sala, permitindo conceder ou revogar acessos.
   - Integrar os novos endpoints via React Query no `useKnowledgeRooms.ts`.
