# Plano — Continuidade do supervisor MAF entre sessões e processos

Status: planejado para execução após o merge do PR #132.
Issue: [#134](https://github.com/JonathanBenicio/Agent-System/issues/134), follow-up de #122 · ADR: [ADR-038](../architecture/adr/038-dynamic-supervisor-orchestrator.md) · Story: BACK-ORCH-134 em [USER-STORIES.md](../USER-STORIES.md).

## Objetivo e escopo

Validar a sessão/fingerprint do supervisor e dos especialistas realmente invocados sob tenant + usuário, incluindo retomada após restart real. Não declarar exactly-once após efeitos externos; exigir idempotência ou compensação.

## Etapas

| Entrega | Dependência | Verificação | Estado/evidência |
|---|---|---|---|
| Confirmar catálogo e fingerprint dos especialistas ativos/bindados | #122 | Mudança de descrição/capability atualiza instrução sem restart | Pendente |
| Persistir supervisor e especialistas chamados | PostgreSQL isolado | Retoma somente sessões executadas, owner/tenant preservados | Pendente |
| Exercitar restart, quota, provider error e cancelamento | Persistência | Estado é recuperado ou falha explicitamente; sem sucesso vazio | Pendente |
| Documentar semântica de efeito externo | Cenários de recovery | Entrega at-least-once, idempotência/compensação; sem claim exactly-once | Pendente |

## Critérios de aceite

- [ ] Especialistas inativos/sem binding não são anunciados.
- [ ] Sessões retomadas mantêm isolamento de tenant/usuário após restart.
- [ ] Erro, quota e cancelamento têm resultado explícito e observável.
- [ ] Build, testes de PostgreSQL e relatório de gaps passam; protocolos preview seguem em #121.
