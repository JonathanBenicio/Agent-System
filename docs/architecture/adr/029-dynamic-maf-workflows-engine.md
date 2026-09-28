# ADR 029: Orquestração Dinâmica de Grafos de Agentes baseada no Microsoft Agent Framework (MAF) (GitHub Issue #107)

**Status:** Proposto  
**Data:** 25 de Maio de 2026  
**Autor(es):** Antigravity AI & Jonathan Benicio  

---

## Contexto

O AgenticSystem utiliza o **Microsoft Agent Framework (MAF)** como runtime de orquestração de múltiplos agentes cooperativos. Embora o sistema de agentes seja dinâmico (permitindo registrar novos especialistas com prompts e modelos no banco de dados PostgreSQL via `IAgentFactory` e `IDynamicAgentRepository`), o mesmo não acontece com os **workflows estruturados**.

Atualmente, workflows complexos de colaboração e encadeamento em grafo (como a produção de banners que envolve análise visual por um agente e renderização/edição por outro) exigem a criação de classes C# imperativas e rígidas, como o [BannerProductionWorkflowService](file:///c:/Users/Jonathan/Documents/Developer/GitHub/Agent-System/src/AgenticSystem.Infrastructure/AI/BannerProductionWorkflowService.cs). 

Nessas classes, a instanciação do `OllamaChatClient`, a criação de instâncias de `ChatClientAgent` (com prompts inline) e a própria topologia do grafo de execução via `WorkflowBuilder` são codificados diretamente em C#. 

Isso gera um **gap de governança** e impede o cumprimento da premissa arquitetural máxima do projeto: *"Tudo deve ser dinâmico, configurável e criado via chat/PostgreSQL, sem depender de compilações em C# para evolução de regras de negócio."*

## Decisão

Adotaremos a **Orquestração de Grafos 100% Dinâmica**, expandindo a infraestrutura existente de `WorkflowDefinition` e `DefaultWorkflowEngine` para suportar a compilação e execução de grafos complexos do MAF de maneira totalmente declarativa (via banco de dados).

Para isso, implementaremos:

1. **Esquema Declarativo de Workflows (WorkflowDefinition):**
   Expandir o modelo JSON de `WorkflowDefinition` para permitir a descrição estruturada de nós que representam agentes (com prompts e modelos mapeados), ferramentas e as arestas de dependência (Edges) de transição.

2. **Compilador Dinâmico de Workflows (`DynamicMafWorkflowCompiler`):**
   Um novo serviço responsável por interpretar o JSON da definição, consultar o `IAgentFactory` para materializar os agentes especialistas definidos e compilar dinamicamente um grafo `Microsoft.Agents.AI.Workflows.Workflow` por meio do `WorkflowBuilder` do MAF em tempo de execução.

3. **Portabilidade do Processo:**
   Migrar o workflow atual do `banner-production` para este formato 100% declarativo no banco, possibilitando que a classe `BannerProductionWorkflowService` seja depreciada e removida sem perda de funcionalidade.

## Justificativa

1. **Alinhamento com a Premissa Dinâmica (Chat-Driven):** O chat agora pode criar e alterar fluxos inteiros de orquestração multi-agente escrevendo apenas na base de dados, sem necessidade de deploy ou alteração de código compilado.
2. **Reuso de Agentes e Ferramentas:** Workflows reutilizam agentes especialistas dinâmicos cadastrados no banco de dados em vez de declará-los inline de forma duplicada.
3. **Observabilidade Nativa Preservada:** Ao compilar as especificações declarativas do banco para objetos nativos do MAF (`Workflow`), mantemos todas as garantias nativas de observabilidade, tratamento de erro do SignalR e checkpoints do motor do MAF.

## Consequências

### Positivas
* **Zero Código Boilerplate:** A criação de novas campanhas ou processos orquestrados não exige novas classes C# nem compilação de infraestrutura.
* **Hot-swapping de Processos:** Modificações nas regras do pipeline, ordens de execução dos agentes ou prompts do workflow podem ser aplicadas instantaneamente a quente na produção.
* **Isolamento por Tenant:** Diferentes tenants podem possuir e executar topologias de grafos de agentes totalmente customizadas para seus negócios específicos.

### Desafios / Pontos de Atenção (Negativas)
* **Complexidade do Mapeamento:** O compilador dinâmico de workflows precisa validar rigorosamente a topologia do JSON para garantir que não existam ciclos infinitos ou dependências de arestas impossíveis de satisfazer.
* **Curva de Depuração:** Como os erros de compilação do workflow ocorrerão em tempo de execução baseados em dados do banco, precisaremos de um log de compilação rico para reportar falhas de estrutura ou falta de agentes.
