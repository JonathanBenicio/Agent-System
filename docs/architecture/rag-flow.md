# RAG Flow — Retrieval-Augmented Generation

> Fluxo alinhado ao runtime atual. Para a narrativa canônica do backend, use [backend-architecture-explained.md](backend-architecture-explained.md).

## Visão Geral

O RAG atual combina compressão de query, retrieval vetorial, HyDE condicional, re-ranking forte via `LlmReRanker`, freshness penalty, compressão semântica e budget trimming antes de devolver um `RAGContext` pronto para o orquestrador hosted.

```
User Query
    → [Semantic Caching Layer (Bypass se houver tools)]
    → (Cache Miss) → IQueryCompressor.CompressAsync()
    → VectorStore.SearchAsync/SearchWithFiltersAsync() (pgvector com Contextual Retrieval)
    → [Reactive Knowledge Feed (Obsidian Sync)]
    → HyDE condicional (se recall inicial vier fraco)
    → IReRanker.ReRankAsync() via LlmReRanker
    → IKnowledgeFreshnessService
    → ISemanticCompressor (quando excede budget)
    → BuildContextString()
    → IContextBudgetManager.TrimContextToBudgetAsync()
    → RAGContext (Retorno em Cache ou LLM)
```

## Camada de Cache Semântico (`SemanticCacheChatClient`)

O runtime opera um decorator transparente no `IChatClient` focado em otimização agressiva de latência e custos via busca de similaridade no pgvector.

```csharp
// Interceptação via DelegatingChatClient
var cacheResult = await _cacheService.GetCachedResponseAsync(prompt, _agentName, _threshold, cancellationToken);
if (cacheResult.IsHit) {
    return new ChatResponse(new[] { new ChatMessage(ChatRole.Assistant, cacheResult.CachedResponse) });
}
```

**Mecanismo de Ação**:
1. **Limiar de Similaridade**: Usa distância de cosseno (Cosine Distance) no pgvector (`semantic_cache`) com threshold configurado em **95%**.
2. **Tool Bypass Dinâmico**: O cache é automaticamente contornado se a requisição de chat exigir o uso de ferramentas (`options?.Tools != null`), garantindo que chamadas funcionais dinâmicas sempre executem.

## Sincronização de Conhecimento Bidirecional (Obsidian)

O sistema mantém um elo vivo com o repositório local de notas (Obsidian) por meio do `FileObsidianSync`.

**Fluxo de Sincronização**:
1. **Monitoramento**: O `FileObsidianSync` observa alterações em arquivos `.md` na pasta configurada.
2. **Ingestão Reativa**: Ao detectar mudança, o arquivo é re-processado pelo `DocumentIngestionPipeline`.
3. **Contextual Retrieval**: Chunks são gerados com resumos contextuais e persistidos no `pgvector`.
4. **Alinhamento**: Garante que o "Segundo Cérebro" do usuário esteja sempre disponível para o orquestrador em sub-segundos após a edição.

## Superfícies de Uso

O runtime usa duas superfícies complementares para retrieval:

| Superfície | Mecanismo | Quando | Determinismo |
| --- | --- | --- | --- |
| `RAGContextProvider` | Provider concreto que estende `MessageAIContextProvider` | Sempre, antes de cada `RunAsync` do orquestrador | Determinístico |
| `retrieve_context` | `AIFunction` auxiliar exposta ao orquestrador | Sob demanda, quando o LLM decide buscar contexto adicional | Não-determinístico |

## Fluxo Completo

```text
1. Query Compression + Query Variants      ← IQueryCompressor.CompressAsync()
2. Vector Search por variante + filtros    ← IVectorStore.SearchAsync()/SearchWithFiltersAsync()
3. HyDE condicional                        ← IChatClient.GetResponseAsync()
4. Merge distinto + Min Score              ← query.MinRelevanceScore (0.3)
5. Re-Ranking                              ← IReRanker.ReRankAsync()
     - LlmReRanker                           ← orquestra shortlist heurística + rerank forte
     - LocalOnnxCrossEncoderReRankerProvider ← caminho local preferencial
     - JinaReRankerProvider                  ← provider externo opcional
     - Embedding scorer                      ← fallback neural leve
     - LLM fallback                          ← último recurso opcional
6. Freshness Penalty                       ← IKnowledgeFreshnessService
7. Semantic Compression                    ← ISemanticCompressor.CompressRankedChunksAsync()
8. Context Build                           ← BuildContextString()
9. Context Budget                          ← IContextBudgetManager.TrimContextToBudgetAsync()
```

## Estratégias de Retrieval

| Strategy | Filtro | Uso |
| --- | --- | --- |
| `Default` | Nenhum | Busca geral |
| `DomainKnowledge` | `content_type=domain` | Conhecimento de domínio |
| `DecisionHistory` | `content_type=decision` | Decisões passadas |
| `Episodic` | `content_type=session` | Sessões anteriores |
| `RecentMemory` | Nenhum | Memória recente |
| `Targeted` | Nenhum | Busca com filtros adicionais fornecidos pelo caller |

Se `AgentId` for informado, o pipeline adiciona filtro `agent_id` ao retrieval.

## Injeção no Runtime Hosted

O `RAGContextProvider` vive em `src/AgenticSystem.Infrastructure/AgentFramework/` porque ele se encaixa na surface hosted do orquestrador.

Antes de cada `RunAsync(...)`, ele:

1. Lê a última mensagem de usuário em `RequestMessages`.
2. Chama `IRAGService.RetrieveContextAsync(query)`.
3. Aplica `IContextBudgetManager.TrimContextToBudgetAsync(...)` quando necessário.
4. Injeta uma mensagem `system` com marker próprio para evitar re-injeção em loops de tool calling.

O tool `retrieve_context` continua disponível para buscas ad-hoc fora da injeção automática.

## RAGContext — Resultado

| Campo | Tipo | Descrição |
| --- | --- | --- |
| `Query` | string | Query original recebida pelo serviço |
| `EffectiveQuery` | string | Query efetivamente usada após compressão/variantes |
| `QueryVariants` | IReadOnlyList<string> | Variantes realmente usadas no retrieval |
| `Chunks` | List<RankedChunk> | Chunks finais ranqueados |
| `BuiltContext` | string | Contexto formatado para prompt |
| `TotalTokensUsed` | int | Estimativa de tokens do contexto final |
| `CandidatesRetrieved` | int | Total bruto vindo do vector store |
| `CandidatesAfterReRank` | int | Total remanescente após re-ranking |
| `StrategyUsed` | RetrievalStrategy | Estratégia aplicada |
| `UsedHydeExpansion` | bool | Sinaliza uso de HyDE por baixo recall |
| `HydeVariant` | string? | Variante hipotética gerada por HyDE |
| `SemanticSummary` | string? | Resumo comprimido quando houve compressão semântica |
| `UsedSemanticCompression` | bool | Indica se o contexto precisou ser comprimido |
| `OriginalContextTokens` | int | Estimativa de tokens antes do trim final |
| `RetrievalTime` | TimeSpan | Tempo de busca vetorial |
| `ReRankTime` | TimeSpan | Tempo de re-ranking |
| `TotalTime` | TimeSpan | Tempo total end-to-end |

## Linguagem Operacional Consolidada

- Use `RAGContextProvider` para falar da injeção automática no orquestrador hosted.
- Use `MessageAIContextProvider` apenas para se referir à abstração do MAF que o provider estende.
- Use `retrieve_context` para a tool auxiliar de retrieval sob demanda.
- Use `LlmReRanker` para o pipeline real de rerank; `HeuristicReRanker` continua como shortlist local dentro dele, não como estágio final isolado.
- Use `IContextBudgetManager` e `ISemanticCompressor` para descrever trimming e compressão do contexto.

---

## 🛠️ Desenvolvimento e Testes

### RagTestController
O projeto da API contém um `RagTestController.cs` (`/api/ragtest`). Este é um endpoint de teste **não-produtivo**, utilizado exclusivamente para desenvolvimento e validação manual do fluxo de RAG e conexões com o banco vetorial. Ele não deve ser exposto ou utilizado em ambientes de produção.
