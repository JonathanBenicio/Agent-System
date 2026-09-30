# Roadmap: Integração Unificada do Chat (12 Pilares)

> **Status documental:** Aprovado e Totalmente Concluído (Fases 1 a 6)  
> **Escopo:** Integração completa do Chat Principal com os 12 pilares tecnológicos do Agentic System.  
> **GitHub Issue:** [#75](https://github.com/JonathanBenicio/Agent-System/issues/75)  
> **Fonte de verdade operacional:** [adr-021](file:///c:/Users/Jonathan/Documents/Developer/GitHub/Agent-System/docs/architecture/adr/022-unified-chat-integration-architecture.md)  
> **Gerado em:** 21 de Maio de 2026  
> **Projeto:** AgenticSystem  

---

## Objetivo

Esta iniciativa visa estruturar e guiar a integração unificada de múltiplos subsistemas avançados do **Agentic System** sob o **Chat Principal Conversacional**. Isso permitirá que o chat execute dinamicamente agentes especialistas, acione ferramentas seguras (C# e MCP), retome workflows de negócios com streaming visual do SignalR, consulte documentos do RAG com isolamento por Salas de Conhecimento e Reranking local, faça buscas controladas em bancos de dados SQL, execute plugins de terceiros, gerencie memórias históricas/episódicas e processe embeddings e reranking baseados no ecossistema local do tenant.

---

## Princípios de Implantação

1. **Isolamento de Tenants Absoluto (Zero Trust):** O `tenant_id` é sagrado. Nenhuma chamada de RAG, SQL, Webhook, Workflow ou Memória pode ignorar o contexto multi-tenant.
2. **Streaming em Tempo Real (SignalR-First):** Interações concorrentes de agentes e progresso de workflows devem ser notificados ao usuário de forma visual e incremental através de WebSockets.
3. **Resiliência e Fallbacks Automatizados:** Falhas em servidores MCP remotos ou dependências externas devem falhar graciosamente (Circuit Breaker) e desviar para ferramentas locais equivalentes sem derrubar a conversa.
4. **Governança de Custos e Latência (FinOps):** Todo enriquecimento de prompt (Skills + RAG + Memórias) deve passar pelo `ContextBudgetManager` para comprimir dados se o limite de tokens do LLM for ameaçado.

---

## Fases e Sequenciamento

| Ordem | Frente/Fase | Motivo do sequenciamento |
|---|---|---|
| 1 | **Fase 1: Fundação do Contexto & Segurança** | Injetar com segurança o `TenantContext` nos SignalR Hubs (`ChatHub` e `WorkflowHub`) e preparar a fiação do `SkillManager` no prompt base do `MetaAgentOrchestrator`. |
| 2 | **Fase 2: RAG por Salas & Rerank** | Integrar o `RAGContextProvider` às `Knowledge Rooms` mapeadas por agente/tenant no banco e configurar o Rerank (ONNX local) pós-busca híbrida. |
| 3 | **Fase 3: Catálogo de Ações (Tools, SQL & MCP)** | Estruturar a `TenantAnalyticsTool` para consultas parametrizadas seguras no Postgres e unificar ferramentas nativas C# com ferramentas remotas via `StitchMCP`. |
| 4 | **Fase 4: Motor de Workflows & Webhooks** | Habilitar a inicialização e o controle de workflows a partir do chat, transmitindo progresso em tempo real e conectando inbound webhooks para retomar execuções. |
| 5 | **Fase 5: Memória de Longo Prazo & Plugins** | Implementar o serviço assíncrono de consolidação de memórias episódicas e expor o ecossistema de Plugins para extensibilidade. |
| 6 | **Fase Transversal: Governança & Consolidação Documental** | Executar tarefas contínuas de rastreabilidade (vinculação de Commits/Issues), criação de especificações BDD (`.feature`), validação de cobertura de testes (min 80%), documentação técnica de APIs/funcionalidades e sincronização de índices globais (`INDEX.md`, `CONSOLIDATED_DOCS.md`). |

---

## Detalhamento: Integração Unificada do Chat

### Por que implementar?

Sem essa integração, o chat do usuário final funciona apenas como uma interface estática "pergunta-e-resposta", incapaz de realizar ações no mundo real, disparar workflows complexos da empresa ou extrair de forma contextualizada informações seguras restritas por Salas de Conhecimento e de forma otimizada com reranking local.

### Arquitetura-alvo

```
[ Usuário (Frontend React) ] 
       │ 
       ▼ (SignalR /hubs/chat)
[ ChatHub ]
       │ 
       ▼ (ITenantContextAccessor)
[ MetaAgentOrchestrator ] 
  ├── Triagem de Complexidade (ISmartRouter)
  │     ├── FastPath (Local ONNX)
  │     └── Specialist Dispatcher
  │
  ├── Context Enriched (ISkillManager)
  │     ├── C# ISkill (Compilado de Fábrica)
  │     └── Dynamic C# Skills (Registradas no Banco)
  │
  ├── RAG Context (RAGContextProvider)
  │     ├── PostgresVectorStore (pgvector)
  │     └── Reranker (ONNX Cross-Encoder)
  │
  ├── Tooling (IToolManager)
  │     ├── C# Native Tools
  │     ├── TenantAnalyticsTool (SQL Seguro)
  │     └── StitchMCP Gateway (MCP Protocol)
  │
  └── Workflow Engine (IWorkflowEngine)
        └── Event Streaming (/hubs/workflow)
```

### Componentes propostos

| Componente | Papel |
|---|---|
| `ChatHub` | Estabelece escopo multi-tenant e propaga `TenantId` no fluxo assíncrono do SignalR. |
| `MetaAgentOrchestrator` | Orquestra a execução, analisando a intenção e ativando RAG, Specialists, Workflows ou Tools. |
| `SkillManager` | Carrega e injeta de forma contextualizada fragmentos de instruções (Skills C# nativas e dinâmicas) no system prompt. |
| `RAGContextProvider` | Intercepta requisições, detecta Salas de Conhecimento vinculadas ao agente e filtra chunks usando metadados `room_id`. |
| `LocalOnnxCrossEncoderReRankerProvider` | Re-ordena semanticamente chunks do RAG para maximizar precisão e evitar ruído no prompt do LLM. |
| `TenantAnalyticsTool` | Interface controlada e parametrizada para execução de queries analíticas no banco PostgreSQL sem risco de injeção. |
| `StitchMCP` | Traduz chamadas de ferramentas de agentes em requisições MCP padronizadas para servidores de terceiros. |
| `IWorkflowEngine` | Motor de execução sequencial e paralela de passos com suporte a compensações e streaming de eventos. |

---

## Critérios de Aceite e SLOs

* [x] **SLO de Latência RAG:** A busca híbrida (pgvector + FTS GIN) combinada com reranking ONNX local em processo não deve adicionar mais de **400ms** de latência à geração inicial.
* [x] **Isolamento de Tenant (Zero Data Leak):** Cobertura de testes unitários garantindo que queries analíticas SQL, buscas vetoriais e resumos de memória episódica contenham obrigatoriamente a cláusula `WHERE tenant_id = X`.
* [x] **Sincronização de Progresso (UX):** As atualizações de passos em workflows assíncronos complexos devem ser exibidas na UI do chat em menos de **100ms** após o disparo no backend.
* [x] **Resiliência MCP (Circuit Breaker):** Se um servidor MCP de terceiros falhar ou exceder timeout de **3000ms**, o agente deve abortar a ferramenta graciosamente e notificar o usuário com um fallback adequado.

---

## Governança Documental, BDD & Qualidade (Transversal)

Para garantir que a integração unificada dos 12 pilares permaneça perfeitamente documentada, rastreável e robusta de ponta a ponta, cada entrega de fase deve cumprir obrigatoriamente as seguintes tarefas documentais:

1. **Especificações de Comportamento (BDD Features):**
   - Criação de especificações BDD usando o template `templates/bdd-template.feature` na pasta de testes para documentar cenários comportamentais do Chat unificado (ex: fluxos multi-tenant, transição de estados de workflows e fallback de ferramentas MCP).
2. **Documentação Técnica e Padrões (12 Pilares):**
   - Documentar detalhadamente na pasta `docs/architecture/` ou `docs/features/` o funcionamento prático de cada pilar técnico focado no chat conversacional.
   - Manter atualizado o arquivo formal de decisão técnica [adr-022-unified-chat-integration-architecture.md](file:///c:/Users/Jonathan/Documents/Developer/GitHub/Agent-System/docs/architecture/adr/022-unified-chat-integration-architecture.md).
3. **Plano de Testes e Validação:**
   - Documentar a cobertura de testes unitários e de integração de cada componente modificado (mantendo a barreira mínima exigida de **80% de cobertura** para backend e frontend).
   - Especificar casos de testes manuais e automatizados para a interface do SignalR.
4. **Sincronização de Índices Gerais (MANDATÓRIO):**
   - Atualizar periodicamente o catálogo e status de documentações em `README.md`, `docs/INDEX.md` e `docs/CONSOLIDATED_DOCS.md` para assegurar que novas especificações constem na árvore viva do repositório.

---

## Riscos e Mitigações

| Risco | Mitigação |
|---|---|
| **SQL Injection e Vazamento Multi-Tenant via SQL livre.** | Banir queries SQL brutas escritas por LLMs. Utilizar exclusivamente a `TenantAnalyticsTool` desenvolvida em C# com consultas estáticas altamente parametrizadas e seguras sob o escopo do tenant. |
| **Estouro de Janela de Contexto (Tokens LLM) devido a excesso de dados (RAG + Memória + Skills).** | Utilizar o `ContextBudgetManager` atrelado ao `ISemanticCompressorService` para resumir e truncar agressivamente partes redundantes do histórico e dos chunks recuperados. |
| **Gargalo de I/O em loops de ferramentas concorrentes de multi-agentes (Debates).** | Limitar o número máximo de iterações de debate (`MaxDebateRounds = 3`) e aplicar timeouts rigorosos em cada agente especialista. |
| **Servidores MCP fora do ar interrompendo execuções críticas.** | Mapear ferramentas MCP como opcionais. Fornecer ferramentas nativas equivalentes como fallback local direto no backend C#. |
