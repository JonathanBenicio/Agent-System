# ADR-004: Adoção do Transactional Outbox Pattern

## Status
Accepted (Retrospective)

## Context
Durante a implementação da persistência e mensageria (Fase 2), precisávamos garantir que eventos de domínio fossem publicados de forma confiável após a persistência de dados no banco (PostgreSQL), evitando o problema de "Dual Write" (gravar no banco mas falhar ao enviar para o bus).

## Decision
Optamos por implementar o **Transactional Outbox Pattern** utilizando `IEventBus` e `TransactionScope`. Os eventos são salvos na mesma transação do banco de dados em uma tabela de "Outbox" e um background service os processa e envia para o broker real.

## Rationale
1. **Garantia de Entrega**: Evita perda de eventos se o broker estiver fora do ar.
2. **Consistência Eventual**: Alinha com os princípios de DDD para comunicação entre agregados e sistemas externos.
3. **Simplicidade Inicial**: Permite usar o próprio PostgreSQL como storage temporário sem precisar de um Kafka/RabbitMQ imediatamente.

## Trade-offs
- **Complexidade**: Exige tabelas extras e um worker processando em background.
- **Latência**: Pequeno atraso entre a gravação e a publicação real.

## Consequences
- **Positive**: Alta confiabilidade na comunicação entre componentes.
- **Negative**: Sobrecarga de I/O no banco para gerenciar a fila do outbox.
- **Mitigation**: Implementação de cleanup automático para eventos processados.
