# Roadmap: Inspeção Automática de Modelos LLM no Login

> **Status documental:** Em Execução  
> **Escopo:** Sincronização assíncrona de chaves por tenant e chaves globais da infraestrutura pós-login, fiação de injeção de dependência e notificações real-time via SignalR.  
> **Fonte de verdade operacional:** [ADR 021: Inspeção Automática de Modelos LLM no Login](file:///c:/Users/Jonathan/Documents/Developer/GitHub/Agent-System/docs/architecture/adr/021-automatic-llm-discovery.md)  
> **Gerado em:** 22 de Maio de 2026  
> **Projeto:** AgenticSystem (Backend .NET 10 + Frontend React 19)  
> **GitHub Issue:** #94  

---

## Objetivo

Esta iniciativa estende o runtime do Agentic System ao automatizar o catálogo dinâmico de modelos de inteligência artificial de cada provedor externo (OpenAI, Gemini, Claude, OpenRouter) configurado. Ao entrar no sistema com sucesso, o backend roda a descoberta de modelos em background e notifica o frontend para atualizar dinamicamente a interface, garantindo que o usuário visualize modelos reais disponíveis de forma transparente e isolada por tenant.

## Princípios de Implantação

1. **Isolamento de Dados por Tenant**: A busca de chaves registradas no banco de dados deve obedecer estritamente ao `TenantId` resolvido a partir do contexto da requisição de login.
2. **Execução Segura em Background**: As inspeções externas que geram alta latência (requisições HTTP) serão disparadas assincronamente em segundo plano via `Task.Run` com `IServiceScopeFactory`, sem atrasar a resposta de autenticação.
3. **Resiliência e Independência**: Falhas em APIs ou timeouts de chaves de um provedor específico não devem crashar o pipeline nem impactar o status de outros provedores.
4. **Acoplamento zero e Notificação Real-Time**: O frontend será notificado via SignalR utilizando eventos direcionados ao grupo do tenant logado, sincronizando as telas em tempo real.

---

## Fases e Sequenciamento

| Ordem | Frente/Fase | Motivo do sequenciamento |
|---|---|---|
| 1 | **Setup de DI e Infraestrutura** | Habilitar a injeção do serviço `ILLMProviderApiKeyService` de forma robusta e segura no container. |
| 2 | **Lógica Core de Inspeção em Background** | Desenvolver o trigger assíncrono pós-login no `AuthController` isolando consultas por tenant. |
| 3 | **Fiação de Mensagens SignalR** | Integrar o `IHubContext` nos hubs SignalR para notificar dinamicamente o frontend sob o evento `LlmCatalogUpdated`. |
| 4 | **Validação e Testes E2E** | Verificar a integridade, compilação de pacotes e simulação de fluxos com Playwright. |

---

## Detalhamento: Multi-Provider LLM Discovery on Login

### Por que implementar?
Atualmente, a sincronização de modelos de uma chave de API exige clique manual ou chamadas individuais na API REST. Automatizar isso na autenticação elimina passos desnecessários do usuário, garantindo integridade imediata do catálogo sem fricção.

### Arquitetura-alvo

```
 [Cliente Frontend] ──(POST /api/auth/login)──> [AuthController]
                                                      │
                       (Retorna 200 OK imediato) <────┤ (Dispara Task.Run)
                                                      ▼
                                           [Background Scope Factory]
                                                      │
                                                      ├─> [Settings/Config (Global API Keys)]
                                                      ├─> [DbKeys (Filtered by TenantId)]
                                                      ▼
                                         [DiscoverModelsAsync (HTTP APIs)]
                                                      │
                                                      ├─> [Update Database (Models List)]
                                                      ├─> [Update LLMManager Catalog]
                                                      ▼
 [Cliente Frontend] <──(SignalR: LlmCatalogUpdated)─── [GatewayHub/ChatHub]
```

### Componentes propostos

| Componente | Papel |
|---|---|
| `ServiceCollectionExtensions` | Registra `ILLMProviderApiKeyService` como Scoped no bootstrap. |
| `AuthController` | Recebe login administrativo, inicia escopo assíncrono e dispara varredura em background filtrada por tenant. |
| `LLMManager` | Sincroniza modelos dinâmicos obtidos via chamadas globais ou locais e expõe o catálogo atualizado em runtime. |
| `GatewayHub` / `ChatHub` | Comunicação real-time que transmite o evento `LlmCatalogUpdated` aos clientes do respectivo Tenant. |

### Plano por etapas

#### Etapa 1: Registro de Dependência (DI)
1. Abrir `src/AgenticSystem.Infrastructure/Extensions/ServiceCollectionExtensions.cs`.
2. Em `AddAgenticLlmServices`, adicionar `services.AddScoped<ILLMProviderApiKeyService, LLMProviderApiKeyService>()` antes de retornar os serviços.

#### Etapa 2: Gatilho e Lógica por Tenant no AuthController
1. Abrir `src/AgenticSystem.Api/Controllers/AuthController.cs`.
2. Injetar `IServiceScopeFactory` e `ILogger<AuthController>` no construtor.
3. Obter o `TenantId` resolvido a partir do `HttpContext` (usando o claim de token ou o header resolvido pelo `TenantMiddleware`).
4. Dentro de `Login`, disparar o bloco assíncrono em background:
   - Extrair o tenant atual para o contexto da thread.
   - Instanciar escopo do provedor.
   - Buscar chaves cadastradas no banco filtrando **estritamente** pelo `TenantId` resolvido.
   - Executar `DiscoverModelsAsync` para as chaves do tenant e chaves globais da infraestrutura se ativas.
   - Atualizar a entidade no banco de dados e o catálogo em tempo de execução (`UpdateProviderAsync`).
   - Salvar as modificações no banco de dados.

#### Etapa 3: Notificação SignalR
1. Injetar `IHubContext<ChatHub>` ou `IHubContext<GatewayHub>` no escopo de background.
2. Disparar `Clients.Group(tenantId).SendAsync("LlmCatalogUpdated")` quando a varredura for concluída com sucesso.

#### Etapa 4: Auditoria e Testes
1. Validar a compilação local do backend.
2. Rodar a bateria de testes com `dotnet test`.

---

## Critérios de Aceite e SLOs

* [ ] **SLO de Latência de Autenticação**: O login HTTP POST `/api/auth/login` deve retornar em < 50ms (inicia a descoberta de forma 100% paralela/assíncrona).
* [ ] **Isolamento de Tenants**: Chaves de API do Tenant A **nunca** devem ser varridas ou expostas durante o login do Tenant B.
* [ ] **SignalR Coverage**: O frontend recebe o payload do evento `LlmCatalogUpdated` contendo o status de sucesso.
* [ ] **Integridade DI**: Iniciar a aplicação local sem erros de runtime na injeção de dependências em controllers.

---

## Riscos e Mitigações

| Risco | Mitigação |
|---|---|
| **Esgotamento de conexões ou timeout de APIs externas** | Timeout explícito de 15 segundos nas chamadas HTTP para os provedores externos em `DiscoverModelsAsync`. |
| **ObjectDisposedException no DbContext em background** | Criação rigorosa de um escopo separado e isolado utilizando `IServiceScopeFactory.CreateScope()` dentro do bloco assíncrono. |
| **Excesso de chamadas HTTP simultâneas** | As políticas de resiliência e circuit breakers implementadas em `LLMManager` e `GovernedChatClient` serão herdadas automaticamente pelas chamadas de infraestrutura. |
