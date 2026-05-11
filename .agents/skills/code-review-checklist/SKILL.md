---
name: code-review-checklist
description: Code review guidelines covering code quality, security, and best practices.
allowed-tools: Read, Glob, Grep
---

# Code Review Checklist

## Quick Review Checklist

### Correctness
- [ ] Code does what it's supposed to do
- [ ] Edge cases handled
- [ ] Error handling in place
- [ ] No obvious bugs

### Security
- [ ] Input validated and sanitized
- [ ] No SQL/NoSQL injection vulnerabilities
- [ ] No XSS or CSRF vulnerabilities
- [ ] No hardcoded secrets or sensitive credentials
- [ ] **AI-Specific:** Protection against Prompt Injection (if applicable)
- [ ] **AI-Specific:** Outputs are sanitized before being used in critical sinks

### Performance
- [ ] No N+1 queries
- [ ] No unnecessary loops
- [ ] Appropriate caching
- [ ] Bundle size impact considered

### Code Quality
- [ ] Clear naming
- [ ] DRY - no duplicate code
- [ ] SOLID principles followed
- [ ] Appropriate abstraction level

### Testing
- [ ] Unit tests for new code
- [ ] Edge cases tested
- [ ] Tests readable and maintainable

### Documentation
- [ ] Complex logic commented
- [ ] Public APIs documented
- [ ] README updated if needed

## AI & LLM Review Patterns (2025)

### Logic & Hallucinations
- [ ] **Chain of Thought:** Does the logic follow a verifiable path?
- [ ] **Edge Cases:** Did the AI account for empty states, timeouts, and partial failures?
- [ ] **External State:** Is the code making safe assumptions about file systems or networks?

### Prompt Engineering Review
```markdown
// ❌ Vague prompt in code
const response = await ai.generate(userInput);

// ✅ Structured & Safe prompt
const response = await ai.generate({
  system: "You are a specialized parser...",
  input: sanitize(userInput),
  schema: ResponseSchema
});
```

## Anti-Patterns to Flag

```typescript
// ❌ Magic numbers
if (status === 3) { ... }

// ✅ Named constants
if (status === Status.ACTIVE) { ... }

// ❌ Deep nesting
if (a) { if (b) { if (c) { ... } } }

// ✅ Early returns
if (!a) return;
if (!b) return;
if (!c) return;
// do work

// ❌ Long functions (100+ lines)
// ✅ Small, focused functions

// ❌ any type
const data: any = ...

// ✅ Proper types
const data: UserData = ...
```

## Review Comments Guide

```
// Blocking issues use 🔴
🔴 BLOCKING: SQL injection vulnerability here

// Important suggestions use 🟡
🟡 SUGGESTION: Consider using useMemo for performance

// Minor nits use 🟢
🟢 NIT: Prefer const over let for immutable variable

// Questions use ❓
❓ QUESTION: What happens if user is null here?
```

## .NET / EF Core Specific (Tabatine Engine)

### Anti-Patterns Críticos (Bloqueantes)

```csharp
// 🔴 BLOCKING: Add() em entidade rastreada pelo EF Core
var existing = await dbContext.Entidades.FirstOrDefaultAsync(e => e.OmieId == id, ct);
dbContext.Entidades.Add(existing); // Causa InvalidOperationException ou INSERT duplicado
// ✅ FIX: Modificar propriedades diretamente — EF detecta automaticamente

// 🔴 BLOCKING: Cursor de sync salvo sem guard de cancelamento
await syncState.SetLastSyncDateAsync("Entidade", DateTime.UtcNow, ct);
// ✅ FIX: if (!ct.IsCancellationRequested) { await syncState.SetLastSyncDateAsync(...); }

// 🔴 BLOCKING: Caminho null silencioso em operação crítica
var dbEvent = await dbContext.WebhookEvents.FindAsync(id, ct);
if (dbEvent != null) { /* processa */ } // Se null, ninguém sabe
// ✅ FIX: Log em nível Error quando null + return explícito
```

### Anti-Patterns de Teste

```csharp
// 🟡 SUGGESTION: Task.Delay fixo antes de polling
await Task.Delay(1000); // Anti-pattern de timing
// ✅ FIX: Usar apenas polling loop com timeout via Stopwatch

// 🟡 SUGGESTION: DisableTestParallelization global
[assembly: CollectionBehavior(DisableTestParallelization = true)]
// ✅ FIX: Usar [Collection("NomeDoCollection")] apenas nas classes que precisam
```

### Checklist Rápido para SyncServices

- [ ] Guard `ct.IsCancellationRequested` antes de salvar cursor?
- [ ] `CancellationToken` em TODAS as chamadas async?
- [ ] Nenhum `Add()` em entidade já rastreada?
- [ ] Log de `Error` para caminhos null críticos?
- [ ] `SyncKey` renomeada com migration de cursor?

> **Referência completa:** `.agents/rules/sync-service-patterns.md`
