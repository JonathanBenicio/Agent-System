# ADR-016: Unificação da Estrutura de Agentes e Regras do Sistema

## Status
Accepted (Retrospective)

## Context
Conforme o sistema crescia, diferentes agentes estavam sendo implementados com estruturas variadas, formas distintas de lidar com memória e regras de comportamento inconsistentes. Isso dificultava a manutenção e a adição de novos agentes.

## Decision
Refatoramos o sistema para **Unificar a Estrutura de Agentes** e generalizar as regras do sistema, criando uma classe base comum e interfaces padronizadas para todos os agentes.

## Rationale
1. **Consistência**: Todos os agentes passam a se comportar de maneira previsível em relação ao ciclo de vida e tratamento de mensagens.
2. **Facilidade de Desenvolvimento**: Criar um novo agente se torna uma tarefa de preencher lacunas (herança/implementação) em vez de inventar uma nova arquitetura.
3. **Manutenibilidade**: Correções na lógica de orquestração ou memória aplicadas na base beneficiam todos os agentes automaticamente.

## Trade-offs
- **Rigidez**: Agentes que precisem de comportamentos muito fora da curva podem encontrar dificuldades para se encaixar no modelo unificado.
- **Esforço de Refatoração**: Exigiu alterar todos os agentes existentes para o novo padrão.

## Consequences
- **Positive**: Código muito mais limpo e modular; redução de duplicação de código.
- **Negative**: Curva de aprendizado inicial para entender o framework interno de agentes antes de criar um novo.
- **Mitigation**: Documentação de templates e exemplos claros de como estender a classe base para criar novos especialistas.
