# Recursos, regras e limites

Este documento descreve os limites aplicados pelo backend. A fonte dos limites por tenant é `Tenant.Limits`; `TenantResourceLimits` é apenas uma projeção compatível desses mesmos valores. Evidências da branch de correção estão em [validação #111–#117](validation/backend-core-remediation.md).

## Recursos e escopo

| Recurso | Escopo e persistência | Regra de autorização/uso |
|---|---|---|
| Tenant | `tenants` | Precisa existir e estar ativo. `X-Tenant-Id` precisa corresponder ao tenant associado à identidade; membership ativa no tenant é exigida. |
| Membership | `tenant_memberships` (com compatibilidade para `role_assignments`) | Cada usuário/API key tem um papel no tenant. Papéis administrativos de tenant não promovem a administrador da plataforma. |
| Papel de plataforma | `platform_administrators` | Registro explícito independente. Administração de tenants e grants não concede acesso aos dados do tenant. |
| Sessão/chat | `session_records` e estado serializado do MAF | A chave MAF é isolada por tenant + usuário. REST exige proprietário e tenant; eventos de usuário são enviados a grupo SignalR que também inclui tenant. |
| Knowledge room | `knowledge_rooms`, `knowledge_room_permissions` | Tenant e ACL de sala são necessários. Reader pode ler, mas não editar. |
| Concessão de suporte | `tenant_support_grants` e ACL ligada ao ID do grant | Somente para usuário já membro, escopo de uma sala, papel Reader, motivo obrigatório, duração máxima de 7 dias, revogável e auditada. A ACL normal continua sendo verificada. |
| Documento/vetor | `vector_documents` + pgvector | `tenant_id` e `room_id` são persistidos. Busca PostgreSQL pré-filtra tenant/salas permitidas antes do ranking. Sem salas permitidas, retorna zero resultados. |
| Agente dinâmico | Repositório de agentes e configuração YAML | Criação respeita `MaxAgents`; atualizar um agente existente continua permitido quando o tenant atingiu o teto. |
| Quota diária | `tenant_quotas` | Contadores por tenant/data UTC são atualizados atomicamente no PostgreSQL e compartilhados por conexões/repositórios independentes. |
| Quota de provider BYOK | `ExternalProviderQuotas` + `TenantId` real | Cotas e alertas de chaves próprias são isolados por tenant; queries sem contexto real falham. |
| Quota de provider global | `platform_external_provider_quotas` | Cotas de chaves do host são globais e não usam TenantId. Alertas globais ficam em `SystemAlerts`, acessíveis em `/api/platform/alerts` só para Platform Admin. |
| Alertas BYOK | `tenant_system_alerts` | Listagem e atualização de leitura em `/api/v1/alerts` usam o filtro global do tenant; tenant A não vê nem atualiza alertas do tenant B. |
| Skill | `agent_skills` | Defaults são preparados por tenant; seed preenche ausentes e conserva customizações/IDs existentes. |
| Workflow | Definições e execuções PostgreSQL | Tenant vem do contexto validado, é persistido e acompanha os eventos; ID de execução sozinho não autoriza acesso. |

## Limites por plano

O plano fornece o teto nominal. Limites configurados em `Tenant.Limits` podem ser menores; alterar o plano não apaga essa configuração. Valores `0` são interpretados conforme a regra do enforcer e não devem ser usados para representar um teto positivo.

| Plano | Requisições/min | Tokens/dia | Custo/dia (USD) | Sessões simultâneas | Agentes | Documentos | Origem por tenant (MB) |
|---|---:|---:|---:|---:|---:|---:|---:|
| Free | 10 | 50.000 | 1 | 3 | 5 | 10.000 | 100 |
| Pro | 60 | 500.000 | 25 | 20 | 50 | 100.000 | 5.000 |
| Enterprise | 300 | 5.000.000 | 500 | 100 | 500 | 1.000.000 | 50.000 |

Os valores são definidos por `TenantLimits.FreeTier/ProTier/EnterpriseTier` em [`Tenant.cs`](../../src/AgenticSystem.Core/Models/Tenant.cs). A coluna de custo é limite diário da quota de tokens/LLM. O campo legado `TenantResourceLimits.MaxMonthlyBudgetUsd` é uma projeção aritmética `MaxDailyCostUsd × 30`; não é um orçamento mensal independente nem um acumulador de gasto mensal.

## Enforcement e métricas

| Controle | Comportamento efetivo | Limite da métrica |
|---|---|---|
| RPM do chat | Rate limiter por tenant usa o menor limite positivo entre plano/configuração aplicáveis. | Estado do limiter é local ao processo; sincronização de RPM entre réplicas não foi provada nesta entrega. |
| Tokens e custo diário | Plano atua como teto e a quota configurada pode restringir. Cada chamada ao provider recebe preflight estimado; respostas persistem tokens/custo retornados pelo provider, com estimativa conservadora quando ele omite uso. PostgreSQL grava os totais por tenant e reset UTC atomicamente. | Quota excedida retorna 429 no chat REST e OpenAI-compatível; SSE termina com evento de erro. O ledger detalhado de auditoria é best-effort; o contador `tenant_quotas` é a fonte de enforcement. |
| Sessões simultâneas | Usa `Tenant.Limits.MaxConcurrentSessions`; sessão nova é recusada quando o total ativo alcança o limite. | Conta sessões ativas persistidas. |
| Agentes | `Tenant.Limits.MaxAgents` limita novas configurações dinâmicas e YAML. | Atualizações de agentes existentes não consomem novo slot. |
| Documentos | `Tenant.Limits.MaxDocuments` limita documentos lógicos no vetor; várias partes/chunks do mesmo documento contam uma vez. | Documentos legados sem ID lógico usam fallback por registro. |
| Armazenamento de origem | `Tenant.Limits.MaxDocumentsMb` é comparado com bytes de origem do upload, contabilizados uma vez por documento lógico. | É uma métrica de tamanho da origem associada aos vetores, não o espaço físico total em disco; não inclui overhead do banco, índice vetorial ou cópias/arquivos físicos. |
| Limite de entrada | Ingestão confere bytes brutos recebidos e total de documentos lógicos antes de persistir. | O número de bytes representa conteúdo de origem recebido; não mede a expansão gerada pelo chunking. |
| Budget do gateway | `Gateway:DefaultDailyBudget` e configuração do gateway continuam sendo um controle separado. | Não é somado nem tratado como a quota diária de tokens do tenant. |
| Estatísticas ONNX | `GET /api/document/stats` expõe contagens e status do runtime. | Latência exibida não é uma medição de inferência real. |
| A2A/AG-UI | `ProtocolHosting:RateLimiting` | Política separada da partição de RPM do chat. |

## Upload e RAG por sala

`POST /api/document/ingest` e `POST /api/document/batch` aceitam `roomId` opcional. Quando informado, o controller exige uma sala existente e permissão de escrita; os chunks recebem `room_id`, `document_id` e `source_bytes` nos metadados. A busca só inclui salas autorizadas pelo chamador. Sem `roomId`, o documento permanece no escopo do tenant/collection de origem e não é implicitamente concedido a uma sala.

Uma chamada de chat pode selecionar uma sala pelo contexto `rag.knowledgeRoomId`. A resposta de recuperação precisa continuar sujeita ao tenant e à ACL; a integração de validação confirma que a resposta usa um chunk da sala autorizada e contém a frase sintética indexada.

## Validação associada

Na branch `fix/backend-core-tenancy`, os testes PostgreSQL/Ollama cobriram teto de agentes, ingestão/bytes lógicos, RAG de ponta a ponta, serviço Gateway habilitado/desabilitado com broadcast tenant-scoped e quotas reais após restart. 32 atualizações concorrentes por dois repositórios/factories persistiram exatamente uma vez. Skills A/B e estado MAF permaneceram estáveis após restart. O limiter de RPM é local ao processo; armazenamento físico total e consumo de hosts múltiplos não foram medidos. Veja [evidências e limites](validation/backend-core-remediation.md).
# Analytics e memória — correções #146/#147

As consultas de `tenant_analytics` mantêm o scope DI vivo até terminar a enumeração; filtros globais continuam no tenant corrente. A cache de contexto é particionada por usuário, tenant, consulta exata, maxMemories e geração. Vectorizar novos insights troca a geração, evitando reutilizar contexto anterior; resultado vazio só é reutilizado para a mesma consulta. Não é prova de relevância semântica do provider real. [Regressões](validation/pr132-review-remediation-2026-10-02.md).
