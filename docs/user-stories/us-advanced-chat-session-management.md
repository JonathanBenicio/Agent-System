# US-44: Chat Avançado e Gerenciamento Unificado de Sessões, Salas de Conhecimento, Agentes e Workflows

**Épico:** Experiência Conversacional Unificada e RAG Enterprise  
**Prioridade:** Alta  
**Estimativa (Story Points):** 13

---

## Descrição

**Como um** analista de dados, desenvolvedor ou usuário do AgenticSystem,  
**Eu quero** ter uma tela de chat centralizada que me permita manter sessões de conversas contínuas e persistentes, restringir o contexto de busca semântica (RAG) a salas de conhecimento específicas, selecionar agentes dedicados para tarefas específicas, anexar documentos temporários e disparar/monitorar workflows em tempo real,  
**Para que** eu possa trabalhar de forma focada, interativa e altamente produtiva com a plataforma, sem barreiras de context-switching ou perda de histórico de conversação.

---

## Regras de Negócio e Contexto

1. **Continuidade de Conversa**: A sessão não deve ser re-criada no banco se o cliente SignalR enviar uma mensagem contendo um `sessionId` que já pertence àquele usuário. Se o `sessionId` não existir ou for de outro tenant/usuário, o sistema deve registrar a falha no log e retornar um erro amigável, impedindo qualquer acesso não autorizado.
2. **NotebookLM Style RAG**: O usuário deve poder escolher uma "Sala de Conhecimento" na barra de ferramentas superior. Ao ativá-la, todas as pesquisas do RAG realizadas no contexto daquela conversa devem retornar **apenas** chunks cujos metadados contenham `room_id` igual ao ID da sala selecionada. Se nenhuma sala estiver selecionada, o RAG busca em todas as salas vinculadas ao agente ativo naquele tenant.
3. **Isolamento de Arquivos no Chat**: Arquivos arrastados ou selecionados para upload no chat principal são ingeridos temporariamente.
   - Se o usuário selecionou uma Sala de Conhecimento, o arquivo é associado a ela permanentemente (`source = roomId`).
   - Se o usuário não selecionou nenhuma sala (Chat Livre), o arquivo é ingerido de forma isolada e restrita a essa sessão de chat (`Collection = sessionId`), ficando invisível para buscas RAG de outras sessões ou salas.
4. **Cards Dinâmicos de Workflows**: Quando o usuário ativa e dispara a execução de um workflow pelo chat, um card especial (`WorkflowExecutionCard`) é inserido na timeline do chat. Esse card deve exibir o progresso em tempo real (etapa atual, status da execução) se comunicando via WebSocket (`/hubs/workflow`), permitindo ao usuário cancelar ou interagir (aprovar ações) diretamente pelo chat.

---

## Critérios de Aceite (DoD)

### Critério 1: Reutilização e Persistência de Sessões
- [ ] O backend deve verificar a existência de `sessionId` na request do SignalR. Se ele existir e pertencer ao usuário e tenant autenticado, a sessão é carregada do `ISessionStore` e suas mensagens históricas são utilizadas para compor o contexto da conversa.
- [ ] Ao carregar a página de Chat com um ID de sessão na URL (ex: `#/chat?session=uuid`), o frontend deve carregar todo o histórico através de `sessionApi.messages(sessionId)`.

### Critério 2: RAG Filtrado por Sala de Conhecimento (NotebookLM)
- [ ] O `RAGContextProvider` deve ler o `KnowledgeRoomId` de `LLMRuntimeContext`.
- [ ] O pipeline RAG deve invocar `SearchWithFiltersAsync` no vector store incluindo o filtro `room_ids = SelectedRoomId`.
- [ ] O RAG deve ignorar e não retornar chunks de outras salas ou documentos que não façam parte da sala de conhecimento explicitamente selecionada.

### Critério 3: Ingestão de Documentos Isolados na Sessão
- [ ] O upload de arquivos no chat livre deve enviar `source = sessionId` para o endpoint `/api/document/ingest`.
- [ ] Quando o RAG buscar por contexto nessa sessão, ele deve consultar a coleção igual ao `sessionId` ativa.

### Critério 4: Execução Visual e Progresso de Workflows
- [ ] A barra lateral ou cabeçalho do chat deve exibir uma listagem de Workflows disponíveis.
- [ ] Ao disparar a execução, um card visual com micro-animações (skeletons e pulses) deve renderizar o status das etapas em tempo real.
- [ ] O card deve se registrar no hub SignalR `/hubs/workflow` para escutar e atualizar o progresso dinamicamente.

---

## Dependências Técnicas

- [x] ADR-025 (Decisão arquitetural de sessões isoladas e RAG contextual)
- [x] RAG Service e Ingestion Pipeline robusto (`IDocumentIngestionPipeline`, `IVectorStore`)
- [x] Hub de Workflows em tempo real (`/hubs/workflow` e `SignalRWorkflowEventBroadcaster`)
