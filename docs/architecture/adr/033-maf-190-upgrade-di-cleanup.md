# ADR 033: Migração do Microsoft Agent Framework para 1.9.0 e Saneamento de DI

**Status:** Proposto
**Data:** 04 de Junho de 2026
**Autor(es):** Antigravity AI & Jonathan Benicio

---

## Contexto

O AgenticSystem utiliza o **Microsoft Agent Framework (MAF)** versão `1.6.2` (e `1.6.2-preview.260521.1` para pacotes AGUI/A2A/DurableTask) como runtime de orquestração de múltiplos agentes cooperativos.

A auditoria arquitetural de Junho/2026 identificou **quatro anti-patterns críticos de Dependency Injection** no núcleo de integração com o MAF:

1. **Double Build:** `FrameworkOrchestratorService` constrói `OrchestratorContext` duas vezes por request — uma ao resolver a factory, outra ao chamar `BuildHandoffWorkflowAsync`.
2. **Sync-over-Async:** `OrchestratorContextFactory` usa `.GetAwaiter().GetResult()` durante registro de DI, bloqueando o thread pool sob carga concorrente.
3. **SpecialistBindings Vazio:** `OrchestratorContext` hardcodeia `SpecialistBindings = []`, causando falha silenciosa no routing de agentes e corrupção de metadados de telemetria.
4. **Captive Dependency:** `OrchestratorHostBuilder` é Singleton mas captura o `IServiceProvider` raiz, impedindo a resolução correta de serviços Scoped (tenant context, HTTP context).

Simultaneamente, a versão **1.9.0** do MAF foi lançada e resolve nativamente o problema de captive dependency via mudanças no `HarnessAgent` DI, promove workflows declarativos a status estável (removendo `[Experimental]`), e introduz output tagging/filtering para multi-agent steps.

**Alternativas consideradas para os problemas de DI:**
- *Opção A (adotada):* Corrigir os anti-patterns manualmente + migrar para 1.9.0 que resolve parte via APIs nativas.
- *Opção B:* Manter 1.6.2 e apenas corrigir os anti-patterns — viável a curto prazo, mas perde benefícios de estabilidade e novas APIs do 1.9.0.
- *Opção C:* Substituir MAF por implementação própria de orquestração — rejeita premissa arquitetural do projeto (ADR-007).

---

## Decisão

Adotaremos a **migração para MAF 1.9.0 com saneamento simultâneo dos anti-patterns de DI**, executada em duas etapas sequenciais:

### Etapa A — Saneamento de DI (pré-upgrade)

1. **Habilitar** `ValidateScopes = true` e `ValidateOnBuild = true` em `Program.cs` para `Development`.
2. **Corrigir SpecialistBindings:** Injetar `IDynamicAgentRepository` em `OrchestratorContextFactory` e popular `SpecialistBindings` com agentes ativos do tenant em runtime.
3. **Registrar como Scoped:** Alterar `OrchestratorHostBuilder` e `OrchestratorContextFactory` de `AddSingleton` para `AddScoped` em `ServiceCollectionExtensions.cs` — **ambos simultaneamente** para evitar nova captive dependency.
4. **Eliminar Double Build:** Refatorar `FrameworkOrchestratorService.ExecuteAsync` para reutilizar o `OrchestratorContext` já construído.
5. **Resolver Sync-over-Async:** Converter inicialização async de `OrchestratorContextFactory` para lazy `ValueTask<T>`.

### Etapa B — Upgrade para MAF 1.9.0

1. Atualizar todos os `<PackageReference Include="Microsoft.Agents.*">` para `1.9.0`.
2. Resolver breaking changes:
   - **1.8.0 Breaking:** Remoção de code-gen em declarative workflows — auditar e remover qualquer geração dinâmica de C#.
   - **1.9.0 Breaking:** `HarnessAgent` requer `ILoggerFactory` + `IServiceProvider` no construtor — atualizar instanciações.
3. Remover decoradores `[Experimental]` de APIs de workflow que foram promovidas.
4. Implementar output tagging e filtering em workflows multi-agente existentes.
5. Executar suite completa de testes (608 testes) para validar zero regressões.

---

## Justificativa

1. **Resolução Nativa de Captive Dependency:** A versão 1.9.0 refatora `HarnessAgent` para receber `IServiceProvider` nativamente, eliminando a necessidade de hacks manuais de DI que atualmente causam captive dependency.

2. **Estabilidade de Workflows:** Workflows declarativos saem de `[Experimental]` no 1.9.0, tornando-os adequados para uso em produção sem risco de breaking changes não anunciados em versões futuras.

3. **Telemetria Correta:** Corrigir `SpecialistBindings` restaura a rastreabilidade do agente executor em cada request — dado crítico para debugging e observabilidade.

4. **Performance:** Eliminar Double Build reduz alocações e tempo de inicialização por request. Eliminar Sync-over-Async previne thread pool starvation sob carga.

5. **Tenant Safety:** Scoped `OrchestratorHostBuilder` garante que o tenant context é corretamente isolado por request, eliminando o risco de cross-tenant data leakage via Singleton captivo.

6. **Output Tagging (1.9.0):** Permite filtrar saídas intermediárias de agentes em pipelines multi-step, reduzindo tokens desnecessários enviados para o LLM orquestrador.

---

## Consequências

### Positivas

* **Zero captive dependency em startup:** `ValidateOnBuild` garante que futuros anti-patterns de DI sejam detectados na construção do contêiner durante o startup.
* **Telemetria restaurada:** `calledBinding` corretamente populado em todos os eventos de roteamento de agentes.
* **Workflows estáveis em produção:** Remoção de `[Experimental]` sinaliza comprometimento do MAF com a API de workflows declarativos.
* **Base sólida para MAF futuro:** Alinhamento com 1.9.0 facilita upgrades incrementais futuros (1.10.x, 2.x).

### Desafios / Pontos de Atenção (Negativas)

* **Breaking change 1.8.0 (code-gen):** Se qualquer workflow usar geração de código C# dinâmico, deve ser refatorado para JSON declarativo antes do upgrade. Requer auditoria cuidadosa.
* **Overhead de Scoped vs. Singleton:** `OrchestratorHostBuilder` Scoped cria uma nova instância por request. Avaliar se há estado pesado que precisa de caching explícito.
* **Risco de regressão em preview packages:** Pacotes `Microsoft.Agents.DurableTask.*` podem estar em versões de preview sem correspondência exata no 1.9.0. Verificar disponibilidade antes do upgrade.
* **Janela de upgrade:** Durante a transição, o sistema deve ser testado em ambiente de staging com carga simulada antes de promover para produção.
