# Plano de Testes de Carga e Performance (Vector Stores)

Este documento foi elaborado para guiar a execução dos testes de carga previstos na Fase 4, com foco em nossa arquitetura de RAG (Retrieval-Augmented Generation) e pesquisa híbrida. O objetivo é validar o desempenho dos nossos Vector Stores (`PostgresVectorStore` e `PineconeVectorStore`) sob stress.

> **Público-alvo:** Desenvolvedores Júnior. Leia atentamente o passo a passo antes de executar as ferramentas.

---

## 1. Visão Geral

Atualmente, nosso sistema suporta consultas vetoriais puras, busca textual e **Pesquisa Híbrida (Hybrid Search via Reciprocal Rank Fusion - RRF)**.
Um teste de carga eficaz deve responder às seguintes perguntas:
1. O índice GIN no Postgres aguenta quantas buscas por segundo sem estourar o uso de CPU?
2. A pesquisa vetorial híbrida mantém o p95 (tempo máximo de 95% das requisições) abaixo de 200ms?
3. Há fuga de memória ou esgotamento do *Connection Pool*?

---

## 2. Ferramentas Necessárias

Para os testes, utilizaremos **k6**, uma ferramenta de testes de carga moderna e baseada em JavaScript (mantida pela Grafana).

### Pré-requisitos
1. Instalar o [k6](https://k6.io/docs/get-started/installation/) na máquina local.
2. Garantir que o ambiente alvo (geralmente local com Docker ou *Staging*) tenha pelo menos **100.000 documentos fictícios** populados na base.

---

## 3. Preparação do Ambiente (Seed de Dados)

Antes de testarmos o fluxo de consulta, precisamos preencher as tabelas com um volume de dados representativo:

1. Crie um script ou endpoint em `VectorStoreController` (ex: `POST /api/vectorstore/seed`) que receba um `count`.
2. Insira textos aleatórios e embeddings sintéticos (ex: um array de floats randômicos, sem chamar a API da OpenAI/Ollama, para não gastar dinheiro nem sofrer *rate limit* do LLM).
3. Efetue o *seed* para um *Tenant ID* específico (ex: `tenant-stress-01`).

---

## 4. Cenários de Teste

No **k6**, você deve criar um script (ex: `stress-test.js`) contemplando os três cenários de busca abaixo.

### Cenário A: Full-Text Search (FTS) Purista
*   **O que testa:** O desempenho do índice GIN (`ts_vector`).
*   **Ação:** Disparar requisições para a API de RAG passando uma flag que force apenas a pesquisa semântica por palavra-chave.
*   **Meta:** Alta taxa de requisições por segundo (RPS) com latência muito baixa (< 50ms).

### Cenário B: Busca Vetorial (Cosine Similarity)
*   **O que testa:** O peso matemático do cálculo de distância entre vetores no banco ou na API do Pinecone.
*   **Ação:** Pesquisar enviando embeddings como filtro de semelhança.
*   **Meta:** Identificar a escalabilidade quando múltiplos usuários pesquisam a base vetorial em paralelo.

### Cenário C: Pesquisa Híbrida (RRF)
*   **O que testa:** O gargalo de efetuar ambas as consultas em paralelo (FTS + Vetor) e ordenar usando a fusão de rankings (RRF).
*   **Ação:** Testar a função core que o usuário de fato usará no chat.
*   **Meta:** p95 de latência inferior a 300ms.

---

## 5. Script k6 de Exemplo

Crie um arquivo chamado `load-test.js`:

```javascript
import http from 'k6/http';
import { check, sleep } from 'k6';

// Configuração do teste: Subir gradativamente até 50 usuários simultâneos
export const options = {
  stages: [
    { duration: '30s', target: 20 }, // rampa de aquecimento
    { duration: '1m', target: 50 },  // carga principal
    { duration: '30s', target: 0 },  // rampa de descida
  ],
  thresholds: {
    // 95% das requisições devem terminar em menos de 500ms
    http_req_duration: ['p(95)<500'], 
    // Taxa de erro menor que 1%
    http_req_failed: ['rate<0.01'],   
  },
};

export default function () {
  const url = 'http://localhost:5000/api/rag/search';
  const payload = JSON.stringify({
    tenantId: 'tenant-stress-01',
    query: 'como configurar a pesquisa hibrida?',
    topK: 5,
    mode: 'hybrid' // Pode ser 'vector', 'fts' ou 'hybrid'
  });

  const params = {
    headers: {
      'Content-Type': 'application/json',
      // 'Authorization': 'Bearer SEU_TOKEN_AQUI' Se houver Auth
    },
  };

  const res = http.post(url, payload, params);

  check(res, {
    'status é 200': (r) => r.status === 200,
    'retornou resultados': (r) => JSON.parse(r.body).length >= 0,
  });

  sleep(1); // tempo de "espera" de um usuário real lendo a tela
}
```

Para rodar o teste, execute no terminal:
`k6 run load-test.js`

---

## 6. O Que Monitorar e Interpretar

Durante a execução do k6, observe as seguintes métricas no console:
*   `http_req_duration`: O tempo de resposta. Olhe para a coluna `p(95)`. Se estiver muito alta, o banco está engasgando.
*   `http_reqs`: O *throughput* total do sistema (quantas resolveu por segundo).
*   **Métricas do PostgreSQL (pg_stat_statements):** Se usar Postgres, verifique quais querys demoram mais tempo de execução e se o *Cache Hit Ratio* está alto.

### Passos Pós-Teste (Ação Dev)
Se o p95 estiver ruim (> 500ms), tente:
1. **Ajuste o Connection Pool:** Aumentar o `MaxPoolSize` na connection string do EF Core.
2. **Review dos Índices HNSW/IVFFlat:** Verifique se os índices de embeddings foram de fato aplicados (bancos de dados costumam ignorar o índice e fazer *seq scan* se a base for muito pequena, mas se for grande eles devem usar!).
3. **Paginação:** Certificar-se de não puxar muitos registros do banco para a memória em RRF (apenas puxe os IDs/Rank, só retorne o conteúdo completo dos *Top K* no final).

---

Ao concluir, documente os resultados em um relatório de performance (.md) para referência futura!
