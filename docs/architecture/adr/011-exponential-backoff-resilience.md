# ADR-011: Resiliência com Exponential Backoff para Chamadas de LLM

## Status
Accepted (Retrospective)

## Context
Chamadas para APIs de LLM (tanto externas quanto locais) estão sujeitas a falhas transientes, como indisponibilidade momentânea, limites de taxa (rate limiting) excedidos ou lentidão na rede. Falhar imediatamente prejudica a experiência do usuário e a autonomia dos agentes.

## Decision
Implementamos uma política de reatentativa com **Exponential Backoff** (atraso exponencial) para todas as chamadas aos provedores de LLM.

## Rationale
1. **Tolerância a Falhas**: Pequenas instabilidades não quebram o fluxo do agente.
2. **Boas Maneiras com a API**: O atraso exponencial evita sobrecarregar o servidor que já está dando sinais de estresse (como em erros HTTP 429).
3. **Autonomia**: Permite que agentes de longa duração continuem tentando resolver a tarefa mesmo se houver um soluço na rede.

## Trade-offs
- **Tempo de Resposta**: Em caso de falha persistente, o tempo total até dar o erro final para o usuário aumenta consideravelmente.
- **Complexidade**: Exige configuração cuidadosa do número máximo de tentativas e do fator de multiplicação.

## Consequences
- **Positive**: Sistema muito mais robusto e menos propenso a erros intermitentes.
- **Negative**: Pode mascarar problemas reais de infraestrutura se não houver monitoramento adequado das falhas que foram resolvidas nas reatentativas.
- **Mitigation**: Implementação de logs detalhados e métricas para cada tentativa falha, permitindo analisar a saúde dos provedores.
