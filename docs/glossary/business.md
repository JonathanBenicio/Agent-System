# Glossário de Domínio (Negócio)

Este documento define os principais termos de negócio e do domínio de agentes inteligentes utilizados no **Agent-System**.

| Termo | Definição |
| :--- | :--- |
| **Agente** | Uma entidade virtual autônoma com um papel específico (persona), capaz de perceber o ambiente (inputs), tomar decisões e executar ações para atingir um objetivo. |
| **Orquestrador (Conductor)** | O agente mestre responsável por receber a requisição complexa do usuário, dividi-la em subtarefas e delegar a execução para os agentes especialistas corretos. |
| **Triagem (Triage)** | O processo inicial de analisar a mensagem do usuário para entender sua intenção e decidir qual o melhor caminho de execução ou qual agente deve responder. |
| **Persona** | O conjunto de características, tom de voz, restrições e conhecimentos específicos que definem o comportamento de um agente (ex: o especialista em .NET age de forma diferente do especialista em UI). |
| **Habilidade (Skill)** | Uma capacidade modular que um agente possui ou pode aprender. Habilidades ensinam o agente a "pensar" ou seguir um processo, em vez de apenas executar comandos fixos. |
| **Ferramenta (Tool)** | Um recurso externo (como um terminal, navegador ou API) que o agente pode invocar para interagir com o mundo real e realizar ações. |
| **Fluxo de Trabalho (Workflow)** | A sequência ordenada de passos ou tarefas que um ou mais agentes executam para concluir um pedido complexo do usuário. |
| **Auto-Melhoria (Self-Improvement)** | A capacidade do sistema de analisar suas próprias falhas ou sucessos passados e ajustar as instruções dos agentes ou o código do sistema para performar melhor no futuro. |
| **Visão Computacional (Vision)** | A capacidade de alguns agentes de "enxergar" e analisar imagens, capturas de tela ou diagramas para tomar decisões de design ou depuração. |
| **Memória de Longo Prazo** | O armazenamento persistente de contextos, preferências do usuário e aprendizados que os agentes podem recuperar em conversas futuras (frequentemente via RAG). |

---
*Nota: Este glossário visa alinhar a linguagem entre desenvolvedores, usuários e o próprio comportamento esperado dos agentes. Atualize sempre que novos conceitos de negócio forem introduzidos.*
