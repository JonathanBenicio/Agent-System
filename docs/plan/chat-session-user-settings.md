# Plano — Chat, sessões e configurações usadas pelo runtime

Status: funcionalidade incluída e validada no [PR #132](https://github.com/JonathanBenicio/Agent-System/pull/132) para `develop`; lint e build global do frontend passaram na árvore consolidada. O PR #124 de origem continua draft e não é o veículo final desta entrega. Issue #123 permanece aberta até revisão/merge de #132 · [ADR-039](../architecture/adr/039-chat-session-user-tenant-settings.md) · Story BACK-CHAT-123 · [evidência](../backend/validation/chat-session-settings-2026-09-29.md).

Base: `fix/backend-core-tenancy` no commit `5701d51`; branch `feat/chat-session-user-settings`. PostgreSQL de validação: Compose `tests/backend-validation/compose.yml`, projeto `agent-system-backend-validation-20260929`, bind `127.0.0.1:55432`; não usar outro banco.

| Entrega | Mudança e prova concreta | Estado |
|---|---|---|
| Chat e sessão | Contrato REST/SignalR, erros, ID servidor; criar/listar/abrir/retomar/encerrar e preservar histórico isolado | Implementado; Cypress conversou duas vezes, recarregou a página, reabriu a sessão e restaurou as duas mensagens. HTTP smoke provou end-session com histórico preservado e chat encerrado negado. |
| BYOK de tenant | Owner/Admin cadastra, atualiza, valida, descobre modelos e remove; segredo não retorna; default é efetivamente usado | Implementado; HTTP smoke e stub local provaram chave default atualizada no header de ambas as chamadas, modelo efetivo, DTO sem segredo e Viewer 403. Login da API key retorna subject id para cache/identidade. |
| Modelos e providers | Catálogo seguro; preferência `(tenant,user)` persistida e aplicada; validação de provider/model | Implementado; store PostgreSQL com chave composta e teste de isolamento, REST e Cypress comprovaram persistência e uso de OpenAI/validation-model. |
| Skills | Owner/Admin ativa/desativa; somente instruções ativas chegam ao contexto; permissões de tools separadas | Implementado; smoke inspecionou a mensagem do provider: skill ativa presente, skill desativada ausente em sessão nova; Cypress confirmou toggle na tela; Viewer 403. |
| Frontend | Chat, sessões e telas de chave/modelo/skills integrados aos contratos de tenant | Build + ESLint dos arquivos tocados passaram; Cypress 1/1 cobriu modelo, agente direto, chat, sessão reaberta, key metadata sem segredo e toggle de skill. |
| Verificação de origem | Build/teste PostgreSQL/Ollama, contratos e rastreabilidade na branch de origem | Snapshot de 2026-09-29: build Release limpo; 763 aprovados/1 skip; migrations EF sem pendências; inventário 216 rotas; smoke HTTP e Cypress UI 1/1. O ESLint global apontou 22 erros/1 warning nesse snapshot. |
| Integração para `develop` | Verificação consolidada em #132 | Suíte .NET 771 aprovados/16 ignorados; Cypress #123 1/1; lint e build global do frontend passaram. Consulte a atualização no [relatório de validação](../backend/validation/chat-session-settings-2026-09-29.md). |

Critérios de aceite separados: (1) preferência/chave/skill/sessão foi validada e salva; (2) a execução seguinte leu o valor salvo e o aplicou ao LLM/agente; (3) outro usuário ou tenant não pode ler nem alterar o recurso. A2A/AG-UI, administração global, merge e deploy estão fora deste plano.
