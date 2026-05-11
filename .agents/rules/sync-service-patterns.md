---
trigger: model_decision
description: Contrato completo e checklist obrigatório para criação, modificação e revisão de qualquer SyncService no Tabatine Engine.
---

# SyncService Contract — Padrões Obrigatórios

> **Escopo:** Este documento define o contrato completo que TODO `SyncService` do Tabatine Engine DEVE seguir. Usado tanto para **criação** de novos serviços quanto para **code review** de PRs existentes.
>
> **Origem:** Gaps identificados no [PR #47](../../pull/47) — [Epic #48](../../issues/48).

---

## 1. Checklist de Implementação (18 Pontos)

O agente DEVE validar TODOS os itens abaixo ao criar ou revisar um `SyncService`.

### ✅ Estrutura Base

| # | Check | Obrigatório |
|---|---|---|
| 1 | Implementa `ISyncService` | Sim |
| 2 | Usa **Primary Constructor** (sem campos `private readonly` manuais) | Sim |
| 3 | `SyncKey` como `private const string` com nome único e estável | Sim |
| 4 | File-scoped namespace (`namespace Tabatine.Infrastructure.Services;`) | Sim |

### ✅ Cancelamento e Cursor (CRÍTICO)

| # | Check | Obrigatório |
|---|---|---|
| 5 | Guard `if (!ct.IsCancellationRequested)` **ANTES** de `SetLastSyncDateAsync` | **Sim — Bloqueante** |
| 6 | `CancellationToken` propagado para **TODAS** as chamadas assíncronas | Sim |
| 7 | Guard `if (ct.IsCancellationRequested) break;` dentro de loops de paginação | Sim |

> [!CAUTION]
> Salvar o cursor sem o guard de cancelamento marca a sincronização como bem-sucedida mesmo que incompleta. O próximo ciclo **ignorará dados não processados**, causando perda silenciosa de dados.

### ✅ Idempotência e Deduplicação

| # | Check | Obrigatório |
|---|---|---|
| 8 | `HashSet<long>` por ciclo para controle de IDs duplicados | Sim |
| 9 | Upsert baseado em `OmieId` (verificar existência ANTES de inserir) | Sim |
| 10 | Comparar `OmieUpdatedAt` para evitar regravações desnecessárias | Recomendado |

### ✅ Performance e Memória

| # | Check | Obrigatório |
|---|---|---|
| 11 | Pré-carregamento em batch via `ToDictionaryAsync` (evita N+1) | Sim |
| 12 | `IAsyncEnumerable<T>` com `yield return` para listagens massivas | Sim (entidades de alto volume) |
| 13 | `AsNoTracking()` em queries de leitura que não serão atualizadas | Recomendado |

### ✅ Operações EF Core (CRÍTICO)

| # | Check | Obrigatório |
|---|---|---|
| 14 | Entidades rastreadas: **NUNCA** usar `Add()` — apenas modificar propriedades | **Sim — Bloqueante** |
| 15 | `SaveChangesAsync(ct)` **sempre** com o CancellationToken | Sim |
| 16 | FK violations: capturar e logar **por item**, não descartar o lote inteiro | Recomendado |

> [!CAUTION]
> Chamar `_dbContext.Set<T>().Add(entity)` em uma entidade já rastreada pelo EF Core (obtida via `FindAsync` ou `FirstOrDefaultAsync` sem `AsNoTracking()`) causa `InvalidOperationException` ou INSERT duplicado.

### ✅ Logging

| # | Check | Obrigatório |
|---|---|---|
| 17 | Caminhos `null` críticos logam em nível `Error` (nunca silêncio) | **Sim — Bloqueante** |
| 18 | Início de sync: `LogInformation` / Erros recuperáveis: `LogWarning` / Falhas que interrompem: `LogError` | Sim |

---

## 2. Anti-Patterns — Exemplos Concretos

### Anti-Pattern 1: Cursor salvo sem guard de cancelamento

```csharp
// ❌ PROIBIDO — salva cursor mesmo com sync incompleta
public async Task SyncAsync(CancellationToken ct)
{
    // ... lógica de sync que pode ser cancelada no meio ...
    await syncState.SetLastSyncDateAsync("Bancos", DateTime.UtcNow, ct);
}

// ✅ CORRETO — guard obrigatório
public async Task SyncAsync(CancellationToken ct)
{
    // ... lógica de sync ...
    if (!ct.IsCancellationRequested)
    {
        await syncState.SetLastSyncDateAsync("Bancos", DateTime.UtcNow, ct);
    }
}
```

### Anti-Pattern 2: `Add()` em entidade rastreada

```csharp
// ❌ PROIBIDO — entidade já rastreada, causa InvalidOperationException
var existing = await dbContext.NotasFiscais.FirstOrDefaultAsync(n => n.OmieId == omieId, ct);
if (existing != null)
{
    existing.Status = novoStatus;
    dbContext.NotasFiscais.Add(existing); // BUG!
    await dbContext.SaveChangesAsync(ct);
}

// ✅ CORRETO — EF Core detecta automaticamente as mudanças
var existing = await dbContext.NotasFiscais.FirstOrDefaultAsync(n => n.OmieId == omieId, ct);
if (existing != null)
{
    existing.Status = novoStatus;
    existing.UpdatedAt = DateTime.UtcNow;
    await dbContext.SaveChangesAsync(ct); // EF gera UPDATE automaticamente
}
```

### Anti-Pattern 3: Silêncio no caminho `null` crítico

```csharp
// ❌ PROIBIDO — null silencioso em operação crítica
var dbEvent = await dbContext.WebhookEvents.FindAsync(id, ct);
if (dbEvent != null)
{
    dbEvent.Status = WebhookEvent.StatusCompleted;
    await dbContext.SaveChangesAsync(ct);
}
// Se dbEvent == null, nada acontece e ninguém sabe

// ✅ CORRETO — log explícito de perda
var dbEvent = await dbContext.WebhookEvents.FindAsync(id, ct);
if (dbEvent == null)
{
    logger.LogError("WebhookEvent {Id} não encontrado para atualização final. Evento pode ter sido perdido.", id);
    return;
}
dbEvent.Status = WebhookEvent.StatusCompleted;
await dbContext.SaveChangesAsync(ct);
```

### Anti-Pattern 4: `ChangeTracker.Clear()` descarta lote inteiro

```csharp
// ❌ PROBLEMÁTICO — 1 FK inválida descarta 499 registros válidos
catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23503" })
{
    logger.LogWarning(ex, "FK violation detectada.");
    dbContext.ChangeTracker.Clear(); // Descarta TODAS as mudanças pendentes
}

// ✅ PREFERÍVEL — salvar item a item com retry individual
foreach (var item in batch)
{
    try
    {
        dbContext.Saldos.Add(item);
        await dbContext.SaveChangesAsync(ct);
    }
    catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23503" })
    {
        logger.LogWarning("FK inválida para item OmieId={OmieId}. Ignorando.", item.OmieId);
        dbContext.Entry(item).State = EntityState.Detached;
    }
}
```

### Anti-Pattern 5: Renomear `SyncKey` sem migrar cursor

```csharp
// ❌ PROIBIDO — cursor antigo "ContasPagar" é abandonado, causa Full Sync
private const string SyncKey = "LancamentosPagar"; // era "ContasPagar"

// ✅ CORRETO — acompanhar com data migration
// 1. Manter compatibilidade temporária OU
// 2. Criar migration SQL:
//    UPDATE sync_states SET key = 'LancamentosPagar' WHERE key = 'ContasPagar';
```

---

## 3. Template Canônico de SyncService

```csharp
namespace Tabatine.Infrastructure.Services;

public class NomeDaEntidadeSyncService(
    IOmieClient omieClient,
    AppDbContext dbContext,
    ISyncStateRepository syncState,
    IDistributedLockService lockService,
    ILogger<NomeDaEntidadeSyncService> logger) : ISyncService
{
    private const string SyncKey = "NomeDaEntidade";

    public async Task SyncAsync(CancellationToken ct)
    {
        // Obter lock distribuído para evitar concorrência entre instâncias
        await using var lockHandle = await lockService.AcquireAsync(SyncKey, ct);
        if (lockHandle == null)
        {
            logger.LogWarning("[{SyncKey}] Não foi possível obter lock. Abortando.", SyncKey);
            return;
        }

        logger.LogInformation("[{SyncKey}] Iniciando sincronização.", SyncKey);

        var lastSync = await syncState.GetLastSyncDateAsync(SyncKey, ct);
        var processados = new HashSet<long>();
        var totalProcessados = 0;

        // Pré-carregamento em batch para evitar N+1
        var existingMap = await dbContext.NomeDaEntidades
            .ToDictionaryAsync(e => e.OmieId, ct);

        await foreach (var dto in omieClient.StreamNomeDaEntidadeAsync(lastSync, ct))
        {
            if (ct.IsCancellationRequested) break; // Guard de loop

            if (!processados.Add(dto.CodigoOmie)) continue; // Deduplicação

            if (existingMap.TryGetValue(dto.CodigoOmie, out var existing))
            {
                // ✅ Modificar propriedades — EF rastreia automaticamente
                existing.Campo = dto.Campo;
                existing.UpdatedAt = DateTime.UtcNow;
                existing.OmieUpdatedAt = OmieTimestampHelper.Parse(dto.DAlt, dto.HAlt);
            }
            else
            {
                var entity = MapToDomain(dto);
                dbContext.NomeDaEntidades.Add(entity);
                existingMap[entity.OmieId] = entity;
            }

            totalProcessados++;
        }

        await dbContext.SaveChangesAsync(ct);

        // ✅ Guard obrigatório antes de salvar cursor
        if (!ct.IsCancellationRequested)
        {
            await syncState.SetLastSyncDateAsync(SyncKey, DateTime.UtcNow, ct);
            logger.LogInformation("[{SyncKey}] Sincronização concluída. {Total} registros processados.", SyncKey, totalProcessados);
        }
        else
        {
            logger.LogWarning("[{SyncKey}] Sincronização cancelada após processar {Total} registros. Cursor NÃO atualizado.", SyncKey, totalProcessados);
        }
    }

    private static NomeDaEntidade MapToDomain(NomeDaEntidadeDto dto)
    {
        return new NomeDaEntidade
        {
            Id = Guid.NewGuid(),
            OmieId = dto.CodigoOmie,
            // ... mapear propriedades ...
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
    }
}
```

---

## 4. Regras para Testes de SyncService

### ✅ Obrigatório

| # | Regra |
|---|---|
| 1 | **Nunca** usar `Task.Delay` fixo como sincronização — usar polling loops com timeout |
| 2 | Preferir `[Collection("NomeDoCollection")]` a `DisableTestParallelization` global |
| 3 | Testes de integração devem validar **idempotência** (executar sync 2x e verificar que não duplica) |
| 4 | Testar o cenário de **cancelamento** (disparar `CancellationToken` e verificar que o cursor NÃO foi salvo) |

### Anti-Pattern de Teste

```csharp
// ❌ PROIBIDO — delay fixo antes de polling
await Task.Delay(1000);
while (!condition) { await Task.Delay(100); }

// ✅ CORRETO — apenas polling com timeout
var timeout = TimeSpan.FromSeconds(10);
var sw = Stopwatch.StartNew();
while (!condition && sw.Elapsed < timeout)
{
    await Task.Delay(100);
}
Assert.True(condition, "Timeout esperando pela condição.");
```

---

## Validação

Este padrão é verificado:
- **Na criação**: pelo workflow `/sync-resource` (Passo 0: ler este documento)
- **No code review**: pelo workflow `/pr-review` e skill `code-review-checklist`

---

*Referências cruzadas:*
- [omie-api-rules.md](omie-api-rules.md) — Regras 7 e 8 (cursor e SyncKey)
- [efcore-supabase-rules.md](efcore-supabase-rules.md) — Regras 8 e 9 (Add vs Update, Raw SQL)
- [dot-net-standards.md](dot-net-standards.md) — Regra de Log de null crítico
