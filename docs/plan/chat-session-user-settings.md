# Plano — Chat, sessões e configurações usadas pelo runtime

Status: fluxos de usuário/tenant implementados e integrados; falta sincronizar o PR draft e resolver o gate global de lint · [Issue #123](https://github.com/JonathanBenicio/Agent-System/issues/123) · [ADR-039](../architecture/adr/039-chat-session-user-tenant-settings.md) · Story BACK-CHAT-123 · [evidência](../backend/validation/chat-session-settings-2026-09-29.md).

Base: `fix/backend-core-tenancy` no commit `5701d51`; branch `feat/chat-session-user-settings`. PostgreSQL de validação: Compose `tests/backend-validation/compose.yml`, projeto `agent-system-backend-validation-20260929`, bind `127.0.0.1:55432`; não usar outro banco.

| Entrega | Mudança e prova concreta | Estado |
|---|---|---|
| Chat e sessão | Contrato REST/SignalR, erros, ID servidor; criar/listar/abrir/retomar/encerrar e preservar histórico isolado | Implementado; Cypress conversou duas vezes, recarregou a página, reabriu a sessão e restaurou as duas mensagens. HTTP smoke provou end-session com histórico preservado e chat encerrado negado. |
| BYOK de tenant | Owner/Admin cadastra, atualiza, valida, descobre modelos e remove; segredo não retorna; default é efetivamente usado | Implementado; HTTP smoke e stub local provaram chave default atualizada no header de ambas as chamadas, modelo efetivo, DTO sem segredo e Viewer 403. Login da API key retorna subject id para cache/identidade. |
| Modelos e providers | Catálogo seguro; preferência `(tenant,user)` persistida e aplicada; validação de provider/model | Implementado; store PostgreSQL com chave composta e teste de isolamento, REST e Cypress comprovaram persistência e uso de OpenAI/validation-model. |
| Skills | Owner/Admin ativa/desativa; somente instruções ativas chegam ao contexto; permissões de tools separadas | Implementado; smoke inspecionou a mensagem do provider: skill ativa presente, skill desativada ausente em sessão nova; Cypress confirmou toggle na tela; Viewer 403. |
| Frontend | Chat, sessões e telas de chave/modelo/skills integrados aos contratos de tenant | Build + ESLint dos arquivos tocados passaram; Cypress 1/1 cobriu modelo, agente direto, chat, sessão reaberta, key metadata sem segredo e toggle de skill. |
| Verificação e entrega | Build/teste PostgreSQL/Ollama, contratos, rastreabilidade, commits e PR draft | Build Release limpo; 763 aprovados/1 skip; migrations EF sem pendências; inventário 216 rotas e docs de endpoints atualizados; smoke HTTP e Cypress UI 1/1 passaram. ESLint global segue com 22 erros/1 warning em arquivos fora do escopo; PR deve permanecer draft até esse gate ser resolvido ou separado. |

Critérios de aceite separados: (1) preferência/chave/skill/sessão foi validada e salva; (2) a execução seguinte leu o valor salvo e o aplicou ao LLM/agente; (3) outro usuário ou tenant não pode ler nem alterar o recurso. A2A/AG-UI, administração global, merge e deploy estão fora deste plano.
