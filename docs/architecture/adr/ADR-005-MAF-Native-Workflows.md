# ADR 005: Migração para Workflows Nativos do Microsoft Agent Framework (MAF 1.6+)

**Status:** Aprovado
**Data:** 24 de Maio de 2026
**Autor:** Antigravity (em colaboração com o usuário)

---

## Contexto

Durante as fases iniciais do projeto, o AgenticSystem implementou uma orquestração customizada de multi-agentes baseada em um construtor de grafos próprio (`AgentWorkflowBuilder`). Com a evolução para a adoção do MAF 1.6.1, a Microsoft introduziu a biblioteca oficial `Microsoft.Agents.AI.Workflows`. Esta biblioteca fornece primitivas nativas, como `WorkflowBuilder`, `WorkflowSession` e `RouteBuilder`, desenvolvidas especificamente para a construção robusta de fluxos declarativos e imperativos no ecossistema do .NET 10. Manter a solução customizada e imperativa em C# gera um débito técnico progressivo, duplicidade de esforço em manutenção e impede a integração fluida com as ferramentas de observabilidade e UI (como o DevUI) recém-anunciadas pelo framework.

## Decisão

Substituir completamente a abstração própria `AgentWorkflowBuilder` pela implementação oficial nativa `Microsoft.Agents.AI.Workflows.WorkflowBuilder`. 
As rotinas presentes em componentes essenciais da infraestrutura, especificamente `AgentCollaborationWorkflow.cs` e `OrchestratorHostBuilder.cs`, serão refatoradas para orquestrar as tarefas, instanciar agentes como nós (Nodes) e rotear as transições através das classes oficiais de Workflow do MAF.

## Justificativa

1. **Alinhamento Arquitetural Oficial:** Usar os pipelines fornecidos pelos engenheiros da Microsoft elimina a necessidade de manutenção de código boilerplate local relacionado à execução paralela, roteamento e handoffs.
2. **Integração Plena do Ecossistema:** A adoção do `WorkflowBuilder` nativo pavimenta o caminho para a Fase 4 (Durable Execution) e para integrações profundas com FIDES, Purview e ferramentas de debug visual.
3. **Escalabilidade de Estado:** A serialização dos fluxos nativos com `WorkflowSession` simplifica o desafio de retomada de tarefas interrompidas.

## Consequências

### Positivas
* Eliminação de código boilerplate e expressiva redução de débito técnico.
* O pipeline de execução reflete o padrão de mercado estabelecido pelo framework MAF.
* Compatibilidade imediata com ferramentas de DevUI oficiais.

### Desafios / Pontos de Atenção (Negativas)
* A forma como o contexto e a sessão fluem sofre alterações paradigmáticas, o que quebra os testes unitários (como `AgentCollaborationWorkflowTests`) que eram fortemente acoplados às assinaturas do sistema legado.
* Curva de aprendizado inicial da sintaxe da API fluente do `RouteBuilder` para recriar as passagens e *handoffs* entre agentes de forma fiel ao comportamento original.