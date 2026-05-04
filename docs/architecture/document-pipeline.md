# Document Ingestion Pipeline

## Visão Geral

Pipeline de ingestão de documentos que transforma arquivos brutos em chunks indexados no VectorStore, prontos para busca semântica via RAG.

```
RawDocument → [Parser] → ParsedDocument → [Chunking] → DocumentChunk[] → [Embedding] → [VectorStore]
```

## Componentes

### 1. Document Parsers (`IDocumentParser`)

Cada parser é especializado por tipo de documento:

| Parser           | DocumentType | Estratégia                          |
| ---------------- | ------------ | ----------------------------------- |
| `MarkdownParser` | Markdown     | Headers, frontmatter, code blocks   |
| `PlainTextParser`| PlainText    | Parágrafos (split por `\n\n`)       |
| `HtmlParser`     | Html         | Extração por tags `<h1>`-`<h6>`     |

**Extensibilidade**: Para adicionar PDF, DOCX ou PPTX, implemente `IDocumentParser` e registre no DI.

### 2. Chunking Strategy (`IChunkingStrategy`)

O `HybridChunkingStrategy` combina três abordagens:

1. **Structural**: Respeita fronteiras de seção (headers, code blocks)
2. **Fixed-size**: Subdivide seções grandes por contagem de tokens
3. **Overlap**: Mantém contexto entre chunks adjacentes (default: 15%)

#### Configuração (`ChunkingConfig`)

| Parâmetro       | Default | Descrição                           |
| --------------- | ------- | ----------------------------------- |
| `TargetTokens`  | 500     | Tamanho alvo do chunk               |
| `MaxTokens`     | 1000    | Limite máximo por chunk             |
| `MinTokens`     | 50      | Mínimo (evita chunks inúteis)       |
| `OverlapPercent` | 0.15   | Overlap entre chunks adjacentes     |
| `Collection`    | default | Coleção destino no VectorStore      |
| `ContentType`   | document| Tipo para metadados                 |

### 3. Document Ingestion Pipeline (`IDocumentIngestionPipeline`)

Orquestra o fluxo completo:

```
IngestAsync(RawDocument)
  1. Resolve parser por DocumentType
  2. Parse → ParsedDocument
  3. Chunk → DocumentChunk[]
  4. Embed (batch via IEmbeddingProvider)
  5. Upsert cada chunk no IVectorStore
  → IngestionResult (sucesso/falha + métricas)
```

Suporta `IngestBatchAsync` para múltiplos documentos sequenciais.

### 4. Chunk Metadata Schema (`ChunkMetadata`)

Cada chunk carrega metadados ricos:

```json
{
  "source": "obsidian",
  "file_name": "Architecture.md",
  "section": "Decisões Técnicas",
  "section_level": 2,
  "content_type": "decision",
  "collection": "squad-x",
  "document_hash": "a1b2c3...",
  "chunk_index": 3,
  "total_chunks": 12,
  "has_overlap": true,
  "agent_id": "specialist-arch",
  "tags": "architecture,adr",
  "document_date": "2024-01-15T10:00:00Z"
}
```

## Versionamento de Documentos

O `ContentHash` (SHA-256) permite:
- Detectar documentos duplicados antes da ingestão
- Identificar versões alteradas para re-ingestão
- Rastrear linhagem chunk → documento original

## Extensão para Multimodal

Tipos como `Pdf`, `Docx`, `Pptx` e `Image` estão definidos no enum `DocumentType`. Para implementá-los:

1. Crie um parser que implemente `IDocumentParser`
2. Registre no DI (`services.AddSingleton<IDocumentParser, PdfParser>()`)
3. O pipeline detecta automaticamente pelo `SupportedType`
