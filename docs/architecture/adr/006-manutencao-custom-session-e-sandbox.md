# ADR 006: Manutenção de Componentes Customizados (Sessão e Sandbox) frente ao MAF 1.6.2

## Status
Aceito para o adapter de sessão; decisão da sandbox parcialmente substituída em 2026-09-29.

## Contexto
Durante o projeto de alinhamento com a arquitetura nativa do **Microsoft Agent Framework (MAF) 1.6.2** (detalhado no `maf-complete-migration-plan.md`), duas migrações foram categorizadas como "Futuro" ou "Lab Feature":
1. **Migração para DurableTask (Gap 2):** Substituir a persistência customizada (`SimpleSessionStoreAdapter`) pela resiliência nativa de execução do MAF usando o pacote estendido `Microsoft.Agents.AI.DurableTask`.
2. **Hyperlight WASM Sandboxing (Gap 4):** Adotar micro-VMs do Hyperlight para isolar o código gerado pelo agente na ferramenta de execução.

## Decisão

### 1. DurableTask: Adiamento Estratégico
Decidimos **não usar DurableTask como scheduler dos grafos dinâmicos do produto**. As sessões MAF permanecem no store PostgreSQL do produto; o scheduler `IWorkflowEngine`/`IWorkflowStore` da aplicação executa definições dinâmicas de tenant, e MAF executa os agentes.
**Motivos:**
- O worker DurableTask resolve workflows de registry conhecido no startup; isso não executa grafos arbitrários que um tenant salva depois.
- A decisão de engine dinâmico está detalhada em [ADR-036](036-maf-122-protocols-and-gateway.md) e [ADR-038](038-dynamic-supervisor-orchestrator.md); restart/external-effect guarantees são registradas nas evidências atuais, sem promessa de exactly-once.

### 2. Hyperlight WASM: integrar Preview sob controle global
**Decisão de produto aprovada em 2026-09-29:** integrar `Microsoft.Agents.AI.Hyperlight` em versão *preview* atrás de uma flag global de configuração, desligada por padrão e habilitável somente em laboratório. A adoção exige testes de segurança antes de ligar a capacidade no ambiente de laboratório. O pacote existe publicamente em versão Preview; isso não equivale a GA nem prova isolamento no Agent-System.

**Requisitos de segurança para a integração:**
- Desligada ou indisponível: a tool informa que a capacidade está desativada; não retorna saída fixa como se código tivesse sido executado.
- Sem filesystem e rede por padrão; qualquer capability deve ser explicitamente configurada, limitada e testada.
- Definir timeout, limite de memória/CPU, cancelamento, lifecycle/teardown, isolamento por tenant e comportamento de erro.
- Não expor secrets, contexto de outros tenants ou objetos de infraestrutura ao guest.
- Manter o rótulo Preview/Lab e não declarar pronta para produção até testes de segurança e compatibilidade passarem.

**Motivos e evidência:**
- A decisão original foi registrada em 2026-05-25. Em 2026-09-29 existem pacotes .NET públicos em *preview*: `Microsoft.Agents.AI.Hyperlight` 1.21.0-preview.260911.1 e `Hyperlight.HyperlightSandbox.Api` 0.7.0. A afirmação de indisponibilidade pública ficou desatualizada. A existência dos pacotes não prova estabilidade, compatibilidade com o MAF 1.22 deste produto, suporte às plataformas alvo nem uso de uma sandbox real neste repositório.
- O runtime atual ainda é simulado em `HyperlightSandboxedExecutor`; a decisão aprova a integração Preview futura, não certifica a implementação atual.

Fontes atuais: [NuGet Microsoft.Agents.AI.Hyperlight](https://www.nuget.org/packages/Microsoft.Agents.AI.Hyperlight/) · [README .NET do Agent Framework](https://github.com/microsoft/agent-framework/blob/main/dotnet/src/Microsoft.Agents.AI.Hyperlight/README.md) · [NuGet Hyperlight.HyperlightSandbox.Api](https://www.nuget.org/packages/Hyperlight.HyperlightSandbox.Api/). Implementação e validação continuam pendentes em [#105](../../plan/open-issues-specification-audit-2026-09-29.md#issue-105).

## Consequências
- **Sandbox Preview:** os pacotes trazem risco de breaking changes e condições de runtime guest/plataforma; o flag fica off por padrão e somente laboratório pode habilitar até os testes de segurança aprovados.
- **Sem sucesso simulado:** retorno hardcoded não pode ser interpretado como execução de código; quando desabilitado/indisponível, a chamada deve falhar com estado explícito.
- **Workflows:** a engine da aplicação permanece a fonte de scheduling para grafos dinâmicos; limites de retry/idempotência, cancelamento de passos longos e efeitos externos são requisitos separados de implementação.
