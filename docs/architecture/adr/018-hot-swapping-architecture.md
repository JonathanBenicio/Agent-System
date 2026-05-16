# ADR 018: Decisão Arquitetural - Arquitetura de Hot-Swapping para Provedores de IA e Vector Stores

**Status:** Aprovado  
**Data:** 16 de Maio de 2026  
**Autor:** Antigravity (em colaboração com o usuário)

---

## Contexto

O Agent-System opera em um ecossistema de Inteligência Artificial altamente dinâmico. Diferentes cenários exigem diferentes modelos de linguagem (LLMs) e bancos de dados vetoriais (Vector Stores) baseados em critérios como custo (FinOps), latência, privacidade e capacidade do modelo.

A abordagem tradicional de configurar esses provedores na inicialização e exigir um reinício da aplicação para alterá-los (estática) não atende aos requisitos de alta disponibilidade e flexibilidade do sistema. Precisávamos de uma forma de alternar entre provedores (ex: de OpenAI para Gemini, ou de SQLite para Pinecone) em tempo de execução, sem derrubar o sistema e sem impactar as requisições em andamento.

## Decisão

Implementamos uma **Arquitetura de Hot-Swapping** baseada em padrões do .NET e abstrações customizadas:

1. **Padrão Options (IOptionsMonitor):** Utilizamos `IOptionsMonitor<T>` para escutar mudanças nas configurações (geralmente vindas de um banco de dados ou arquivo JSON atualizado) em tempo real.
2. **Fábricas Dinâmicas (Dynamic Factories):** Implementamos `LLMProviderFactory` e `VectorStoreFactory` que criam instâncias dos provedores baseados na configuração atual.
3. **Provedores "Hot-Swappable" (Wrappers):** Criamos classes como `HotSwappableVectorStore` e `HotSwappableLLMProvider` que implementam as interfaces principais (`IVectorStore`, `ILLMProvider`) e atuam como proxies. Eles interceptam as chamadas e as delegam para a instância do provedor concreto que está ativa no momento, obtida via fábrica.

## Justificativa

### Por que esta abordagem?

1. **Continuidade do Serviço:** Permite que administradores mudem a infraestrutura de IA (ex: fallback para um modelo local se a API do Gemini falhar) sem que os usuários percebam qualquer indisponibilidade.
2. **Otimização de Custos (FinOps):** Facilita a troca para modelos mais baratos em horários de pico ou para tarefas menos complexas de forma automatizada.
3. **Isolamento de Complexidade:** O restante do sistema (agentes, serviços de domínio) continua conversando com as interfaces estáveis (`IVectorStore`), sem precisar saber que o banco de dados por trás mudou de SQLite para Pinecone no meio da execução.

## Consequências

### Positivas

* **Flexibilidade Extrema:** O sistema pode se adaptar a falhas de provedores ou mudanças de custo instantaneamente.
* **Excelente Experiência de Administração:** Configurações podem ser alteradas via painel administrativo (`HotSwapPanel.tsx`) e aplicadas imediatamente.
* **Testabilidade:** Facilita a injeção de mocks ou provedores em memória durante testes de integração sem mudar o setup do container.

### Desafios / Pontos de Atenção (Negativas)

* **Complexidade de Código:** A introdução de proxies e fábricas dinâmicas adiciona camadas de indireção que tornam o rastreamento do código mais complexo.
* **Gerenciamento de Estado:** Recursos que mantêm estado (como conexões persistentes ou caches locais de embeddings) precisam ser cuidadosamente descartados (Disposed) e recriados durante a troca para evitar vazamento de memória.
* **Consistência de Dados:** Trocar de Vector Store em tempo de execução significa que os dados salvos no SQLite não estarão magicamente no Pinecone. O sistema precisa de uma estratégia de sincronização ou aceitar que a memória de longo prazo muda com o provedor.

---
*Nota: Este documento registra a decisão de implementar a infraestrutura de troca dinâmica concluída no PR #27.*
