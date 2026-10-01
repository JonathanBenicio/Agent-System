# ADR-040 — Aprovação humana para auto-melhoria de agentes

Data: 2026-09-29 · Issue: [#16](https://github.com/JonathanBenicio/Agent-System/issues/16) · Story: ML39 em [USER-STORIES.md](../../USER-STORIES.md#ml39-finops--auto-melhoria) · [Especificações das issues](../../plan/open-issues-specification-audit-2026-09-29.md#issue-16).

## Status

Aceito. Fluxo de proposta, revisão Owner/Admin, persistência/versionamento, auditoria e rollback está implementado na pilha do PR #132; forecast de quota é follow-up separado em #135.

## Contexto

O plano histórico `self-improvement-async-job.md` descrevia auto-apply por `AutoApplyThreshold`; essa regra foi rejeitada. O fluxo atual processa reflexões críticas em background e gera propostas tenant-scoped. Mesmo que `confidence` seja calculada, ela só prioriza a revisão e nunca altera instruções/versões do agente sem aprovação humana.

## Decisão

- A análise em batch pode gerar uma **proposta** tenant-scoped; `confidence` serve para priorizar ou ordenar a revisão, nunca para autorizar mudança.
- Toda mudança exige aprovação humana antes de aplicação. O aprovador é Owner/Admin do tenant; aprovação cria uma nova versão do agente, rejeição preserva a versão ativa.
- Registrar tenant, agente, autor/origem, aprovador, decisão, timestamp, versão anterior/nova, rationale, evidência de avaliação e rollback.
- Nunca promover proposta ou mudança de um tenant para outro. Falha/timeout do job não pode aplicar conteúdo parcial.
- Remover `AutoApplyThreshold` como mecanismo de autoplicação; eventual threshold futuro não substitui autorização humana.

## Fora do escopo desta decisão

- UX de forecast proativo de quota e trabalho de triagem específico de `DotNetExpertAgent` não são aceitos por esta ADR; forecast está separado em #135 e a rota existente do agente já está coberta por ML35.
- Platform Admin não recebe acesso implícito ao conteúdo tenant; aprovação permanece sob papel Owner/Admin do tenant.

## Consequências

- Sugestões podem acumular até revisão humana; isso reduz velocidade de adaptação, mas evita modificar comportamento de agentes com heurísticas de confiança sem responsabilização.
- A aplicação versiona configurações, permite rollback e produz auditoria antes de declarar uma mudança ativa.
- O job permanece proposal-only; o endpoint de aprovação não substitui autorização Owner/Admin. O plano antigo foi marcado `SUPERSEDED` para que o critério de auto-apply não volte a orientar implementação.

## Verificação necessária

- Confiança acima do threshold continua gerando somente proposta.
- Usuário sem papel aprovado recebe negação; aprovador válido altera apenas o tenant/agente autorizado.
- Aprovação guarda versão/hashes e permite rollback; rejeição não modifica agente.
- Jobs concorrentes/retries não aplicam proposta duas vezes nem avançam cursor de outro tenant.
