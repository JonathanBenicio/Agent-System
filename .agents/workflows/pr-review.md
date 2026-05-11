---
description: Revisão estruturada de Pull Requests do Tabatine Engine com checklist automatizado.
---

# /pr-review — Workflow de Code Review

> **Propósito:** Guiar o agente em uma revisão estruturada de PRs, baseada nos padrões documentados em `.agents/rules/`.

---

## Passo 1: Carregar Contexto

Antes de analisar qualquer código, o agente DEVE ler os seguintes arquivos de regras:

1. `.agents/rules/sync-service-patterns.md` — Checklist de 18 pontos para SyncServices
2. `.agents/rules/omie-api-rules.md` — Rate limits, estratégia de sync, guard de cursor
3. `.agents/rules/efcore-supabase-rules.md` — Regras de EF Core (Add vs Update, Raw SQL)
4. `.agents/rules/dot-net-standards.md` — Loggin, Result Pattern, Primary Constructors

---

## Passo 2: Identificar Escopo do PR

Classifique os arquivos alterados:

| Tipo de Arquivo | Checklist Aplicável |
|---|---|
| `*SyncService.cs` | `sync-service-patterns.md` (18 checks completos) |
| `*Worker.cs`, `*Handler.cs` | Regras de log de null, CancellationToken, EF Core tracking |
| `*Configuration.cs` | `efcore-supabase-rules.md` (Fluent API, snake_case) |
| `*Dto.cs`, `*Models.cs` | `dot-net-standards.md` (Records, JsonPropertyName) |
| `*.Tests.cs` | Anti-patterns de teste (Task.Delay, parallelization) |
| `Migrations/*` | Verificar se há renomeação de SyncKey sem migration |

---

## Passo 3: Executar Checklist

### Para SyncServices (prioridade máxima)

Verificar **cada serviço** individualmente contra o checklist de 18 pontos:

```
Para cada *SyncService.cs alterado:
  ├── [CRÍTICO] Check #5: Guard ct.IsCancellationRequested antes de SetLastSyncDateAsync?
  ├── [CRÍTICO] Check #14: Add() em entidade rastreada? (BUG se sim)
  ├── [CRÍTICO] Check #17: Log de null em operações críticas?
  ├── Check #6: CancellationToken em TODAS as chamadas async?
  ├── Check #8: HashSet para deduplicação por ciclo?
  ├── Check #9: Upsert por OmieId?
  ├── Check #11: ToDictionaryAsync para evitar N+1?
  └── Demais checks conforme sync-service-patterns.md
```

### Para Workers e Handlers

```
  ├── [CRÍTICO] Log de Error para caminhos null em FindAsync/FirstOrDefaultAsync?
  ├── Raw SQL usado para registro único? (Preferir EF Core)
  ├── CancellationToken propagado para todas as chamadas?
  └── Constantes de status (não strings inline)?
```

---

## Passo 4: Gerar Relatório

O relatório de review deve seguir o formato do `pr_review_47.md`:

```markdown
# Code Review — PR #XX

## ✅ Pontos Positivos
| # | O que está bem |
|---|---|

## 🔴 Issues Críticos (Bloquear Merge)
### 1. [Descrição] — [Tipo de Padrão Violado]
**Arquivo**: [NomeDoArquivo.cs]
**Risco**: [Impacto se não corrigido]
**Correção**: [Exemplo de código correto]

## 🟡 Issues Menores (Melhorias Recomendadas)
[...]

## 📊 Resumo por Arquivo
| Arquivo | Status | Issues |
|---|---|---|

## Veredicto Final
> 🔴/🟡/✅ [Decisão]
```

---

## Passo 5: Classificar Veredicto

| Condição | Veredicto |
|---|---|
| Qualquer check **CRÍTICO** (#5, #14, #17) falhar | 🔴 Solicitar Alterações |
| Apenas checks não-bloqueantes falharem | 🟡 Aprovar com Ressalvas |
| Todos os checks passarem | ✅ Aprovado |

---

## Referências

- `.agents/rules/sync-service-patterns.md` — Contrato completo de SyncService
- `.agents/skills/code-review-checklist/SKILL.md` — Checklist genérico + seção .NET
- `pr_review_47.md` — Exemplo de review bem executado
