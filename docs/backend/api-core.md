# Contratos HTTP do núcleo
Fonte atual: controladores, DTOs e stores na baseline f8de7a6. [Inventário completo](endpoint-inventory.md). Exemplos ilustram schema, não respostas capturadas. Evidências executadas em [relatório](validation/2026-09-28.md).

## Convenções
Base URL configurada pelo host; exemplos usam http://localhost:5188. JSON dos controllers usa camelCase, enums camelCase e omite nulos. Não há envelope único: arrays, objetos, ProblemDetails, texto e respostas vazias coexistem. Exceções não tratadas retornam 500 com error/correlationId e X-Correlation-Id. 429 retorna error e Retry-After. Não assumir correlationId em todos os erros de validação.
Credenciais: X-Api-Key ou Bearer. Tenant: [resolução e gaps](access-tenants.md). Auth/tenant/rate limiting ocorrem antes da action.

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
400: message ausente/branco/grande ou binding inválido. 401/403: identidade/tenant; 429: quota HTTP; 500: exceção. Efeitos: LLM, sessão/artifacts, tools e custo conforme fluxo; não é idempotente. SSE usa o mesmo request, mas serialização difere: [transportes](transports.md).

## Sessões — /api/session
[SessionController](../../src/AgenticSystem.Api/Controllers/SessionController.cs).
| Método/rota | Entrada | Resultado |
|---|---|---|
| GET /api/session | limit default 50, search opcional | 200 array ordenado por EndedAt/StartedAt; usuário atual |
| GET /api/session/{id} | id | 200 detalhe; 404 ausente ou outro dono |
| GET /api/session/{id}/messages | id | 200 mensagens; 404 ausente/outro dono |
| PUT /api/session/{id}/title | {"title":"Novo título"} | 200 id/title; 400 título branco; 404 ausente/outro dono |
| DELETE /api/session/{id} | id | 204; apaga sessão e tenta apagar collection vetorial; erro da purga é logado |

Identidade sem user id: 401 na action. Tenant em stores via context/filtros; testar mesmo id entre tenants e usuários. DTOs: [SessionDtoMapper](../../src/AgenticSystem.Core/Models/SessionDtos.cs). Alterar/apagar emite SessionUpdated/SessionDeleted por usuário no ChatHub. Falha da purga não muda o 204.

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
POST /ingest: multipart/form-data com file não vazio, source opcional na query (também collection). 200 documentId/fileName/chunksCreated/tokensProcessed/contentHash/durationMs/fileDiskPath. 400 arquivo vazio/tipo não suportado; 422 pipeline sem sucesso (error/documentId).
POST /ingest/batch: multipart files e source; 200 total/succeeded/failed/results, incluindo success/error por item; tipos não suportados são ignorados, 400 se nenhum suportado.
Tipos: md/txt/pdf/docx/html/htm/pptx, imagens png/jpg/jpeg/gif/webp, áudio mp3/wav/ogg/webm/mpeg. Suporte de parsing/LLM depende de providers: extensão aceita não comprova extração.
```sh
curl -X POST "http://localhost:5188/api/document/ingest?source=validacao" \
  -H "X-Api-Key: CHAVE_DE_TESTE" -H "X-Tenant-Id: tenant-a" -F "file=@fonte.txt"
```
Efeitos: parse/chunk/embed/index e cópia física por tenant/nome. fileDiskPath expõe caminho de servidor; mesma combinação nome/tenant sobrescreve cópia. Não existe parâmetro de sala nesta action. Quotas e storage precisam validação real.

## Outros recursos
Todo endpoint dos controladores aparece no [inventário](endpoint-inventory.md), com assinatura e fonte. Isso é catálogo, não contrato detalhado validado dos módulos fora do núcleo.
Rotas administrativas de MCP plugins são /api/admin/plugins, não /api/admin/mcp/plugins. Scheduled tasks: /api/admin/scheduled-tasks.
Golden sets têm aliases /api/golden-sets, /api/evaluation/golden-sets e /api/goldensets; lista é array. Create/update exigem agentName além de name/cases. Runs pequenos retornam 200 com suiteId/results; grandes 202 com runId e polling GET /runs/{runId}. Consultar controlador para rota exata e cache.
