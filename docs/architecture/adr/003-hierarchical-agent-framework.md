# ADR-003: Implementação de Framework de Agentes Hierárquicos

## Status
Accepted (Retrospective)

## Context
No início do projeto, precisávamos de uma estrutura para gerenciar múltiplos agentes com diferentes especialidades. A abordagem ingênua de um único agente "faz-tudo" não escala e resulta em respostas de baixa qualidade para tarefas complexas.

## Decision
Decidimos implementar um **Framework de Agentes Hierárquicos** baseado em uma classe base (`BaseAgent`), tipos de agentes especializados e uma fábrica de orquestração (`HierarchicalAgentFactory`).

## Rationale
1. **Especialização**: Permite que cada agente foque em um domínio específico (ex: .NET, Frontend, Segurança), melhorando a precisão.
2. **Escalabilidade**: Facilita a adição de novos agentes sem impactar os existentes.
3. **Orquestração**: Um agente mestre (Orchestrator) pode decompor tarefas e delegar para os especialistas, imitando uma equipe humana.

## Trade-offs
- **Complexidade de Orquestração**: Gerenciar o estado e o contexto entre múltiplos agentes é complexo.
- **Latência**: Múltiplas chamadas de agentes aumentam o tempo de resposta final.

## Consequences
- **Positive**: Sistema altamente modular e extensível; respostas mais precisas para tarefas complexas.
- **Negative**: Dificuldade em rastrear o fluxo de conversação completo e debugar falhas de handoff.
- **Mitigation**: Implementação de rastreamento de telemetria e logs estruturados para o ciclo de vida do agente.
