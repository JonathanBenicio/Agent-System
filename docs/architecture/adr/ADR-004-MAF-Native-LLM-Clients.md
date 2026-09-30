# ADR 004: Migração para Clientes Nativos do Microsoft Agent Framework (MAF 1.6+)

**Status:** Aprovado
**Data:** 24 de Maio de 2026
**Autor:** Antigravity (em colaboração com o usuário)

---

## Contexto

O AgenticSystem utiliza o Microsoft Agent Framework (MAF) para orquestrar agentes e gerenciar contexto e workflow. No entanto, para a camada de comunicação com os Modelos de Linguagem (LLM), o sistema estava altamente acoplado às abstrações genéricas `Microsoft.Extensions.AI.OpenAI`. Embora funcionais, essas abstrações genéricas geravam overhead na tradução de chamadas, impediam o uso pleno dos bindings nativos otimizados do próprio MAF e dificultavam a adesão imediata a recursos avançados de streaming e execução durável (Durable Execution) recém-lançados na versão 1.6.

## Decisão

Substituir a dependência genérica `Microsoft.Extensions.AI.OpenAI` pelo pacote oficial e otimizado `Microsoft.Agents.AI.OpenAI` na camada de Infraestrutura. 
A configuração de Injeção de Dependência (DI) em `ServiceCollectionExtensions.cs` foi atualizada para utilizar os providers nativos do MAF. Isso garante que o núcleo lógico do sistema converse com os Modelos de Linguagem através dos canais preferenciais do framework sem a necessidade de adapters manuais.

## Justificativa

1. **Eficiência e Performance:** Clientes projetados especificamente para a stack MAF gerenciam o fluxo de memória e a orquestração de ferramentas (Function Calling) de forma nativamente mais fluida do que abstrações puramente baseadas em Extensions genéricas.
2. **Escalabilidade Tecnológica:** Estar alinhado à principal forma recomendada pela Microsoft na versão 1.6 habilita automaticamente a adoção paralela de Providers como `Gemini` e `OpenRouter` sem ter que lutar contra limitações de middleware de um cliente base.
3. **Padrão Oficial:** Uniformizar os clientes reduz pontos de falha arquiteturais quando atualizações drásticas do motor do LLM ocorrem.

## Consequências

### Positivas
* Redução efetiva de código boilerplate de tradução nas chamadas.
* Maior estabilidade técnica, garantindo interoperabilidade imediata com futuras atualizações de framework do MAF.
* Melhoria intrínseca na propagação do contexto de rastreabilidade (Telemetria) ao usar clientes nativos.

### Desafios / Pontos de Atenção (Negativas)
* Alterou o modelo como os Middlewares legados (como `GovernedChatClient` e `ContextAwareChatClient`) interceptavam as execuções, exigindo refatorações minuciosas na camada de pipeline de chat para continuar aplicando restrições de qualidade (FIDES/Quality Gates).