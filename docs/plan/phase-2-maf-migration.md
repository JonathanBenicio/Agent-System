# Roadmap: Fase 2 — Saneamento de DI e Migração MAF 1.9.0

> **Status documental:** Concluído
> **Escopo:** Resolver 4 problemas críticos de Dependency Injection no núcleo do Microsoft Agent Framework e migrar os pacotes `Microsoft.Agents.*` de 1.6.2 para 1.9.0.
> **Fonte de verdade operacional:** [Backend Architecture Audit](backend-architecture-audit.md) · [ADR-033](../architecture/adr/033-maf-190-upgrade.md)
> **Gerado em:** 04 de Junho de 2026
> **Projeto:** AgenticSystem

---

## Objetivo

Eliminar os 4 anti-patterns de Dependency Injection identificados no núcleo de orquestração MAF (`FrameworkOrchestratorService`, `OrchestratorHostBuilder`, `OrchestratorContextFactory`) e sincronizar os pacotes para a versão estável 1.9.0, que resolve os problemas de captive dependency via mudanças nativas de DI (`HarnessAgent`) e promove workflows declarativos a status estável.

Esta fase deve ser executada **após a Fase 1** para garantir que a base de dados e lógica de negócio estejam estáveis antes de refatorar a infraestrutura de orquestração.

## Princípios de Implantação

1. **Mudanças incrementais** — cada correção de DI deve ter seu próprio commit e PR, para facilitar rollback isolado.
2. **Testes primeiro** — escrever testes de integração que reproduzam o bug antes de aplicar a correção.
3. **Nenhum breaking change de protocolo** — endpoints A2A, AgUI e MCP devem continuar funcionando idênticos durante toda a migração.
4. **Feature flag para MAF 1.9.0** — usar `appsettings` para controlar ativação de novas APIs experimentais durante a transição.
5. **Validação de startup DI** — habilitar `ValidateOnBuild = true` em desenvolvimento para detectar captive dependencies em startup.

## Fases e Sequenciamento

| Ordem | Frente/Fase | Motivo do sequenciamento |
|---|---|---|
| 1 | Habilitar ValidateOnBuild + Testes de DI | Detectar todos os problemas antes de corrigir |
| 2 | Corrigir SpecialistBindings (telemetria) | Correção isolada, baixo risco, alto valor de rastreabilidade |
| 3 | Refatorar Scoped lifetime (OrchestratorHostBuilder + Factory) | Elimina captive dependency — requer teste de regressão de escopo |
| 4 | Eliminar Double Build + Sync-over-Async | Refatoração de performance — depende do Scoped estar correto |
| 5 | Atualizar pacotes MAF para 1.9.0 | Fazer por último para aproveitar as APIs nativas de DI do 1.9.0 |

---

## Detalhamento 1: ValidateOnBuild e Testes de DI

### Por que implementar?

Sem `ValidateOnBuild`, captive dependencies só explodem em runtime (geralmente em produção, sob carga). A habilitação em desenvolvimento permite que o pipeline de CI detecte violações de escopo na fase de build.

### Plano por etapas

1. **Habilitar** `ValidateScopes = true` e `ValidateOnBuild = true` em `Program.cs` para `Development` environment.
2. **Executar** a aplicação localmente e capturar todos os erros de captive dependency reportados.
3. **Criar testes** de integração usando `WebApplicationFactory<Program>` que validam a resolução correta dos serviços de orquestração sem lançar `InvalidOperationException`.

### Critérios de Aceite

* [x] `dotnet run` em Development não lança `InvalidOperationException` de captive dependency.
* [x] CI pipeline valida DI em build sem necessidade de execução runtime.

---

## Detalhamento 2: Corrigir SpecialistBindings (Telemetria Corrompida)

### Por que implementar?

`OrchestratorContextFactory` hardcodeia `SpecialistBindings = []`. O loop de matching em `FrameworkOrchestratorService` sempre falha (`calledBinding == null`), corrompendo metadados de telemetria de roteamento de agentes.

### Componentes propostos

| Componente | Papel |
|---|---|
| `OrchestratorContextFactory.cs` | Popular `SpecialistBindings` com agentes registrados em runtime |
| `IAgentRegistry` / `IDynamicAgentRepository` | Fonte de verdade dos agentes disponíveis |

### Plano por etapas

1. **Mapear** como agentes especialistas são registrados no sistema (via `IDynamicAgentRepository` ou `IAgentFactory`).
2. **Injetar** `IAgentRegistry` ou `IDynamicAgentRepository` em `OrchestratorContextFactory`.
3. **Popular** `SpecialistBindings` com os agentes ativos do tenant a partir do repositório.
4. **Verificar** que `calledBinding` é resolvido corretamente em `FrameworkOrchestratorService` após o fix.
5. **Escrever testes** validando que o binding correto é capturado no `AgentTelemetryEvent`.

### Critérios de Aceite

* [x] `calledBinding` nunca é `null` para uma requisição com agente registrado.
* [x] Eventos de telemetria contêm `AgentTier` e `AgentName` corretos.
* [x] Testes unitários de `FrameworkOrchestratorService` validam routing correto.

---

## Detalhamento 3: Refatorar Lifetime para Scoped

### Por que implementar?

`OrchestratorHostBuilder` é Singleton mas captura `IServiceProvider` raiz, forçando resoluções de serviços Scoped (como `ITenantContext`) através do provider raiz — violação clássica de captive dependency.

**Regra crítica:** `OrchestratorHostBuilder` e `OrchestratorContextFactory` **devem ser registrados como Scoped juntos**. Mudar um sem o outro causa nova captive dependency.

### Componentes propostos

| Componente | Mudança |
|---|---|
| `ServiceCollectionExtensions.cs` | `AddSingleton<OrchestratorHostBuilder>` → `AddScoped<OrchestratorHostBuilder>` |
| `ServiceCollectionExtensions.cs` | `AddSingleton<OrchestratorContextFactory>` → `AddScoped<OrchestratorContextFactory>` |

### Plano por etapas

1. **Alterar** ambos os registros simultaneamente em `ServiceCollectionExtensions.cs`.
2. **Verificar** com `ValidateOnBuild` que nenhuma nova captive dependency é introduzida.
3. **Rodar testes de integração** de A2A, AgUI e chat para confirmar que escopo por request funciona corretamente.
4. **Medir** overhead de construção por request (esperado: negligível pois eram Singletons que já executavam lógica por request).

### Critérios de Aceite

* [x] Nenhum `InvalidOperationException` de captive dependency no startup (Development).
* [x] Tenant context corretamente isolado por request em cenário multi-tenant.
* [x] Testes de integração A2A e AgUI passando sem regressão.

---

## Detalhamento 4: Eliminar Double Build e Sync-over-Async

### Por que implementar?

**Double Build:** `FrameworkOrchestratorService` constrói `OrchestratorContext` (Build #1) e depois chama `BuildHandoffWorkflowAsync` (Build #2), duplicando alocações e logs de configuração.

**Sync-over-Async:** `OrchestratorContextFactory` usa `.GetAwaiter().GetResult()` durante DI registration — bloqueio síncrono de operação assíncrona, risco de thread pool starvation sob carga.

### Plano por etapas

1. **Eliminar Double Build:**
   - Refatorar `ExecuteAsync` em `FrameworkOrchestratorService` para reutilizar o `OrchestratorContext` já construído ao chamar `BuildHandoffWorkflowAsync`.
   - Garantir que `OrchestratorContext` é construído uma única vez por request.
2. **Resolver Sync-over-Async:**
   - Converter `OrchestratorContextFactory` para inicialização lazy assíncrona.
   - Mover qualquer setup assíncrono para `IAsyncDisposable` ou lazy `ValueTask<T>` em vez de executar no construtor/registro DI.
3. **Benchmark antes/depois** — medir tempo de construção do host builder por request com `Stopwatch`.

### Critérios de Aceite

* [x] `BuildHandoffWorkflowAsync` chamado apenas uma vez por request (verificável por logging).
* [x] Nenhum `.GetAwaiter().GetResult()` em código de registro DI ou construtores.
* [x] Benchmark mostra redução mensurável de latência p99 em cenário de carga.

---

## Detalhamento 5: Upgrade MAF 1.9.0

### Por que implementar?

A versão 1.9.0 resolve nativamente o problema de captive dependency via mudanças no `HarnessAgent` DI, promove workflows declarativos a estável e adiciona output tagging/filtering para multi-agent steps.

### Breaking Changes Conhecidos (1.6.2 → 1.9.0)

| Versão | Breaking Change | Impacto no Projeto |
|---|---|---|
| 1.8.0 | Remoção de code-gen em declarative workflows | Auditar `WorkflowDefinition` — nenhum código C# gerado dinamicamente deve existir |
| 1.9.0 | `HarnessAgent` requer `ILoggerFactory` + `IServiceProvider` no construtor | Atualizar instanciações de `HarnessAgent` no projeto |

### Plano por etapas

1. **Atualizar** todos os `<PackageReference Include="Microsoft.Agents.*">` para `1.9.0` nos arquivos `.csproj`.
2. **Resolver** erros de compilação — especialmente mudanças de assinatura em `HarnessAgent`.
3. **Remover decoradores `[Experimental]`** de classes de workflow onde aplicável.
4. **Auditar** `WorkflowDefinition` para garantir ausência de geração de código C# dinâmico (breaking do 1.8.0).
5. **Implementar output tagging** nos steps multi-agente para filtrar saídas intermediárias.
6. **Rodar suite completa** de 608 testes para garantir zero regressões.

### Critérios de Aceite

* [x] `dotnet build` sem erros após atualização dos pacotes.
* [x] `dotnet test` — 608 testes passando (zero regressões).
* [x] `[Experimental]` removido de APIs de workflow declarativo.
* [x] `HarnessAgent` recebe `ILoggerFactory` e `IServiceProvider` nativamente (sem hacks manuais).
* [x] Output tagging implementado em pelo menos 1 workflow multi-agente existente.

### Riscos e Mitigações

| Risco | Mitigação |
|---|---|
| Breaking changes não documentados entre sub-versões | Testar em branch separada antes de merge |
| Incompatibilidade com DurableTask preview | Verificar pacotes `Microsoft.Agents.DurableTask.*` separadamente |
| Regressões em A2A/AgUI após mudanças de protocolo | Manter testes de integração de protocolo no CI |
