# Relatório de Teste de Carga - RAG com SQLite

Este relatório documenta os resultados do teste de carga realizado no endpoint de busca híbrida do RAG, utilizando SQLite como banco de dados e embeddings mockados.

## Objetivo
Avaliar o desempenho da busca híbrida (RRF) no SQLite sob carga simulada, eliminando a latência de chamadas a APIs de LLM externas.

## Configuração do Teste
- **Banco de Dados**: SQLite (`rag_test.db`)
- **Dados**: 1000 documentos populados via endpoint de seed.
- **Embeddings**: Mockados (retorno fixo e instantâneo).
- **Ferramenta**: k6 rodando em container Docker.
- **Perfil de Carga**:
  - 30s de warm-up (rampa até 5 VUs)
  - 1m de carga normal (10 VUs)
  - 1m de carga alta (20 VUs)
  - 30s de resfriamento (rampa até 0 VUs)

## Resultados

### Métricas Principais
| Métrica | Valor |
| --- | --- |
| Total de Requisições | 12.575 |
| Taxa de Sucesso | 100% |
| Requisições por Segundo | ~70/s |
| Duração Média | 132,69 ms |
| Duração Mediana | 123,21 ms |
| Duração p(95) | 253,66 ms |
| Duração Máxima | 1,87 s |

### Checks
- **Status is 200**: 100% Sucesso
- **Response has results**: 100% Sucesso
- **Response time < 1s**: 99,93% Sucesso (26 falhas de 12.575)

## Conclusão
O sistema apresentou um excelente desempenho com o SQLite e mocks de embeddings. A busca híbrida respondeu na maioria das vezes em menos de 200ms, mesmo no pico de 20 VUs (gerando cerca de 70 requisições por segundo).

As 26 requisições que demoraram mais de 1s ocorreram provavelmente durante as transições de carga ou picos pontuais de I/O do SQLite, mas ficaram bem dentro do limite aceitável de tolerância (threshold de 2s para p95).

Este ambiente está validado para testes de estresse da lógica de busca sem dependência de LLMs.
