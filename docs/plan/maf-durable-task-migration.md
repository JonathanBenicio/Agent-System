# Roadmap: Migração para DurableTask e Resiliência (Gaps 2 e 3)

> **Status documental:** Planejamento
> **Escopo:** Substituição do `SimpleSessionStoreAdapter` pelo `Microsoft.Agents.AI.DurableTask` com banco de dados PostgreSQL e isolamento Multi-Tenant no `InstanceId`.
> **Fonte de verdade operacional:** [ADR 030](file:///c:/Users/Jonathan/Documents/Developer/GitHub/Agent-System/docs/architecture/adr/030-maf-durable-task-migration.md)
> **Gerado em:** 26 de Maio de 2026
> **Projeto:** AgenticSystem
> **Issue Relacionada:** #108

---

## Objetivo

Esta iniciativa visa estender o runtime atual do Microsoft Agent Framework (MAF) do Agentic System para usar de forma nativa a engine resiliente do **DurableTask**. Isso garante que todas as conversações, orquestrações de múltiplos agentes e execução de workflows dinâmicos orientados a grafos sobrevivam a restarts do servidor, possuam checkpoints automáticos persistidos e tolerem falhas temporárias com políticas robustas de retry.

## Princípios de Implantação

1. **Multi-Tenancy por Design:** Todas as orquestrações duráveis devem ser isoladas por tenant aplicando o prefixo `{TenantId}:` no identificador da instância da orquestração.
2. **Separação de Preocupações de Banco:** As tabelas internas da engine do DurableTask PostgreSQL são inicializadas de forma nativa pela engine no startup, não sendo mapeadas pelo Entity Framework Core (EF Core) para evitar poluição das migrations.
3. **Transparência para a Aplicação:** A transição deve ser transparente para a camada de controle de agentes e hubs do SignalR, mantendo as interfaces de execução idênticas.

## Fases e Sequenciamento

| Ordem | Frente/Fase | Motivo do sequenciamento |
|---|---|---|
| 1 | Configuração de Pacotes & Schema | Instalação das dependências necessárias e provisionamento das tabelas internas do DurableTask no PostgreSQL. |
| 2 | Adaptador de Sessão e Tenant Provider | Implementação do novo `DurableSessionStoreAdapter` com isolamento Multi-Tenant no `InstanceId`. |
| 3 | Compilador Dinâmico Durável | Adaptação do `DynamicMafWorkflowCompiler` para rodar seus nós declarativos como Atividades (Activities) e Sub-orquestrações resilientes. |
| 4 | Testes & Homologação | Execução da suíte completa de testes unitários e de integração para validar a consistência de persistência e retomada de estado. |

---

## Detalhamento: Migração para DurableTask

### Por que implementar?
Reduzir o risco de perda de estado em workflows de longa duração ou sob indisponibilidade de contêineres, agregando resiliência corporativa e tolerância a falhas nativas na plataforma de agentes.

### Componentes propostos

| Componente | Papel |
|---|---|
| `DurableSessionStoreAdapter` | Substitui o `SimpleSessionStoreAdapter` para ler e salvar sessões via DurableTask Client. |
| `TenantIsolatedSessionIdProvider` | Resolve chaves no formato `"{TenantId}:{SessionId}"` de forma thread-safe baseando-se no `ITenantContextAccessor`. |
| `DurableWorkflowCompiler` | Traduz os nós declarativos do grafo (`WorkflowStep`) em chamadas duráveis a Atividades (`DurableTask.Activities`). |

### Plano por etapas

1. **Configuração de Bibliotecas:**
   - Adicionar os pacotes NuGet: `Microsoft.Agents.AI.DurableTask`, `Microsoft.DurableTask.Client`, `Microsoft.DurableTask.Worker` e `DurableTask.PostgreSql`.
2. **Mapeamento de Schema do Banco:**
   - Adicionar rotina de inicialização `await durableTaskBackend.CreateIfNotExistsAsync()` no pipeline do startup (`Program.cs` ou no bootstrap da infraestrutura).
3. **Refatoração dos Adaptadores:**
   - Criar `DurableSessionStoreAdapter.cs` estendendo `AgentSessionStore`.
   - Implementar `DurableWorkflowCompiler.cs` acionando as activities de orquestração.
4. **Substituição de DI:**
   - Atualizar `ServiceCollectionExtensions.cs` removendo referências antigas do `SimpleSessionStoreAdapter` e configurando a injeção nativa de dependências duráveis.

### Critérios de Aceite e SLOs
* [ ] Workflows de longa duração podem ser suspensos e retomados do último checkpoint com sucesso.
* [ ] 100% de isolamento: Um tenant não consegue listar ou interceptar orquestrações de outro tenant.
* [ ] Overhead de latência para carregamento de sessão durável inferior a 15ms.

### Riscos e Mitigações

| Risco | Mitigação |
|---|---|
| Incompatibilidade de histórico de sessões em andamento durante o deploy | Planejar janela de manutenção curta ou implementar fallback temporário de leitura para o formato antigo nas primeiras 24 horas. |
| Concorrência de escrita de histórico no PostgreSQL | Configurar índices compostos adequados e utilizar transações rápidas providas nativamente pelo DurableTask PostgreSQL provider. |
