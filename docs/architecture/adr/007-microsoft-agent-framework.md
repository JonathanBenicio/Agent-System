# ADR-007: Orquestração com Microsoft Agent Framework

## Status
Accepted (Retrospective)

## Context
A coordenação de múltiplos agentes, delegando tarefas e consolidando respostas, é um problema complexo. Inicialmente construímos soluções customizadas, mas precisávamos de uma base padronizada e robusta para escalar a inteligência do sistema.

## Decision
Adotamos o **Microsoft Agent Framework** (como base para a orquestração de múltiplos agentes) para gerenciar o ciclo de vida e a comunicação entre os agentes especialistas.

## Rationale
1. **Padronização**: Segue padrões de mercado estabelecidos pela Microsoft para sistemas multi-agente.
2. **Abstração de Complexidade**: Facilita a criação de conversas em grupo, delegação de tarefas e tratamento de erros.
3. **Ecosistema**: Integração nativa com outras ferramentas do ecossistema .NET.

## Trade-offs
- **Acoplamento**: Dependência de uma biblioteca específica que dita o fluxo de trabalho.
- **Curva de Aprendizado**: Exige que os desenvolvedores entendam os conceitos específicos do framework da Microsoft.

## Consequences
- **Positive**: Desenvolvimento mais rápido de novos comportamentos de agentes; maior estabilidade na orquestração complexa.
- **Negative**: Menor flexibilidade para desvios extremos do padrão do framework.
- **Mitigation**: Criação de wrappers ou interfaces próprias para isolar a regra de negócio do framework específico quando possível.
