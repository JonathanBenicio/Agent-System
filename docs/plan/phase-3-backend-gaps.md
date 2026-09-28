# Roadmap: Phase 3 Backend Gaps and Refactoring

> **Status documental:** Em Execução
> **Escopo:** Implementação dos gaps do backend para a Phase 3 (GoldenSetController refactoring, DDD repository structure alignment, e testes correspondentes)
> **Fonte de verdade operacional:** c:\Users\Jonathan\Documents\Developer\GitHub\Agent-System\.agents\worker_phase3_refactor\original_prompt.md
> **Gerado em:** 2026-06-04
> **Projeto:** AgenticSystem

---

## Objetivo

Refatorar o `GoldenSetController` para suportar múltiplas rotas de compatibilidade, execução assíncrona baseada em fila/cache quando a quantidade de testes for superior a 20 casos, alinhar o `InMemoryGoldenSetRepository` ao domínio (DDD) movendo-o para o projeto Core e remover dependências de infraestrutura, garantindo isolamento de tenant e integridade de todos os testes unitários/integração.

## Princípios de Implantação

1. **Retrocompatibilidade**: Manter o suporte às rotas antigas e novas (`api/golden-sets`, `api/evaluation/golden-sets`, `api/goldensets`).
2. **Isolamento de Tenant**: Todo acesso a dados e execução deve respeitar estritamente o TenantId do contexto atual.
3. **DDD Boundary**: Entidades de domínio (Core) não devem depender de classes e entidades da persistência (Infrastructure).
4. **Sem Facades/Hardcoding**: Toda lógica de polling e concorrência deve ser implementada genuinamente (usando `IMemoryCache` e `Task.Run`).

## Fases e Sequenciamento

| Ordem | Frente/Fase | Motivo do sequenciamento |
|---|---|---|
| 1 | Alinhamento Repositório DDD | Mover e refatorar o repositório em memória para desvincular do persistence/Infrastructure. |
| 2 | Refatoração GoldenSetController | Implementar suporte a rotas, limite de casos (> 20 assíncrono), cache de polling e isolamento. |
| 3 | Correção de Testes e Dependências | Atualizar namespaces nos testes existentes e adicionar novos casos de teste. |
| 4 | Validação e Compilação | Garantir que o projeto compila e passa nos 608+ testes. |

---

## Detalhamento: Refatoração de GoldenSet e Repositório

### Por que implementar?
- Desacoplar regras de domínio e persistência básica em memória.
- Evitar timeouts em requisições HTTP para conjuntos de teste grandes (> 20 casos) permitindo execução assíncrona e polling.

### Componentes propostos
| Componente | Papel |
|---|---|
| `InMemoryGoldenSetRepository` | Armazenamento de GoldenSet em memória usando ConcurrentDictionary direta no modelo de domínio. |
| `GoldenSetController` | Orquestração da API de GoldenSets, controle de fluxo (sync vs async), e endpoints de polling `/runs/{runId}`. |

### Plano por etapas
1. **Repository Move**: Mover `InMemoryGoldenSetRepository.cs` para `src/AgenticSystem.Core/Services/InMemoryGoldenSetRepository.cs`. Atualizar namespace e utilizar `ConcurrentDictionary<string, GoldenSet>` direto.
2. **Repository Registration**: Ajustar no Core e no Infrastructure as injeções de dependência (`ServiceCollectionExtensions.cs`).
3. **Controller Refactoring**:
   - Ajustar rotas com `[Route]`.
   - Injetar `IMemoryCache` e `IServiceProvider`.
   - Adicionar lógica condicional para contagem de casos no endpoint de `/run`:
     - <= 20: executa sincrono.
     - > 20: inicia `Task.Run`, cria escopo DI, flows `TenantContext` e salva no Cache com 30min expiração. Retorna 202.
   - Endpoint `GET runs/{runId}` com retorno de status da execução.
4. **Test Suite Updates**: Atualizar imports nos testes e escrever testes para novas funcionalidades (async runs, polling 404, tenant isolation).

### Critérios de Aceite e SLOs
* [ ] Suporte às rotas: `api/golden-sets`, `api/evaluation/golden-sets`, `api/goldensets`.
* [ ] Testes assíncronos retornam 202 Accepted.
* [ ] Validação de isolamento de Tenant: Tenant A não acessa ou roda GoldenSets de Tenant B.

### Riscos e Mitigações
| Risco | Mitigação |
|---|---|
| Concorrencia em background sem escopo | Criar escopo DI explicitamente e setar o `TenantContext` de forma apropriada no fluxo. |
