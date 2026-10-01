# Plano — Validar A2A e AG-UI em hosting preview

Status: planejado · Issue: [#121](https://github.com/JonathanBenicio/Agent-System/issues/121) · [ADR-037](../architecture/adr/037-a2a-agui-preview-validation.md) · Story: BACK-PROTO-121. Dependência: core MAF/Gateway da [#120](maf-122-protocols-gateway.md).

## Objetivo

Validar os endpoints A2A e AG-UI de ponta a ponta depois da atualização do core MAF, mantendo explícito o risco de seus pacotes de hosting permanecerem preview. Esta validação não bloqueia a entrega de core/Gateway da #120.

## Etapas e aceite

| Entrega | Verificação | Estado |
|---|---|---|
| Smoke de host com pacote compatível | Restore/build e descoberta/roteamento dos endpoints sob flags | Pendente |
| Autenticação e tenant | Sem credencial, tenant inexistente/inativo ou membership são negados; usuário válido consegue executar | Pendente |
| Sessão/stream isolados | Dois tenants não retomam nem recebem mensagens/eventos do outro; cancelamento encerra stream | Pendente |
| Evidências e contratos | PostgreSQL/Ollama reais quando disponíveis; fixture, skip e não execução diferenciados; comportamento preview documentado | Pendente |

## Limites

Não alterar defaults ou expor os protocolos como estáveis apenas com base no build. Não fazer merge/deploy nesta issue. Se o ambiente real não estiver disponível, os cenários que não forem executados permanecem gaps abertos.
