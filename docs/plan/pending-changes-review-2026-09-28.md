# Revisão das alterações pendentes — 28/09/2026

Esta revisão organiza as alterações existentes antes da atualização futura do Microsoft Agent Framework. Os pacotes da solução permanecem em MAF 1.9.0; nenhuma migração para 1.22.0 foi realizada.

## Contextos para commits

1. Workflows: resposta HTTP 202, timeouts de steps, sessões PostgreSQL, cliente DurableTask e schema `dt` (referência à issue #108).
2. Documentos: cópia física de uploads individuais e em lote com retorno de `fileDiskPath`.
3. Banners: layout, localização e telefone, override de modelo por step, bootstrap, registro da ferramenta e artefatos de demonstração.
4. MAF 1.9.0 e DI: atualização de pacotes, inicialização assíncrona do orquestrador, bindings de especialistas e lifetimes.
5. RAG/Rooms: filtragem SQL de metadados antes da seleção dos candidatos.
6. FinOps: repositório de quotas, cache, persistência PostgreSQL e reset diário.
7. Scheduler: normalização de cron Linux para Quartz e erro explícito de expressão inválida.
8. Golden Sets: modelos, repositórios, migrations, CRUD, avaliação e polling isolado por tenant.
9. Postman: corpos de requests, multipart, cenários de banners e propagação dos IDs/caminhos.
10. Testes de push: health check com HTTP simulado para eliminar dependência do FCM externo.
11. Documentação: auditoria, planos e índices com status e validações sustentados pelo código.

## Correções encontradas na revisão

- Retirado `WorkflowEngineTests.ListExtensions`, diagnóstico temporário que falhava incondicionalmente.
- Corrigido o setup do teste de DI para incluir storage, ambiente e broadcaster; dependências scoped passam a ser resolvidas em escopos de execução por callers singleton.
- Cache de execuções de Golden Sets usa chave composta por tenant e run ID, com teste de acesso entre tenants.
- O modelo de destino de `AddGoldenSets` foi regenerado; o snapshot foi alinhado ao modelo. `TenantQuotaEntity.RowVersion` usa `uint` para o `xmin` PostgreSQL, e a nulabilidade de `description` foi corrigida na migration ainda pendente.
- Removida chave de API literal do script manual de imagem; utiliza `AGENT_SYSTEM_API_KEY`.
- Corrigida a extração de `executionId` nos cenários Postman de workflows.

## Validação executada

- Build Release da solução concluído sem erros.
- Suíte backend: 687 testes aprovados, 1 ignorado, 0 falhas.
- Teste ignorado: `PostgresVectorStoreTests.SearchWithFiltersAsync_PreFiltersRoomsAtSqlLevel_ReturnsAuthorizedDocuments`, pois exige PostgreSQL real.
- `dotnet ef migrations has-pending-model-changes`: sem diferenças após as correções. Nenhuma migration foi aplicada a um banco nesta revisão.
- Cobertura coletada com XPlat Code Coverage: **22,39% das linhas**, abaixo dos **80%** exigidos; esta revisão não conclui esse requisito de qualidade.
- Corpos JSON e scripts Postman validados sem requests. Scripts Python revisados estaticamente; Python não está disponível no ambiente para execução.

## Pendências operacionais

- O RunId retornado pelo compilador DurableTask não possui integração comprovada com o polling do `WorkflowController`, que consulta `IWorkflowStore`. Não considerar o fluxo durável validado ponta a ponta.
- O semáforo do `PostgresWorkflowStore` serializa gravações somente no processo atual; não substitui controle de concorrência entre instâncias.
- Uploads de mesmo nome no mesmo tenant sobrescrevem a cópia física; faltam testes de integração para o fluxo de arquivos/banners.
- Reset diário no fallback de quotas em memória e formas avançadas de cron precisam de validação adicional.
- O BannerRunner foi alinhado ao ImageSharp 3.1.9 da solução; o restore ainda aponta um advisory moderado (NU1902). Demonstrações de banners não substituem testes do workflow com LLM. Execuções Postman e integração com PostgreSQL não foram realizadas.
- A API de Golden Sets usa status de avaliação em memória e `Task.Run`; o progresso não sobrevive a restart nem é compartilhado entre instâncias.

## Arquivos locais

Registros de sessões em `.agents/` (incluindo a pasta com espaço inicial), `ORIGINAL_REQUEST.md`, `PROJECT.md` e `notas/` permanecem fora dos commits, preservados no workspace. São registros e notas de trabalho, não dependências do backend.
