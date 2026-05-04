# RAG Flow — Retrieval-Augmented Generation

## Visão Geral

O RAG Service orquestra busca vetorial, re-ranking e construção de contexto para injeção no prompt de agentes.

```
RAGQuery → [VectorStore Search] → SearchMatch[] → [Score Filter] → [ReRanker] → RankedChunk[] → [Context Builder] → RAGContext
```

## Fluxo de Execução

### 1. Retrieval (VectorStore)

```
RAGQuery.Strategy → BuildFilters() → VectorStore.SearchAsync/SearchWithFiltersAsync
```

Filtros automáticos por estratégia:

| Strategy          | Filtro aplicado          |
| ----------------- | ------------------------ |
| Default           | Nenhum (busca geral)     |
| RecentMemory      | Nenhum                   |
| DomainKnowledge   | `content_type = domain`  |
| DecisionHistory   | `content_type = decision`|
| Episodic          | `content_type = session` |
| Targeted          | Nenhum                   |

Se `AgentId` for fornecido, adiciona filtro `agent_id`.

### 2. Score Filter

Remove candidatos abaixo de `MinRelevanceScore` (default: 0.3) antes do re-ranking.

### 3. Re-Ranking (`HeuristicReRanker`)

Re-ordena candidatos sem modelo ML. Fórmula:

```
FinalScore = VectorScore × 0.4 + TF × 0.3 + PhraseBonus + MetaBonus + OverlapPenalty
```

| Componente      | Peso  | Descrição                              |
| --------------- | ----- | -------------------------------------- |
| Vector Score    | 0.4   | Score original da busca vetorial       |
| TF Score        | 0.3   | Term frequency (query terms no chunk)  |
| Phrase Bonus    | +0.2  | Se query aparece literalmente          |
| Meta Bonus      | +0.1  | Se section title contém query terms    |
| Tags Bonus      | +0.05 | Se tags contêm query terms             |
| Overlap Penalty | -0.05 | Chunks com overlap (prefere originais) |

### 4. Context Builder

Gera string formatada em Markdown para injeção no prompt:

```markdown
## Relevant Context

### [1] — Decisões Técnicas (source: obsidian)
Conteúdo do chunk mais relevante...

### [2] — Arquitetura (source: upload)
Segundo chunk mais relevante...
```

## RAGQuery — Parâmetros

| Campo              | Tipo              | Default | Descrição                        |
| ------------------ | ----------------- | ------- | -------------------------------- |
| `Query`            | string            | —       | Texto da busca                   |
| `AgentId`          | string?           | null    | Filtrar por agente               |
| `SessionId`        | string?           | null    | Contexto da sessão               |
| `Scope`            | SearchScope       | All     | Escopo da busca vetorial         |
| `MaxResults`       | int               | 10      | Máximo de candidatos             |
| `TopKAfterReRank`  | int               | 5       | Top-K após re-ranking            |
| `MinRelevanceScore`| double            | 0.3     | Score mínimo                     |
| `Strategy`         | RetrievalStrategy | Default | Estratégia de retrieval          |
| `Filters`          | Dictionary?       | null    | Filtros adicionais               |

## RAGContext — Resultado

| Campo                  | Tipo             | Descrição                       |
| ---------------------- | ---------------- | ------------------------------- |
| `Query`                | string           | Query original                  |
| `Chunks`               | List<RankedChunk>| Chunks ranqueados               |
| `BuiltContext`         | string           | Contexto formatado para prompt  |
| `TotalTokensUsed`      | int              | Estimativa de tokens            |
| `CandidatesRetrieved`  | int              | Total do VectorStore            |
| `CandidatesAfterReRank`| int              | Após re-ranking                 |
| `StrategyUsed`         | RetrievalStrategy| Estratégia utilizada            |
| `RetrievalTime`        | TimeSpan         | Tempo de busca vetorial         |
| `ReRankTime`           | TimeSpan         | Tempo de re-ranking             |
| `TotalTime`            | TimeSpan         | Tempo total end-to-end          |

## Otimizações de Custo/Performance

1. **Score filter antes do re-rank** — evita processar candidatos irrelevantes
2. **Heuristic re-ranker** — zero custo de API (sem cross-encoder externo)
3. **Token estimation** — `~text.Length / 4` para estimativa rápida
4. **Batch embeddings** — geração em lote na ingestão
5. **Sequential batch ingestion** — evita sobrecarga de memória em lotes grandes
