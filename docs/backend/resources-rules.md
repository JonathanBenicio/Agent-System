# Recursos, regras e limites

Estado da branch `fix/backend-core-tenancy`; evidências e lacunas em [validação da correção](validation/backend-core-remediation.md). Este mapa separa autorização, estado persistido e limite efetivo.

## Recursos e controle de acesso

| Recurso | Escopo e identidade | Persistência | Regra aplicada |
|---|---|---|---|
| Tenant | `tenantId` | `tenants` | Precisa existir e estar ativo nas rotas comuns. O Platform Admin pode listar e mudar plano; isso não dá acesso a dados do tenant. |
| Membership | subject + tipo + tenant + role | `tenant_memberships` | Obrigatória para principal autenticado no tenant. Claims de role são reconstruídas do registro daquele tenant. |
| Sessão/chat | tenant + NameIdentifier | MAF isolation key + `session_records` | ID do MAF incorpora escopo; leitura/retomada REST, SignalR exige owner e tenant iguais. |
| Sala | tenant + usuário | `knowledge_rooms`, `knowledge_room_permissions` | ACL é obrigatória para obter sala e contexto RAG; permissões Reader não autorizam edição. |
| Suporte de sala | tenant + usuário membro + `room:<id>` | `tenant_support_grants` + ACL vinculada | Expira em até sete dias, motivo obrigatório, revogável; leitura gera auditoria. Não contorna ACL nem concede acesso a outras salas. |
| Documento/vetor | tenant + collection + metadata da sala | `vector_documents` + pgvector | SQL filtra tenant/sala antes do ranking; lista de salas vazia retorna zero. Uploads por source não são automaticamente atribuídos a sala. |
| Skill | tenant | `agent_skills` | Defaults são estáveis por tenant; seed preenche ausentes e preserva customizações e IDs legados. |
| Quota diária | tenant + UTC date | `tenant_quotas` | Incremento e reset PostgreSQL são atômicos; consumo e valores sobrevivem à troca de contexto/conexão. |
| Grupo SignalR | tenant + nome do recurso | memória do hub/conexão | Nome do grupo inclui tenant; evento de um tenant não é enviado via broadcast global. |
| Workflow | tenant + workflow/execution | definições e execuções PostgreSQL | Eventos carregam tenant e são endereçados ao grupo tenant/workflow; validação cruzada concorrente dos cinco hubs ainda está pendente. |
| Agente/tool | tenant/configuração | registros PostgreSQL e serviços | Ter catálogo ou vínculo não substitui autorização para executar ferramentas. |
| Golden set/run | tenant + agente/casos | sets PostgreSQL; run parcialmente em cache | Até 20 casos síncronos; acima disso assíncrono; cache de run 30 min não é armazenamento durável. |

## Planos e limites declarados

Fonte de valores do plano: [`TenantLimits`](../../src/AgenticSystem.Core/Models/Tenant.cs). Os valores mostram o teto nominal; para recursos sem enforcement unificado, não prometem limite efetivo.

| Plano | req/min | tokens/dia | USD/dia | sessões simultâneas | agentes | documentos MB |
|---|---:|---:|---:|---:|---:|---:|
| Free | 10 | 50.000 | 1 | 3 | 5 | 100 |
| Pro | 60 | 500.000 | 25 | 20 | 50 | 5.000 |
| Enterprise | 300 | 5.000.000 | 500 | 100 | 500 | 50.000 |

## Enforcement efetivo

| Limite | Fonte e enforcement | Estado/evidência |
|---|---|---|
| Chat RPM | Plano, `Tenant.Limits`, quota persistida; menor valor positivo é aplicado pela middleware/rate limiter | Partição em memória por processo; comportamento contra duas instâncias não foi testado. |
| Tokens e custo/dia | Plano como teto; `Tenant.Limits` e `TenantQuotas` podem restringir; `QuotaEnforcer` consulta estado persistido | Concorrência, persistência, reset e quota maior que plano passaram no PostgreSQL isolado. |
| Budget do gateway | `Gateway:DefaultDailyBudget` e fontes específicas do gateway | Modelo separado da quota diária LLM; não presumir igualdade de valores. |
| Sessões/agentes/documentos | `TenantResourceLimits` em `TenantIsolationService` | Fonte distinta de `Tenant.Limits`; unificação e enforcement nos endpoints permanecem pendentes. |
| Upload/storage físico | pipeline e `DocumentController` | Checagem não mede consistentemente tamanho efetivo; arquivo físico de mesmo nome pode sobrescrever cópia. |
| Estatística ONNX | `DocumentController.GetStats` | Estado pode ser inferido de configuração/paths; latência exibida é valor fixo, não medição real. |
| Limites de protocolos | `ProtocolHosting:RateLimiting` | Policy separada para A2A/AG-UI; não é a mesma partição usada pelo chat. |

O `TenantResourceLimits` atual possui defaults diferentes (sessões 10, storage 1000 MB, documentos 10000, agentes 20, budget mensal USD 100). Não some nem compare esses valores com `Tenant.Limits` como se houvesse uma única política.

## RAG e membership

[`PostgresVectorStore`](../../src/AgenticSystem.Infrastructure/Persistence/PostgresVectorStore.cs) aplica tenant e `room_id` na query PostgreSQL antes do ranking. O teste real encontrou a sala permitida entre 61 documentos same-tenant e excluiu outro tenant; lista vazia retornou zero. Outros filtros de metadata podem continuar em memória. `DocumentController.Ingest` define collection por `source`, sem parâmetro explícito de sala.

A migration copia `role_assignments` e API keys atuais para memberships sem promoção a Platform Admin. Administrações atuais ficam documentadas em [identidade e tenants](access-tenants.md); o índice de rotas foi regenerado a partir das actions de controller.

Veja [backlog](backlog.md) e [relatório de validação](validation/backend-core-remediation.md) para as lacunas que seguem abertas.