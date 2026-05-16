# ADR 002: Decisão Arquitetural - Implementação de Smart Triage e Fast Path com ML.NET

**Status:** Aprovado  
**Data:** 15 de Maio de 2026  
**Autor:** Antigravity (em colaboração com o usuário)

---

## Contexto

Com o crescimento do uso do Agent-System, identificamos que a triagem de intenções do usuário dependia quase exclusivamente de chamadas a modelos de linguagem de grande porte (LLM). Isso resultava em:
1. **Alta Latência:** Mesmo comandos simples ou perguntas diretas demoravam segundos para serem processados.
2. **Custo Elevado (FinOps):** Uso desnecessário de tokens de LLM para classificar intenções triviais.
3. **Sobrecarga da API:** Risco de atingir limites de taxa (rate limits) dos provedores de LLM.

Precisávamos de uma estratégia de "atalho" (Fast Path) que pudesse resolver intenções comuns localmente e com latência próxima de zero.

## Decisão

Decidimos implementar uma arquitetura de triagem em 3 camadas (Smart Triage):

1. **Camada 1: Regras e Regex (Hard Path):** Para comandos exatos ou padrões muito claros.
2. **Camada 2: Fast Path (ML.NET):** Um modelo de classificação multiclasse local treinado para identificar intenções comuns com alta velocidade.
3. **Camada 3: Fallthrough (LLM Path):** O modelo de LLM completo, acionado apenas quando as camadas anteriores não atingem o limiar de confiança necessário.

## Justificativa

### Por que ML.NET para o Fast Path?

1. **Execução Local e In-Process:** O modelo roda diretamente no processo do .NET, sem chamadas de rede, garantindo latência na casa dos milissegundos.
2. **Custo Zero por Chamada:** Não consome tokens nem gera custos de infraestrutura adicionais além do processamento da própria máquina.
3. **Integração Nativa:** Sendo uma biblioteca da Microsoft para .NET, a integração com o restante do ecossistema do projeto foi direta e sem necessidade de bridges complexas (como seria com Python).

### A Abordagem em 3 Camadas

* Garante que o sistema seja **econômico e rápido** para a maioria dos casos de uso comuns.
* Mantém a **inteligência e flexibilidade** do LLM para lidar com a cauda longa de requisições complexas e ambíguas.

## Consequências

### Positivas

* **Redução Drástica de Latência:** Respostas quase instantâneas para comandos mapeados.
* **Economia de Custos:** Redução significativa no consumo de tokens de LLM.
* **Melhoria na UX:** O sistema parece muito mais "vivo" e responsivo.

### Desafios / Pontos de Atenção (Negativas)

* **Manutenção do Modelo:** Exige a coleta contínua de dados de treinamento e retreinamento periódico do modelo ML.NET para manter a acurácia.
* **Gerenciamento de Limiares (Thresholds):** Ajustar o nível de confiança necessário para aceitar a resposta do ML.NET versus passar para o LLM exige monitoramento.
* **Complexidade no Pipeline:** O `TriageService` agora precisa gerenciar três fluxos diferentes em vez de apenas um.

---
*Nota: Este documento reflete uma decisão recente de otimização de infraestrutura e custos.*
