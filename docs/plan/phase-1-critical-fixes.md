# Roadmap: Fase 1 — Correções Críticas de Segurança e Corretude

> **Status documental:** Concluído
> **Escopo:** Corrigir 3 bugs de alto risco identificados na auditoria arquitetural: filtragem vetorial in-memory, quotas voláteis de tenant e normalização incorreta de cron.
> **Fonte de verdade operacional:** [Backend Architecture Audit](backend-architecture-audit.md)
> **Gerado em:** 04 de Junho de 2026
> **Projeto:** AgenticSystem

---

## Objetivo

Eliminar três falhas críticas que comprometem segurança, corretude e confiabilidade do sistema em produção, sem quebrar nenhuma interface existente. Estas correções são pré-requisito para a Fase 2 (Migração MAF 1.9.0), pois garantem que a base de dados e lógica de negócio estejam estáveis antes de qualquer refatoração de infraestrutura.

## Princípios de Implantação

1. **Zero breaking changes em contratos públicos** — nenhuma interface ou endpoint existente deve mudar de assinatura.
2. **Retrocompatibilidade de dados** — migrações de banco devem manter dados existentes íntegros.
3. **Falha explícita > silêncio** — substituir todos os catch vazios por logging estruturado e exceções tipadas.
4. **Cobertura de testes obrigatória** — cada correção deve ser acompanhada de testes unitários e/ou de integração (padrão AAA, cobertura ≥ 80%).

## Fases e Sequenciamento

| Ordem | Frente/Fase | Motivo do sequenciamento |
|---|---|---|
| 1 | Filtragem SQL no PGVector | Risco de segurança ativo em produção (bypass de room isolation) |
| 2 | Persistência de Quotas no PostgreSQL | Sem persistência, FinOps é inutilizável em produção multi-instância |
| 3 | Cron normalization + error propagation | Jobs silenciosamente nunca executam; risco de starvation de tarefas |

---

## Detalhamento 1: Filtro de Room no PGVector (SQL-Level)

### Por que implementar?

`PostgresVectorStore.SearchWithFiltersAsync` executa o filtro de `room_ids` **em memória**, após buscar os top-50 candidatos semânticos. Isso significa:
- Se os 50 documentos mais próximos semanticamente não pertencem à room autorizada, o retorno é vazio — **falso negativo de segurança**.
- Um tenant pode indiretamente provar a existência de documentos de outra room baseando-se em ausências de resultado.

### Arquitetura-alvo

```
Antes:  pgvector(cosine_distance) → TOP 50 → [in-memory filter room_ids] → result
Depois: pgvector(cosine_distance) WHERE metadata->>'room_id' = ANY(@roomIds) → TOP K → result
```

### Componentes propostos

| Componente | Papel |
|---|---|
| `PostgresVectorStore.cs` | Incorporar filtro de metadata diretamente no predicado SQL/pgvector |
| `IVectorStore` (interface) | Nenhuma mudança de assinatura necessária |
| Migration EF Core | Adicionar índice GIN em `metadata` jsonb se ainda não existir |

### Plano por etapas

1. **Auditar a query atual** em `SearchWithFiltersAsync` — mapear o ponto exato do filtro in-memory.
2. **Adicionar predicado SQL** usando `metadata @> '{"room_id": "X"}'::jsonb` ou `metadata->>'room_id' = ANY(...)` na query pgvector.
3. **Verificar índice GIN** — criar migration se necessário: `CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_vectors_metadata ON vectors USING GIN (metadata)`.
4. **Atualizar testes** — unit tests com InMemory provider (mock) e integration test com Npgsql real.
5. **Remover código de filtro in-memory** após validação dos testes.

### Critérios de Aceite e SLOs

* [ ] Filtro de `room_ids` executado inteiramente no banco (SQL), sem processamento in-memory posterior.
* [ ] Consultas com `room_ids` válidos retornam apenas documentos daquela room.
* [ ] Consultas cross-room retornam `[]` mesmo que existam documentos semanticamente próximos.
* [ ] Testes de integração passando com banco PostgreSQL real (não InMemory).
* [ ] Índice GIN criado via migration sem downtime (`CONCURRENTLY`).

### Riscos e Mitigações

| Risco | Mitigação |
|---|---|
| Índice GIN em tabela grande pode demorar | Usar `CREATE INDEX CONCURRENTLY` para não bloquear reads |
| Sintaxe jsonb diferente do InMemory | Isolar lógica em provider-specific method + mock nos testes unitários |

---

## Detalhamento 2: Persistência de Quotas de Tenant no PostgreSQL

### Por que implementar?

`QuotaEnforcer` usa `ConcurrentDictionary` em memória. Em produção:
- **Quotas resetam** a cada restart da aplicação.
- **Escalabilidade horizontal impossível** — cada instância tem seu próprio contador.
- **Planejado mas não implementado:** `ProactiveQuotaManager` estava documentado como ativo, mas nunca foi implementado.

### Arquitetura-alvo

```
Request → QuotaEnforcer → IMemoryCache (TTL: 60s) → PostgreSQL (TenantQuota table)
                                 ↑ Cache Miss / Invalidação
```

### Componentes propostos

| Componente | Papel |
|---|---|
| `TenantQuotaEntity` | Nova entidade EF Core para persistência de quotas |
| `QuotaEnforcer` | Refatorar para ler/gravar via IMemoryCache + PostgreSQL |
| `ITenantQuotaRepository` | Abstração de repositório para quotas |
| `TenantQuotaRepository` | Implementação concreta com EF Core |
| Migration EF Core | Criar tabela `TenantQuotas` com índice por `TenantId` |

### Plano por etapas

1. **Criar entidade** `TenantQuotaEntity` com campos: `TenantId`, `DailyBudget`, `CurrentDailyUsage`, `ResetAt`, `UpdatedAt`.
2. **Criar migration** EF Core: `dotnet ef migrations add AddTenantQuotas ...`.
3. **Implementar** `ITenantQuotaRepository` e `TenantQuotaRepository`.
4. **Refatorar** `QuotaEnforcer`:
   - Ler quota via `IMemoryCache` primeiro (TTL 60s).
   - Em cache miss, buscar do `ITenantQuotaRepository` e popular cache.
   - Gravar incrementos no banco com atualização otimista (row-level locking).
5. **Implementar reset diário** via `IHostedService` ou Quartz job para zerar `CurrentDailyUsage` à meia-noite UTC.
6. **Atualizar testes** — substituir mocks de `ConcurrentDictionary` por `ITenantQuotaRepository`.

### Critérios de Aceite e SLOs

* [ ] Quotas persistem após restart da aplicação.
* [ ] Múltiplas instâncias compartilham o mesmo estado de quota via PostgreSQL.
* [ ] Cache com TTL de 60s reduz leituras ao banco sob carga.
* [ ] Reset diário funcional via job agendado.
* [ ] Testes unitários com `ITenantQuotaRepository` mockado (NSubstitute).

### Riscos e Mitigações

| Risco | Mitigação |
|---|---|
| Contention no banco em alta concorrência | Usar `UPDATE ... RETURNING` com incremento atômico no PostgreSQL |
| Cache stale entre instâncias | Aceitar janela de 60s de inconsistência (documentar como trade-off em ADR-031) |

---

## Detalhamento 3: Cron Normalization + Error Propagation

### Por que implementar?

`ScheduledTaskManager.NormalizeCronExpression` tem dois bugs:
1. **Day-of-Week incorreto:** Não aplica a conversão Linux (0-6) → Quartz (1-7), causando `ParseException` silenciosa.
2. **Catch vazio:** Exceções de parsing retornam `NextRunAt = null` sem nenhum log ou erro — tarefas parecem agendadas mas nunca executam.

### Componentes propostos

| Componente | Papel |
|---|---|
| `ScheduledTaskManager.cs` | Corrigir `NormalizeCronExpression` e adicionar propagação de erro |
| `IScheduledTaskValidator` (novo) | Interface para validação de cron com resultado tipado |

### Plano por etapas

1. **Corrigir** `NormalizeCronExpression`: aplicar `(day % 7) + 1` no campo Day-of-Week ao converter de 5 para 6 campos.
2. **Substituir catch vazio** por `catch (FormatException ex)` com `logger.LogError(ex, "Cron inválido: {Expression}", expression)` e re-throw de exceção tipada `InvalidCronExpressionException`.
3. **Adicionar validação antecipada** no endpoint de criação de tasks para rejeitar expressões inválidas antes de persistir.
4. **Escrever testes parametrizados** cobrindo: expressão 5-campos, 6-campos, Day-of-Week 0 (domingo Linux), Day-of-Week 7 (domingo Linux alternativo), expressões inválidas.

### Critérios de Aceite e SLOs

* [ ] Cron `0 9 * * 1` (segunda-feira às 9h) agendado e executado corretamente.
* [ ] Cron `0 9 * * 0` (domingo) converte corretamente para Quartz `0 9 * * 1`.
* [ ] Cron inválido lança `InvalidCronExpressionException` com mensagem legível (não retorna null).
* [ ] `NextRunAt` nunca é `null` para um cron válido após o fix.
* [ ] Testes parametrizados com ≥ 8 casos de entrada cobrindo edge cases de DOW.

### Riscos e Mitigações

| Risco | Mitigação |
|---|---|
| Jobs já agendados com DOW errado no banco | Script de migration para reprocessar `NextRunAt` dos jobs existentes |
| Breaking change na API de tasks | Validar que o campo `cronExpression` no DTO já é validado — adicionar feedback 422 |
