# SSE, SignalR e protocolos
Fonte: [Program.cs](../../src/AgenticSystem.Api/Program.cs), hubs e [SseWriter](../../src/AgenticSystem.Api/Helpers/SseWriter.cs).

## SSE
POST /api/chat/stream, mesmo body de chat. Content-Type text/event-stream, Cache-Control no-cache, X-Accel-Buffering no. Blocos event: nome do enum em minúsculas (sessionstarted, token, sessioncompleted, error), data: JSON seguido de linha vazia.
SseWriter usa JsonSerializer.Serialize sem opções dos controllers: propriedades PascalCase e enum Type numérico. Schema é AgentStreamEvent: Id, Sequence, SessionId, Type, AgentName, Message, Timestamp, IsTerminal, Data. Não tratar data como apenas token. Cancellation acompanha RequestAborted; após headers enviados, falha pode vir em evento/encerramento, não novo status HTTP.
Exemplo conceitual:
```text
event: token
data: {"Id":"...","Sequence":1,"SessionId":"...","Type":7,"AgentName":"...","Message":"...","Timestamp":"...","IsTerminal":false,"Data":{}}
```
Na baseline, Token=7, SessionCompleted=22 e Error=23 em [RuntimeModels](../../src/AgenticSystem.Core/Models/RuntimeModels.cs). Reconexão/replay não são garantidos pela presença de Sequence.

## SignalR
| Hub | Finalidade | Fonte |
|---|---|---|
| /hubs/chat | conversa e eventos de sessão | [ChatHub](../../src/AgenticSystem.Api/Hubs/ChatHub.cs) |
| /hubs/gateway | monitoramento de gateway | [GatewayHub](../../src/AgenticSystem.Api/Hubs/GatewayHub.cs) |
| /hubs/external-agent | agentes externos | [ExternalAgentHub](../../src/AgenticSystem.Api/Hubs/ExternalAgentHub.cs) |
| /hubs/workflow | progresso de workflow | [WorkflowHub](../../src/AgenticSystem.Api/Hubs/WorkflowHub.cs) |
| /hubs/onnx | progresso de modelos ONNX | [OnnxHub](../../src/AgenticSystem.Api/Hubs/OnnxHub.cs) |

Todos exigem autenticação. JWT access_token ou API key em query são aceitos para /hubs; evitar guardar URL com segredo. Tenant header/query/claim: [regras](access-tenants.md). Opções JSON do MVC não configuram automaticamente o serializer SignalR.

ChatHub.SendMessage(message, targetAgent?, provider?, model?, apiKey?, sessionId?, selectedRoomId?). selectedRoomId vira preferência rag.knowledgeRoomId. Eventos: ProcessingStarted, StreamEvent (AgentStreamEvent completo), ReceiveMessage em SessionCompleted, ReceiveError em exceção. AgentSelected é um tipo de StreamEvent, não evento independente. SessionUpdated e SessionDeleted são enviados por usuário; verificar isolamento tenant/usuário em cenários reais.

## Endpoints de host
| Endpoint | Condição | Segurança/contrato |
|---|---|---|
| GET/HEAD /health | sempre | anônimo, liveness; não valida PostgreSQL/LLM |
| GET /version | sempre | sem RequireAuthorization; Version fixa e Build calculado no request |
| /a2a | ProtocolHosting:A2A:Enabled | RequireAuthorization + rate policy; contrato da biblioteca hospedada |
| /agui | ProtocolHosting:AgUI:Enabled | RequireAuthorization + rate policy |
| /v1/chat/completions | controller OpenAICompatible | autenticação Bearer própria na action; sem AuthorizeAttribute e sem gating pela flag OpenAICompatible no controller |
| /responses, /conversations, DevUI | apenas Development, mapeados por biblioteca | não confundir com compat controller |
| /mcp | não mapeado na baseline | MCP client/plugin não é servidor HTTP /mcp |
| /api/test/rag/* | compilação DEBUG ou STAGING | sem AuthorizeAttribute; não expor como API de produção |

Apenas A2A/AGUI habilitados registram keyed agent AgenticSystem; Development tenta resolvê-lo para DevUI mesmo se ambos estiverem desabilitados. Configuração e autenticação precisam validação específica por protocolo. Inventário de fonte inclui rotas de compilação condicional; não implica que estejam ativas no build Release.
