# [FEAT] Chat Avançado e Gerenciamento Unificado de Sessões, Salas de Conhecimento, Agentes e Workflows (#93)

## 📝 Descrição
Esta iniciativa visa estender a interface de Chat principal do AgenticSystem para integrá-la unificadamente com os pilares fundamentais da plataforma:
1. **Gerenciamento de Sessões**: Continuidade real-time de sessões existentes via SignalR e REST, permitindo listar, renomear, excluir e interagir sem a recriação redundante de sessões a cada nova mensagem.
2. **Filtro de Sala de Conhecimento (Estilo NotebookLM)**: Capacidade de selecionar uma sala de conhecimento ativa para que as buscas semânticas (RAG) sejam restringidas estritamente àquela base de documentos (Zero Trust).
3. **Seleção de Agente Especializado**: Possibilidade de direcionar prompts a um agente registrado no catálogo (ex: `@code-reviewer`, `@security-auditor`), ignorando temporariamente o roteamento automático do MetaAgent.
4. **Envio de Arquivos na Sessão**: Upload de arquivos diretamente do chat, direcionando-os à Sala de Conhecimento ativa ou isolando-os estritamente na própria sessão atual (`Collection = sessionId`) para perguntas e respostas contextuais.
5. **Execução de Workflows**: Seletor visual de Workflows e exibição de card dinâmico de execução (`WorkflowExecutionCard`) que consome atualizações em tempo real via SignalR (`/hubs/workflow`).

Tudo isso encapsulado sob uma estética visual Premium UI/UX que respeita a paleta Zinc/Teal do Design Manifesto e a regra inegociável do *Purple Ban*.

## 🎯 Objetivo / Valor de Negócio
- **Experiência Unificada**: Consolidar a interface de chat como o centro operacional da plataforma, integrando documentos, fluxos de trabalho automatizados (workflows) e inteligência especializada (agentes).
- **Segurança da Informação (Zero Trust)**: Garantir que a busca de documentos no RAG seja estritamente isolada por Sala de Conhecimento ou por ID de sessão de chat, impedindo vazamentos de contexto entre tenants ou salas não autorizadas.
- **Rastreabilidade**: Vincular o histórico de conversas aos respectivos workflows disparados e documentos consumidos, melhorando a auditoria e transparência do sistema.

## 🔗 Rastreabilidade & Documentação (Obrigatório)
- **ADR (Architectural Decision Record)**: [ADR-025: Advanced Chat & Session Management Architecture](architecture/adr/025-advanced-chat-session-management.md)
- **User Story**: [US-44: Chat Avançado e Gerenciamento Unificado de Sessões](user-stories/us-advanced-chat-session-management.md)
- **BDD Feature**: [BDD: Advanced Chat & Session Management Feature](bdd/advanced-chat-session-management.feature)
- **Implementation Plan**: Roadmap: Chat Enhancements (referência histórica; arquivo ausente na baseline)

## ✅ Critérios de Aceite
- [ ] A listagem de sessões na barra lateral do chat deve permitir carregar o histórico de conversas completas (`loadHistory(sessionId)`).
- [ ] O envio de mensagens via SignalR deve reutilizar a sessão atual ativa (`sessionId`) se fornecida, em vez de criar uma nova sessão de chat a cada interação.
- [ ] A barra superior do chat deve conter um seletor visual de Salas de Conhecimento. Se uma sala estiver selecionada, o RAG deve restringir as buscas semânticas apenas aos documentos pertencentes a ela.
- [ ] A barra superior do chat deve conter um seletor visual de Agentes cadastrados, permitindo bypassar o roteamento inteligente e enviar mensagens diretamente ao agente selecionado.
- [ ] O chat deve aceitar o upload de arquivos via drag-and-drop ou seletor. Se uma sala estiver selecionada, o arquivo deve ser ingerido nela. Se não, o arquivo é ingerido na coleção da sessão de chat ativa (`Collection = sessionId`), e o RAG deve conseguir ler seu conteúdo para responder à conversa.
- [ ] A barra lateral ou de ferramentas do chat deve permitir selecionar um workflow cadastrado. Ao clicar em "Executar", o sistema dispara a execução do workflow em background e adiciona um card interativo de progresso em tempo real na listagem do chat.
- [ ] Todo o frontend deve ser responsivo, livre de placeholders e livre de cores roxas/violetas (paleta Zinc, Teal, Emerald).

## 🛠️ Requisitos Técnicos
- [ ] Estender `ChatRequest` e assinaturas do `ChatHub` e `IMetaAgent` para aceitar `sessionId` opcional.
- [ ] Modificar `SessionManager` e `SessionLifecycleCoordinator` para aceitar e reutilizar um ID de sessão pré-existente caso ele seja válido e pertença ao mesmo tenant/usuário.
- [ ] Atualizar `LLMRuntimeContext` e `LLMRuntimeContextAccessor` para ler e propagar o filtro de `KnowledgeRoomId`.
- [ ] Modificar `RAGContextProvider` para aplicar o filtro de `KnowledgeRoomId` ou `sessionId` (para arquivos isolados no chat) no `IVectorStore.SearchWithFiltersAsync`.
- [ ] Criar componentes de frontend premium no Vite/React: `KnowledgeRoomSelector`, `SpecializedAgentSelector`, `WorkflowExecutionCard`, além da repaginada visual do chat e sidebar.

## 🏁 Definition of Done (DoD)
- [ ] Código backend (.NET 10) buildando com sucesso e testes unitários passando.
- [ ] Código frontend compilando via `npm run build` e linter executado com sucesso.
- [ ] UX Audit realizada sem violações da paleta HSL e regras de contraste WCAG AA.
- [ ] Sincronização de todos os índices de documentação (`docs/INDEX.md`, `CONSOLIDATED_DOCS.md`, `README.md`).
