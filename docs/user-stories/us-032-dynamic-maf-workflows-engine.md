# US-032 (Issue #107): Orquestração Dinâmica de Grafos de Agentes e Compilador MAF Declarativo

**Épico:** Orquestração Multi-Agent & Workflows Dinâmicos (GitHub Epic #107)  
**Prioridade:** Alta  
**Estimativa (Story Points):** 8

## Descrição

**Como um** administrador ou usuário desenvolvedor da plataforma AgenticSystem,  
**Eu quero** cadastrar, listar e estruturar definições de workflows em formato de grafos declarativos (JSON) no banco de dados,  
**Para que** o sistema utilize um compilador dinâmico em tempo de execução para materializar, configurar e encadear os subagentes do Microsoft Agent Framework (MAF) e suas respectivas ferramentas, eliminando qualquer dependência de classes estáticas C# para a orquestração de processos.

## Regras de Negócio e Contexto

* **Grafo Declarativo no Postgres:** O JSON de `WorkflowDefinition` deve suportar uma coleção de nós (`Nodes`) e conexões (`Edges`). Cada nó pode ser do tipo `Agent` (referenciando um agente especialista do banco) ou `Action` (referenciando uma ferramenta de infraestrutura ou customizada).
* **Compilação sob Demanda (Just-in-Time):** No startup ou no momento da execução, o motor de workflows do MAF (`DefaultWorkflowEngine`) delegará o grafo para o `DynamicMafWorkflowCompiler`. Esse componente resolverá cada nó de agente via `IAgentFactory` (resgatando os prompts, as LLMs configuradas como `llama3.2-vision` ou `llama3-8b` e as chaves do Postgres) e vinculará as ferramentas corretas para compilar o objeto nativo `Microsoft.Agents.AI.Workflows.Workflow` usando o `WorkflowBuilder` em memória.
* **Validação de Ciclos e Estrutura:** O compilador dinâmico de workflows deve conter um algoritmo de ordenação topológica (ex: DFS/Kahn) para detectar e rejeitar grafos ciclicamente inválidos ou com nós órfãos antes de registrar a definição ou iniciar a execução.
* **Streaming de Eventos via SignalR:** Toda transição de nó do grafo (ex: `VisionAnalyst` concluiu a análise visual e passou a bola para o `EditorChefe`) deve emitir eventos estruturados em tempo real no SignalR Hub (`/hubs/workflow`), contendo o status de progresso, latência da etapa e outputs parciais.
* **Isolamento de Tenants:** O compilador dinâmico deve carregar apenas agentes e ferramentas que estejam associados ou sejam visíveis ao `TenantId` da execução corrente.
* **Retrocompatibilidade e Descontinuação de Código Rígido:** A especificação do workflow `banner-production` inserida no seed do banco inicial no bootstrap deve ser expandida para descrever a cooperação completa em grafo. Isso permitirá descontinuar e apagar com segurança o [BannerProductionWorkflowService.cs](file:///c:/Users/Jonathan/Documents/Developer/GitHub/Agent-System/src/AgenticSystem.Infrastructure/AI/BannerProductionWorkflowService.cs).

## Critérios de Aceite (DoD)

- [ ] **Critério 1 (Esquema de Grafo Estendido):** O modelo `WorkflowDefinition` no banco de dados e na API suporta a declaração de múltiplos nós de agentes com links direcionais (`Edges`) especificando a ordem de orquestração do MAF.
- [ ] **Critério 2 (Validação de Grafo):** A API valida a integridade do grafo em requisições de criação ou atualização. Grafos com loops infinitos ou nós desconectados retornam erro HTTP 400 com diagnóstico detalhado.
- [ ] **Critério 3 (Compilador Dinâmico MAF):** O `DynamicMafWorkflowCompiler` instancia com sucesso os agentes declarados por meio do `IAgentFactory` (respeitando seus modelos individuais como `llama3.2-vision` ou `llama3-8b`), injeta suas respectivas `AIFunctions` e constrói dinamicamente o fluxo do MAF.
- [ ] **Critério 4 (SignalR Progress):** O SignalR Hub transmite com sucesso eventos detalhados de transição de estado de cada nó (`StepStarted`, `StepCompleted`, `StepFailed`) no pipeline para permitir o monitoramento em tempo real no frontend.
- [ ] **Critério 5 (Depreciação Completa do Serviço Estático):** O pipeline de produção de banner roda com sucesso no motor dinâmico e o arquivo [BannerProductionWorkflowService.cs](file:///c:/Users/Jonathan/Documents/Developer/GitHub/Agent-System/src/AgenticSystem.Infrastructure/AI/BannerProductionWorkflowService.cs) pode ser excluído da base de código sem quebrar a compilação.
- [ ] **Critério 6 (Isolamento Multi-tenant):** A execução dinâmica do workflow valida e restringe os agentes e chaves do provedor LLM ao `TenantId` ativo.

## Dependências Técnicas

* [ ] ADR 029 (Orquestração Dinâmica de Grafos de Agentes baseada no MAF)
* [ ] Injeção de dependência de `IAgentFactory` e `IToolDiscoveryService` no compilador de workflows.
* [ ] Atualização do seed de dados do bootstrap para suportar a nova estrutura de grafo estendido.
