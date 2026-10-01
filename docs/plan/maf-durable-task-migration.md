# Roadmap: Migração para DurableTask e Resiliência (Gaps 2 e 3)

> **Status documental:** CONCLUÍDO (COMPLETED)\
> **Nota de escopo:** este marco entregou a API assíncrona e o engine PostgreSQL próprio; não comprova migração para `Microsoft.Agents.AI.DurableTask` nem recuperação completa de workflows. O caminho atual e seus gaps estão em [ADR-030](../architecture/adr/030-maf-durable-task-migration.md), [ADR-036](../architecture/adr/036-maf-122-protocols-and-gateway.md), [#120](https://github.com/JonathanBenicio/Agent-System/issues/120) e [#122](https://github.com/JonathanBenicio/Agent-System/issues/122).
> **Escopo:** Arquitetura de Orquestração Nativa baseada em `DefaultWorkflowEngine` + PostgreSQL, Padrão Async HTTP API (202 Accepted + Polling), Timeouts de Step com CancellationToken e remoção de código Azure-only.\
> **Fonte de verdade operacional:** [ADR 030](../architecture/adr/030-maf-durable-task-migration.md)
> **Revisado e Concluído em:** 26 de Maio de 2026\
> **Projeto:** AgenticSystem\
> **Issue Relacionada:** #108\

---

## Objetivo Concluído

Esta iniciativa visava estender o runtime atual do Microsoft Agent Framework (MAF) do Agentic System para garantir resiliência e prevenção de timeouts em execuções de workflows e agentes de longa duração sob ambiente Docker + PostgreSQL (sem Azure Functions).

Após análise técnica detalhada (documentada no ADR 030), identificou-se que a API `Microsoft.Agents.AI.DurableTask` com Durable Entities depende estritamente dos bindings do Azure Functions Runtime, sendo inviável em contêineres ASP.NET Core puros. Em substituição, projetou-se e implementou-se uma **Arquitetura de Orquestração Nativa Resiliente** no `DefaultWorkflowEngine` persistida via PostgreSQL, alcançando 100% dos objetivos do plano.

---

## Entregas Realizadas

### 1. Prevenção de Timeout (Padrão Async HTTP API)
* **Status:** Concluído.
* **Detalhes:** O endpoint `POST /api/workflow/executions/start/{id}` do `WorkflowController` foi alterado para responder imediatamente com **`202 Accepted`**, contendo o cabeçalho `Location` e o campo `statusUrl`.
* **Benefício:** Evita timeouts HTTP (504 Gateway Timeout) na conexão do cliente. O cliente pode monitorar o progresso em segundo plano via polling do `statusUrl` (`GET /api/workflow/executions/{id}`).

### 2. Timeout por Step com CancellationToken
* **Status:** Concluído.
* **Detalhes:** Implementado no `DefaultWorkflowEngine.cs` usando `CancellationTokenSource` por step. Steps do tipo `Action` que executam agentes ou ferramentas possuem um timeout limite configurável (padrão de 5 minutos).
* **Benefício:** Se um modelo LLM ou ferramenta externa travar, o step é cancelado de forma limpa, liberando recursos e marcando o workflow como `Failed` com uma mensagem clara sobre o estouro do tempo limite (`OperationCanceledException` tratada).

### 3. Remoção de Código Morto (Azure-only)
* **Status:** Concluído.
* **Detalhes:** Exclusão completa do arquivo `DurableSessionStoreAdapter.cs`, que exigia `DurableTaskClient` inexistente no DI nativo do Docker/PostgreSQL.
* **Benefício:** Código mais limpo e livre de acoplamento inútil a serviços de nuvem da Azure. O `SimpleSessionStoreAdapter` foi mantido como o store de sessão definitivo baseado em banco de dados.

### 4. BannerProductionTool: Retorno Estruturado para Polling
* **Status:** Concluído.
* **Detalhes:** Atualização da interface `IDynamicWorkflowCompiler` e suas implementações (`DynamicMafWorkflowCompiler` e `DurableWorkflowCompiler`) para retornar um objeto estruturado `WorkflowStartResult` contendo `RunId`, `IsAsync` e `Message`. O `BannerProductionTool` consome esse objeto e retorna um `ToolResult.Ok` descritivo contendo o `RunId` para polling assíncrono.

---

## Critérios de Aceite e Validação

- [x] Workflows de longa duração executam em segundo plano via `Task.Run` e persistem estados intermediários de step no PostgreSQL.
- [x] Prevenção de timeout: Conexão HTTP liberada imediatamente com retorno HTTP 202.
- [x] Limite de Step: Timeouts individuais de step propagam o `CancellationToken` corretamente e interrompem a execução do LLM/Tool.
- [x] Compilação limpa da solução: `0 Erros` no Release build; há aviso de nulabilidade preexistente.
- [x] Testes unitários passando com sucesso.

---

## Conclusão da Iniciativa

Esta entrega solidifica a base de orquestração do Agentic System sob infraestrutura Docker autônoma. O ciclo de vida de execuções de longa duração agora é resiliente a falhas temporárias e imune a timeouts na camada de transporte HTTP.

## Limites da validação

O caminho DurableTask ainda requer verificação com PostgreSQL e integração do RunId com o polling HTTP. Consulte [a revisão de 28/09/2026](pending-changes-review-2026-09-28.md).
