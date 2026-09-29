# Chat, sessões e configurações do tenant

O escopo cobre membros e administradores de tenant. Administração global de providers continua fora do produto nesta etapa. Use `Authorization: Bearer <JWT>` ou `X-Api-Key` e `X-Tenant-Id` coerente com a identidade. O middleware resolve a membership antes dos controllers e injeta os papéis atuais do tenant.

## Chat e preferências

| Método e rota | Acesso | Comportamento |
|---|---|---|
| `GET /api/chat/configuration` | membro autenticado | Retorna somente providers habilitados, modelos permitidos para o provider e preferência do par `(tenantId,userId)`. Modelos de BYOK são disponibilizados somente para uma chave habilitada marcada como default. `canManageTenant` indica se o membro é Owner/Admin. Nenhum segredo é retornado. |
| `PUT /api/chat/configuration` | membro autenticado | Body `{ "provider":"OpenAI", "model":"validation-model" }`. Valida provider/model contra o catálogo e persiste por usuário e tenant. 400 se a opção não está disponível. |
| `POST /api/chat` | membro autenticado | Body inclui `message`, `targetAgent?`, `provider?`, `model?`, `sessionId?`, `apiKey?` e contexto RAG permitido. Preferência salva é usada por padrão. Provider/model explícitos são validados e passam a preferência da sessão; `apiKey` explícita vale somente para a chamada atual e não é gravada na sessão. `AgentResponse.sessionId` é a identidade persistida pelo servidor; `success=false` indica erro funcional. |
| `POST /api/chat/stream` | membro autenticado | Mesmo request por SSE; eventos de erro terminal seguem o contrato em [transportes](transports.md). |
| `ChatHub.SendMessage` | membro autenticado, membership no tenant | Argumentos: message, targetAgent, provider, model, apiKey temporária, sessionId, selectedRoomId. Emite ProcessingStarted, eventos e ReceiveMessage com sessionId. Opções persistidas e explicitamente selecionadas usam a mesma validação do REST. |

Precedência: escolha explícita de provider/model na chamada atual; depois escolha já guardada na sessão; depois preferência persistida do usuário/tenant; por último default do catálogo. Provider/model não disponíveis falham com erro, sem fallback silencioso para outro provider.

## Sessões

| Método e rota | Acesso | Comportamento |
|---|---|---|
| `POST /api/session` | usuário autenticado | Cria ID no servidor e associa a tenant e principal atuais. |
| `GET /api/session?limit=50&search=...` | dono da sessão no tenant | Lista até 100 sessões, pesquisa título/resumo e ordena por atividade mais recente; inclui `isEnded`. |
| `GET /api/session/{id}` | dono/tenant | Detalhe com histórico, resumo e seleção não secreta de provider/model; 404 se ausente ou de outro dono/tenant. |
| `GET /api/session/{id}/messages` | dono/tenant | Recupera histórico também após encerramento. |
| `PUT /api/session/{id}/title` | dono/tenant | Renomeia; não modifica owner/tenant. |
| `POST /api/session/{id}/end` | dono/tenant | Marca `endedAt`; preserva histórico e bloqueia novos chats nessa sessão. |
| `DELETE /api/session/{id}` | dono/tenant | Apaga a sessão e pede a remoção da coleção vetorial. Operação destrutiva separada de encerrar. |

Cada sessão pertence a um único usuário e tenant. IDs de sessão são criados no backend. A retomada reutiliza o estado MAF e provider/model configurados; sessão encerrada não pode reabrir o chat (POST retorna 404), mas seu detalhe e histórico seguem acessíveis até DELETE.

## Chaves BYOK

`GET /api/admin/llm/providers/{provider}/keys` lista somente chaves do tenant atual, com `id`, nome, últimos quatro caracteres, estado, default e modelos descobertos. Owner/Admin pode chamar:

- `POST /api/admin/llm/providers/{provider}/keys` com `{ "name":"trabalho", "apiKey":"...", "isDefault":true }`.
- `PUT /api/admin/llm/providers/{provider}/keys/{id}` para nome, nova chave, estado, default e modelos.
- `POST /api/admin/llm/providers/{provider}/keys/{id}/test` para consultar o endpoint de modelos configurado do provider com a chave cifrada salva.
- `POST /api/admin/llm/providers/{provider}/keys/{id}/discover-models` valida e persiste a lista de modelos da chave.
- `POST /api/admin/llm/providers/{provider}/keys/{id}/default` para selecionar a chave padrão daquele provider.
- `DELETE /api/admin/llm/providers/{provider}/keys/{id}` para remover.

POST/PUT/GET não devolvem o segredo depois de gravado; o banco guarda valor cifrado e `LastFour`. A execução do chat escolhe o default habilitado do provider/tenant, exceto quando uma credencial explícita e temporária é fornecida no request. Owner/Admin gere credenciais; Viewer/Operator recebe 403 nas mutações.

## Skills do tenant

`GET /api/agent/skills/all` retorna catálogo com `isEnabled`, `isSystem` e `canManage`. Owner/Admin pode criar, editar, excluir skills customizadas e ativar/desativar qualquer skill do tenant:

- `PUT /api/agent/skills/{id}/enabled` body `{ "enabled":false }`.
- `POST /api/agent/skills` e `POST /api/agent/skills/upload` criam skills customizadas.
- `PUT /api/agent/skills/{id}` e `DELETE /api/agent/skills/{id}` editam/removem customizadas; skills de sistema permanecem read-only para essas operações.

Somente instruções de skills ativas e relevantes são adicionadas ao contexto MAF. Skills não concedem permissão a tools; catálogo permitido do agente e `Permission.Execute` seguem verificação independente.

## Prova de persistência e uso

O smoke [chat-session-settings-smoke.mjs](../../tests/backend-validation/chat-session-settings-smoke.mjs) usa apenas PostgreSQL/Ollama Compose e um stub OpenAI compatível preso a `127.0.0.1:5190`. A evidência JSONL registra request com modelo `validation-model` e `Authorization: Bearer synthetic-chat-123-secret`; os dois turnos usaram o mesmo sessionId e o segundo carregou o primeiro no contexto. O stub também permite confirmar a skill ativa no prompt e sua ausência em uma sessão iniciada após desativação. Os segredos e identidades do smoke são sintéticos e os registros de teste são removidos no cleanup.
