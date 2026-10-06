# Roadmap: Auto-Bootstrap e Remoção do Tenant Default

> **Status documental:** SUPERSEDED — proposta de 2026-05 substituída pela decisão atual em [ADR-026](../../architecture/adr/026-auto-bootstrap-remove-default-tenant.md), implementação de bootstrap em [#99](https://github.com/JonathanBenicio/Agent-System/issues/99) e remediação de escopo tipado em [#97](https://github.com/JonathanBenicio/Agent-System/issues/97) / [plano atual](../tenant-system-scope-remediation.md). As caixas abaixo são históricas e não comprovam conclusão.
> **Escopo:** Substituição do uso de "default" tenant em toda a aplicação por uma gestão estrita de Tenants baseada em banco de dados e auto-provisionamento no startup.
> **Fonte de verdade operacional:** Código-fonte atual e documentação do sistema.
> **Gerado em:** 2026-05-23
> **Projeto:** AgenticSystem (Backend .NET 10 & Frontend React + Vite)
> **Epic Tracker:** #100

---

## 1. Overview & Contexto

O AgenticSystem atualmente depende de uma constante estática de tenant `"default"` (`Tenant.DefaultTenantId = "default"`) como fallback de segurança e identidade em todo o ecossistema (Core, Infrastructure e Api). Além disso, a autenticação por API Key (`ApiKeyAuthenticationHandler`) valida as requisições comparando strings estáticas com chaves fixas lidas diretamente de `appsettings.json` (`AdminApiKey`).

### O Problema
1. **Risco de Vazamento/Poluição de Dados:** O fallback automático para "default" mascara falhas na passagem de escopo de tenant, misturando dados entre diferentes clientes num cenário SaaS real.
2. **Insegurança nas API Keys:** Chaves administrativas estáticas em arquivos de configuração impedem a rotação dinâmica de chaves por Tenant, revogação instantânea e auditoria.
3. **Complexidade no Startup:** Falta de um mecanismo resiliente de auto-provisionamento para novos deploys que garanta o bootstrap seguro do primeiro tenant e de suas credenciais de administração.

### Objetivos do Plano
- **Expurgar "default":** Remover toda a infraestrutura baseada no ID `"default"`, impondo uma verificação estrita em tempo de compilação e execução.
- **API Keys Dinâmicas no Banco:** Introduzir a tabela `access_api_keys` associada a `TenantId`, armazenando apenas hashes SHA-256 e protegendo o pipeline contra timing attacks.
- **Auto-Bootstrap Seguro (Zero-Lockout):** Implementar um `SystemBootstrapService` executado imediatamente após as migrações automáticas de startup, provisionando o tenant administrador (`admin`) e sua primeira chave a partir das credenciais existentes.

---

## 2. Tipo de Projeto & Escopo Tecnológico

- **Project Type:** **BACKEND** (com implicações no fluxo de autenticação e comunicação do Frontend)
- **Primary Agent:** `@[backend-specialist]` e `@[dotnet-expert]`
- **Frameworks:** .NET 10, Entity Framework Core, Serilog, SignalR

---

## 3. Tech Stack & Análise de Trade-offs

Analisamos duas estratégias para a gestão de chaves de API:

| Critério | Opção A: Hash SHA-256 no BD (Recomendada) | Opção B: Criptografia Simétrica (AES-256) |
|---|---|---|
| **Segurança contra vazamento de BD** | **Excelente:** Chaves puras nunca tocam o banco. Hashing unilateral protege credenciais. | **Moderada:** Se a chave de criptografia do servidor for exposta, todas as API keys são comprometidas. |
| **Desempenho da Autenticação** | **Excepcional ($O(1)$):** Hashing rápido em memória e busca direta por índice único no banco de dados. | **Moderado:** Exige descriptografar as chaves ou recuperar da memória e descriptografar para comparação. |
| **Timing Attacks** | **Zero:** A busca direta de hash exato no banco impede ataques baseados em tempo de comparação. | **Risco:** Comparação em memória precisa de operações de tempo fixo dedicadas. |
| **Recuperação de Chave** | **Impossível:** Uma vez gerada, a chave pura deve ser guardada pelo cliente. O sistema nunca consegue revelá-la novamente. | **Possível:** Permite que o administrador descriptografe e exiba a chave original no painel. |

**Decisão Arquitetural:** **Opção A (Hash SHA-256)**. Para garantir segurança máxima em nível de produção (SaaS), as chaves de API serão hashed via SHA-256 e apenas o hash será armazenado. A chave em texto puro será exibida **exclusivamente uma única vez** no momento de sua criação.

---

## 4. Matriz de Riscos & Mitigações

| Risco | Impacto | Probabilidade | Mitigação Proposta |
|---|---|---|---|
| **Lock-out do Sistema no Startup** | Altíssimo | Baixa | O `SystemBootstrapService` lerá a configuração legada `AdminApiKey` do `appsettings.json` de forma transitória. Se o banco estiver vazio, provisiona o tenant `admin` e grava o hash da chave legada. A autenticação continua idêntica no 1º boot. |
| **Quebra de Hubs/Jobs Background** | Alto | Média | Auditoria detalhada e substituição de fallbacks por injeção explícita de `TenantContext` scoped usando `ITenantContextAccessor.BeginScope(new TenantContext { TenantId = ... })`. |
| **Concorrência no Startup de Instâncias** | Médio | Baixa | O `SystemBootstrapService` usará bloqueios explícitos ou capturará exceções de violação de chave primária (`DbUpdateException`) para garantir concorrência segura em deploys clusterizados (multi-pods). |
| **Quebra de Testes Unitários/E2E** | Médio | Alta | Ajustar as classes base de testes unitários e de integração (`tests/frontend` e `AgenticSystem.Tests`) para incluir chaves válidas e tenants correspondentes nas sementes de banco (seeds) de testes. |

---

## 5. Estrutura de Arquivos

Diretórios e arquivos criados/modificados no escopo da operação:

```
src/
├── AgenticSystem.Core/
│   ├── Models/
│   │   ├── Tenant.cs (Modificar: Remover DefaultTenantId)
│   │   └── TenantContext.cs (Modificar: Tornar TenantId estrito)
│   ├── Services/
│   │   ├── InMemoryTenantStore.cs (Modificar: Remover seeding de "default")
│   │   └── TenantResolver.cs (Modificar: Ajustar resolução)
│   └── Interfaces/
│       └── ISystemBootstrapService.cs [NEW]
├── AgenticSystem.Infrastructure/
│   ├── Persistence/
│   │   ├── Entities/
│   │   │   └── AccessApiKeyEntity.cs [NEW]
│   │   ├── Configurations/
│   │   │   └── AccessApiKeyConfiguration.cs [NEW]
│   │   ├── EfTenantStore.cs (Modificar: Remover EnsureDefaultTenantAsync)
│   │   └── AgenticDbContext.cs (Modificar: Remover lógica de fallback em OnBeforeSaving)
│   ├── Services/
│   │   └── SystemBootstrapService.cs [NEW]
│   └── Extensions/
│       └── ServiceCollectionExtensions.cs (Modificar: Registrar novo HostedService)
└── AgenticSystem.Api/
    ├── Auth/
    │   └── ApiKeyAuthenticationHandler.cs (Modificar: Consultar hash no banco de dados)
    ├── Controllers/
    │   ├── AuthController.cs (Modificar: Login via validação de banco)
    │   └── OpenAIChatCompletionController.cs (Modificar: Validar bearer token contra DB)
    └── Program.cs (Modificar: Invocar bootstrap service de forma controlada)
```

---

## 6. Detalhamento Técnico das Tarefas (Tracker: Epic #100)

### Fase 1: Infraestrutura de Banco e Bootstrap (Zero-Downtime)

#### 📝 Tarefa 1.1: Criação da Entidade de Chaves de Acesso
- **Agent:** `@[database-architect]`
- **Skills:** `database-design`, `clean-code`
- **Arquivos:**
  - `src/AgenticSystem.Infrastructure/Persistence/Entities/AccessApiKeyEntity.cs` [NEW]
  - `src/AgenticSystem.Infrastructure/Persistence/Configurations/AccessApiKeyConfiguration.cs` [NEW]
- **Descrição:** Criar a tabela `access_api_keys` mapeada por `AccessApiKeyEntity`:
  ```csharp
  public class AccessApiKeyEntity : ITenantEntity
  {
      public Guid Id { get; set; }
      public string TenantId { get; set; } = null!;
      public string KeyHash { get; set; } = null!; // SHA-256
      public string Name { get; set; } = null!;
      public bool IsEnabled { get; set; }
      public DateTime CreatedAt { get; set; }
      public DateTime? LastUsedAt { get; set; }
  }
  ```
  Adicionar índice único em `KeyHash` e `TenantId` no arquivo de configuração do EF.
- **INPUT:** Necessidade de guardar hashes de chaves.
- **OUTPUT:** Entidade e configuração EF mapeadas.
- **VERIFY:** `dotnet build` sem erros.

#### 📝 Tarefa 1.2: Geração e Aplicação da Migração EF Core
- **Agent:** `@[database-architect]`
- **Skills:** `powershell-windows`, `dotnet-best-practices`
- **Comando de Migração:**
  ```powershell
  dotnet ef migrations add AddAccessApiKeys --project src/AgenticSystem.Infrastructure --startup-project src/AgenticSystem.Api --output-dir Persistence/Migrations
  ```
- **INPUT:** Nova entidade no DbContext.
- **OUTPUT:** Arquivo de migração gerado na pasta `Persistence/Migrations`.
- **VERIFY:** Executar migração localmente e inspecionar a tabela no PostgreSQL.

#### 📝 Tarefa 1.3: Implementação do SystemBootstrapService
- **Agent:** `@[backend-specialist]`
- **Skills:** `dotnet-best-practices`, `clean-code`
- **Arquivos:**
  - `src/AgenticSystem.Core/Interfaces/ISystemBootstrapService.cs` [NEW]
  - `src/AgenticSystem.Infrastructure/Services/SystemBootstrapService.cs` [NEW]
- **Descrição:** Implementar o serviço de inicialização. Ao rodar:
  1. Verifica se a tabela `Tenants` está vazia.
  2. Se vazia, cria o tenant `admin` com limites `ProTier()`.
  3. Lê `AgenticSystem:AdminApiKey` da configuração. Se presente, gera o hash SHA-256 e insere na tabela `access_api_keys` vinculada ao tenant `admin`.
  4. Executa de forma resiliente capturando exceções de violação de chave exclusiva (caso outro nó suba em paralelo).
- **INPUT:** Configuração administrativa legada e banco vazio.
- **OUTPUT:** Tenant `admin` e API Key persistidos no primeiro boot.
- **VERIFY:** Inicializar a aplicação com banco limpo e validar se o tenant `admin` e o hash da chave aparecem no banco.

---

### Fase 2: Refatoração da Autenticação e Expurgo do Fallback

#### 📝 Tarefa 2.1: Refatoração do ApiKeyAuthenticationHandler e AuthController
- **Agent:** `@[dotnet-expert]`
- **Skills:** `dotnet-best-practices`, `clean-code`, `vulnerability-scanner`
- **Arquivos:**
  - `src/AgenticSystem.Api/Auth/ApiKeyAuthenticationHandler.cs` (Modificar)
  - `src/AgenticSystem.Api/Controllers/AuthController.cs` (Modificar)
- **Descrição:**
  - No `ApiKeyAuthenticationHandler`, extrair a chave recebida no request, calcular o hash SHA-256 e buscar na tabela `access_api_keys`. Se encontrada e ativa, autenticar a requisição injetando o `tenant_id` real correspondente na Claim do principal.
  - No `AuthController.Login`, realizar a mesma lógica de hashing e busca no banco para autenticar a rota de login administrativa.
- **INPUT:** Chave enviada via Cookie/Header.
- **OUTPUT:** Requisição autenticada com tenant dinâmico recuperado diretamente do banco.
- **VERIFY:** Chamadas com chaves válidas devem retornar HTTP 200/Ok e carregar as permissões; chaves inválidas devem retornar HTTP 401/403.

#### 📝 Tarefa 2.2: Expurgo dos Fallbacks de "default" no Core e Infrastructure
- **Agent:** `@[dotnet-expert]`
- **Skills:** `clean-code`
- **Arquivos:**
  - `src/AgenticSystem.Core/Models/Tenant.cs`
  - `src/AgenticSystem.Core/Models/TenantContext.cs`
  - `src/AgenticSystem.Core/Models/UserContext.cs`
  - `src/AgenticSystem.Infrastructure/Persistence/EfTenantStore.cs`
  - `src/AgenticSystem.Infrastructure/Persistence/AgenticDbContext.cs`
  - Ocorrências identificadas de fallback (via compiler warnings)
- **Descrição:**
  - Excluir a constante `Tenant.DefaultTenantId`.
  - Configurar `TenantContext.TenantId` para não possuir valor default (forçando inicialização por middleware ou exceção em tempo de execução se acessado indevidamente).
  - Remover o método `EnsureDefaultTenantAsync` em `EfTenantStore` e o seeding no `InMemoryTenantStore`.
  - Remover a lógica de fallback no `OnBeforeSaving` do `AgenticDbContext`.
- **INPUT:** Constante `DefaultTenantId` em uso.
- **OUTPUT:** Falta de qualquer referência hardcoded a "default" e falha de compilação/execução caso haja fallback sem injeção explícita de tenant.
- **VERIFY:** Compilar todo o projeto via `dotnet build`.

---

### Fase 3: Validação do Ecossistema e Testes

#### 📝 Tarefa 3.1: Ajuste nos Hubs e SignalR Filters
- **Agent:** `@[dotnet-expert]`
- **Skills:** `dotnet-best-practices`
- **Arquivos:**
  - `src/AgenticSystem.Api/SignalR/TenantHubFilter.cs`
  - `src/AgenticSystem.Api/Hubs/ChatHub.cs`
- **Descrição:** Garantir que o filtro do Hub SignalR resolva e injete o TenantContext baseando-se estritamente no token de conexão ou nos parâmetros autenticados, nunca usando "default".
- **INPUT:** Conexão SignalR do cliente.
- **OUTPUT:** Conexão atribuída a grupo e escopo corretos baseados em Tenant.
- **VERIFY:** Conectar no Hub e enviar mensagens, validando o isolamento de canais de chat.

#### 📝 Tarefa 3.2: Correção da Suite de Testes (Unitários e E2E)
- **Agent:** `@[test-engineer]`
- **Skills:** `testing-patterns`, `clean-code`
- **Arquivos:**
  - `src/AgenticSystem.Tests/` (Múltiplos arquivos)
  - `tests/frontend/` (Suites Playwright/Cypress)
- **Descrição:** Atualizar todas as fixtures e sementes de banco (seeds) de testes para criar e validar dados vinculados ao tenant `admin` ou novos tenants dinâmicos criados durante os testes.
- **INPUT:** Testes quebrando devido à ausência do tenant "default".
- **OUTPUT:** Suites de testes completamente verdes.
- **VERIFY:** Executar `dotnet test` e `npm run cy:run` ou Playwright suites.

---

## 7. Estratégia de Rollback

Se um problema grave for identificado em produção após a remoção dos fallbacks:
1. **Passo 1:** Desfazer o commit correspondente da remoção de fallbacks no Git.
2. **Passo 2:** Manter a tabela `access_api_keys` populada com a chave legada. A persistência continuará válida, pois a migração é aditiva.
3. **Passo 3:** Caso de falha catastrófica de migração no banco de dados PostgreSQL, restaurar o snapshot do banco anterior ao deploy.

---

## 8. Fase X: Validação Final e Definição de Concluído (Done)

Para considerarmos a Epic concluída com êxito, os seguintes passos automatizados de qualidade devem rodar com sucesso:

```bash
# 1. Executar testes de unidade do backend (Mínimo de 80% de cobertura)
dotnet test --configuration Release

# 2. Executar validação de linter e compilação do Frontend
cd frontend
npm run lint && npm run build
```

Adicionalmente, os critérios manuais abaixo serão auditados:
- [ ] Nenhum arquivo no core/api contém referências ao tenant `"default"`.
- [ ] O banco de dados PostgreSQL reflete a tabela `access_api_keys` com índice de chave única na coluna hash.
- [ ] Fluxo de login e reconexão SignalR funcionando perfeitamente de forma isolada por tenant.

## ✅ PHASE X COMPLETE
- Lint: [ ] Pending
- Security: [ ] Pending
- Build: [ ] Pending
- Date: [Date]
