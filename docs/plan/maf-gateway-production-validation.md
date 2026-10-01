# Plano — Gateway de produção e provider global

Status: planejado para execução após o merge do PR #132.
Issue: [#133](https://github.com/JonathanBenicio/Agent-System/issues/133), follow-up de #120 · ADR: [ADR-036](../architecture/adr/036-maf-122-protocols-and-gateway.md) · Story: BACK-MAF-133 em [USER-STORIES.md](../USER-STORIES.md).

## Objetivo e escopo

Comprovar que a composição normal da API registra e encaminha providers de plataforma pelo Gateway, com atualização multi-host; BYOK e quota de tenant continuam isolados. Validação usa provider stub local OpenAI-compatible e registra que isso não é provider externo real.

## Etapas

| Entrega | Dependência | Verificação | Estado/evidência |
|---|---|---|---|
| Reconciliar o registro do serviço na API normal e no host de validação | #120/#132 | Provider habilitado registrado; provider desabilitado ausente | Pendente |
| Validar geração completa e streaming pelo Gateway | Registro normal | Stub local confirma provider, modelo e headers de escopo | Pendente |
| Validar atualização da configuração global em dois hosts | PostgreSQL Compose isolado | NOTIFY propaga alteração sem restart e sem vazar tenants | Pendente |
| Documentar limites da evidência | Cenários anteriores | Relatório distingue stub local de provider externo real | Pendente |

## Critérios de aceite

- [ ] Provider global do PlatformConfigStore passa pela composição de produção do Gateway.
- [ ] BYOK conserva identidade/tenant e não reutiliza quota/circuit state entre tenants.
- [ ] Reload por NOTIFY é observado em dois hosts de teste.
- [ ] Build, testes direcionados e relatório de evidências passam; A2A/AG-UI ficam em #121.
