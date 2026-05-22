# ADR-022: Arquitetura de Integração Unificada do Chat (12 Pilares)

**Status:** Proposed  
**Data:** 21 de Maio de 2026  
**Autor(es):** Antigravity Orchestrator

---

## Contexto

O ecossistema **Agentic System** cresceu de forma modular, com componentes sofisticados desenvolvidos de maneira isolada: agentes especialistas, ferramentas em C# (`ITool`), conhecimento passivo (`ISkill`), Model Context Protocol (MCP), Webhooks, busca vetorial e híbrida via PostgreSQL/pgvector (RAG com Knowledge Rooms), workflows sequenciais complexos com SignalR progress streaming, plugins, geração de embeddings, resumos de memórias e pipelines de Reranking.

Para maximizar o valor do sistema, o **Chat Principal** do usuário final precisa se tornar o ponto único de entrada inteligente que integra, de forma transparente, orquestrada e com total isolamento multi-tenant, todos esses 12 pilares tecnológicos.

## Decisão

Adotamos a **Arquitetura de Integração Unificada do Chat**, consolidando a comunicação do `ChatHub` e do `MetaAgentOrchestrator` como o hub central que coordena a execução de agentes, ferramentas, webhooks, RAG, workflows, banco de dados, extensibilidade de plugins, embeddings, memórias e reranking.

```
                             [ CHATHUB (SignalR) ]
                                       │
                         [ MetaAgentOrchestrator ]
                                       │
      ┌────────────────────────┬───────┴────────┬──────────────────────┐
      ▼                        ▼                ▼                      ▼
[ RAG Engine ]          [ Agent Engine ]  [ Workflow Engine ]  [ External Interfaces ]
├── Knowledge Rooms     ├── Specialists   ├── Execution Steps  ├── MCP Servers
├── Embeddings (ONNX)   ├── C# Skills     ├── Progress Stream  ├── Inbound/Outbound
├── Hybrid Search       ├── C# Tools      └── Compensation     └── Webhooks
└── ONNX Reranker       └── Memory Store
```

### Detalhamento dos 12 Pilares Integrados:

1. **Agentes (MetaAgent & Specialists):** O `MetaAgentOrchestrator` realiza a triagem de intenções (`ISmartRouter`) nas sessões do SignalR. Consultas simples são executadas diretamente pelo agente especialista correto (`IDirectAgentRequestExecutor`), enquanto tarefas complexas iniciam colaboração multi-agente (`IAgentCollaborationWorkflow`).
2. **Tools (ITool & IToolManager):** Capacidades ativas em C# que realizam I/O e APIs. O LLM emite `tool_calls` via protocolo ReAct, que são interceptados e executados sob governança estrita e regras de isolamento de tenant.
3. **Skills (ISkill & ISkillManager):** Conhecimento passivo injetado dinamicamente no system prompt. Unifica as **Skills Declarativas** (Markdown em `.agents/skills/`) e as **Skills Compiladas em C#** (`BuiltInSkills`) na construção de prompts enriquecidos.
4. **MCP (Model Context Protocol):** Expansão dinâmica do catálogo de ferramentas dos agentes por meio de conexões com servidores MCP de terceiros (ex: StitchMCP, Supabase, GitHub).
5. **Webhooks:** Inbound webhooks retomam workflows sequenciais (`IWorkflowEngine.ResumeAsync`) ou acionam agentes assincronamente em background. Outbound webhooks permitem que agentes enviem dados estruturados a plataformas do cliente.
6. **RAG & Knowledge Rooms:** Restrição de contexto do agente baseado em salas de conhecimento autorizadas para aquele tenant e agente. Filtros do RAG buscam exclusivamente na coluna `MetadataJson` correspondente às tags `room_id`. Se o agente não possuir nenhuma sala de conhecimento associada, o RAG adota Isolamento Estrito Zero Trust (retorna vazio).
7. **Banco SQL (Acesso Seguro):** Agentes interagem com o banco relacional de forma estritamente controlada e segura por meio de uma `TenantAnalyticsTool` parametrizada em C#, banindo qualquer tipo de execução direta de queries livres geradas pelo LLM para mitigar riscos de SQL Injection.
8. **Workflows (SignalR Progress Streaming):** Processos de negócios complexos (ações, decisões, aprovações, passos paralelos) rodam no `IWorkflowEngine` e transmitem seu status passo-a-passo via SignalR `/hubs/workflow` para renderização visual e viva na interface do chat.
9. **Plugins:** Capacidade de carregar módulos de terceiros que injetam novas skills e tools isoladamente, ampliando a funcionalidade do chat sem tocar no núcleo do sistema.
10. **Embeddings:** Pipeline de ingestão multi-tenant de documentos que gera vetores dinamicamente com suporte a ONNX local e os armazena vinculados a salas de conhecimento com indexação `hnsw`.
11. **Memória:** Segmentada em memória de curto prazo (histórico da sessão SignalR) e memória de longo prazo/episódica (resumos das conversações passadas gerados e guardados como chunks semânticos).
12. **Rerank:** Otimização final do contexto do RAG usando Reciprocal Rank Fusion (RRF) e Reranking. O motor padrão é o ONNX Cross-Encoder local (in-process), mas permitimos que o usuário selecione e configure dinamicamente seu provedor de rerank de preferência (nuvem/local/desativado) através das configurações do tenant persistidas no banco.

---

## Justificativa

1. **Segurança e Isolamento Multi-Tenant:** Centralizar todos os 12 pilares unter a triagem e o fluxo do `MetaAgentOrchestrator` garante que os filtros de `tenant_id` e as permissões de acesso sejam sempre aplicados antes de qualquer I/O, busca vetorial ou chamada de tool.
2. **Experiência do Usuário (UX) Viva:** O uso intensivo de SignalR para streamings simultâneos de chats e progressos de workflows elimina telas estáticas e traz transparência ao processo de orquestração da IA.
3. **Escalabilidade e Extensibilidade:** A clara distinção entre Skills (prompts passivos) e Tools (ações), estendidas por MCP e Plugins, permite que o sistema cresça sem inflar a base de código do monorepo C#.

---

## Consequências

### Positivas
* **Orquestração Omnipresente:** O chat deixa de ser apenas uma caixa de perguntas e respostas para se tornar um painel dinâmico que inicia workflows complexos e comanda ferramentas físicas.
* **Isolamento de Tenant Absoluto:** A injeção automática de `tenant_id` na memória, banco SQL, RAG e ferramentas mitiga totalmente riscos de vazamento de dados.
* **Rendimento Inteligente (ONNX Local):** O suporte a ONNX in-process para embeddings e reranking economiza latência de rede e custos de API.

### Desafios / Pontos de Atenção (Negativas)
* **Complexidade de Depuração:** Rastrear a execução concorrente de múltiplos agentes, ferramentas C# e servidores MCP requer logs Serilog extremamente estruturados e correlation IDs universais.
* **Consumo de Contexto (Context Budget):** Integrar skills, memórias, MCP e RAG exige o uso rigoroso de compressores semânticos e controle de tokens (`ContextBudgetManager`) para evitar estouros de contexto LLM.
