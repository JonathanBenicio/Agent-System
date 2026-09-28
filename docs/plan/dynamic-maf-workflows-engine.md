# Roadmap: Orquestração Dinâmica de Grafos de Agentes (Abordagem B)

> **Status documental:** Draft (Planejamento)  
> **Escopo:** Backend (.NET 10, Core, Infrastructure, Api) e persistência em PostgreSQL.  
> **Fonte de verdade operacional:** [ADR 029](../architecture/adr/029-dynamic-maf-workflows-engine.md) e [US-032](../user-stories/us-032-dynamic-maf-workflows-engine.md).  
> **Gerado em:** 25 de Maio de 2026  
> **Projeto:** AgenticSystem  

---

## Objetivo

Unificar o motor do **Microsoft Agent Framework (MAF)** com o catálogo de agentes e a persistência relacional do **PostgreSQL**. A iniciativa visa permitir que workflows complexos de múltiplos agentes sejam definidos e editados dinamicamente via chat/banco em tempo de execução (just-in-time), eliminando a necessidade de codificar ou compilar novas classes C# estáticas como [BannerProductionWorkflowService.cs](../../src/AgenticSystem.Infrastructure/AI/BannerProductionWorkflowService.cs).

## Princípios de Implantação

1. **Segurança de Grafo (DAG):** Validação topológica obrigatória no momento de salvar ou atualizar o workflow para prevenir ciclos de dependência infinitos ou nós desconectados.
2. **Isolamento de Tenant Absoluto:** Compilador e motor de workflows operam restritos aos agentes e chaves LLM vinculados ao `TenantId` da sessão em execução.
3. **Protocolo de Mensagens Padronizado:** A comunicação e o hand-off de dados entre nós do grafo dinâmico utilizam `ChatMessage` (Microsoft.Extensions.AI) como contrato universal.
4. **Descontinuação Limpa:** O código de infraestrutura acoplado herdado (Banner Workflow estático) será removido do backend após validação de conformidade.

## Fases e Sequenciamento

| Ordem | Frente/Fase | Motivo do sequenciamento |
|---|---|---|
| 1 | **Fase 1: Mapeamento e Validação de Grafos** | Garantir que o JSON da especificação de workflows estendidos seja parseado e validado logicamente contra ciclos antes de codificar a compilação do MAF. |
| 2 | **Fase 2: Compilador Dinâmico MAF** | Desenvolver o interpretador (`DynamicMafWorkflowCompiler`) capaz de ler o grafo, resolver os agentes via `IAgentFactory` e gerar o objeto `Workflow` nativo do MAF. |
| 3 | **Fase 3: Motor & SignalR Progress Streaming** | Integrar o compilador no ciclo de execução do `DefaultWorkflowEngine` e garantir que o SignalR Hub transmita o status de cada nó para o frontend. |
| 4 | **Fase 4: Portabilidade do Banner Production** | Substituir o bootstrap estático do banner por um grafo dinâmico correspondente, atualizando a tool e removendo a classe legado `BannerProductionWorkflowService.cs`. |
| 5 | **Fase 5: Testes e Validação Completa** | Execução de testes de integração automatizados e manual E2E de ponta a ponta. |

---

## Detalhamento: Orquestração Dinâmica de Grafos de Agentes

### Por que implementar?
Reduzir o tempo de criação de novos fluxos de agentes inteligentes de dias (exigindo ciclo de desenvolvimento, testes em C#, PR e deploy de backend) para segundos (apenas salvando o novo grafo de orquestração via chat diretamente no PostgreSQL do tenant).

### Arquitetura-alvo

```
[Chat / API Client] ──> [DefaultWorkflowEngine] ──> Consulta [WorkflowDefinition] (PostgreSQL)
                                │
                                ├──> Chama [DynamicMafWorkflowCompiler]
                                │          │
                                │          ├──> Busca Agentes via [IAgentFactory]
                                │          └──> Compila em runtime [Microsoft.Agents.AI.Workflows.Workflow]
                                │
                                └──> Executa [InProcessExecution.RunAsync]
                                           │
                                           └──> Stream de progresso no [SignalR Hub] (/hubs/workflow)
```

### Componentes propostos

| Componente | Papel |
|---|---|
| `WorkflowGraphValidator` | Executa ordenação topológica (algoritmo de Kahn/DFS) no JSON do grafo para detectar ciclos ou nós isolados antes do salvamento da definição. |
| `DynamicMafWorkflowCompiler` | Lê o grafo validado, materializa os nós de `Agent` através do `IAgentFactory`, mapeia suas tools compatíveis e injeta no `WorkflowBuilder` para retornar o `Workflow` nativo do MAF pronto para rodar. |
| `DefaultWorkflowEngine` (Refatorado) | Utiliza o compilador em tempo de execução para rodar workflows em memória de forma transacional e multi-tenant. |
| `BannerProductionTool` (Atualizado) | Em vez de rodar o serviço estático, chama o `DefaultWorkflowEngine` passando a definição dinámica `"banner-production"`. |

### Plano por etapas

1. **Modelagem & Validação:**
   * Atualizar `WorkflowDefinitionEntity` no PostgreSQL para conter o novo modelo de nós e edges detalhado.
   * Criar a classe utilitária `WorkflowGraphValidator` no projeto `AgenticSystem.Core`.
2. **Compilador Dinâmico:**
   * Desenvolver `DynamicMafWorkflowCompiler` no projeto `AgenticSystem.Infrastructure`.
   * Integrar a resolução de dependências de ferramentas e agentes dinâmicos através da factory.
3. **Fluxo de Runtime:**
   * Modificar `DefaultWorkflowEngine` para invocar o compilador e executar o grafo de forma resiliente.
   * Rastrear métricas de execução por nó do grafo no `SignalRWorkflowEventBroadcaster`.
4. **Bootstrapping & Limpeza:**
   * Atualizar o seed do `SystemBootstrapService.cs` para popular a estrutura de grafo do `banner-production` no banco.
   * Atualizar a classe `BannerProductionTool.cs` para invocar o motor de workflows dinâmicos.
   * Apagar o arquivo `BannerProductionWorkflowService.cs`.
5. **Verificação de Regressões:**
   * Executar os 608 testes existentes para garantir que nenhuma regressão foi introduzida no core do sistema.

### Critérios de Aceite e SLOs
* [ ] O banco de dados PostgreSQL aceita o novo formato do seed do workflow de banner sem erros de migração.
* [ ] O compilador valida e rejeita um workflow artificialmente criado que contenha dependências circulares (ex: A -> B -> A).
* [ ] A execução do pipeline de banner dinâmico é concluída com sucesso em < 10 segundos no Ollama.
* [ ] O arquivo `BannerProductionWorkflowService.cs` é deletado e a API compila perfeitamente sem erros de DI ou referências.

### Riscos e Mitigações

| Risco | Mitigação |
|---|---|
| Desempenho no roteamento dinâmico em tempo de execução | Compiladores de workflows utilizam cache local em memória (`ConcurrentDictionary`) para armazenar o grafo compilado, invalidando-o apenas quando a definição do banco for alterada. |
| Erros de configuração de prompts que geram falhas no MAF | O validador na API verifica a presença física e a assinatura dos agentes referenciados no catálogo antes de salvar a topologia do workflow. |
