# Roadmap: Backend Dependency Injection & Background Services Refactoring

> **Status documental:** Planejamento futuro / Em Execução
> **Escopo:** Refatoração do padrão de injeção de dependência em HostedServices e BackgroundServices para evitar Captive Dependencies e garantir robustez de runtime.
> **Fonte de verdade operacional:** [GEMINI.md](file:///c:/Users/Jonathan/Documents/Developer/GitHub/Agent-System/src/GEMINI.md) & [dotnet-best-practices](file:///c:/Users/Jonathan/Documents/Developer/GitHub/Agent-System/.agents/skills/dotnet-best-practices/SKILL.md)
> **Gerado em:** 2026-05-23
> **Projeto:** AgenticSystem (Backend .NET 10)

---

## Objetivo

Esta iniciativa visa corrigir o padrão de injeção de dependência (DI) nos serviços de background (`BackgroundService` e `IHostedService`) do sistema. Atualmente, múltiplos HostedServices injetam diretamente em seus construtores serviços que dependem ou podem depender de escopos específicos (como DbContext, Tenancy Context ou outros repositórios transitórios). Esta refatoração garante que o construtor desses serviços injete unicamente Singletons ou fábricas (`IServiceProvider`), delegando a resolução de dependências específicas a escopos transitórios criados a cada ciclo de execução (`CreateScope`), eliminando o risco de Captive Dependencies e vazamento de memória em produção.

## Princípios de Implantação

1. **Captive-Dependency-Free**: Nenhuma dependência scoped (ou transient com dependências scoped) deve ser injetada no construtor de classes com ciclo de vida Singleton (como `BackgroundService`).
2. **Escopo Limpo e Isolado**: Cada ciclo de execução (ou tick do cron/timer) de um background service deve possuir um escopo de DI próprio (`IServiceProvider.CreateScope()`), garantindo que recursos como DbContext sejam liberados imediatamente após o término do processamento.
3. **Sem Placeholders e 100% Funcional**: Toda alteração de injeção de dependência deve ser acompanhada de uma validação estrita em compilação e execução de testes.

## Fases e Sequenciamento

| Ordem | Frente/Fase | Motivo do sequenciamento |
|---|---|---|
| 1 | **Refatoração dos BackgroundServices de Core** | O projeto Core abriga os orquestradores de ciclo de vida de sessão (`SessionAutoConsolidator`), limpeza e tarefas agendadas, que são críticos para a lógica de negócio do SaaS. |
| 2 | **Refatoração dos HostedServices de Infrastructure** | O projeto de infraestrutura possui o `DataSyncBackgroundService` que interage diretamente com conectores e armazenamento físico. |
| 3 | **Validação e Testes Backend** | Garantir que o sistema compila sem avisos e que toda a suíte de 652 testes passa perfeitamente. |

---

## Detalhamento: Injeção Dinâmica em BackgroundServices

### Por que implementar?
Evitar exceções catastróficas de ciclo de vida em ambientes de produção. O .NET por padrão em ambientes que ativam a validação de escopo lança `InvalidOperationException` se um Singleton consumir um serviço Scoped. Além disso, segurar instâncias de DbContext ou contextos de tenant em memória de forma estática impede o correto isolamento multi-tenant e causa vazamentos de conexão com o banco de dados.

### Componentes propostos
| Componente | Papel |
|---|---|
| `SessionAutoConsolidator` | Consolidará sessões pendentes resolvendo `ISessionStore`, `ISessionConsolidator`, `IMemoryInjectionService`, `ITenantStore` e `ISemanticCompressor` dinamicamente de forma isolada por ciclo. |
| `SecretRotationBackgroundService` | Resolverá `IConfigManager` e `IAuditLog` sob escopo dinâmico por ciclo de verificação. |
| `ScheduledTaskHostedService` | Resolverá `IScheduledTaskManager` e `ITriggerEngine` dinamicamente por tick de execução. |
| `AgentCleanupHostedService` | Resolverá `IMetaAgent` sob escopo dinâmico a cada ciclo de limpeza. |
| `DataSyncBackgroundService` | Resolverá `IDataConnectorManager` sob escopo dinâmico a cada ciclo de sincronização de conectores. |

### Plano por etapas
1. **Refatorar Core/Services**: Modificar `SessionAutoConsolidator`, `SecretRotationBackgroundService`, `ScheduledTaskHostedService` e `AgentCleanupHostedService` para receber `IServiceProvider` nos construtores e criar scopes dinâmicos em suas execuções.
2. **Refatorar Infrastructure/Services**: Modificar `DataSyncBackgroundService` para receber `IServiceProvider` e criar scopes.
3. **Executar Testes**: Validar todas as mudanças com `dotnet test`.

### Critérios de Aceite e SLOs
* [ ] Compilação com **zero** avisos de injeção de dependência.
* [ ] **Zero** Captive Dependencies detectadas em tempo de inicialização do container de DI.
* [ ] Todos os 652 testes executados com sucesso.

### Riscos e Mitigações
| Risco | Mitigação |
|---|---|
| Exceção por falta de serviço no container de DI | Manter injeção estrita com `GetRequiredService` em vez de `GetService` para sinalizar imediatamente qualquer erro de registro de container. |
