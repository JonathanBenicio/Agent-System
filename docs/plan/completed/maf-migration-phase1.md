# Implementation Plan: Migração MAF Nativo - Fase 1 (Infraestrutura e LLM)

## Background & Motivation
O AgenticSystem atualmente utiliza o Microsoft Agent Framework (MAF), mas delega a comunicação com os modelos para abstrações genéricas e implementações manuais. O Roadmap de Migração define a necessidade de migrar para as bibliotecas nativas do MAF 1.6+ para reduzir boilerplate, melhorar a governança nativa e suportar nativamente provedores oficiais.

## Scope & Impact
Esta é a Fase 1 de 4. O escopo é estritamente restrito à camada de infraestrutura de LLM.
Impacto: A forma como a API Key e os provedores LLM são injetados será refatorada.
O que não muda: Os workflows de colaboração e as interfaces de sessão não serão alterados nesta fase.

## Proposed Solution
Substituir as dependências diretas de Microsoft.Extensions.AI.OpenAI pelos pacotes oficiais de orquestração LLM do MAF (Microsoft.Agents.AI.OpenAI). Atualizar a injeção de dependência na camada AgenticSystem.Infrastructure para refletir os builders nativos do MAF em vez de clientes manuais.

## Atendimento à Governança (Regra de Ouro)
Antes de alterar os códigos fonte, os seguintes artefatos devem ser gerados/atualizados:
1. ADR: Criar docs/architecture/adr/ADR-004-MAF-Native-LLM-Clients.md.
2. User Story: Atualizar docs/USER-STORIES.md refletindo a capacidade técnica do novo framework.

## Implementation Steps

1. Documentação de Arquitetura (Governança):
   Criar o ADR descrevendo a mudança para os clientes nativos do MAF.
   Atualizar a rastreabilidade nas User Stories.

2. Atualização de Pacotes:
   No arquivo src/AgenticSystem.Infrastructure/AgenticSystem.Infrastructure.csproj:
   Remover o pacote Microsoft.Extensions.AI.OpenAI versão 10.6.0.
   Adicionar o pacote Microsoft.Agents.AI.OpenAI versão 1.6.2.
   Garantir que as versões de Microsoft.Agents.AI estejam estabilizadas em 1.6.2.

3. Refatoração da Injeção de Dependências:
   Localizar os arquivos de setup (Program.cs e extensões de ServiceCollection na infraestrutura).
   Substituir o uso de AddChatClient puro pela nova sintaxe do MAF.
   Adaptar ou substituir os decorators legados que não se alinharem mais com o novo pipeline oficial, ou envolve-los adequadamente no novo modelo de provedor.

## Verification & Testing
Executar o comando dotnet restore e dotnet build para garantir que não haja erros de compilação.
Executar os testes unitários com dotnet test. Espera-se que todos os testes continuem passando.

## Migration & Rollback Strategy
Rollback: Caso a abstração quebre contratos fortemente acoplados de RAG, o plano de rollback imediato será reverter o commit do git correspondente à Fase 1, retornando as bibliotecas legadas sem perda de dados.