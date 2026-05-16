# Glossário Técnico

Este documento define os principais termos técnicos e acrônimos utilizados no ecossistema do **Agent-System**.

| Termo / Acrônimo | Definição |
| :--- | :--- |
| **DDD** | *Domain-Driven Design*. Uma abordagem de design de software focada na modelagem de um domínio de negócio complexo. No projeto, é usado para isolar a lógica dos agentes da infraestrutura. |
| **MAF** | *Multi-Agent Framework*. A estrutura que gerencia a criação, ciclo de vida e comunicação de múltiplos agentes autônomos no sistema. |
| **RAG** | *Retrieval-Augmented Generation*. Técnica que combina busca de informações (recuperação) com geração de texto por IA (geração), permitindo que os agentes acessem documentos locais (como o Obsidian) para responder a perguntas. |
| **Smart Triage** | O pipeline de triagem de intenções do usuário que utiliza múltiplas camadas (Regex, ML local e LLM) para direcionar a tarefa ao agente correto de forma eficiente. |
| **Fast Path** | O caminho de execução rápida dentro do Smart Triage que utiliza o modelo local do **ML.NET** para classificar intenções comuns sem gastar tokens de LLM ou gerar alta latência. |
| **Fallthrough** | O fluxo de contingência que aciona o LLM completo quando as camadas mais rápidas de triagem não conseguem classificar a intenção com confiança suficiente. |
| **FinOps** | Práticas de gerenciamento financeiro aplicadas à nuvem e ao consumo de IA. No projeto, foca em reduzir o custo de chamadas a APIs de LLM externas. |
| **LLM** | *Large Language Model*. Modelos de IA generativa (como Gemini, GPT) usados para processamento de linguagem natural complexo e raciocínio dos agentes. |
| **Padrão AAA** | *Arrange, Act, Assert*. Padrão de organização de testes unitários que divide o teste em três etapas claras: Preparação, Ação e Verificação. |
| **SoC** | *Separation of Concerns* (Separação de Preocupações). Princípio de design que prega que cada parte do software deve focar em fazer apenas uma coisa bem feita, evitando acoplamento. |
| **DI / IoC** | *Dependency Injection / Inversion of Control*. Padrões usados no .NET para gerenciar as dependências entre classes, facilitando a testabilidade e o desacoplamento. |
| **Self-Improvement Engine** | O motor de auto-melhoria do sistema que analisa os logs de execução e sugere melhorias para o código ou para o comportamento dos agentes. |

---
*Nota: Este glossário é mantido pela equipe de engenharia e deve ser atualizado sempre que novos termos técnicos fundamentais forem introduzidos no projeto.*
