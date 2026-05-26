# Roadmap: Refatoração Arquitetural e Limpeza Técnica do Backend (.NET 10)

> **Status documental:** Draft
> **Escopo:** Refatoração do MetaAgentOrchestrator, modularização de Program.cs, unificação do Rate Limiting e remoção de scripts órfãos/redundantes na solução AgenticSystem.sln.
> **Fonte de verdade operacional:** [backend-architecture-explained.md](file:///c:/Users/Jonathan/Documents/Developer/GitHub/Agent-System/docs/architecture/backend-architecture-explained.md)
> **Gerado em:** 2026-05-22
> **Projeto:** AgenticSystem.sln

---

## Objetivo

Reduzir o débito técnico acumulado e complexidade desnecessária (Overengineering) no core da aplicação. Esta iniciativa visa modularizar o arquivo principal de entrada (`Program.cs`), quebrar a "Classe Deus" (`MetaAgentOrchestrator`), sincronizar projetos órfãos no compilador e unificar as estratégias de controle de tráfego (Rate Limiting), facilitando a manutenção, escalabilidade e a escrita de testes unitários com alta cobertura.

## Princípios de Implantação

1. **Retrocompatibilidade Restrita:** Os contratos de API pública (rotas, payloads JSON de Request/Response) e barramento de eventos SignalR devem permanecer absolutamente idênticos e inalterados para os clientes externos.
2. **Separação de Preocupações (SoC):** Configurações de infraestrutura, autenticação e regras de negócio/endpoints de chat não devem coexistir em arquivos de bootstrap.
3. **Uso de Padrões Nativos:** Substituir estruturas ad-hoc customizadas (ex: dicionário de rate limiting manual, `ReplaceSingleton` customizado) por abstrações nativas robustas do ASP.NET Core e do framework de DI do .NET.

## Fases e Sequenciamento

| Ordem | Frente/Fase | Motivo do sequenciamento |
|---|---|---|
| 1 | **Sincronização & Limpeza (Fase 1)** | Adicionar o projeto órfão à solução e remover os scripts redundantes para garantir integridade e sincronismo total no build da solução antes de qualquer alteração de lógica. |
| 2 | **Modularização do Bootstrap (Fase 2)** | Mover Auth, Swagger, DTOs e Rate Limiting para classes separadas e extension methods, deixando o `Program.cs` limpo para acomodar as próximas modificações de roteamento. |
| 3 | **Unificação de Rate Limit e ChatController (Fase 3)** | Criar o `ChatController` para substituir os endpoints mínimos inline e adotar o rate limiting nativo, eliminando o dicionário local in-memory. |
| 4 | **Desacoplamento do MetaAgentOrchestrator (Fase 4)** | Quebrar a classe orquestradora introduzindo coordenadores de sessão e comandos de workflow de chat separados, reduzindo as dependências de 14 para 7. |

---

## Detalhamento: Refatoração do Bootstrap & Rate Limiting

### Por que implementar?
Atualmente, o arquivo `Program.cs` com 600 linhas mistura responsabilidades críticas, dificultando auditorias e diagnósticos de erros. Adicionalmente, existem 3 estratégias de rate limit concorrendo simultaneamente, sendo que a checagem manual em memória através de `ConcurrentDictionary` impede o escalonamento horizontal adequado e causa redundância ineficiente.

### Componentes propostos
| Componente | Papel |
|---|---|
| `SecurityServiceCollectionExtensions` | Encapsular registros de esquemas ApiKey, JWT e Supabase, expondo apenas o método `AddApiSecurity`. |
| `RateLimitingServiceCollectionExtensions` | Configurar as políticas nativas de Rate Limiting (`ProtocolEndpoints` e a nova política `TenantChatLimit`). |
| `ChatController` | Controller MVC padrão responsável por lidar com chamadas síncronas e streams de chat (/api/chat). |

### Plano por etapas
1. **Fase 2.1:** Criar classes de extensão `SecurityServiceCollectionExtensions.cs` e `SwaggerServiceCollectionExtensions.cs` e refatorar seus registros correspondentes em `Program.cs`.
2. **Fase 2.2:** Criar `RateLimitingServiceCollectionExtensions.cs` adicionando a política nativa `TenantChatLimit`.
3. **Fase 3.1:** Criar `ChatRequest.cs` (DTOs), `SseWriter.cs` (escrita Server-Sent Events) e o novo `ChatController.cs` anotado com a nova política de Rate Limiting.
4. **Fase 3.2:** Mapear os endpoints para o novo controller em `Program.cs` e remover os mapeamentos inline antigos `/api/chat` e `/api/chat/stream`, deletando a variável em memória `ChatRateLimiter`.

---

## Detalhamento: Desacoplamento do MetaAgentOrchestrator

### Por que implementar?
O `MetaAgentOrchestrator.cs` tornou-se um ponto de alta complexidade técnica e acoplamento devido às suas 14 dependências injetadas. Testar esta classe de forma isolada exige mocks frágeis e extensivos.

### Componentes propostos
| Componente | Papel |
|---|---|
| `ISessionLifecycleCoordinator` | Interface unificada para controlar o início, cota, criação de escopo de runtime, fim de sessão e publicação de eventos. |
| `SessionLifecycleCoordinator` | Implementação concreta que consome `ISessionManager`, `IAgentRuntimeCoordinator`, `ITenantIsolationEnforcer` e `IEventPublisher`. |
| `IChatWorkflowCommandHandler` | Interface para parsing e execução de instruções de controle de workflow digitadas via chat. |
| `ChatWorkflowCommandHandler` | Classe contendo a lógica direta de parsing, listagem e cancelamento interagindo com `IWorkflowEngine` e `IWorkflowStore`. |

### Plano por etapas
1. **Fase 4.1:** Criar as novas abstrações `ISessionLifecycleCoordinator` e `IChatWorkflowCommandHandler` junto às suas respectivas implementações concretas no projeto `AgenticSystem.Core`.
2. **Fase 4.2:** Injetar as duas novas dependências no construtor principal de `MetaAgentOrchestrator.cs` e remover as 6 dependências antigas obsoletas.
3. **Fase 4.3:** Adaptar os métodos `ProcessRequestAsync` e `ProcessRequestCoreAsync` para encaminhar as chamadas aos novos coordenadores modulares.
4. **Fase 4.4:** Corrigir os registros de DI em `ServiceCollectionExtensions.cs` no projeto de infraestrutura para injetar os novos serviços unificados.

## Critérios de Aceite e SLOs
* [ ] Solução compila 100% com sucesso sem erros de sintaxe ou vinculação.
* [ ] Cobertura de testes unitários se mantém rigorosamente acima dos **80%** exigidos pelas diretrizes de qualidade do projeto.
* [ ] Chamadas HTTP síncronas e assíncronas (SSE streams) para `/api/chat` retornam os mesmos payloads de sucesso perfeitamente.
* [ ] Tentativas de flooding de requisições acionam o rate limiting nativo e retornam status HTTP 429 adequadamente.

## Riscos e Mitigações
| Risco | Mitigação |
|---|---|
| Quebra de retrocompatibilidade com o frontend ou ferramentas de E2E devido à mudança para Controller MVC. | Testar exaustivamente a API usando chamadas manuais no Swagger e testes automatizados existentes antes do merge. |
| Perda de propriedades ou ordem crítica de DI na limpeza do `Program.cs`. | Fazer a migração em etapas pequenas incrementais e rodar a suíte completa de testes a cada alteração. |
