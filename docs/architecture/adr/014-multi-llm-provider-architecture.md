# ADR-014: Arquitetura Multi-Provedor de LLM

## Status
Accepted (Retrospective)

## Context
O ecossistema de Inteligência Artificial muda rapidamente. Novos modelos surgem, preços variam e APIs podem falhar. Ficar preso a um único provedor (como apenas OpenAI ou apenas Gemini) representaria um risco de lock-in e de disponibilidade.

## Decision
Implementamos uma **Arquitetura Multi-Provedor** com suporte a Gemini, Ollama e OpenRouter, gerenciados por um Registro centralizado que permite alternar e balancear as chamadas.

## Rationale
1. **Evitar Lock-in**: Facilidade para trocar de provedor se as condições comerciais ou de performance mudarem.
2. **Alta Disponibilidade**: Se um provedor falhar, o sistema pode tentar outro automaticamente (Fallback).
3. **Especialização**: Permite usar modelos menores e mais baratos (Ollama) para tarefas simples e modelos potentes (Gemini 1.5 Pro) para raciocínios complexos.

## Trade-offs
- **Abstração Complexa**: Exige criar interfaces comuns que cubram as particularidades de cada API (streaming, parâmetros, etc.).
- **Manutenção**: É preciso atualizar o sistema sempre que um provedor mudar sua API.

## Consequences
- **Positive**: Sistema resiliente, flexível e otimizado para custos.
- **Negative**: Desenvolvimento inicial mais lento devido à necessidade de criar adaptadores para cada provedor.
- **Mitigation**: Uso de padrões como *Strategy* e *Factory* para isolar a lógica de cada provedor e facilitar a adição de novos sem alterar o código existente.
