# ADR 001: Decisão Arquitetural - Adoção de .NET 10 e Domain-Driven Design (DDD)

**Status:** Aprovado (Retrospectivo)  
**Data:** 15 de Maio de 2026 (Reflexão sobre decisão anterior)  
**Autor:** Antigravity (em colaboração com o usuário)

---

## Contexto

No início do desenvolvimento do projeto Agent-System, enfrentamos o desafio de escolher a stack tecnológica e o padrão arquitetural ideais para sustentar um sistema complexo de múltiplos agentes (Multi-Agent Framework - MAF). O sistema exigia alta performance para orquestração em tempo real, capacidade de lidar com regras de negócio complexas e em constante evolução, e uma estrutura que permitisse testes rigorosos.

As alternativas consideradas envolviam stacks baseadas em Python (amplamente utilizadas no ecossistema de IA) versus uma abordagem baseada em .NET, que oferecia maior robustez em nível corporativo.

## Decisão

Optamos por utilizar o **.NET 10** como plataforma principal para o backend e adotar os princípios de **Domain-Driven Design (DDD)** para guiar a arquitetura do sistema.

Esta decisão foi tomada para garantir que o "coração" do sistema (o domínio da orquestração de agentes) permanecesse isolado e protegido de detalhes de infraestrutura, permitindo uma evolução sustentável.

## Justificativa

### Por que .NET 10?

1. **Performance Extrema:** O .NET 10 oferece melhorias significativas de performance e redução de alocação de memória, cruciais para um sistema que processa múltiplas requisições de agentes simultaneamente.
2. **Tipagem Forte e Segurança:** Reduz a incidência de erros em tempo de execução em um sistema onde o fluxo de dados entre agentes precisa ser estritamente controlado.
3. **Ecosistema Maduro:** Excelente suporte para construção de APIs assíncronas, processamento em segundo plano e integração com ferramentas de mensageria.

### Por que Domain-Driven Design (DDD)?

1. **Separação de Áreas de Interesse (SoC):** Isola a lógica de negócio complexa (regras dos agentes, estratégias de triagem) da infraestrutura (acesso a banco de dados, chamadas de API externas).
2. **Linguagem Ubíqua:** Facilita a comunicação ao alinhar o código com os conceitos de negócio e os modelos mentais dos agentes.
3. **Testabilidade:** A arquitetura desacoplada permite testar o domínio exaustivamente sem depender de banco de dados ou serviços externos (padrão AAA).

## Consequências

### Positivas

* **Robustez e Escalabilidade:** O sistema demonstra alta estabilidade e capacidade de escala vertical e horizontal.
* **Evolução Segura:** Mudanças na infraestrutura (como a troca de um banco de dados ou provedor de LLM) têm impacto mínimo na lógica de negócio central.
* **Código Auto-Documentado:** A estrutura do domínio reflete claramente as intenções do negócio.

### Desafios / Pontos de Atenção (Negativas)

* **Complexidade Inicial:** A criação de camadas, entidades, agregados e mappers exige mais código inicial (boilerplate) do que uma arquitetura CRUD simples.
* **Curva de Aprendizado:** Exige que a equipe (e os agentes que codificam) compreendam profundamente os conceitos de DDD para evitar o vazamento de abstrações.
* **Sobrecarga de Mapeamento:** A necessidade de mapear objetos entre as camadas de Domínio, Aplicação e Infraestrutura pode ser trabalhosa se não for bem automatizada.

---
*Nota: Este documento foi criado retrospectivamente para registrar e justificar as decisões que moldaram o estado atual do sistema.*
