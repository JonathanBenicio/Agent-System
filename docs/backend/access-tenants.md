# Identidade, tenants e autorização

Este documento descreve os contratos da branch `fix/backend-core-tenancy`. O estado e as limitações das verificações estão no [relatório de validação](validation/backend-core-remediation.md), com decisão em [ADR-035](../architecture/adr/035-backend-core-isolation-and-reliability.md) e referência de acesso em [ADR-034](../architecture/adr/034-backend-contracts-and-access-target.md).

## Identidade, membership e papéis

| Camada | Fonte | Regra |
|---|---|---|
| Identidade | API key ou JWT/Supabase | API keys são localizadas por SHA-256; JWT usa o handler configurado. |
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
| `GET /api/platform/tenants/{tenantId}/rooms/{roomId}/support-grants` | — | Lista grants do escopo, incluindo expirados e revogados para auditoria operacional. |
| `POST /api/platform/tenants/{tenantId}/rooms/{roomId}/support-grants` | `{ "userId": "...", "reason": "...", "expiresAt": "2026-09-29T12:00:00Z" }` | Concede somente `Reader` a usuário que já seja membro do tenant. Motivo é obrigatório; expiração deve estar no futuro e no máximo sete dias. Grant ativo duplicado para a mesma sala/usuário retorna 409. |
| `DELETE /api/platform/tenants/{tenantId}/rooms/{roomId}/support-grants/{grantId}` | — | Revoga o grant, remove apenas a ACL temporária com o ID vinculado e registra auditoria. Revogação repetida retorna 204. |

O bootstrap de administradores é explícito: configure `AgenticSystem__PlatformAdministrators__0=<subject-id>` (e índices seguintes, se necessário). O seed ocorre somente quando `platform_administrators` está vazia. Remova a configuração depois do primeiro bootstrap para que uma revogação operacional não seja reintroduzida em um reinício.

Administração da plataforma não concede acesso implícito a salas, documentos, RAG ou sessões. A permissão de sala criada para suporte usa o mesmo ID do grant; o store nega essa ACL após expiração/revogação. Cada leitura de sala por esse caminho grava `TenantSupportRoomAccessed` em `audit_entries`. Somente usuários já membros do tenant podem receber concessão. ACL continua sendo requisito em cada leitura.

## HTTP e SignalR

Envie `X-Api-Key` ou `Authorization: Bearer`. Para rotas comuns, `X-Tenant-Id` precisa corresponder ao `tenant_id`/`app_metadata.tenant_id`; sem header, a claim é usada. Tenant desconhecido/inativo e membership ausente são negados antes do endpoint. Falha de credencial pode resultar em 401; seleção, atividade ou membership inválida resulta em 403.

Nos hubs, tenant de query/header também precisa corresponder à claim e à membership. O filtro valida invocações e middleware valida a negociação/handshake. `OnnxHub`, `WorkflowHub`, `ExternalAgentHub` e `GatewayHub` prefixam grupos com o tenant; notificações de workflow/FinOps são direcionadas ao grupo tenant. Testes integrados exercitaram negociação Gateway e chat; entrega cruzada concorrente nos cinco hubs ainda não foi comprovada.

## Sessões MAF

O store hospedado aplica `IsolationKeyScopedAgentSessionStore`; a chave da API combina tenant e `NameIdentifier`. Falta de identidade de isolamento falha fechada. O adaptador persiste no ID de sessão original e propaga falhas de escrita. REST/SSE geraram sucesso, SignalR chat foi exercitado, retomada para usuário/tenant diferente foi negada e os registros persistiram após reinício. Restauração do estado serializado interno do MAF após reinício ainda não tem teste isolado.

## Planos, quotas e limites

Free/Pro/Enterprise definem o teto de RPM, tokens/dia e custo/dia. `Tenant.Limits` e configuração de quota persistida podem restringir o teto; alterar o plano não sobrescreve limites configurados. Incremento/reset diário usam operações atômicas em PostgreSQL e a leitura de autorização não reutiliza snapshot diário em cache. RPM do chat também usa o menor limite efetivo para o tenant.

Limites de sessões, agentes e armazenamento ainda têm fontes distintas em `TenantResourceLimits`; não estão unificados com `Tenant.Limits`. Veja [recursos e regras](resources-rules.md) para fonte, persistência e enforcement por recurso.

## Evidência e lacunas

PostgreSQL 16/pgvector isolado aplicou a migration. A suíte unitária passou 689 testes, ignorou um teste dependente de PostgreSQL e não teve falhas. Integração real passou auth/membership, ACL, grants auditados, expiração/revogação, quota, RAG entre 61 documentos candidatos, chat REST/SSE/SignalR e store/skills. Entrega cruzada negativa por hub, restore do estado MAF, backfill contra cópia de dados legados e unificação de limites de recursos permanecem pendentes. Evidência discriminada está no [relatório](validation/backend-core-remediation.md).