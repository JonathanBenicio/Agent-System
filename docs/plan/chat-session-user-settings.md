# Plano — Chat, sessões e configurações usadas pelo runtime

Status: em execução · [Issue #123](https://github.com/JonathanBenicio/Agent-System/issues/123) · [ADR-039](../architecture/adr/039-chat-session-user-tenant-settings.md) · Story BACK-CHAT-123.

Base: `fix/backend-core-tenancy` no commit `5701d51`; branch `feat/chat-session-user-settings`. PostgreSQL de validação: Compose `tests/backend-validation/compose.yml`, projeto `agent-system-backend-validation-20260929`, bind `127.0.0.1:55432`; não usar outro banco.

| Entrega | Mudança e prova concreta | Estado |
|---|---|---|
| Chat e sessão | Corrigir contrato REST/SignalR, erros e `sessionId`; criar/listar/abrir/retomar/encerrar, preservar mensagens e owner/tenant após restart | Pendente |
| BYOK de tenant | Owner/Admin escreve; retorno sem segredo; teste consulta provider; chave salva/default ou escolhida é usada em chamada do chat | Pendente |
| Modelos e providers | Catálogo seguro para membros; preferência `(tenant,user)` persistida, validada e aplicada ao próximo chat; request explícito autorizado prevalece | Pendente |
| Skills | Owner/Admin ativa/desativa; listagem mostra estado; prompt MAF inclui apenas ativas; autorização das tools continua independente | Pendente |
| Frontend | Chat, sessões e telas de chave/modelo/skills usam os contratos corretos; seleção e erro são claros; sem tela de administração global para membro comum | Pendente |
| Verificação e entrega | Build/lint frontend, suíte .NET, PostgreSQL/Ollama Compose, fluxos HTTP/SignalR e UI; docs de endpoints/acesso/escopos, commits por contexto, PR draft com limites | Pendente |

Critérios de aceite separados: (1) preferência/chave/skill/sessão foi validada e salva; (2) a execução seguinte leu o valor salvo e o aplicou ao LLM/agente; (3) outro usuário ou tenant não pode ler nem alterar o recurso. A2A/AG-UI, administração global, merge e deploy estão fora deste plano.
