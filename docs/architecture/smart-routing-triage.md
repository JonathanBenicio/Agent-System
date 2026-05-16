# 🧭 Smart Routing & Triage Architecture

## Overview
O **Smart Routing** é o mecanismo de roteamento inteligente do AgenticSystem, projetado para minimizar latência, reduzir custos de LLM e garantir que cada requisição seja tratada pelo agente mais adequado. Ele opera em um modelo de **múltiplas camadas (N-Tier Triage)**.

---

## 🏗️ As 3 Camadas de Triagem

O sistema processa a entrada do usuário através de três filtros sequenciais:

### 1. Camada 0: Regex Fast-Path (`ConversationalFastPathInterceptor`)
*   **Propósito**: Identificar saudações e interações triviais instantaneamente.
*   **Mecanismo**: Expressões regulares otimizadas e compiladas.
*   **Latência**: < 1ms.
*   **Exemplo**: "Oi", "Tudo bem?", "Bom dia".

### 2. Camada 0.5: ML Local Fast-Path (`MlFastPathInterceptor`)
*   **Propósito**: Classificar intenções simples que não exigem LLM, mas são variadas demais para Regex.
*   **Mecanismo**: Modelo **ML.NET (Multiclass Classification)** exportado para **ONNX**.
*   **Pipeline de Treino (Granular)**:
    1.  `TokenizeIntoWords`: Quebra o texto em tokens.
    2.  `MapValueToKey`: Mapeia tokens para chaves numéricas.
    3.  `ProduceNgrams`: Gera n-grams (unigrams/bigrams) para captura de contexto.
    4.  `OneHotHashEncoding`: Vetorização do texto.
    5.  `SdcaMaximumEntropy`: Algoritmo de classificação de alta performance.
*   **Exportação ONNX**: O pipeline utiliza apenas transformações compatíveis com o runtime ONNX, permitindo execução em cross-platform com latência mínima.
*   **Latência**: < 10ms (Local Inference).
*   **Exemplo**: Small talk variado, agradecimentos, perguntas sobre o estado do sistema.

### 3. Camada 1: LLM Triage (`TriageService`)
*   **Propósito**: Análise semântica profunda da complexidade e intenção.
*   **Mecanismo**: LLM de baixo custo (ex: `gpt-4o-mini` ou `gemini-1.5-flash`) com `ResponseFormat.Json`.
*   **Saída**: Objeto `QueryTriageResult` contendo:
    - `Intent`: (SmallTalk, DirectAnswer, ComplexReasoning)
    - `Complexity`: (Low, Medium, High)
    - `RequiresRAG`: Booleano
    - `RequiresTools`: Booleano
    - `RecommendedAgentTier`: Sugestão de especialista.

## 🏋️ Treinamento do Modelo ML (FastPathTrainer)

O projeto `src/AgenticSystem.FastPathTrainer` é uma aplicação console dedicada a treinar o modelo de classificação multiclasse do ML.NET e exportá-lo para os formatos necessários.

*   **Localização**: `src/AgenticSystem.FastPathTrainer`
*   **O que faz**: Carrega uma lista de exemplos rotulados (Saudações, Status do Sistema, Ajuda, etc.), treina o modelo e gera os arquivos `fastpath_model.zip` e `fastpath_model.onnx`.
*   **Como Executar**:
    ```bash
    dotnet run --project src/AgenticSystem.FastPathTrainer/AgenticSystem.FastPathTrainer.csproj
    ```
*   **Categorias Suportadas**: `Greeting`, `SmallTalk_HowAreYou`, `SmallTalk_Thanks`, `Agent_Capabilities`, `System_Status`, `Goodbye`, `Feedback_Positive`, `Feedback_Negative`, `User_Help`.

---

## 🔄 Detalhamento do Fluxo de Triagem (Legível por Humanos)

Para entender o fluxo sem depender do diagrama visual, imagine o seguinte processamento passo a passo:

### Exemplo 1: Usuário diz "Olá"
1.  **Camada 0 (Regex)**: O interceptor verifica se a string bate com padrões como `^ol[áa]$`.
2.  **Resultado**: Bateu! O sistema responde "Olá! Como posso ajudar?" instantaneamente. **Fim do fluxo.**

### Exemplo 2: Usuário diz "Como está a saúde da aplicação?"
1.  **Camada 0 (Regex)**: Não há regex exata para essa frase variada.
2.  **Camada 0.5 (ML.NET)**: O modelo analisa a frase e a classifica como `System_Status` com 92% de confiança.
3.  **Resultado**: Como 92% > 80% (threshold), o sistema executa a ação de status. **Fim do fluxo.**

### Exemplo 3: Usuário diz "Crie um plano de migração para o banco X"
1.  **Camada 0 (Regex)**: Não bate.
2.  **Camada 0.5 (ML.NET)**: O modelo não encontra um padrão conhecido com alta confiança (< 80%).
3.  **Camada 1 (LLM Triage)**: A requisição é enviada para um LLM menor. Ele retorna um JSON dizendo que a intenção é complexa e requer o `Orchestrator`.
4.  **Resultado**: A tarefa é enviada para o Orquestrador Completo.

---

## 🎯 Fluxo de Decisão (Decision Tree)

```mermaid
graph TD
    In[Entrada do Usuário] --> C0{Regex Match?}
    C0 -- Sim --> Resp0[Resposta Instantânea]
    C0 -- Não --> C05{ML Confidence > 80%?}
    C05 -- Sim --> Resp05[Resposta ML]
    C05 -- Não --> C1[LLM Triage]
    C1 --> Comp{Complexidade?}
    Comp -- Low --> Direct[Execução Direta Tier 1]
    Comp -- High/Medium --> Orchestrator[Orquestração Completa]
```

---

## ⚙️ Configuração e Extensibilidade

### Adicionando Novos Interceptores
Novos interceptores podem ser adicionados implementando a interface `IFastPathInterceptor` e registrando-os no `IServiceCollection`. O `SmartRouter` os executará em ordem de registro.

### Otimização de Custos (FinOps)
Ao resolver requisições nas Camadas 0 e 0.5, o sistema evita chamadas de API externas, resultando em:
*   **Custo Zero** para interações básicas.
*   **Latência de UI** extremamente baixa.
*   **Resiliência**: O sistema responde saudações mesmo se o provedor de LLM estiver offline.
