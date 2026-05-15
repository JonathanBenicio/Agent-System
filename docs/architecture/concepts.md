# Conceitos Aplicados e Justificativas

Este documento detalha os principais conceitos arquiteturais e tecnológicos aplicados no **Agent-System**, explicando o **porquê** de suas escolhas e como eles se sustentam mutuamente.

---

## 1. .NET 10 & C#

O backend do sistema é construído inteiramente sobre o **.NET 10** e a linguagem **C#**.

### Por quê?
* **Performance de Próxima Geração:** O .NET 10 traz melhorias drásticas em JIT (Just-In-Time compilation) e redução de alocação de memória, permitindo que o sistema processe múltiplos fluxos de agentes com latência mínima.
* **Tipagem Forte e Robustez:** Em um sistema onde mensagens e estados trafegam entre múltiplos agentes, a tipagem forte do C# previne uma vasta categoria de erros em tempo de execução que seriam comuns em linguagens dinâmicas.
* **Programação Assíncrona Nativa:** O modelo `async/await` do C# é um dos mais maduros do mercado, ideal para operações de I/O intensivas, como chamadas a APIs de LLM e consultas a bancos de dados vetoriais.

---

## 2. Inteligência Artificial (IA) Híbrida

O sistema não depende apenas de um tipo de IA, mas combina diferentes abordagens para obter o melhor custo-benefício (FinOps) e performance.

### Por quê?
* **LLMs (Large Language Models):** Usados para o raciocínio complexo, geração de código e compreensão de linguagem natural aberta. São o "cérebro" dos agentes especialistas.
* **ML.NET (Machine Learning Local):** Aplicado no *Fast Path* da triagem. Um modelo clássico de classificação roda localmente para poupar custos e tempo, provando que nem tudo precisa de um LLM caro.
* **RAG (Retrieval-Augmented Generation):** Permite que os agentes consultem fontes de verdade locais (como arquivos Markdown no vault do Obsidian) antes de responder, garantindo que as respostas sejam baseadas em dados reais do projeto e não em alucinações.

---

## 3. Multi-Agent Framework (MAF)

A arquitetura do sistema é baseada em múltiplos agentes especializados em vez de um único chatbot monolítico.

### Por quê?
* **Divisão de Trabalho:** Assim como em uma equipe humana, agentes especialistas (focados em .NET, UI/UX, Banco de Dados, etc.) performam melhor em suas áreas do que um agente generalista tentaria fazer tudo.
* **Orquestração Hierárquica:** O uso de um agente Orquestrador (Conductor) permite decompor problemas complexos em passos menores e delegá-los, aumentando a taxa de sucesso na conclusão de tarefas difis.
* **Escalabilidade de Funções:** É fácil adicionar um novo especialista ao sistema (ex: um agente focado em segurança) sem precisar reescrever as regras dos outros agentes.

---

## 4. Domain-Driven Design (DDD)

O código segue rigorosamente os princípios do DDD para gerenciar a complexidade do negócio.

### Por quê?
* **Proteção do Domínio:** As regras de como os agentes se comunicam e operam estão isoladas na camada de `Domain`. Mudanças em como salvamos os dados (Infraestrutura) não quebram o comportamento do sistema.
* **Linguagem Ubíqua:** O código usa os mesmos termos que usamos para planejar o sistema (Agente, Triagem, Skill, Tool), facilitando a leitura e a manutenção tanto por humanos quanto por IAs que geram código no repositório.

---
*Nota: A combinação de uma base sólida e performática (.NET 10) com a flexibilidade da IA moderna (MAF/LLM) é o que torna este sistema único e pronto para produção.*
