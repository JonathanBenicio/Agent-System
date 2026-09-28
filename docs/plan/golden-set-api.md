# Roadmap: Golden Set CRUD API e Integração com IRuntimeEvaluator

> **Status documental:** Em Execução
> **Escopo:** Implementar a API REST de gerenciamento de Golden Sets da Evaluation Suite e integrar com o pipeline de execução e avaliação do Runtime.
> **Fonte de verdade operacional:** docs/architecture/adr/032-evaluation-golden-sets-rest-api.md
> **Gerado em:** 2026-06-04
> **Projeto:** AgenticSystem

---

## Objetivo

Implementar a API REST para gerenciamento de Golden Sets, que armazena conjuntos de casos de teste usados para avaliar a qualidade e a regressão de agentes. Esta funcionalidade é uma entrega pendente da Track 4 (Avaliação Contínua) e permite a manipulação desses conjuntos de forma multi-tenant nativa.

## Princípios de Implantação

1. **Isolamento de Tenant**: Todo acesso a Golden Sets e execuções deve ser restrito ao tenant proprietário, identificado no `TenantContext` ou nos cabeçalhos da requisição.
2. **Consistência REST**: Responder seguindo a modelagem de contratos do sistema ( envelope de resposta coerente, paginação e envelopes de erro).
3. **Resiliência e Execução Assíncrona**: Para testes grandes (> 20 casos), a execução deve ser assíncrona, usando processamento em segundo plano e cache na memória para status de progresso.
4. **Alinhamento com .NET 10**: O código deve compilar e rodar em .NET 10, com 100% de cobertura nos casos de isolamento de tenants.

## Fases e Sequenciamento

| Ordem | Frente/Fase | Motivo do sequenciamento |
|---|---|---|
| 1 | Entidade e Infraestrutura DB | Preparar o modelo e a persistência Postgres e InMemory |
| 2 | Contratos (DTOs) | Definir as fronteiras de dados da API |
| 3 | Injeção de Dependências | Configurar os serviços no container do ASP.NET Core |
| 4 | Geração da Migration | Criar schema do banco para PostgreSQL |
| 5 | Implementação do Controller | Expor os endpoints REST e lógica de execução síncrona/assíncrona |
| 6 | Testes Unitários e Integração | Validar comportamento do controller, lógica do Evaluator e isolamento de tenant |

---

## Detalhamento: Golden Set CRUD API e Integração com IRuntimeEvaluator

### Componentes propostos
| Componente | Papel |
|---|---|
| `GoldenSetEntity` | Modelo de banco de dados mapeando a tabela `golden_sets` no DbContext |
| `GoldenSetCase` | Tipo para representar um caso individual (input/output/tags) |
| `IGoldenSetRepository` | Abstração para consulta e persistência |
| `PostgresGoldenSetRepository` | Implementação com EF Core e isolamento de tenant |
| `InMemoryGoldenSetRepository` | Fallback em memória caso Postgres esteja desabilitado |
| `GoldenSetController` | Controlador de API mapeando `/api/golden-sets` e `/api/evaluation/golden-sets` |
| `GoldenSetDtos` | Classes contendo requests e responses formatados |

### Plano por etapas
1. **Entidade**: Criar `GoldenSetEntity` e a classe de valor `GoldenSetCase`.
2. **Contexto**: Adicionar DbSet correspondente em `AgenticDbContext`.
3. **Repositório**: Escrever o repositório PostgreSQL e a version InMemory.
4. **DTOs**: Implementar a listagem de classes de transferência conforme a especificação.
5. **Injeção**: Configurar o container DI de Core e Infrastructure.
6. **Migração**: Gerar e aplicar a migração de banco usando `dotnet ef`.
7. **Controller**: Criar `GoldenSetController` com CRUD + run.
8. **Runs**: Codificar execução síncrona e assíncrona com `IMemoryCache` e `BackgroundJob`.
9. **Verificação**: Desenvolver testes, rodar o build e garantir cobertura.

### Critérios de Aceite e SLOs
* [ ] Sucesso na compilação do projeto e execução de todos os testes backend.
* [ ] CRUD isolado por tenant verificado via testes de integração.
* [ ] Endpoint de execução com processamento em lote síncrono e assíncrono.
* [ ] Migrações geradas corretamente no diretório Persistence/Migrations.
