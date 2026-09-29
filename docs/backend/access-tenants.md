# Identidade, tenants e autorização

Este documento descreve os contratos da branch `fix/backend-core-tenancy`. O estado e as limitações das verificações estão no [relatório de validação](validation/backend-core-remediation.md), com decisão em [ADR-035](../architecture/adr/035-backend-core-isolation-and-reliability.md) e referência de acesso em [ADR-034](../architecture/adr/034-backend-contracts-and-access-target.md).

## Identidade, membership e papéis

| Camada | Fonte | Regra |
|---|---|---|
| Identidade | API key ou JWT/Supabase | API keys são localizadas por SHA-256; MVC normalmente recebe `X-Api-Key`, enquanto `/v1/chat/completions` recebe API key opaca no Bearer. JWT de tenant/Supabase usa o handler configurado. |
| Tenant | `tenant_id`, `app_metadata.tenant_id`, `X-Tenant-Id` | Tenant precisa existir e estar ativo. Identidade autenticada não pode selecionar outro tenant pelo header. |
| Membership | `tenant_memberships` | Liga principal, tenant, tipo de principal e papel. Papéis efetivos são carregados para o tenant selecionado. |
| API key | `access_api_keys` + membership `ApiKey` | A role armazenada é usada; roles desconhecidas falham fechadas. |
| Papel de plataforma | `platform_administrators` | Registro explícito e separado; Admin/Owner do tenant nunca são promovidos automaticamente. |
| Sala | `knowledge_room_permissions` | ACL por usuário é obrigatória para ler uma sala, inclusive quando há suporte temporário. |

A migration `AddTenantMembershipAndSupportAccess` copia vínculos de `role_assignments` e API keys existentes, mantendo tenant e papel. Ela não preenche `platform_administrators`. Novas atribuições de papel sincronizam a tabela legada e membership.

API keys aceitam `Owner`, `Admin`, `Operator`, `Viewer`, `Member` e `ServiceAccount`; `Member` efetivamente recebe leitura. `Owner` e `Admin` são papéis do tenant, nunca equivalem ao administrador da plataforma.

## Administração da plataforma

As rotas abaixo exigem autenticação e um registro explícito do `sub`/`NameIdentifier` em `platform_administrators`. A middleware dispensa seleção/membership de tenant apenas em `/api/platform/*`; o handler JWT ainda exige uma claim `tenant_id`, que não é usada para escolher o tenant nessa área.

| Método e rota | Entrada | Resultado e regra |
|---|---|---|
| `GET /api/platform/tenants` | — | Lista ID, nome, slug, plano, limites configurados e estado ativo. |
| `PUT /api/platform/tenants/{tenantId}/plan` | `{ "plan": "Free" }` | Aceita `Free`, `Pro` ou `Enterprise`; altera plano e registra auditoria no tenant. Mantém `Tenant.Limits`, que podem restringir o teto do plano. |
| `GET /api/platform/tenants/{tenantId}/memberships` | — | Lista memberships por tipo/identidade, papel e dados de concessão. |
| `PUT /api/platform/tenants/{tenantId}/memberships/{subjectType}/{subjectId}` | `{ "role": "Viewer" }` | Cria ou altera membership. `subjectType` é `User` ou `ApiKey`; papéis aceitos: `Owner`, `Admin`, `Operator`, `Viewer`. API key precisa pertencer ao tenant. Para usuários, sincroniza `role_assignments`; para API keys, sincroniza o papel em `access_api_keys`. A operação é transacional e auditada. |
| `DELETE /api/platform/tenants/{tenantId}/memberships/{subjectType}/{subjectId}` | — | Revoga a membership e a atribuição legada do usuário, quando aplicável; registra auditoria. Repetição retorna 204. API key sem membership deixa de autenticar no tenant. |
| `GET /api/platform/tenants/{tenantId}/rooms/{roomId}/support-grants` | — | Lista grants do escopo, incluindo expirados e revogados para auditoria operacional. |
| `POST /api/platform/tenants/{tenantId}/rooms/{roomId}/support-grants` | `{ "userId": "...", "reason": "...", "expiresAt": "2026-09-29T12:00:00Z" }` | Concede somente `Reader` a usuário que já seja membro do tenant. Motivo é obrigatório; expiração deve estar no futuro e no máximo sete dias. Grant ativo duplicado para a mesma sala/usuário retorna 409. |
| `DELETE /api/platform/tenants/{tenantId}/rooms/{roomId}/support-grants/{grantId}` | — | Revoga o grant, remove apenas a ACL temporária com o ID vinculado e registra auditoria. Revogação repetida retorna 204. |

O bootstrap de administradores é explícito: configure `AgenticSystem__PlatformAdministrators__0=<subject-id>` (e índices seguintes, se necessário). O seed ocorre somente quando `platform_administrators` está vazia. Remova a configuração depois do primeiro bootstrap para que uma revogação operacional não seja reintroduzida em um reinício.

Administração da plataforma não concede acesso implícito a salas, documentos, RAG ou sessões. A permissão de sala criada para suporte usa o mesmo ID do grant; o store nega essa ACL após expiração/revogação. Cada leitura de sala por esse caminho grava `TenantSupportRoomAccessed` em `audit_entries`. Somente usuários já membros do tenant podem receber concessão. ACL continua sendo requisito em cada leitura.

## HTTP e SignalR

Envie `X-Api-Key` ou `Authorization: Bearer <JWT>` nas rotas comuns; o compatível OpenAI aceita a API key opaca como Bearer. Para rotas comuns, `X-Tenant-Id` precisa corresponder ao `tenant_id`/`app_metadata.tenant_id`; sem header, a claim é usada. Tenant desconhecido/inativo e membership ausente são negados antes do endpoint. Falha de credencial pode resultar em 401; seleção, atividade ou membership inválida resulta em 403.

Nos hubs, tenant de query/header precisa corresponder à claim e à membership. O filtro valida invocações e o middleware valida negociação/handshake. Grupos de chat, Gateway, Workflow, ExternalAgent e ONNX incluem tenant. Notificações de sessão são endereçadas ao grupo `{tenant}:{user}`, evitando `Clients.User` global para a mesma identidade em vários tenants. Workflow restaura `TenantId` persistido ao carregar execuções para manter o endereço do grupo após reload.

O registry e os dashboards do Gateway são globais, portanto controller REST e hub exigem registro explícito em `platform_administrators`; role `Viewer`/`Admin` do tenant não autoriza essas operações. Serviço desconhecido retorna 404 e não emite mudança de estado. O hub só autoriza dashboard/status/subscribe para Platform Admin.

Validação integrada conectou o mesmo Platform Admin em tenants A e B: `ServiceStatusChanged` de disable/enable foi entregue apenas à conexão A; um Viewer recebeu 403 no REST e negação explícita no hub; serviço inexistente retornou 404 sem broadcast. A fixture Gateway existe somente no ambiente `Validation`; ela valida controller e transporte SignalR, enquanto o produto ainda não registra serviços Gateway de providers na inicialização.

## Sessões MAF

O store hospedado aplica `IsolationKeyScopedAgentSessionStore`; a chave da API combina tenant e `NameIdentifier`. Falta de identidade de isolamento falha fechada. O adaptador persiste no ID de sessão original e propaga falhas de escrita. REST/SSE geraram sucesso, SignalR chat foi exercitado, retomada para usuário/tenant diferente foi negada e os registros persistiram após reinício. O diagnóstico também retomou chat real após restart, manteve o mesmo ID e confirmou que o estado MAF serializado foi desserializado e marcado no store.

## Planos, quotas e limites

Free/Pro/Enterprise definem o teto de RPM, tokens/dia e custo/dia. `Tenant.Limits` e configuração de quota persistida podem restringir o teto; alterar o plano não sobrescreve limites configurados. Antes de cada chamada ao provider, o cliente mede uso estimado e consulta o teto; após resposta, registra tokens/custo reais (ou estimativa conservadora se o provider não enviar uso) no PostgreSQL. Falha por quota é HTTP 429 em REST e OpenAI-compatível; SSE envia evento de erro terminal. RPM do limiter continua local ao processo e usa o menor limite efetivo do tenant.

`Tenant.Limits` é a única fonte de teto para sessões simultâneas, agentes dinâmicos, contagem de documentos e bytes de origem. `TenantResourceLimits` projeta esses campos para compatibilidade; o campo mensal legado é `MaxDailyCostUsd × 30`, não uma segunda quota. Veja [recursos e regras](resources-rules.md) para enforcement e limites das métricas de armazenamento.

## Evidência e lacunas

PostgreSQL 16/pgvector/Ollama isolados validaram rotas de tenant, membership, ACL/grants, quotas, RAG e hubs. A execução Release passou 701 testes, ignorou 1 e não teve falhas. Integração core passou 43/43, broadcast Gateway passou, store/quota/skills passou 10/10, backfill legado passou e os seis cenários pós-restart passaram, incluindo skills, quota tokens/custo e OpenAI-compatível por API key. Evidências e limitações estão no [relatório](validation/backend-core-remediation.md).
