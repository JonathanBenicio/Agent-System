# ADR 021: Decisão Arquitetural - Framework de Avaliação Contínua e Golden Sets

**Status:** Proposto  
**Data:** 18 de Maio de 2026  
**Autor:** Gemini CLI (em colaboração com o usuário)

---

## Contexto

A medida da qualidade de um sistema agentic é frequentemente subjetiva ("parece bom"). Mudanças no System Prompt ou na estratégia de RAG de um agente podem melhorar um cenário mas quebrar outro silenciosamente (regressão semântica).

Precisamos de uma forma objetiva, quantitativa e automatizada de medir o desempenho dos nossos agentes antes de promovê-los para produção.

## Decisão

Implementaremos o **Evaluation Engine** integrado ao sistema, baseado nos seguintes pilares:

1.  **Golden Sets (Evaluation Cases):** Um repositório de datasets contendo trios `{ Entrada, Saída Esperada, Contexto de Referência }` por agente ou domínio.
2.  **Microsoft.Extensions.AI.Evaluation:** Integração nativa com a biblioteca de avaliação do .NET para utilizar modelos avaliadores (LLM-as-a-judge).
3.  **Métricas de Qualidade:** Rastreamento de scores automáticos como:
    *   **Relevância:** A resposta atende à pergunta?
    *   **Grounding (Fidelidade):** A resposta se baseia apenas nos documentos recuperados do RAG?
    *   **Fluência:** A linguagem é natural e profissional?
4.  **Runtime Benchmarking:** Medição de latência e consumo de tokens durante a bateria de testes.
5.  **Dashboard de Qualidade:** Interface visual para comparar versões de agentes e visualizar regressões.

## Justificativa

### Por que esta abordagem?

1.  **Confiança na Evolução:** Permite iterar rapidamente nos prompts com a segurança de que o desempenho geral não caiu.
2.  **Identificação de Alucinações:** O uso de métricas de *Grounding* ajuda a detectar quando um agente está "inventando" informações fora do contexto das Knowledge Rooms.
3.  **Aceleração do Ciclo de Feedback:** Desenvolvedores e especialistas de domínio podem validar mudanças sem precisar de centenas de testes manuais.

## Consequências

### Positivas

*   **Ciclo de vida maduro (MLOps):** Traz práticas de DevOps para o mundo da IA Generativa.
*   **Decisões baseadas em dados:** Escolha de modelos (ex: GPT-4o vs Claude 3.5) baseada em scores reais de acerto para o domínio do projeto.
*   **Documentação Viva:** O Golden Set serve como especificação do que o agente deve saber responder.

### Desafios / Pontos de Atenção (Negativas)

*   **Custo de Avaliação:** Rodar avaliações via LLM consome tokens. Deve ser usado com critério em ambientes de CI/CD.
*   **Manutenção de Datasets:** datasets de teste precisam ser atualizados conforme o conhecimento do sistema evolui.

---
*Nota: Este documento suporta a Track 4 do Roadmap Q2 2026.*
