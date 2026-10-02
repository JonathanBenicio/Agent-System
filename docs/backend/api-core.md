# Contratos HTTP do núcleo
Fonte atual: controladores, DTOs e stores na branch integrada para `develop`, com escopos tenant e plataforma separados. [Inventário completo](endpoint-inventory.md). Exemplos ilustram schema; evidências de execução estão em [validação da correção](validation/backend-core-remediation.md), e o diagnóstico de baseline permanece em [2026-09-28](validation/2026-09-28.md).

Alertas de quota seguem o tenant autenticado em `GET /api/v1/alerts` e `POST /api/v1/alerts/{id}/read`; o filtro de tenant limita listagem e atualização de leitura. Alertas de chaves globais de plataforma são acessíveis somente por Platform Admin em `GET /api/platform/alerts` e `POST /api/platform/alerts/{id}/read`.

## Convenções
Base URL configurada pelo host; exemplos usam http://localhost:5188. JSON dos controllers usa camelCase, enums camelCase e omite nulos. Não há envelope único: arrays, objetos, ProblemDetails, texto e respostas vazias coexistem. Exceções não tratadas retornam 500 com error/correlationId e X-Correlation-Id. 429 retorna error e Retry-After. Não assumir correlationId em todos os erros de validação.
Credenciais: `X-Api-Key` para API key ou `Authorization: Bearer <JWT>` nas rotas autenticadas. O endpoint OpenAI-compatível `/v1/chat/completions` recebe a API key opaca no Bearer. Tenant/membership: [resolução e papéis](access-tenants.md). Auth/tenant/rate limiting ocorrem antes da action.

`POST /api/auth/login` valida a chave, o tenant ativo e a membership, define cookie HttpOnly/Secure/SameSite=Strict e retorna `role`, `roles`, `tenantId` e `userId` opaco da chave. `GET /api/auth/session` exige autenticação e membership e devolve apenas identidade/papéis, nunca a credencial; permite retomar o navegador sem API key no localStorage. `POST /api/auth/logout` remove o cookie inclusive quando o vínculo foi revogado; a UI só confirma logout após sucesso. Login/logout são exchanges sem dados tenant-owned. Login não atualiza nem descobre providers globais.

## Chat — POST /api/chat e POST /api/chat/stream
[ChatController](../../src/AgenticSystem.Api/Controllers/ChatController.cs), [ChatRequest](../../src/AgenticSystem.Api/Models/ChatRequest.cs).
| Campo | Obrigatório | Regra |
|---|---|---|
| message | sim | não branco; até 10.000 caracteres |
| targetAgent | não | vazio: orquestração; preenchido: agente direto |
| sessionId | não | retoma sessão; isolamento/dono deve ser validado |
| provider/model/apiKey | não | preferências de LLM por request/session; não confundir apiKey do provider com X-Api-Key |
| context | não | dicionário de preferências, inclui filtros RAG conforme runtime |
| userId/userName | não | userId ignorado em favor do principal; nome pode ser fallback |

```sh
curl -X POST http://localhost:5188/api/chat \
  -H "X-Api-Key: CHAVE_DE_TESTE" -H "X-Tenant-Id: tenant-a" \
  -H "Content-Type: application/json" \
  -d '{"message":"Responda em português: olá","provider":"Ollama","model":"qwen2.5:0.5b"}'
```
200 retorna [AgentResponse](../../src/AgenticSystem.Core/Models/AgentResponse.cs): content, agentName, agentTier, actionsPerformed, toolsUsed, success, metadata, timestamp, sessionId e opcionais errorMessage/confidence. Não retorna ChatResponse.response/agentUsed. HTTP 200 com success=false representa falha funcional; verificar ambos.
400: message ausente/branco/grande ou binding inválido. 401/403: identidade/tenant/membership. 429: teto de token ou custo excedido; o body continua AgentResponse com `errorMessage`. 500: exceção não tratada. Efeitos: LLM, sessão/artifacts, tools e custo conforme fluxo; não é idempotente. SSE usa o mesmo request, mas após abrir o stream comunica quota excedida por evento `error`: [transportes](transports.md).

## Propostas de auto-melhoria — /api/agent/improvements
[AgentSelfImprovementController](../../src/AgenticSystem.Api/Controllers/AgentSelfImprovementController.cs). Esta capacidade Lab exige `AgenticSystem:SelfImprovement:Enabled=true`, desligado por padrão. Com a flag habilitada, propostas e agentes são isolados pelo tenant atual. A geração apenas persiste uma proposta; confiança nunca aplica instruções automaticamente. Owner/Admin pode revisar, rejeitar, aprovar e reverter. Aprovação atualiza o agente do tenant, cria uma versão de agente e de prompt e registra auditoria; rollback grava novas versões com as instruções anteriores.

| Método/rota | Resultado | Regra |
|---|---|---|
| GET /api/agent/improvements | lista propostas recentes | membership e filtro do tenant autenticado |
| POST /api/agent/improvements/{proposalId}/approve | 204 | Owner/Admin; proposta pendente; cria versão de prompt |
| POST /api/agent/improvements/{proposalId}/reject | 204 | Owner/Admin; proposta pendente |
| POST /api/agent/improvements/{proposalId}/rollback | 204 ou 409 | Owner/Admin; somente proposta aplicada; registra versão com conteúdo anterior |

Provider/model podem vir da preferência persistida do membro ou ser escolhidos explicitamente no request. Regras, credenciais e prova de execução: [Chat, sessões e configurações do tenant](chat-sessions-settings.md). Administração global em `/api/admin/llm` não é a interface de configuração do membro.

## Sessões — /api/session
[SessionController](../../src/AgenticSystem.Api/Controllers/SessionController.cs).
| Método/rota | Entrada | Resultado |
|---|---|---|
| POST /api/session | vazio | 201 e detalhe da sessão criada para a identidade atual |
| GET /api/session | limit default 50 (1–100), search opcional (até 100 chars) | 200 array do usuário e tenant atuais, ordenado por LastActivity |
| GET /api/session/{id} | id | 200 detalhe; 404 ausente ou outro dono |
| GET /api/session/{id}/messages | id | 200 mensagens; 404 ausente/outro dono |
| PUT /api/session/{id}/title | {"title":"Novo título"} | 200 id/title; 400 título branco; 404 ausente/outro dono |
| DELETE /api/session/{id} | id | 204; apaga sessão e tenta apagar collection vetorial; erro da purga é logado |
| POST /api/session/{id}/end | id | 200; marca encerrada, preserva mensagens e impede novo POST /api/chat nessa sessão |

Identidade sem user id: 401 na action. Tenant em stores via context/filtros; testar mesmo id entre tenants e usuários. DTOs: [SessionDtoMapper](../../src/AgenticSystem.Core/Models/SessionDtos.cs). Alterar/apagar emite SessionUpdated/SessionDeleted por usuário no ChatHub. Falha da purga não muda o 204.

Preferência/provider e modelo são metadados não secretos. Histórico pode ser consultado após encerramento; a sessão encerrada não pode retomar execução do chat.

## Preferências, BYOK e skills

- `GET/PUT /api/chat/configuration`: leitura para membro, gravação da própria preferência. Persistência `(tenantId,userId)`; modelos vêm do catálogo habilitado e de chaves BYOK default ativas. Cada mensagem valida a escolha e a executa no provider/modelo persistido ou explicitamente selecionado.
- `/api/admin/llm/providers/{provider}/keys`: listagem no tenant atual; POST/PUT/DELETE/test/discover/default exigem Owner/Admin. DTOs nunca incluem o segredo; armazenam cifra e últimos quatro caracteres. Testa a credencial contra o endpoint de modelos configurado.
- `/api/agent/skills`: catálogo de membros retorna `isEnabled`/`canManage`; escrita exige Owner/Admin. Só skills ativas relevantes entram no contexto MAF; a skill não concede permissão a ferramentas.
- Fluxo integrado: [prova de configuração salva e usada](validation/chat-session-settings-2026-09-29.md).

## Salas — /api/knowledge/rooms
[KnowledgeRoomController](../../src/AgenticSystem.Api/Controllers/KnowledgeRoomController.cs), [modelos](../../src/AgenticSystem.Core/Models/KnowledgeRoomModels.cs).
POST body mínimo ilustrativo: {"name":"Pesquisa","description":"Fontes autorizadas"}. Campos do modelo: id, name, description, color, icon, documentCount, tags, createdAt, updatedAt; defaults no modelo não equivalem a restrições de negócio. Criador recebe Admin da sala.
| Método/rota | Entrada | Resultado/regra |
|---|---|---|
| GET base | — | 200 array acessível ao usuário/tenant |
| GET /{id} | id | 200 sala ou 404 |
| POST base | KnowledgeRoom | 201 com Location e sala criada |
| PUT /{id} | KnowledgeRoom com id igual à rota | 200; 400 texto ID mismatch; 403 sem permissão |
| DELETE /{id} | id | 204 ou 404 |
| GET /{id}/permissions | id | 200 ACL; 403 sem administração |
| POST /{id}/permissions | {"userId":"usuario-b","role":"reader"} | 200 permissão; 403 sem administração |
| DELETE /{id}/permissions/{targetUserId} | ids | 204/404; 403 sem administração |

Reader/Editor/Admin são papéis de sala, não papéis de tenant. Persistência: PostgresKnowledgeRoomStore; tenant + ACL em serviço. Testar revogação, ACL de outro tenant e papel global sem ACL.

## Documentos — /api/document
[DocumentController](../../src/AgenticSystem.Api/Controllers/DocumentController.cs).
GET /stats: 200 totalChunks, searchCount24h e onnxStatus. O status/latência ONNX é parcialmente inferido/fixo: [limitações](resources-rules.md).
POST /ingest: multipart/form-data com file não vazio, `source` opcional na query (também collection) e `roomId` opcional. Com `roomId`, sala precisa existir e caller precisa permissão de escrita; chunks carregam room/document/source byte metadata. 200 documentId/fileName/chunksCreated/tokensProcessed/contentHash/durationMs/fileDiskPath. 400 arquivo vazio/tipo não suportado; 403/404 sem acesso/sala ausente; 413 limite de documento/bytes; 422 pipeline sem sucesso (error/documentId).
POST /ingest/batch: multipart files, `source` e `roomId` opcionais; mesma validação da sala/tenant. 200 total/succeeded/failed/results, incluindo success/error por item; tipos não suportados são ignorados, 400 se nenhum suportado.
Tipos: md/txt/pdf/docx/html/htm/pptx, imagens png/jpg/jpeg/gif/webp, áudio mp3/wav/ogg/webm/mpeg. Suporte de parsing/LLM depende de providers: extensão aceita não comprova extração.
```sh
curl -X POST "http://localhost:5188/api/document/ingest?source=validacao" \
  -H "X-Api-Key: CHAVE_DE_TESTE" -H "X-Tenant-Id: tenant-a" -F "file=@fonte.txt"
```
Efeitos: parse/chunk/embed/index e cópia física por tenant/nome. `fileDiskPath` ainda expõe caminho de servidor e nomes repetidos podem sobrescrever a cópia física; planeje nomes únicos. A quota de documentos/bytes conta documento lógico e tamanho de origem, não alocação física total; [recursos e limites](resources-rules.md).

## Workflows dinâmicos — /api/workflow
[WorkflowController](../../src/AgenticSystem.Api/Controllers/WorkflowController.cs), [engine](../../src/AgenticSystem.Core/Services/DefaultWorkflowEngine.cs) e [modelos/status](../../src/AgenticSystem.Core/Models/WorkflowModels.cs). Todas as rotas exigem autenticação e tenant ativo.

| Método/rota | Entrada/resultado | Regras atuais |
|---|---|---|
| GET /definitions, GET /definitions/{id} | lista/detalhe | somente definições do tenant; ausente ou outro tenant retorna 404 |
| POST /definitions, DELETE /definitions/{id} | WorkflowDefinition / id | grava ou remove no store tenant-scoped |
| POST /executions/start/{definitionId} | mapa opcional de variáveis; 202 com `id` e alias `executionId`, status e `statusUrl` | ambos os IDs apontam para a mesma execução persistida |
| GET /executions/{id}, GET /executions | id; status/limit opcionais | consulta o mesmo `IWorkflowStore` usado pelo engine; outra tenant não vê a execução |
| POST /executions/{id}/cancel | `reason` opcional na query | grava Cancelled e emite evento; ainda não interrompe token de uma etapa em execução, então uma ação longa pode continuar e sobrescrever estado |
| POST /executions/{id}/approve | sem body; retorna WorkflowExecution | Owner/Admin/Operator do tenant aprovam a etapa pendente e retomam; Viewer recebe 403; papel/grupo por etapa ainda não configurável |
| POST /executions/{id}/reject | `reason` opcional na query; retorna WorkflowExecution | Owner/Admin/Operator rejeitam a etapa pendente e terminam como Cancelled; Viewer recebe 403 |

Status: Pending=0, Running=1, Paused=2, WaitingForApproval=3, Completed=4, Failed=5, Cancelled=6, Compensating=7. O engine executa `Agent`/`Action`, pausa em Approval e aguarda a duração configurada em Wait. `Subworkflow` ainda não é executado e falha explicitamente. Novas execuções persistem versão/hash/snapshot imutável da definição, e a retomada após edição usa esse snapshot; execuções legadas sem snapshot falham fechadas. Execuções ainda usam `Task.Run` no processo: registros ficam no PostgreSQL, mas não há lease ou recuperação automática depois de crash. O cancelamento grava status, porém não interrompe token de etapa ainda em execução. A [decisão e plano de recuperação](../plan/maf-122-protocols-gateway.md) registra esses limites. O hub pode enviar `ApprovalRequested`; o hook frontend ainda precisa escutar esse evento e corrigir o polling que hoje só refaz GET para status Pending=0.

## Outros recursos
Todo endpoint dos controladores aparece no [inventário](endpoint-inventory.md), com assinatura e fonte. Isso é catálogo, não contrato detalhado validado dos módulos fora do núcleo.
Rotas administrativas de MCP plugins são /api/admin/plugins, não /api/admin/mcp/plugins. Scheduled tasks: /api/admin/scheduled-tasks.
Golden sets têm aliases /api/golden-sets, /api/evaluation/golden-sets e /api/goldensets; lista é array. Create/update exigem agentName além de name/cases. Runs pequenos retornam 200 com suiteId/results; grandes 202 com runId e polling GET /runs/{runId}. Consultar controlador para rota exata e cache.
