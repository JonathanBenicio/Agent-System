# Plano — Escopos explícitos de sistema sem tenant sintético

Status: desenho aprovado; implementação pendente de definição de merge gate.
Issue: #97 · ADR: [ADR-026](../architecture/adr/026-auto-bootstrap-remove-default-tenant.md) · Story: ML19.1 em [USER-STORIES.md](../USER-STORIES.md).
Baseline: PR #132, branch `integration/develop-pr-stack-2026-09-30` contra `develop`; inspeção dos fluxos atualizada em 2026-10-01.

## Objetivo

Manter `TenantId` exclusivamente para dados de um tenant real. Operações de plataforma/background usam contexto de sistema tipado, não um `TenantId` sintético (`platform` ou `system-background`), e esse contexto nunca pode vir de header ou claim.

## Fluxos auditados

- **Quotas externas:** BYOK exige tenant real. Chaves globais da plataforma usam escopo explícito de plataforma e armazenamento não tenant-scoped; ausência de tenant não deve converter automaticamente uma operação tenant-scoped em plataforma.
- **Tools e skills:** registro global de recursos internos não cria linhas em tabelas tenant-owned com `TenantId=system-background`. Customizações do tenant continuam exigindo o tenant autenticado.
- **Outbox:** o worker pode selecionar mensagens pendentes sob operação de sistema explicitamente autorizada; cada mensagem mantém seu `TenantId` real e é publicada/atualizada sob o contexto desse tenant.
- **Rotação de secrets:** ConfigEntry é tenant-owned; o job percorre tenants provisionados e processa cada um sob seu contexto. Configuração global de plataforma, se expirar, segue fluxo separado.

## Etapas

| Entrega | Dependência | Verificação | Estado/evidência |
|---|---|---|---|
| Introduzir `SystemOperationContext` tipado, com operações internas permitidas e sem propriedade `TenantId` | Decisão aprovada | API/JWT/header não conseguem criar ou selecionar escopo de sistema | Pendente |
| Separar quota global da plataforma das quotas tenant-scoped | Contexto tipado | Migration preserva dados; consultas de tenant e plataforma não se misturam | Pendente |
| Remover persistência de tools/skills em pseudo-tenant; separar catálogo global de registros/customizações do tenant | Catálogo de recursos | Inicialização sem tenant não grava `platform`/`system-background`; runtime tenant mantém catálogo esperado | Pendente |
| Processar outbox com escopo de sistema apenas na seleção e tenant real por mensagem | Contexto tipado | Eventos de A/B são publicados uma vez por mensagem e estado permanece no tenant de origem | Pendente |
| Fazer rotação de secrets tenant-scoped por tenant real | Contexto tipado | Secrets de A e B são encontrados/auditados apenas no escopo correspondente | Pendente |
| Reconciliar dados legados e documentação | Implementação | Busca/SQL confirma zero novos pseudo-TenantIds; migration mantém histórico e escopos corretos | Pendente |

## Critérios de aceite

- [ ] Toda escrita em entidade tenant-owned tem `TenantId` real e validado.
- [ ] `platform`/`system-background` não resolvem como tenant em middleware, JWT, hub ou membership.
- [ ] Escopo de sistema é interno, tipado e não desabilita filtros globais implicitamente; cada consulta cross-tenant usa operação permitida e predicados explícitos.
- [ ] Quota BYOK é armazenada por tenant; quota de chave global fica em store de plataforma, sem TenantId fictício.
- [ ] Job por tenant enumera tenants reais; operação global não varre dados tenant-owned por meio de um pseudo-ID.
- [ ] Testes de PostgreSQL validam isolamento A/B, outbox, rotação e compatibilidade com dados legados.

## Riscos e gaps

O código atual ainda usa `system-background` em `PostgresToolManager`, `PostgresSkillManager`, `OutboxProcessorBackgroundService` e `SecretRotationBackgroundService`, e `platform` em `ExternalQuotaSyncService`. O PR #132 não deve declarar #97 completamente atendida até esta refatoração e sua validação serem concluídas ou o usuário decidir explicitamente movê-la para follow-up.
