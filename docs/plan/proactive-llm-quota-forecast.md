# Plano — Previsão proativa de esgotamento de quota LLM

Status: planejado.
Issue: [#135](https://github.com/JonathanBenicio/Agent-System/issues/135), extraída de #16 · ADR de referência: [ADR-008](../architecture/adr/008-quota-monitoring-finops.md) · Story: ML40 em [USER-STORIES.md](../USER-STORIES.md).

## Objetivo e escopo

Expor previsão tenant-scoped de consumo de LLM e alertar sobre possível esgotamento antes do limite rígido. O planejamento deve especificar janela, horizonte, método/confiança e canal de alerta antes da implementação. Previsão não bloqueia chamadas nem substitui quotas vigentes.

## Etapas

| Entrega | Dependência | Verificação | Estado/evidência |
|---|---|---|---|
| Definir contrato de forecast e mínimo de histórico | Issue #135 | Resposta distingue previsão confiável de dados insuficientes | Pendente |
| Especificar parâmetros tenant-scoped | Contrato | Plano como teto; configuração de quota mais restritiva prevalece | Pendente |
| Implementar cálculo/alerta sem enforcement implícito | ADR/Story aprovadas | Métricas e alerts não cruzam tenants; quotas rígidas inalteradas | Pendente |
| Validar histórico, virada de janela, alteração de plano e isolamento | Implementação | Testes de PostgreSQL e relatório publicados | Pendente |

## Critérios de aceite

- [ ] UI futura consome contrato documentado; não faz estimativa no cliente.
- [ ] Forecast apresenta origem dos dados/janela e estado de insuficiência.
- [ ] Notificações respeitam tenant e não bloqueiam requisições por previsão.
