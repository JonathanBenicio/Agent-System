# Identidade, tenants e autorização
Estado atual por leitura da baseline; resultados executados em [validação](validation/2026-09-28.md).

## Camadas que não devem ser confundidas
| Camada | Modelo atual | Ponto de aplicação | Limitação |
|---|---|---|---|
| Identidade | API key ou JWT/Supabase | [MultiAuth](../../src/AgenticSystem.Api/Extensions/SecurityServiceCollectionExtensions.cs) | JWT com issuer AgenticSystem vai ao handler próprio; outros ao Supabase |
| Tenant | organização com plano, limites, ativo | [TenantMiddleware](../../src/AgenticSystem.Api/Middleware/TenantMiddleware.cs) e query filters EF | resolução de tenant não prova membership |
| Papel | Owner/Admin/Operator/Viewer no modelo de permissões | [PostgresPermissionService](../../src/AgenticSystem.Infrastructure/Persistence/PostgresPermissionService.cs) | presença do serviço não significa enforcement em todos os endpoints |
| Sala | Reader/Editor/Admin por usuário/sala | [PostgresKnowledgeRoomStore](../../src/AgenticSystem.Infrastructure/Persistence/PostgresKnowledgeRoomStore.cs) | ACL independente do papel global |
| Plano | Free/Pro/Enterprise | [Tenant](../../src/AgenticSystem.Core/Models/Tenant.cs) | valores declarados e limites efetivos divergem; ver [matriz](resources-rules.md) |

## HTTP atual
Enviar X-Api-Key ou Authorization: Bearer e, quando necessário, X-Tenant-Id. A identidade vem do principal autenticado; userId de ChatRequest não substitui a identidade.
Header X-Tenant-Id precede claim tenant_id; fallback app_metadata.tenant_id. Usuário autenticado sem papel Admin recebe 403 se tenant resolvido diferir do claim. Admin é dispensado dessa comparação. Em rotas com AuthorizeAttribute, tenant ausente/desconhecido/inativo recebe 403. Fallback de tenant desconhecido em rota sem esse atributo não é condicionado por ambiente; endpoints RequireAuthorization devem ser verificados separadamente.
Ordem do middleware pode produzir 403 antes do challenge 401: não assumir resposta uniforme para falta de credencial.

A [API key](../../src/AgenticSystem.Api/Auth/ApiKeyAuthenticationHandler.cs) procura SHA-256 de chave habilitada sem filtro de tenant; emite role Admin literal, ignorando AccessApiKeyEntity.Role. Isso permite override de tenant pelo header para qualquer chave válida. É lacuna, não hierarquia desejada.
JWT próprio exige assinatura válida, issuer/audience e tenant_id, com tolerância de expiração de dois minutos. Não implementar validação só decodificando JWT.

## SignalR atual
[TenantHubFilter](../../src/AgenticSystem.Api/SignalR/TenantHubFilter.cs) resolve header → query X-Tenant-Id → tenant_id → app_metadata.tenant_id. Mantém fallback para tenant desconhecido sem condicionamento por ambiente. O filtro não reproduz a checagem de papel/claim do middleware. HUB-02/03 confirmaram que JWT de A com query de B/desconhecido completou GetDashboard; não se comprovou leitura de conteúdo de salas nessa prova. Ver [#112](https://github.com/JonathanBenicio/Agent-System/issues/112).

## Hierarquia desejada, ainda não implementada
| Ator | Alcance desejado | Conteúdo |
|---|---|---|
| Platform Admin | tenants, planos, saúde da plataforma | acesso somente com concessão temporária de suporte |
| Owner | administração do próprio tenant e ownership | sala exige ACL |
| Admin | administração delegada no tenant | sala exige ACL |
| Operator | operação dos recursos autorizados | sala exige ACL |
| Viewer | consulta dos recursos autorizados | sala exige ACL |

Membership por usuário/tenant com papel próprio; suporte exige motivo, escopo de recursos, expiração, auditoria e revogação. Matriz detalhada de ações e migração deve ser decidida na implementação; não inferir do enum atual. Ver [ADR-034](../architecture/adr/034-backend-contracts-and-access-target.md).
