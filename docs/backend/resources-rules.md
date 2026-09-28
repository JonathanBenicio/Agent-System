# Recursos, regras e limites
Estado por leitura da baseline; aplicação integrada não presumida.

## Recursos
| Recurso | Escopo/identidade | Persistência/configuração | Regra relevante |
|---|---|---|---|
| Tenant | organização | PostgreSQL tenants | ativo, plano e limits JSON |
| Sessão | tenant e usuário | ISessionStore; StorageMode | GET/PUT/DELETE exigem dono; retomada no chat precisa teste negativo |
| Sala | tenant e ACL de usuário | knowledge rooms/permissions | criador recebe Admin da sala; leitura só salas permitidas |
| Documento/chunk | tenant, collection e metadata de sala | vector_documents, pgvector | room_id/roomId em filtros de segurança; uploads físicos separados |
| Agente/tool/skill | configuração e runtime | registros/arquivos/DB conforme recurso | catálogo não equivale a autorização de execução |
| Workflow | tenant e definição/execução | engine nativo + compiler Durable coexistem | não declarar retomada automática/multiinstância comprovada |
| Golden set/run | tenant, agente e casos | sets PostgreSQL; run cache local | <=20 casos síncrono; >20 async; cache 30min sem durabilidade |

## Planos declarados
Fonte: [TenantLimits](../../src/AgenticSystem.Core/Models/Tenant.cs). Estes valores não são promessa de enforcement uniforme.
| Plano | req/min | tokens/dia | USD/dia | sessões simultâneas | agentes | documentos MB |
|---|---:|---:|---:|---:|---:|---:|
| Free | 10 | 50.000 | 1 | 3 | 5 | 100 |
| Pro | 60 | 500.000 | 25 | 20 | 50 | 5.000 |
| Enterprise | 300 | 5.000.000 | 500 | 100 | 500 | 50.000 |

## Limites efetivos e conflitos
| Regra | Fonte/enforcement | Persistência | Evidência/limitação |
|---|---|---|---|
| Chat 30 req/60s, quatro segmentos, fila zero | [rate limiter](../../src/AgenticSystem.Api/Extensions/RateLimitingServiceCollectionExtensions.cs), ChatController | memória por processo | claim tenant_id antes do header, diferente de TenantMiddleware; não usa plano |
| Protocolos 60/60s default | mesmo registro; ProtocolHosting:RateLimiting | memória | configurável; A2A/AGUI aplicam policy |
| Quota de LLM | [QuotaEnforcer](../../src/AgenticSystem.Core/Services/QuotaEnforcer.cs), gateway/interceptor | minuto em memória; diário em repository | Gateway budget default 50 USD é outro modelo |
| Persistência diária | [TenantQuotaRepository](../../src/AgenticSystem.Infrastructure/Persistence/TenantQuotaRepository.cs) | PostgreSQL | EF SaveChanges com concorrência otimista; não UPDATE RETURNING direto |
| Reset diário | [background service](../../src/AgenticSystem.Infrastructure/BackgroundServices/DailyQuotaResetBackgroundService.cs) | repository | não atribuir ao Quartz sem evidência |
| Sessões/documentos | [TenantIsolationService](../../src/AgenticSystem.Core/Services/TenantIsolationService.cs) | estatísticas dos stores | usa novo TenantResourceLimits, não tenant.Limits; tenant ausente permite operação |
| Upload/storage | pipeline + controller | vetores e arquivo físico | checagem não recebe tamanho real; arquivo de mesmo nome sobrescreve cópia física |
| Estatística ONNX | DocumentController.GetStats | configuração/artifacts | loaded inferido de config/paths; avgLatencyMs literal 12.4, não medida real |

TenantResourceLimits é um terceiro conjunto: defaults sessões 10, storage 1000MB, documentos 10000, agentes 20, budget mensal 100USD. Unificação é trabalho futuro; não somar limites como se formassem uma política única.

## Isolamento e RAG
[PostgresVectorStore](../../src/AgenticSystem.Infrastructure/Persistence/PostgresVectorStore.cs) tenta pré-filtrar IDs por sala antes do scoring. Na validação real, o SQL falhou: usa metadata_json, mas a coluna é metadata; room_ids vazio retornou documentos. O filtro EF de tenant passou no cenário vetorial executado. Outros filtros de metadata podem ocorrer em memória. Ver [#113](https://github.com/JonathanBenicio/Agent-System/issues/113). DocumentController.ingest define Collection por source, sem parâmetro de sala específico; ingestão por outros fluxos pode anexar metadata. Não assumir que todo upload pertence a uma sala.

[Backlog](backlog.md) registra lacunas e prioridade.
