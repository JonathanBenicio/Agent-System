# ADR-010: Execução de Modelos In-Process com ONNX Runtime

## Status
Accepted (Retrospective)

## Context
Para tarefas de RAG (como reranking) e classificação rápida de intenções, precisávamos de uma forma de executar pequenos modelos de Machine Learning com altíssima performance e baixíssima latência, sem o overhead de chamadas de rede para APIs externas ou mesmo para o Ollama.

## Decision
Decidimos utilizar o **ONNX Runtime** rodando *in-process* (dentro do próprio processo do .NET) para carregar e executar modelos de IA locais.

## Rationale
1. **Latência Ultra-Baixa**: Zero latência de rede; a inferência acontece na memória do próprio servidor.
2. **Independência**: Não depende de serviços externos estarem ativos (como o Ollama ou APIs de terceiros).
3. **Eficiência**: O formato ONNX é altamente otimizado para execução em CPU e GPU em diversas plataformas.

## Trade-offs
- **Uso de Memória**: O processo do .NET consome mais memória ao carregar os modelos diretamente.
- **Tamanho do Build**: Os arquivos do modelo (.onnx) precisam ser distribuídos junto com a aplicação ou baixados na inicialização.

## Consequences
- **Positive**: Performance excepcional para o "Fast Path" e Reranking no RAG.
- **Negative**: Aumento do consumo de recursos do servidor web.
- **Mitigation**: Uso de modelos quantizados (menores e mais rápidos) e carregamento preguiçoso (lazy loading) apenas quando necessário.
