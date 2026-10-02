# Identidade, tenants e autorização

Este documento descreve os contratos da branch `fix/backend-core-tenancy`. O estado e as limitações das verificações estão no [relatório de validação](validation/backend-core-remediation.md), com decisão em [ADR-035](../architecture/adr/035-backend-core-isolation-and-reliability.md) e referência de acesso em [ADR-034](../architecture/adr/034-backend-contracts-and-access-target.md).

## Identidade, membership e papéis

Correção #143: ingestão simples/batch em sala exige ACL Editor/Admin no tenant real. Reader e grant de suporte sem ACL válida não autorizam escrita. Catálogo dinâmico InMemory e salas InMemory são particionados por tenant; a ausência de contexto não cria tenant implícito. `room_ids` é uma allow-list de `room_id`, acompanhada de `tenant_id`, também em InMemory/SQLite/Pinecone; lista vazia nega a busca.

Correção #140/PR #152: o navegador autentica API keys por cookie HttpOnly, sem gravar ou reenviar a chave no localStorage, store JavaScript ou headers dos clientes/hubs. `GET /api/auth/session` valida a sessão pelo pipeline e retorna o subject/tenant/papéis; logout exige sucesso do backend antes de limpar a interface. JWT explícito continua sendo um modo separado.

OpenAI compatível (`/v1/chat/completions` e `/v1/models`) usa o mesmo pipeline de autenticação e middleware tenant/membership. `Authorization: <raw-key>` é suportado por compatibilidade apenas sob `/v1`, com as mesmas negações de revogação, tenant inativo e spoofing do Bearer opaco. O campo `user` do request não vira identidade de autorização; o principal autenticado define o owner. O controller conserva o contexto e o plano resolvidos pelo middleware. [Evidência da correção](validation/pr132-review-remediation-2026-10-02.md).

| Camada | Fonte | Regra |
|---|---|---|
| Identidade | API key ou JWT/Supabase | API keys são localizadas por SHA-256; MVC normalmente recebe `X-Api-Key`, enquanto `/v1/chat/completions` recebe API key opaca no Bearer. JWT de tenant/Supabase usa o handler configurado. |
| Tenant | `tenant_id`, `app_metadata.tenant_id`, `X-Tenant-Id` | Tenant precisa existir e estar ativo. Identidade autenticada não pode selecionar outro tenant pelo header. |
| Membership | `tenant_memberships` | Liga principal, tenant, tipo de principal e papel. Papéis efetivos são carregados para o tenant selecionado. |
| API key | `access_api_keys` + membership `ApiKey` | A chave identifica o principal, não concede membership; papel efetivo vem do vínculo no tenant selecionado. Roles desconhecidas falham fechadas. |
| Papel de plataforma | `platform_administrators` | Registro explícito e separado; Admin/Owner do tenant nunca são promovidos automaticamente. |
| Sala | `knowledge_room_permissions` | ACL por usuário é obrigatória para ler uma sala, inclusive quando há suporte temporário. |

O runtime não atribui o tenant `default` quando falta contexto, e esse ID legado também é rejeitado explicitamente. `platform`, `system-background` e `system-devui` não são identificadores de tenant válidos; operações globais usam `SystemOperationContext` interno, separado de claims, headers e `TenantId`. Rotas autenticadas que usam dados de tenant exigem um tenant existente; persistência vetorial e arquivos temporários de modelos também recusam dados sem `TenantId`. Dados tenant-owned, incluindo alertas de quota, são filtrados pelo tenant corrente. Quotas e alertas de providers globais pertencem aos stores de plataforma e às rotas `/api/platform/*` autorizadas para Platform Admin.

Em banco vazio, configure `AgenticSystem__AdminApiKey` para provisionar o tenant `admin`, persistir somente o hash e criar a membership correspondente. Sem a chave, a API inicia sem tenant ou credencial e exige provisionamento explícito antes de operações tenant-owned. Em Development, as rotas de agente/DevUI que precisam de tenant não são mapeadas até existir um tenant ativo. Se já houver tenants, o bootstrap ignora a chave. O bootstrap não semeia agentes ou workflows de produto.

A migration `AddTenantMembershipAndSupportAccess` copia vínculos de `role_assignments` e API keys existentes, mantendo tenant e papel. Ela não preenche `platform_administrators`. Novas atribuições de papel sincronizam a tabela legada e membership.

API keys aceitam `Owner`, `Admin`, `Operator`, `Viewer`, `Member` e `ServiceAccount`; `Member` efetivamente recebe leitura. `Owner` e `Admin` são papéis do tenant, nunca equivalem ao administrador da plataforma.

## Política FIDES por tenant

Correção #141: a factory e o supervisor protegem o `IChatClient` em cada despacho ao provider, inclusive execução direta, streaming, instructions e rodadas de tools. O middleware de agente reutiliza o mesmo motor de proteção. Resultado/argumento estruturado de tool é serializado e redigido; CallId e opções são preservados. Ausência de política/tenant válido, política de outro tenant, payload não inspecionável, erro ou timeout bloqueiam antes da chamada externa. [Regressões e limites](validation/pr132-review-remediation-2026-10-02.md).

`GET /api/security/fides/policy` lê os toggles do tenant autenticado; `PUT` altera apenas detectores built-in conhecidos e exige `Owner` ou `Admin`. Sem uma política persistida, todos os detectores ficam ativos. `CredentialToken` é obrigatório e não pode ser desligado. Cada alteração incrementa a versão da política e gera auditoria.

| Método/rota | Entrada/resultado | Regra |
|---|---|---|
| GET /api/security/fides/policy | `{ tenantId, enabledDetectors, version, updatedBy, updatedAt }` | Somente Owner/Admin; ausência de registro retorna todos os detectores ligados. |
| PUT /api/security/fides/policy | `{ "enabledDetectors": { "Email": false } }` | Somente Owner/Admin; rejeita nomes desconhecidos e tentativa de desligar `CredentialToken`; grava versão e auditoria. |

O middleware inspeciona texto de usuário/sistema antes do provider e nunca registra o conteúdo original. Imagens e páginas PDF usam Tesseract OCR local; regiões detectadas são cobertas por redaction opaca. PDFs são rasterizados em um novo PDF para remover camadas de texto ocultas e metadados do original. Configure `AgenticSystem:Fides:TessDataPath` para a pasta com os modelos `eng` e `por` (`eng+por` é o padrão). Política ausente mantém todos os detectores ativos.

Falha de leitura da política, timeout/erro do detector, modelo OCR indisponível, baixa confiança, anexo não suportado ou região sem coordenadas conclusivas bloqueia a chamada antes do provider e pede uma cópia redigida. Limites de entrada: 10 MB, até 8 páginas PDF; timeout padrão 10 s. A configuração atual usa TesseractOCR 5.5.2 e PDFtoImage 5.4.0.

## Administração da plataforma

As rotas abaixo exigem autenticação e um registro explícito do `sub`/`NameIdentifier` em `platform_administrators`. A middleware dispensa seleção/membership de tenant apenas em `/api/platform/*`; o handler JWT ainda exige uma claim `tenant_id`, que não é usada para escolher o tenant nessa área.

| Método e rota | Entrada | Resultado e regra |
|---|---|---|
| `GET /api/platform/tenants` | — | Lista ID, nome, slug, plano, limites configurados e estado ativo. |
| `GET /api/platform/alerts` | `limit` (1–200) | Lista alertas globais de plataforma; alertas de BYOK de tenants não aparecem aqui. |
| `POST /api/platform/alerts/{id}/read` | — | Marca como lido somente um alerta global da plataforma. |
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
