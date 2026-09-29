# ADR-040 — Aprovação humana para auto-melhoria de agentes

Data: 2026-09-29 · Issue: [#16](https://github.com/JonathanBenicio/Agent-System/issues/16) · Story: ML39 em [USER-STORIES.md](../../USER-STORIES.md#ml39-finops--auto-melhoria) · [Especificações das issues](../../plan/open-issues-specification-audit-2026-09-29.md#issue-16).

## Status

Aceito para a política de produto; papel específico do aprovador pendente. Implementação atual não cumpre esta decisão.

## Contexto

O job de Self-Improvement processa reflexões e usa `AutoApplyThreshold` para tentar aplicar sugestões. `ApplyImprovementAsync` atualmente é no-op, portanto o comportamento do código não é uma aplicação real nem um fluxo confiável de aprovação. Mesmo após a implementação de apply, confiança calculada não deve alterar instruções/versões do agente sem revisão humana.

## Decisão

- A análise em batch pode gerar uma **proposta** tenant-scoped; `confidence` serve para priorizar ou ordenar a revisão, nunca para autorizar mudança.
- Toda mudança exige aprovação humana antes de aplicação. Aprovar cria uma nova versão do agente; rejeitar preserva a versão ativa.
- Registrar tenant, agente, autor/origem, aprovador, decisão, timestamp, versão anterior/nova, rationale, evidência de avaliação e rollback.
- Nunca promover proposta ou mudança de um tenant para outro. Falha/timeout do job não pode aplicar conteúdo parcial.
- Remover `AutoApplyThreshold` como mecanismo de autoplicação; eventual threshold futuro não substitui autorização humana.

## Pontos ainda sem decisão

- Papel autorizado a aprovar: recomendação inicial é Owner/Admin do tenant. Confirmar se Operator também pode aprovar; Platform Admin não recebe acesso implícito ao conteúdo tenant.
- O schema, endpoints e UX de revisão/aprovação serão especificados numa etapa de implementação própria, depois de confirmar o papel.

## Consequências

- Sugestões podem acumular até revisão humana; isso reduz velocidade de adaptação, mas evita modificar comportamento de agentes com heurísticas de confiança sem responsabilização.
- A aplicação precisa versionar a configuração, permitir rollback e produzir evidência antes de declarar a mudança ativa.
- O job atual deve permanecer sem auto-aplicar. A API/UX de aprovação e o armazenamento auditável ainda precisam ser implementados.

## Verificação necessária

- Confiança acima do threshold continua gerando somente proposta.
- Usuário sem papel aprovado recebe negação; aprovador válido altera apenas o tenant/agente autorizado.
- Aprovação guarda versão/hashes e permite rollback; rejeição não modifica agente.
- Jobs concorrentes/retries não aplicam proposta duas vezes nem avançam cursor de outro tenant.
