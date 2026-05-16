# ADR-008: Monitoramento de Cotas e FinOps para LLMs

## Status
Accepted (Retrospective)

## Context
O uso de Large Language Models (LLMs) comerciais pode gerar custos imprevisíveis e elevados se não houver controle estrito sobre a quantidade de tokens consumidos, chamadas por segundo e uso abusivo.

## Decision
Implementamos um sistema de **Monitoramento de Cotas Híbrido** e práticas de **FinOps** para rastrear, limitar e alertar sobre o uso de recursos de IA.

## Rationale
1. **Previsibilidade Financeira**: Permite definir orçamentos e evitar surpresas na fatura dos provedores.
2. **Segurança**: Protege o sistema contra loops infinitos de agentes que poderiam consumir milhares de dólares em poucos minutos.
3. **Justiça de Uso**: Garante que nenhum usuário ou agente monopolize os recursos disponíveis.

## Trade-offs
- **Sobrecarga de Infraestrutura**: Exige persistência e checagem de cotas a cada requisição (Redis/Postgres).
- **Experiência do Usuário**: Pode interromper tarefas legítimas se o limite for atingido de forma conservadora.

## Consequences
- **Positive**: Controle total sobre os custos de IA; maior segurança operacional.
- **Negative**: Código adicional para gerenciar estados de cotas e tratar exceções de limite excedido.
- **Mitigation**: Implementação de alertas preventivos (ex: 80% da cota) e renovação automática baseada em tempo (diária/mensal).
