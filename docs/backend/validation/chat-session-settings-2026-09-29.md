# Validação — chat, sessões e configurações de tenant

Branch feat/chat-session-user-settings, ambiente .NET Validation e Compose isolado agent-system-backend-validation-20260929: PostgreSQL 16/pgvector em 127.0.0.1:55432/backend_validation, Ollama em 127.0.0.1:11435, app em 127.0.0.1:5188. A validação BYOK usou um stub OpenAI compatível local em 127.0.0.1:5190; nenhum serviço LLM externo nem segredo real foi usado.

| Requisito | Prova observada | Resultado |
|---|---|---|
| Chat usa seleção salva de provider/modelo | Smoke HTTP guardou OpenAI/validation-model; duas chamadas posteriores chegaram ao stub com esse modelo e o Bearer da chave default atualizada do tenant. Cypress alterou o modelo no seletor da UI e leu a preferência salva novamente pela API. | Passou |
| Retomar sessão preserva contexto | Segundo turno, na mesma sessão do PostgreSQL, enviou ao provider o primeiro marcador do usuário. Cypress recarregou o navegador, abriu a sessão listada e voltou a renderizar as duas mensagens. | Passou |
| Credencial BYOK e segredo | Smoke registrou, validou via GET /v1/models, descobriu modelos, atualizou a chave, definiu default e confirmou uso real nas chamadas do chat. POST/PUT/GET não retornaram o valor; a chave foi removida no cleanup. Viewer recebeu 403 para criação e atualização. | Passou |
| Skills do tenant | Smoke confirmou o marcador de instrução da skill ativa nas mensagens enviadas ao modelo; após desativar, uma sessão nova não recebeu o marcador. Viewer recebeu 403. Cypress verificou a chave na tela de IA sem mostrar segredo e desativou a skill pela tela de skills. | Passou |
| Isolamento e encerramento | Viewer não leu sessão alheia (404); preferências PostgreSQL foram testadas por tenant+user. Encerrar manteve mensagens consultáveis e bloqueou novo POST de chat nessa sessão. | Passou |
| API key e principal | POST /api/auth/login retorna tenant, role e userId opaco da chave; o frontend persiste esse identificador para particionar caches de preferências. Login não dispara discovery ou alterações globais. | Passou no smoke |
| Suíte/backend | dotnet build AgenticSystem.sln --configuration Release --no-restore: 0 avisos/erros. Suíte PostgreSQL/Ollama: 763 aprovados, 1 ignorado, 0 falhas. EF informou que não há mudanças pendentes. | Passou |
| Frontend | Build TypeScript/Vite passou; ESLint passou nos arquivos tocados; Cypress 16.1.0: 1 spec, 1 passou. O browser escolheu o agente direto, usou o provider/modelo, abriu a sessão após reload e validou as telas de chave/skills. | Passou no escopo alterado |
| ESLint de todo o frontend | npm run lint ainda encontra 22 erros e 1 warning em componentes/hooks fora desta entrega (AgentDetailModal, Alerts, Gateway, ONNX, plugins, scheduled tasks, Settings, workflows, dashboard e hooks de gateway/providers/plugins/tools/workflows). | Gate de repositório pendente |

O smoke é reproduzível com tests/backend-validation/chat-session-settings-smoke.mjs; a prova visual é frontend/cypress/e2e/chat-session-settings.ui.cy.js. Memberships de teste, chaves, skills, sessões e a preferência sintética foram removidos depois da validação. Os volumes do Compose são preservados. O stub local registra o Bearer só no arquivo de evidência; o valor é sintético e não é credencial externa.

## Atualização da consolidação #132 — 2026-10-01

Os resultados acima pertencem à branch de origem e ao snapshot de 2026-09-29; o registro de 22 erros e 1 warning no ESLint global é histórico, não o estado da consolidação. Na árvore consolidada antes do commit documental `eb4fa07`, `npm run lint` e `npm run build` globais passaram, Cypress da story #123 passou (1 spec), e a suíte .NET totalizou 771 aprovados/16 ignorados. O PR #132 permanece aberto para revisão em `develop`, portanto isso não fecha a issue #123.
