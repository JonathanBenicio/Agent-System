# ADR 019: Decisão Arquitetural - Associação Granular entre Agentes e Knowledge Rooms (Contextualização Restrita)

**Status:** Proposto  
**Data:** 18 de Maio de 2026  
**Autor:** Gemini CLI (em colaboração com o usuário)

---

## Contexto

Atualmente, o sistema suporta **Knowledge Rooms** para organizar documentos, mas a associação entre um Agente e o conhecimento que ele pode acessar é feita de forma implícita ou global. Em um cenário multi-tenant e de alta especialização, um "Agente Jurídico" não deve ter acesso a documentos de uma "Sala de Engenharia", mesmo que ambos pertençam ao mesmo Tenant.

Precisamos de um mecanismo para restringir o escopo de busca semântica de cada agente a um subconjunto específico de salas de conhecimento, garantindo maior precisão (redução de ruído) e segurança (isolamento de dados sensíveis).

## Decisão

Implementaremos a **Associação Granular Agente-Sala** através das seguintes mudanças:

1.  **Modelo de Dados:** Criação da entidade `AgentKnowledgeRoomAssignment` (junction table) vinculando `AgentName` (ou `AgentId`) a `KnowledgeRoomId`.
2.  **Configuração via UI:** Atualização do `AgentFormModal` para incluir um seletor múltiplo de Knowledge Rooms disponíveis no workspace.
3.  **Pipeline de Injeção de Contexto:** O serviço `KnowledgeSpecialist` (ou o orquestrador) passará os IDs das salas permitidas para o `IVectorStore.SearchAsync`.
4.  **Filtragem no Banco:** A query SQL/pgvector incluirá um filtro `WHERE room_id IN (...)` baseado nas permissões do agente executor.

## Justificativa

### Por que esta abordagem?

1.  **Redução de Ruído:** Ao limitar o espaço de busca, evitamos que o modelo recupere chunks irrelevantes de outros domínios que poderiam causar alucinações ou respostas confusas.
2.  **Segurança e Compliance:** Garante que segredos ou documentos sensíveis fiquem restritos apenas aos agentes autorizados a manipulá-los.
3.  **Flexibilidade:** Permite criar agentes "poliglotas" em conhecimento (associados a múltiplas salas) ou agentes "ultra-especialistas" (associados a uma única sala).

## Consequências

### Positivas

*   **Precisão Melhorada:** O RAG torna-se muito mais eficiente ao buscar em silos de dados curados.
*   **Controle de Acesso Robusto:** Extende o conceito de RBAC das salas para a camada de execução de IA.
*   **Melhor Auditoria:** Torna explícito qual conhecimento cada agente está autorizado a consumir.

### Desafios / Pontos de Atenção (Negativas)

*   **Gerenciamento de Configuração:** Adiciona um passo extra na criação de agentes (selecionar as salas).
*   **Impacto em Performance:** A filtragem adicional no banco de dados (cláusula `IN`) deve ser indexada corretamente para não degradar a latência de busca vetorial.
*   **Sincronização:** Se uma sala for excluída, as associações com os agentes precisam ser removidas ou tratadas (Cascade delete).

---
*Nota: Este documento propõe a estrutura para a Track 1 do Roadmap Q2 2026.*
