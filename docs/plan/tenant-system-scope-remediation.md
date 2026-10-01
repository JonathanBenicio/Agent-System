# Plano — Escopos explícitos de sistema sem tenant sintético

Status: desenho e implementação concluídos nesta branch; gate de #132 validado em 2026-10-01. #97 permanece aberta até a revisão/merge da consolidação.
Issue: #97 · ADR: [ADR-026](../architecture/adr/026-auto-bootstrap-remove-default-tenant.md) · Story: ML19.1 em [USER-STORIES.md](../USER-STORIES.md).
Baseline: PR #132, branch `integration/develop-pr-stack-2026-09-30` contra `develop`; inspeção dos fluxos atualizada em 2026-10-01.

## Objetivo

Manter `TenantId` exclusivamente para dados de um tenant real. Operações de plataforma/background usam contexto de sistema tipado, não um `TenantId` sintético (`platform`, `system-background` ou `system-devui`); o identificador legado `default` também não pode ser resolvido em runtime. O contexto de sistema nunca pode vir de header ou claim.

## Fluxos auditados

- **Quotas externas:** BYOK exige tenant real. Chaves globais da plataforma usam escopo explícito de plataforma e armazenamento não tenant-scoped; ausência de tenant não deve converter automaticamente uma operação tenant-scoped em plataforma.
- **Tools e skills:** registro global de recursos internos não cria linhas em tabelas tenant-owned com `TenantId=system-background`. Catálogo de plataforma e variações/customizações de tenant ficam em stores separados; seeding tenant-owned percorre tenants reais.
- **Outbox:** o worker seleciona mensagens tenant-owned com capacidade de sistema tipada e filtros ignorados apenas nessa consulta; cada mensagem é processada/atualizada sob seu tenant real. Eventos globais usam outbox/platform store separado e não recebem TenantId fictício.
- **Rotação de secrets:** ConfigEntry é tenant-owned; o job enumera tenants provisionados sob operação de sistema tipada e processa cada um sob seu contexto real. Configuração global de plataforma, se expirar, segue fluxo separado.
- **Autenticação e administração:** resolução de Tenant registry, lookup global de hash de API key e autorização/admin de plataforma usam capacidades internas tipadas. Após resolver a chave, toda leitura/gravação tenant-owned usa o tenant real da chave e membership; os escopos não vêm de header/claim.
- **EF Core:** filtros tenant continuam ativos por padrão. Cada leitura cross-tenant recebe operação tipada, predicados explícitos e store global quando o dado pertencer à plataforma; `SaveChanges` exige contexto real apenas para entidades `ITenantEntity`.

## Etapas

| Entrega | Dependência | Verificação | Estado/evidência |
|---|---|---|---|
| Introduzir `SystemOperationContext` tipado, sem propriedade `TenantId` | Decisão aprovada | Reserved IDs rejeitados no resolver/contexto; missing tenant não concede acesso tenant-owned | Concluído; regressões de middleware, resolver e accessor passaram |
| Separar quotas/configuração global da plataforma das quotas/alertas tenant-scoped | Contexto tipado | Migration move pseudo-quotas; alertas globais e de tenant ficam em stores distintos | Concluído; migrations no Compose e teste PostgreSQL A/B passaram |
| Separar outbox global e tenant-owned | Contexto tipado | Migration preserva eventos; evento com tenant real fica no outbox tenant; global fica sem TenantId | Concluído; migração exercitada com dados semeados e teste PostgreSQL valida dispatch/ack por tenant |
| Separar catálogo global de tools/skills dos registros/customizações de tenant | Contexto tipado | Startup escreve no catálogo de plataforma; seed e CRUD tenant usam TenantId real | Concluído; catálogo platform/tenant e migration validados |
| Processar outbox e jobs multi-tenant sob escopos corretos | Contexto tipado | Seleção global usa capability; publicação, sessão, quota, workflow, ONNX e rotação usam tenant real por item | Concluído; testes isolados de outbox/rotação e suite PostgreSQL passaram |
| Resolver tenant/API key e autorizar administração de plataforma por operações tipadas | Contexto tipado | Nenhum header/claim cria `default`, `platform`, `system-background` ou `system-devui`; stores rejeitam esses IDs | Concluído; testes resolver/middleware e build Release passaram |
| Migrar pseudo-scope legado sem atribuir dados órfãos a outro tenant | Stores de plataforma | Migration preserva catálogos/quota/eventos e associa eventos somente a tenant real reconhecido | Concluído; PostgreSQL Compose confirmou zero pseudo-TenantIds nos registros migrados |

## Critérios de aceite

- [x] Toda escrita em entidade tenant-owned tem `TenantId` real e validado.
- [x] `default`, `platform`, `system-background` e `system-devui` não resolvem como tenant em middleware, JWT, hub, membership ou tenant registry.
- [x] Escopo de sistema é tipado e separado; consultas privilegiadas restantes são explícitas e com predicados/capabilities documentados.
- [x] Lookup de API key/admin e operação de plataforma não recebem escopo por request/claim/header.
- [x] Quota BYOK/alertas são tenant-scoped; quota global e alertas globais ficam em stores de plataforma.
- [x] Catálogo platform de tools/skills não grava em tabelas tenant-owned; overrides e ativação tenant ficam separados.
- [x] Jobs multi-tenant enumeram tenants reais e processam cada tenant sob seu contexto.
- [x] Mensagens tenant-owned da outbox preservam tenant real; eventos globais usam store separado sem pseudo-tenant.
- [x] PostgreSQL Compose validou isolamento A/B, outbox, rotação, migração e alertas.

## Validação consolidada

- `dotnet build --configuration Release`: passou sem warnings ou erros.
- Suite .NET com PostgreSQL Compose: passou; skips restantes são integrações dependentes de serviços não configurados nessa execução.
- PostgreSQL local isolado (`tests/backend-validation/compose.yml`, porta 55432): migration e linhas de teste verificaram movimentação de ferramentas, skills, quotas e outbox sintéticos; eventos com tenant real reconhecido foram devolvidos ao tenant correto.
- Teste de startup em `Development`, banco vazio e sem `AdminApiKey`: `/health` retornou 200; tenants, access keys, agents e workflows permaneceram vazios; endpoints de agente que exigem tenant não foram mapeados.
- `dotnet ef migrations has-pending-model-changes`: sem alterações pendentes.
- `IgnoreQueryFilters` restante está limitado a bootstrap/registro global, autenticação/admin plataforma e seleção global da outbox sob capability tipada; stores tenant-owned usam filtro e validação do contexto.
