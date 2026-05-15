# ADR-006: Suporte a Self-Hosting com Ollama

## Status
Accepted (Retrospective)

## Context
Para garantir a privacidade dos dados, permitir a execução offline e reduzir os custos com APIs externas (como OpenAI ou Gemini), precisávamos de uma solução para executar modelos de linguagem (LLMs) localmente ou em infraestrutura privada.

## Decision
Decidimos dar suporte nativo ao **Ollama** como provedor de LLM para cenários de self-hosting.

## Rationale
1. **Privacidade e Segurança**: Dados sensíveis não saem da infraestrutura do cliente.
2. **Redução de Custo**: Zero custo por token em ambientes de desenvolvimento e tarefas de menor complexidade.
3. **Portabilidade**: O Ollama facilita o empacotamento e distribuição de modelos em containers.

## Trade-offs
- **Recursos Locais**: Exige hardware dedicado (GPUs) para manter uma boa performance.
- **Capacidade do Modelo**: Modelos locais menores podem ter menor precisão que modelos estado-da-arte na nuvem (ex: GPT-4).

## Consequences
- **Positive**: Independência de provedores externos; maior controle sobre os dados.
- **Negative**: Complexidade na gestão de infraestrutura de hardware e deployment de modelos.
- **Mitigation**: Implementação de fallback automático para provedores em nuvem (como Gemini) quando o modelo local não atingir a confiança necessária.
