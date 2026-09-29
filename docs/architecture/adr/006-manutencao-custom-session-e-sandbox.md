# ADR 006: Manutenção de Componentes Customizados (Sessão e Sandbox) frente ao MAF 1.6.2

## Status
Aceito

## Contexto
Durante o projeto de alinhamento com a arquitetura nativa do **Microsoft Agent Framework (MAF) 1.6.2** (detalhado no `maf-complete-migration-plan.md`), duas migrações foram categorizadas como "Futuro" ou "Lab Feature":
1. **Migração para DurableTask (Gap 2):** Substituir a persistência customizada (`SimpleSessionStoreAdapter`) pela resiliência nativa de execução do MAF usando o pacote estendido `Microsoft.Agents.AI.DurableTask`.
2. **Hyperlight WASM Sandboxing (Gap 4):** Adotar micro-VMs do Hyperlight para isolar o código gerado pelo agente na ferramenta de execução.

## Decisão

### 1. DurableTask: Adiamento Estratégico
Decidimos **manter o `SimpleSessionStoreAdapter`** atual, apoiado pelo PostgreSQL, como a solução de persistência primária da sessão.
**Motivos:**
- O pacote nativo do DurableTask encontra-se apenas em versão *preview* (`1.6.2-preview.260521.1`) e requer configuração complexa de provedores de estado adicionais.
- A solução existente via `AgenticDbContext` cumpre plenamente o requisito de isolamento Multi-Tenant do projeto (`X-Tenant-Id`).
- Os workflows em execução não possuem ciclos de parada prolongados do tipo *Human-in-the-Loop* extensivo que superem os tempos de timeout de requests padrão, tornando a orquestração em processo (`InProcessExecution`) perfeitamente adequada para a volumetria atual.

### 2. Hyperlight WASM: Manutenção da Simulação (Stub)
A execução de código sandbox (`HyperlightSandboxedExecutor`) será **mantida como uma simulação / stub**, registrando *warnings* nos logs.
**Motivos:**
- A decisão original foi registrada em 2026-05-25. Em 2026-09-29 existem pacotes .NET públicos em *preview*: `Microsoft.Agents.AI.Hyperlight` 1.21.0-preview.260911.1 e `Hyperlight.HyperlightSandbox.Api` 0.7.0. A afirmação de indisponibilidade pública ficou desatualizada. A existência dos pacotes não prova estabilidade, compatibilidade com o MAF 1.22 deste produto, suporte às plataformas alvo nem uso de uma sandbox real neste repositório.
- O runtime do produto continua simulado em `HyperlightSandboxedExecutor`; esta ADR mantém a decisão de não declarar isolamento real até compatibilidade, threat model, capabilities e lifecycle do guest serem verificados.
- Até que haja disponibilidade geral (GA), os comandos de linha ou código gerado continuarão bloqueados e apenas testados em cenários de laboratório controlados para proteger a infraestrutura do host.

Fontes atuais: [NuGet Microsoft.Agents.AI.Hyperlight](https://www.nuget.org/packages/Microsoft.Agents.AI.Hyperlight/) · [README .NET do Agent Framework](https://github.com/microsoft/agent-framework/blob/main/dotnet/src/Microsoft.Agents.AI.Hyperlight/README.md) · [NuGet Hyperlight.HyperlightSandbox.Api](https://www.nuget.org/packages/Hyperlight.HyperlightSandbox.Api/). A especificação e as lacunas de #105 estão no [registro das issues abertas](../../plan/open-issues-specification-audit-2026-09-29.md#issue-105).

## Consequências
- **Consistência Imediata:** Não há novos pontos de falha decorrentes do estado Preview. A performance observada e o tempo de resposta se mantêm intactos.
- **Dívida Técnica Deliberada:** Um novo ciclo de atualização arquitetural precisará ser agendado (possivelmente para .NET 11 ou MAF 2.0) quando essas features nativas amadurecerem e entrarem em General Availability (GA).
