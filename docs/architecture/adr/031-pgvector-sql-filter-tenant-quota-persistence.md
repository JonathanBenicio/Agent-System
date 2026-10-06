# ADR 031: Filtro de Metadata SQL-Nativo no PGVector e Persistência de Quotas de Tenant

> Revisão operacional 2026-09-28: A proposta abaixo é histórica. Implementação atual usa vector_documents, pré-filtro de sala e scoring dos candidatos; quotas usam SaveChangesAsync com concorrência otimista e DailyQuotaResetBackgroundService, não UPDATE RETURNING/Quartz. Ver [matriz operacional](../../backend/resources-rules.md) e [evidência](../../backend/validation/2026-09-28.md).

**Status:** Proposto
**Data:** 04 de Junho de 2026
**Autor(es):** Antigravity AI & Jonathan Benicio

---

## Contexto

A auditoria arquitetural de Junho/2026 identificou dois problemas críticos de segurança e corretude na camada de dados:

**Problema 1 — Filtro In-Memory no VectorStore:**
`PostgresVectorStore.SearchWithFiltersAsync` executa a filtragem de `room_ids` **após** a query semântica ao banco, em memória. Isso significa que o banco retorna os top-50 documentos semanticamente mais próximos sem considerar a room do usuário. Se nenhum dos 50 candidatos pertencer à room autorizada, a busca retorna vazio — mesmo que existam documentos relevantes na room. Isso representa um **falso negativo de segurança** e viola o princípio de isolamento de contexto por room definido no ADR-019.

**Problema 2 — Quotas Voláteis em Memória:**
`QuotaEnforcer` mantém os contadores de uso de tenant em `ConcurrentDictionary` em memória. Isso impede:
- Persistência de quotas entre restarts da aplicação.
- Compartilhamento de estado de quota entre múltiplas instâncias horizontais.
- Auditoria histórica de consumo por tenant.

O componente `ProactiveQuotaManager` estava documentado como implementado em ADR-008, mas nunca foi de fato implementado.

**Alternativas consideradas:**

Para o filtro vetorial:
- *Opção A (adotada):* Mover o filtro para predicado SQL direto na query pgvector.
- *Opção B:* Aumentar o top-K de 50 para um número maior — não resolve o problema de segurança, apenas reduz a probabilidade de falso negativo.

Para as quotas:
- *Opção A (adotada):* PostgreSQL como backing store + IMemoryCache com TTL curto.
- *Opção B:* Redis como cache distribuído — introduz nova dependência de infraestrutura desnecessária dado que o PostgreSQL já está no stack.

---

## Decisão

### 1. Filtro Vetorial SQL-Nativo

Mover a lógica de filtragem por `room_ids` do código C# para o predicado SQL da query pgvector em `PostgresVectorStore.SearchWithFiltersAsync`. A query deve incluir o filtro de metadata **antes** do cálculo de distância semântica:

```sql
SELECT id, content, metadata, 1 - (embedding <=> @query) AS score
FROM document_chunks
WHERE metadata->>'room_id' = ANY(@roomIds)
ORDER BY embedding <=> @query
LIMIT @topK
```

Um índice GIN na coluna `metadata` (jsonb) deve ser criado via migration para garantir performance do filtro:

```sql
CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_chunks_metadata
ON document_chunks USING GIN (metadata);
```

### 2. Persistência de Quotas com Cache

Implementar persistência de quotas de tenant em PostgreSQL com caching de curto TTL:

- Nova entidade `TenantQuotaEntity` com campos: `TenantId`, `DailyBudget`, `CurrentDailyUsage`, `ResetAt`, `UpdatedAt`.
- `QuotaEnforcer` refatorado para usar `IMemoryCache` (TTL: 60 segundos) sobre leituras de `ITenantQuotaRepository`.
- Incremento de uso via `UPDATE ... RETURNING` atômico no PostgreSQL (sem race conditions).
- Reset diário via Quartz job à meia-noite UTC.

---

## Justificativa

1. **Segurança First:** O filtro in-memory é uma violação do princípio de room isolation definido no ADR-019. Mover para SQL garante que o banco nunca retorna dados de uma room não autorizada, independentemente do top-K configurado.

2. **Conformidade com o ADR-019:** O ADR-019 (Agent-Room Association) define que o acesso a documentos deve ser controlado por permissão. O filtro in-memory não garante isso quando o top-K é menor que o total de documentos da room.

3. **Performance Adequada:** O índice GIN sobre metadata jsonb no PostgreSQL garante que o filtro SQL não degrada a performance — tipicamente mais rápido que filtrar 50 registros em C#.

4. **Quotas Persistentes para Produção Real:** Sem persistência, o sistema FinOps é inutilizável em produção multi-instância. O PostgreSQL já está no stack, evitando nova dependência de infraestrutura.

5. **TTL de Cache como Trade-off Documentado:** Aceita-se uma janela de inconsistência de até 60 segundos entre instâncias horizontais em troca de não gerar um read ao banco em cada request. Este trade-off é aceitável para controle de orçamento diário (granularidade de horas).

---

## Consequências

### Positivas

* **Eliminação de falso negativo de segurança:** Documentos de outras rooms nunca aparecem como candidatos.
* **Escalabilidade horizontal real:** Múltiplas instâncias compartilham estado de quota via PostgreSQL.
* **Auditoria histórica:** `TenantQuotaEntity` permite consultas de consumo por período.
* **Alinhamento com ADR-019:** Implementação real do isolamento de contexto por room.

### Desafios / Pontos de Atenção (Negativas)

* **Migration em tabela existente:** O índice GIN deve ser criado com `CONCURRENTLY` para não bloquear operações em produção durante o deploy.
* **Janela de inconsistência de quota:** 60 segundos de TTL significa que um tenant pode exceder levemente o budget antes do enforcement ser aplicado. Mitigado com budget safety margin de 5% recomendado na configuração.
* **Mudança de comportamento observável:** Buscas semânticas que antes retornavam dados (por acidente) de outras rooms agora retornam vazio — pode parecer "regressão" para usuários que dependiam do comportamento incorreto.
