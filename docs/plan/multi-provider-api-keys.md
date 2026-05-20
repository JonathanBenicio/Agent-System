# Roadmap: Multi-Provider API Keys

> **Status documental:** Planejamento Aprovado  
> **Escopo:** Cadastro de múltiplas chaves de API por provedor por tenant, descoberta de modelos isolada por credencial, exibição de sufixo seguro (`LastFour`) e roteamento dinâmico.  
> **Fonte de verdade operacional:** [ADR-020: Multi-Provider API Key Architecture](file:///c:/Users/Jonathan/Documents/Developer/GitHub/Agent-System/docs/architecture/adr/020-multi-provider-api-keys.md)  
> **Gerado em:** 20 de Maio de 2026  
> **Projeto:** AgenticSystem  

---

## Objetivo

Esta iniciativa estende a infraestrutura de LLM do Microsoft Agent Framework (MAF) hospedada no Agentic System para permitir o cadastro, teste, descoberta de modelos e roteamento de múltiplas API Keys para o mesmo provedor de IA (Google Gemini, OpenAI, Anthropic Claude, OpenRouter).

Isso habilita cenários corporativos avançados, permitindo que diferentes chaves com orçamentos e permissões distintas operem de forma isolada, além de garantir que modelos customizados (finetuned) de uma credencial específica sejam devidamente descobertos e utilizados sem poluir o catálogo de chaves não autorizadas.

## Princípios de Implantação

1. **Retrocompatibilidade Absoluta (Fallback Legado):** O sistema deve continuar funcionando normalmente utilizando as chaves de API globais configuradas no `appsettings.json` ou `ConfigEntryEntity` caso nenhuma credencial específica seja cadastrada na nova tabela.
2. **Segurança de Credenciais por Criptografia AES:** Nenhuma chave secreta em texto puro deve ser armazenada no banco de dados ou transmitida pelas APIs HTTP de listagem. A decriptografia ocorre estritamente em memória no momento da chamada de infraestrutura.
3. **Exibição Mascarada com Sufixo Seguro (`LastFour`):** A diferenciação visual das chaves na interface gráfica de configurações deve ocorrer exibindo apenas os últimos 4 caracteres das chaves configuradas (ex: `•••• 3e8g`), salvos em texto plano na criação/edição.
4. **Isolamento de Modelos por Chave:** O catálogo de modelos válidos não será mais mesclado globalmente por provedor, mas isolado e vinculado diretamente à chave de API que realizou a sincronização bem-sucedida.

## Fases e Sequenciamento

| Ordem | Frente/Fase | Motivo do sequenciamento |
|---|---|---|
| 1 | **Fase 1: Persistência & Migração (P0)** | Criação da entidade `LLMProviderApiKeyEntity` e migração PostgreSQL para fundamentar o armazenamento isolado de dados. |
| 2 | **Fase 2: Core Logic & Resolução de Chaves (P1)** | Implementação do serviço de CRUD de chaves, decriptografia e refatoração do `LLMManager.ResolveSelectionAsync` para rotear chamadas. |
| 3 | **Fase 3: APIs & Endpoints do Controller (P1)** | Exposição de rotas REST para gerenciamento de chaves, validação, teste rápido e rotina de descoberta de modelos isolada. |
| 4 | **Fase 4: Frontend UI Premium (P2)** | Construção da interface com visualização de chaves por cartão, sufixo mascarado, indicador de teste rápido, tags de modelos e dropdown de chat integrado. |
| 5 | **Fase X: Validação & Scripts Automatizados (P3)** | Execução do checklist de segurança, lint, compilação de build e testes unitários do backend com 80% de cobertura. |

---

## Detalhamento: Multi-Provider API Keys

### Por que implementar?
Atualmente, a limitação de uma única chave global por provedor impede que clientes empresariais segreguem seus orçamentos de IA por equipes (ex: equipe de marketing usando Gemini Flash vs equipe de dev usando Gemini Pro com chaves separadas). Além disso, modelos privados ou finetunados restritos a uma chave de API específica não podiam ser isolados, causando falhas de roteamento ao tentar usá-los com a chave global.

### Arquitetura-alvo

```
                 LLMRequest / LLMRuntimeContext (com LlmApiKeyId opcional)
                                    ↓
                         LLMManager.ResolveSelectionAsync
                                    ↓
             ┌──────────────────────┴──────────────────────┐
             ▼                                             ▼
     [LlmApiKeyId Presente?]                       [Sem LlmApiKeyId]
             │                                             │
             ├─► Sim ──► Obter LLMProviderApiKey           ├─► Obter IsDefault = true para Provider
             │           pelo ID no Tenant                 │   na tabela de chaves no Tenant
             │                                             │
             └─► Não ──────────────────────────────────────┼─► Se Não Existir → Fallback para
                                                           │   Chave Global / appsettings.json
                                                           ▼
                                               Descriptografar chave (AES)
                                                           ▼
                                               CreateEphemeralProvider
                                                           ▼
                                              BuildChatClient / IChatClient
```

### Componentes propostos

| Componente | Papel |
|---|---|
| `LLMProviderApiKeyEntity` | Entidade do EF Core herdando de `ITenantEntity` mapeando o banco de dados. |
| `ILLMProviderApiKeyService` | Interface contendo regras de negócio para CRUD, testes de chave e rotina de descoberta de modelos. |
| `LLMProviderApiKeyService` | Implementação do serviço com injeção do DbContext, encriptação AES e execução HTTP das descobertas. |
| `LLMManager` | Refatorado para suportar `ResolveSelectionAsync` com carregamento de chaves do banco por ID/Default. |
| `LLMController` | Controlador de API expondo rotas para o frontend. |
| `ProvidersPage.tsx` | Frontend atualizado para exibir cartões de chaves com sufixo mascarado, botões de teste, tags de modelos e painel integrado. |

### Plano por etapas

1. **Setup Inicial & Migrações:**
   - Adicionar `LLMProviderApiKeyEntity` a `PersistenceEntities.cs` e registrar em `AgenticDbContext.cs`.
   - Adicionar o índice composto único composto por `(TenantId, ProviderName, Name)` em `PersistenceConfigurations.cs`.
   - Executar o comando para gerar e aplicar a migração:
     ```bash
     dotnet ef migrations add AddLLMProviderApiKeysTable --project src/AgenticSystem.Infrastructure --startup-project src/AgenticSystem.Api --output-dir Persistence/Migrations
     ```
2. **Implementação da Camada Core:**
   - Criar interface `ILLMProviderApiKeyService.cs` e classe `LLMProviderApiKeyService.cs`.
   - Injetar `IConfigEncryptionService` para encriptar e decriptar os valores das chaves em repouso.
3. **Refatoração do Roteamento (`LLMManager`):**
   - Atualizar a classe `LLMRequest` / `LLMRuntimeContext` para conter o campo opcional `LlmApiKeyId`.
   - Atualizar o método `ResolveSelectionAsync` em `LLMManager.cs` para carregar a chave correta baseada no ID ou no sinalizador `IsDefault` ativo.
   - Refatorar `BuildModelList` para consolidar a união de modelos de todas as chaves ativas do provedor como fallback na listagem geral.
4. **Exposição dos Endpoints:**
   - Criar rotas CRUD e endpoints para sincronização individual em `LLMController.cs`.
5. **Desenvolvimento do Frontend:**
   - Atualizar contratos em `api.ts` e tipos.
   - Desenvolver o layout com cartões premium no `ProvidersPage.tsx` exibindo apenas os 4 últimos caracteres da chave (`LastFour`), tags de modelos e painéis elegantes de controle.

### Critérios de Aceite e SLOs

* [ ] **SLO de Latência de Roteamento:** A resolução e descriptografia da chave de API em memória no `ResolveSelectionAsync` deve adicionar menos de 5ms de latência ao tempo total do request do chat.
* [ ] **Isolamento de Chaves por Tenant:** Garantir através de testes unitários que a tabela de chaves respeita rigorosamente o `TenantId` ativo, lançando exceção ou retornando vazio em caso de tentativas de acesso cruzado.
* [ ] **Ocultação de Texto Puro:** Sob nenhuma hipótese a API de leitura ou logs de auditoria do Serilog devem exibir a chave de API em texto puro. O frontend deve receber estritamente o campo `LastFour` e o valor mascarado `•••• •••• •••• 4x9t`.

### Riscos e Mitigações

| Risco | Mitigação |
|---|---|
| **Vazamento de Chaves nos Logs** | Mapear e higienizar todos os logs no `LLMManager` e `LLMProviderApiKeyService`, garantindo que parâmetros de credenciais sensíveis nunca sejam impressos no Serilog. |
| **CORS na Descoberta do Frontend** | A chamada de descoberta de modelos externa deve ocorrer de forma restrita server-to-server pelo backend proxy utilizando o `HttpClient` do C#, eliminando restrições de CORS impostas por OpenAI/Anthropic para navegadores. |
| Inconsistência de Modelos Associados | Ao desativar ou deletar uma credencial, seus modelos correspondentes devem ser limpos, forçando a interface a re-sincronizar se reativada. |

---

## Respostas do Usuário & Decisões Arquiteturais Definidas

Após consulta socrática com o usuário, as seguintes decisões foram consolidadas para a execução da funcionalidade:

> [!NOTE]
> **D1. Tratamento de Chaves Legadas:**
> **Decisão:** Sim, o fallback retrocompatível para a chave global legada (configurada no `appsettings.json` ou no banco chave-valor sob a chave `llm.providers.{provider}.apiKey`) está ativado. Caso o tenant não possua chaves cadastradas na nova tabela, o sistema fará o fallback automático garantindo funcionamento ininterrupto da aplicação.

> [!NOTE]
> **D2. Seleção de Credencial no Chat:**
> **Decisão:** O usuário final poderá escolher explicitamente a credencial e modelo que deseja utilizar na interface de chat. O seletor de modelos da tela de chat exibirá a dupla `[Modelo, Credencial/Chave]` (ex: `gemini-1.5-pro [Chave Marketing]`, `gemini-1.5-pro [Chave Dev]`), proporcionando total controle granular sobre qual orçamento e credencial utilizar na sessão ativa.

> [!NOTE]
> **D3. Sincronização e Isolamento de Modelos por Chave:**
> **Decisão:** A descoberta de modelos (`DiscoverModels`) será isolada individualmente por chave de API. Cada chave manterá em banco a lista de modelos suportados por ela, sem poluição cruzada com outras chaves do mesmo provedor.

> [!NOTE]
> **D4. Diferenciação Visual por Sufixo Seguro:**
> **Decisão:** A visualização de cartões das chaves na interface gráfica de configurações de provedores exibirá os 4 últimos caracteres da chave (`LastFour`) no formato `•••• •••• •••• 4x9t`, garantindo que o usuário diferencie com facilidade e absoluta segurança suas credenciais sem expor valores confidenciais.
