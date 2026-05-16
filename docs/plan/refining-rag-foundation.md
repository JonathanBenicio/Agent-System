# Project Plan: Refining RAG & Hot-Swapping Foundation

## 1. Background & Motivation

A arquitetura base de **Hot-Swapping e Ingestão Local de RAG** atingiu a maturidade da sua especificação original (Fases 0 a 4). O sistema agora suporta múltiplos vetores, provedores LLM dinâmicos e isolamento *multi-tenant*. No entanto, para alcançar o estado da arte e fornecer uma experiência verdadeiramente fluida e robusta em ambientes corporativos e de pesquisa (nível *Enterprise*), o sistema exige ferramentas avançadas de governança de hot-swap e inteligência de ingestão contínua.

Este plano ("Phase 5" ou "Refining") descreve as próximas etapas de evolução dessas funcionalidades.

## 2. Scope & Impact

**Scope:**
*   Implementação de serviços em *background* para sincronização bidirecional de diretórios (como *Vaults* do Obsidian).
*   Expansão das opções de *Chunking* com trocas dinâmicas de heurística.
*   Mecanismos de salvaguarda (Segurança de Estado) para o *Hot-swapping*.
*   Ferramentas avançadas de *Benchmarking* interno na UI (Arena Mode).
*   Processamento avançado de arquivos não-textuais.

**Impact:**
*   **Autonomia:** Redução drástica da intervenção manual para atualização de bases de conhecimento.
*   **Resiliência:** Prevenção de quebras no sistema por configuração de LLM/Vector Store maliciosas ou erradas.
*   **Observabilidade:** Transparência imediata de qual modelo entrega as melhores respostas e métricas (RRF/Latência).

---

## 3. Proposed Features & Architecture Changes

### Feature 1: Ingestão Reativa (Local Sync & Obsidian Integration)
**Objetivo:** Eliminar a necessidade de upload manual contínuo.
*   **Backend:** Criação de um `DirectoryWatcherBackgroundService` que subscreve eventos de *file system* em diretórios pré-configurados (como um Vault de Obsidian local).
*   **Lógica:** Operações *Delta* (inserir apenas documentos novos, re-vetorizar documentos atualizados, e remover vetores de documentos deletados).
*   **Frontend:** Um novo *card* na aba de integrações: "Sync Local Folder", onde o usuário informa o `AbsolutePath` que a API deve monitorar.

### Feature 2: Hot-Swapping de Estratégias de Chunking
**Objetivo:** Permitir experimentação de particionamento de texto sem reiniciar o serviço.
*   **Backend:** Atualizar a interface do `DocumentIngestionPipeline` para receber via DI a heurística ativa: `FixedSizeChunker`, `SlidingWindowChunker` ou `SemanticChunker` (que respeita parágrafos, cabeçalhos, ou usa LLM para separar contexto).
*   **Frontend:** Adicionar à tela de configurações a seleção do algoritmo de chunking e seus hiperparâmetros (ex: `overlap_size`, `chunk_size`).

### Feature 3: Auto-Rollback e Health Checks Dinâmicos
**Objetivo:** Tornar o *Hot-Swapping* à prova de falhas (Safe Swap).
*   **Lógica:** Ao aplicar uma nova configuração (ex: mudar a API key da OpenAI ou trocar de Sqlite para Pinecone), a `DynamicFactory` cria a instância em estado *Pending*.
*   **Health Check:** Dispara um *Ping* ou tenta extrair um *Embedding* simples de teste.
*   **Ação:** Se responder `< 5s` e `Status 200`, a instância é promovida a *Active*. Se falhar, um *EventBus* notifica a falha, o banco de dados é revertido, e a UI exibe o erro e mantém o estado anterior ativo.

### Feature 4: RAG A/B Testing (Arena Mode UI)
**Objetivo:** Prover uma ferramenta visual de comparação.
*   **Frontend:** Nova aba chamada `Arena` (inspirada no *LMSYS Chatbot Arena*).
*   **Lógica:** Ao enviar um prompt de RAG, o backend bifurca a *request* internamente (`Task.WhenAll`), roteando para dois provedores/modelos diferentes simultaneamente (ex: GPT-4o + Pinecone vs. Llama3 Local + DuckDB).
*   **Display:** Renderização lado a lado (*Split View*) com *badges* de latência (ms), custo estimado do prompt, e o score de RRF (Reciprocal Rank Fusion).

### Feature 5: Extração Multimodal (Vision/OCR Avançado)
**Objetivo:** Ampliar a capacidade do RAG para ingerir faturas, PDF scans e apresentações.
*   **Integração:** Adotar `PdfPig` para extração posicional e/ou um modelo LLM Multimodal local (`LLaVA` ou equivalente no Ollama) pré-ingestão para descrever gráficos/imagens e gerar a representação textual do chunk.

---

## 4. Prioritization Matrix

> [!TIP]
> A recomendação é iniciar pelas features de **Segurança (Feature 3)** e **Conveniência (Feature 1)** antes de avançar para as funcionalidades puramente analíticas.

| Feature | Esforço | Impacto | Prioridade |
| :--- | :--- | :--- | :--- |
| **Feature 3: Auto-Rollback** | Médio | Alto | Alta |
| **Feature 1: Obsidian Sync** | Médio | Alto | Alta |
| **Feature 2: Dynamic Chunking** | Baixo | Médio | Média |
| **Feature 4: Arena Mode UI** | Alto | Médio | Baixa |
| **Feature 5: Vision/OCR** | Alto | Alto | Baixa (Long-term) |

## 5. Next Steps

Quando houver intenção de iniciar esta fase, os passos serão:
1. Aprovar a matriz de priorização acima.
2. Iniciar pela **Feature 3**, isolando as funções de *Health Check* dentro dos atuais proxies `HotSwappableLLMProvider` e `HotSwappableVectorStore`.
3. Criar os tickets/tasks no quadro do projeto correspondente a este plano.
