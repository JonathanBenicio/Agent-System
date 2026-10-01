# ADR 025: Arquitetura de Chat Avançado, Sessões Isoladas e Filtros Contextuais

**Status:** Proposto  
**Data:** 22 de Maio de 2026  
**Autor(es):** Antigravity, Specialist Orchestrator Agent

---

## Contexto

Atualmente, o sistema de chat do AgenticSystem é tratado de forma transitória no backend:
1. **Sessões Voláteis**: Toda vez que o SignalR `ChatHub` recebe uma mensagem via `SendMessage`, ele gera dinamicamente um novo ID de sessão e o retorna junto com a resposta. Isso impede a continuidade estrita da conversa do usuário a partir da UI (o frontend não consegue continuar enviando mensagens na mesma sessão sem re-instanciá-la).
2. **RAG Irrestrito**: As buscas do RAG (`RAGContextProvider`) buscam automaticamente documentos de todas as Salas de Conhecimento associadas ao agente, sem permitir que o usuário foque a busca semântica em apenas uma base documental específica (comportamento tipo NotebookLM).
3. **Falta de Integração Operacional**: Embora existam subsistemas de Agentes Especializados e Workflows, a tela de chat principal não possui acoplamento visual e funcional direto com eles. O upload de arquivos também carece de isolamento contextual por sessão de chat.

Precisamos definir padrões técnicos robustos para:
- Reutilizar IDs de sessão existentes no fluxo real-time do SignalR.
- Propagar filtros de Salas de Conhecimento e isolamento semântico no pipeline RAG.
- Ingerir e buscar documentos restritos por sessão de chat.
- Mapear a integração de fluxos assíncronos (Workflows) e Agentes Dedicados na interface de conversação.

---

## Decisão

Tomamos a decisão de arquitetar a expansão do Chat em três camadas integradas:

1. **Camada de Continuidade de Sessão (Backend & Hub)**:
   - Alterar o `ChatRequest` REST e a assinatura do SignalR `ChatHub.SendMessage` para aceitar um parâmetro opcional `sessionId`.
   - Modificar a interface `ISessionLifecycleCoordinator` e sua implementação `SessionLifecycleCoordinator` para que, ao receber um `sessionId` válido e pertencente ao mesmo tenant e usuário, reutilize esta sessão existente no `ISessionStore` em vez de gerar um novo UUID.
   - Preservar o histórico de eventos da sessão recuperada para manter a memória injetada no LLM coerente.

2. **Camada de Filtro Contextual RAG (NotebookLM & Sessões Isoladas)**:
   - Adicionar ao `LLMRuntimeContext` a propriedade `KnowledgeRoomId`. Ela será injetada a partir de `UserContext.Preferences["rag.knowledgeRoomId"]` durante a criação do escopo de execução no `LLMRuntimeContextAccessor`.
   - Modificar o `RAGContextProvider` para que:
     - Se `KnowledgeRoomId` estiver presente e for válido para o tenant do usuário, restrinja stritamente a lista de `allowedRoomIds` a este ID específico.
     - Se nenhum `KnowledgeRoomId` for especificado, continue aplicando a política padrão de Zero Trust (todas as salas permitidas do agente ativo).
   - Para arquivos isolados na sessão de chat:
     - Ingerir arquivos enviados diretamente no chat usando a rota `/api/document/ingest` com `source = sessionId`.
     - No `RAGContextProvider`, se não houver sala selecionada e a sessão de chat contiver documentos associados, adicionar a coleção `sessionId` como escopo de busca, garantindo que perguntas sobre os PDFs enviados sejam respondidas estritamente no escopo daquela conversa.

3. **Camada de Componentização Visual Premium (Frontend SPA)**:
   - Criar uma barra flutuante superior com estilo *glassmorphism* (`AISelectorBar.tsx` estendido) contendo:
     - Seletor de Salas de Conhecimento (NotebookLM style).
     - Seletor de Agentes Especializados (bypassando o MetaAgent e aplicando `targetAgent`).
     - Seletor e disparador de Workflows cadastrados.
   - Criar o component `WorkflowExecutionCard.tsx` no chat que se conecta ao SignalR `/hubs/workflow` para renderizar o progresso visual de etapas de workflows em tempo real (cards dinâmicos com *micro-animations*).
   - Seguir estritamente a paleta de cores HSL Zinc + Teal e o *Purple Ban* (proibido o uso de roxo).

---

## Justificativa

1. **Eficiência no RAG e Economia de Budget (FinOps)**: A capacidade de focar a busca semântica em apenas uma Sala de Conhecimento específica (estilo NotebookLM) reduz significativamente a quantidade de chunks carregados no contexto do LLM, diminuindo o uso de tokens e o custo financeiro por requisição, além de mitigar alucinações.
2. **Segurança Multitenant e Isolamento Estrito (Zero Trust)**: A ingestão de arquivos isolados na sessão (`source = sessionId`) e a verificação de permissões do tenant na recuperação da sala garantem que dados confidenciais não vazem entre diferentes sessões de conversação ou entre usuários do mesmo tenant sem acesso.
3. **Resiliência e Desacoplamento via SignalR**: Utilizar hubs de SignalR separados (`/hubs/chat` e `/hubs/workflow`) para atualizar o status do chat e o status de execução de workflows em background garante que falhas ou lentidões em execuções de fluxo de trabalho não travem ou congestionem a conexão de streaming de mensagens.
4. **UX Premium e Alinhamento ao Manifesto**: O design *glassmorphism* com micro-animações, estados de loading fluidos e *skeletons* elegantes garante uma percepção de valor extremamente premium (Premium UI/UX) para o usuário final, aumentando o engajamento com a plataforma.

---

## Consequências

### Positivas
* **Conversação Natural e Persistente**: O usuário pode retomar conversas antigas da sidebar, e a inteligência do LLM lembrará do contexto histórico acumulado.
* **Foco Semântico Altamente Preciso**: O comportamento tipo NotebookLM permite aos usuários "conversarem" exclusivamente com um livro, código ou conjunto de relatórios específicos de forma limpa.
* **Transparência de Processos (Workflows)**: O monitoramento de workflows direto na linha do tempo do chat torna a automação intuitiva e visível, sem exigir que o usuário mude de página para ver se um fluxo complexo foi concluído.

### Desafios / Pontos de Atenção (Negativas)
* **Gerenciamento de Cache no Vector Store**: O upload contínuo de arquivos temporários isolados em sessões de chat pode inflar a tabela de embeddings (`vector_documents`). Mitigamos isso incluindo uma tarefa de limpeza automática de documentos de sessões finalizadas/antigas via `IVectorStore.CleanupOldDocumentsAsync(olderThan)` executada em background.
* **Complexidade do Estado do Frontend**: O gerenciamento simultâneo do hook de chat (SignalR), hook de progresso de workflows e seletores exige um design de estado altamente reativo para evitar renderizações redundantes. Usaremos uma store centralizada via Zustand no frontend para gerenciar os modais e execuções de workflows de forma limpa.
