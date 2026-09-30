# US-021: Integração Unificada de Pilares Tecnológicos no Chat Principal

**Épico:** Experiência Conversacional Inteligente  
**GitHub Issue:** [#75](https://github.com/JonathanBenicio/Agent-System/issues/75)  
**Prioridade:** Alta  
**Estimativa (Story Points):** 13  

---

## Descrição

**Como um** usuário final do **Agentic System**,  
**Eu quero** interagir com um chat unificado que utilize integradamente Agentes Especialistas, Ferramentas C#, Skills, MCP, Webhooks, RAG estruturado por Salas de Conhecimento, Queries SQL seguras, Execução de Workflows visuais, Plugins externos, Embeddings, Memória e Rerank,  
**Para que** eu possa resolver problemas complexos de negócios e desenvolvimento de ponta a ponta sem trocar de ferramenta ou perder contexto.

---

## Regras de Negócio e Contexto

1. **Isolamento de Tenants (Multi-Tenancy):**
   - Nenhuma operação (seja de busca RAG, execução SQL, acesso à memória, ou execução de workflows) pode expor dados entre diferentes `tenant_id`s.
   - O `TenantContext` deve persistir e fluir em todas as execuções assíncronas do SignalR.
2. **Controle de Custo e Contexto (Context Budget):**
   - O sistema deve monitorar o tamanho das mensagens compostas (instruções do agente + skills + RAG chunks + memórias + inputs do usuário).
   - Se o payload ultrapassar o limite configurado para o LLM, o compressor semântico (`ISemanticCompressorService`) deve resumir o contexto RAG e a memória histórica.
3. **Segurança em Banco de Dados (SQL Queries):**
   - Queries livres em SQL geradas por LLMs são terminantemente proibidas para agentes.
   - O agente deve realizar consultas analíticas de forma controlada através da `TenantAnalyticsTool` que utiliza parâmetros e APIs estritas definidas em C# sob o escopo do tenant ativo.

---

## Critérios de Aceite (DoD)

- [x] **Critério 1 (SignalR Handshake & Context):** O `ChatHub` deve extrair o `tenant_id` do JWT claims decodificado de forma dinâmica nas conexões SignalR e disponibilizá-lo para os agentes via `ITenantContextAccessor`.
- [x] **Critério 2 (RAG & Knowledge Rooms):** O chat deve permitir a seleção ou auto-associação de `Knowledge Rooms` por sessão. A busca vetorial no `PostgresVectorStore` deve usar o operador JSONb `JsonContains` para ler tags `room_id` na coluna `MetadataJson`. Se o agente não tiver salas de conhecimento associadas, o RAG deve retornar vazio (Isolamento Estrito Zero Trust).
- [x] **Critério 3 (Reranking Flexível & Configurável):** As buscas de RAG no chat devem passar por fusão híbrida (busca textual GIN + similaridade vetorial pgvector) e Reranking. O motor padrão é o ONNX Cross-Encoder local (in-process), mas o tenant deve poder selecionar e configurar na tela o provedor de sua preferência (nuvem/local/desativado), cujos parâmetros e chaves são persistidos nas configurações do banco.
- [x] **Critério 4 (Fiação de Skills C#):** O `SkillManager` deve resolver e fundir dinamicamente no system prompt as skills baseadas em código C# (`ISkill` compiladas) registradas no catálogo de acordo com o contexto de domínio do agente, sem acoplamento a arquivos Markdown internos da IDE.
- [x] **Critério 5 (MCP & Tools):** Ferramentas expostas por servidores MCP autorizados no tenant devem ser exibidas e utilizáveis pelo agente no chat, mapeando os parâmetros em runtime via `IToolManager`.
- [x] **Critério 6 (Workflows & SignalR Progress):** Ao solicitar a execução de um workflow complexo (ex: "execute workflow X"), o chat deve disparar o `IWorkflowEngine`, e as atualizações de passos devem ser publicadas no `WorkflowHub` e transmitidas em tempo real para a interface de chat.
- [x] **Critério 7 (Memória Episódica):** No fim da sessão de chat, um hosted service assíncrono deve resumir os fatos marcantes da conversa e salvá-los como um novo chunk semântico de memória de longo prazo associado ao usuário e tenant.

---

## Dependências Técnicas

* [x] ADR-022 (Arquitetura de Integração Unificada do Chat)
* [x] Injeção de `ITenantContextAccessor` no SignalR `ChatHub` (resolvido na fase de fundação)
* [x] Implementação de `TenantAnalyticsTool` parametrizada
* [x] Ajuste no pipeline de busca vetorial filtrada em `PostgresVectorStore.cs`
