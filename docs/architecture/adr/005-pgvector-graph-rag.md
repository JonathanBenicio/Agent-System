# ADR-005: Uso de PostgreSQL (pgvector) e Graph RAG para Recuperação de Conhecimento

## Status
Accepted (Retrospective)

## Context
Para capacitar os agentes com conhecimento específico do projeto e memória de longo prazo, precisávamos de um sistema de Recuperação Aumentada por Geração (RAG). Era necessário escolher onde armazenar os embeddings vetoriais e como estruturar a busca.

## Decision
Decidimos utilizar a extensão **pgvector** no PostgreSQL para armazenamento de vetores e implementar técnicas de **Graph RAG** para conectar conceitos e melhorar a recuperação contextual.

## Rationale
1. **Consolidação de Stack**: Evita adicionar um novo banco de dados vetorial dedicado (como Pinecone ou Milvus) na fase inicial, reduzindo custos operacionais.
2. **Consultas Híbridas**: O PostgreSQL permite combinar busca vetorial com filtros relacionais e busca por texto completo (Full-Text Search) na mesma consulta.
3. **Relacionamentos Complexos**: O Graph RAG permite mapear conexões entre entidades, útil para entender a arquitetura do código.

## Trade-offs
- **Performance em Escala**: Bancos vetoriais dedicados podem superar o pgvector em volumes extremos de dados (milhões de vetores).
- **Complexidade do Grafo**: Construir e manter o grafo de conhecimento exige processamento adicional.

## Consequences
- **Positive**: Arquitetura simplificada usando apenas o PostgreSQL; excelente capacidade de recuperação contextual.
- **Negative**: Uso intensivo de CPU/Memória no banco de dados durante buscas vetoriais complexas.
- **Mitigation**: Uso de índices IVFFlat ou HNSW no pgvector para otimizar a performance de busca.
