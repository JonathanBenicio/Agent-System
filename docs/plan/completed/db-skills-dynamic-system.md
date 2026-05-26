# Roadmap: Implementação do DbAgentSkillsSource e Dynamic Skills System

> **Status documental:** Planejamento futuro / Em Execução
> **Escopo:** Substituição completa do modelo estático de Skills baseadas em arquivos físicos locais por um catálogo 100% dinâmico baseado em banco PostgreSQL, integrado ao MAF 1.6.1 com suporte a CRUD via tela, upload de arquivos, download de templates e assistente conversacional de IA.
> **Fonte de verdade operacional:** [docs/architecture/adr/028-db-skills-dynamic-system.md](file:///c:/Users/Jonathan/Documents/Developer/GitHub/Agent-System/docs/architecture/adr/028-db-skills-dynamic-system.md)
> **Gerado em:** 24 de Maio de 2026
> **Projeto:** AgenticSystem

---

## Objetivo

O objetivo deste roadmap é guiar o desenvolvimento técnico completo da funcionalidade de gestão dinâmica de habilidades de agentes (Skills) no **AgenticSystem**. Esta implementação estende a infraestrutura de agentes nativa do MAF 1.6.1 de forma a persistir **100% das habilidades em banco de dados**, eliminando a necessidade de leitura de arquivos markdown locais no pipeline de execução da LLM. Habilidades criadas ou modificadas pela tela (CRUD) serão aplicadas imediatamente nas conversas com agentes em tempo de execução, garantindo escalabilidade em ambientes cloud serverless sem estado de arquivo local.

---

## Princípios de Implantação

1. **Persistência Exclusiva em Banco de Dados (Sem Provedor Híbrido):** Todas as habilidades do sistema e de usuários residem no PostgreSQL. O carregamento físico de diretórios markdown locais é desativado.
2. **Multi-tenancy Rígido por Banco (ITenantEntity):** Toda skill criada é associada automaticamente ao `TenantId` da sessão e filtrada globalmente nas consultas EF Core.
3. **Bloqueio de Mutação de Skills do Sistema:** Habilidades padrão semeadas automaticamente pelo sistema (ex: `coding-assistant`, `productivity`) possuem a flag `IsSystem = true` e são protegidas contra atualizações e deleções por usuários na API e na UI.
4. **Resiliência a Falhas:** Falhas de conexões do banco de dados na injeção de skills possuem fallback com log estruturado, prevenindo a interrupção completa da thread de chat principal.

---

## Fases e Sequenciamento

| Ordem | Frente/Fase | Motivo do sequenciamento |
|---|---|---|
| 1 | **Modelagem e Persistência (Postgres & Migrations)** | Criar a tabela física `DbSkillEntity` e mapear no `AgenticDbContext` para dar suporte aos dados. |
| 2 | **Provedor de Habilidades (`DbAgentSkillsSource` & Auto-seeding)** | Implementar a leitura do banco de dados convertida ao formato MAF `ISkill` com auto-seeding para novos tenants. |
| 3 | **Controladores & REST API (REST CRUD, Template & Brainstorm)** | Expor na API as rotas de CRUD (`GET/POST/PUT/DELETE /api/agent/skills`), upload de arquivos markdown, download de template e brainstorming por IA. |
| 4 | **Interface do Usuário React (CRUD, Form, Upload & Preview)** | Implementar a tela de cadastro e gestão no frontend com live markdown preview. |
| 5 | **AI Brainstorming Chat (Assistência Inteligente)** | Integrar o chat conversacional de apoio para gerar prompts em Markdown e salvar na base com um clique. |

---

## Detalhamento: Dynamic Skills System

### Por que implementar?
Atualmente, as habilidades do MAF 1.6.1 funcionam sob escaneamento físico local em disco. Isso impossibilita que usuários configurem ou calibrem novas habilidades corporativas dinamicamente via tela, além de ser incompatível com infraestrutura de containers efêmeros em escala horizontal. Centralizar 100% da persistência no banco de dados operacional remove dependências de disco local e simplifica a orquestração do pipeline de inteligência artificial.

### Arquitetura-alvo

```mermaid
flowchart TD
    API["Skills API (CRUD / Upload)"] -->|Write skills| DB[("PostgreSQL (DbSkillEntity)")]
    DB -->|Read skills per Tenant (Auto-Seeded)| DbSource["DbAgentSkillsSource"]
    
    DbSource -->|Load ISkill| Provider["AgentSkillsProvider"]
    
    Provider -->|Provide dynamic messages| MAF["MAF 1.6.1 MessageAIContextProvider"]
    MAF -->|Inject Prompt Instructions| LLM["LLM Chat Execution"]
```

### Componentes propostos

#### Backend (.NET 10)
| Componente | Papel |
|---|---|
| `DbSkillEntity` | Entidade que representa a skill no PostgreSQL (Id, TenantId, Name, Domain, Type, SystemPromptFragment, FewShotExamples, IsSystem, MetadataJson). Implementa `ITenantEntity`. |
| `DbAgentSkillsSource` | Serviço de infraestrutura que busca as habilidades ativas do banco filtradas por Tenant. Executa o auto-seeding caso a base esteja vazia para o Tenant. |
| `AgentSkillsProvider` | Classe de contexto MessageAIContextProvider do MAF 1.6.1 refatorada para ler unicamente de `DbAgentSkillsSource`. |
| `AgentSkillsController` | Endpoint HTTP expondo as rotas de CRUD (`GET/POST/PUT/DELETE /api/agent/skills`), upload (`/upload`), template (`/template`) e assistente de chat (`/brainstorm`). |

#### Frontend (React + TS + Tailwind CSS)
| Componente | Papel |
|---|---|
| `SkillsPage.tsx` | Tela principal que lista as habilidades com badges diferenciadores ("Sistema" vs "Customizada"). |
| `SkillFormDialog.tsx` | Formulário para preenchimento de campos e editor com preview de markdown em tempo real. |
| `SkillUploadZone.tsx` | Área de drag-and-drop de arquivos `.md` com botão de download para o arquivo padrão de template. |
| `SkillAIBrainstormPanel.tsx` | Chat lateral conversacional para gerar e calibrar instruções de prompt antes de salvar a skill. |

---

### Plano por etapas

#### Etapa 1: Persistência & Modelagem (PostgreSQL EF Core)
1. Criar o arquivo de entidade `DbSkillEntity.cs` na pasta `Persistence/Entities/`.
2. Adicionar o `DbSet<DbSkillEntity> AgentSkills` em `AgenticDbContext.cs`.
3. Adicionar uma nova migração do Entity Framework Core usando o projeto correto:
   ```bash
   dotnet ef migrations add AddDbAgentSkills --project src/AgenticSystem.Infrastructure --startup-project src/AgenticSystem.Api --output-dir Persistence/Migrations
   ```
4. Confirmar que a migração executa normalmente na inicialização do serviço.

#### Etapa 2: Implementar o `DbAgentSkillsSource` com Auto-Seeding
1. Criar o arquivo `DbAgentSkillsSource.cs` sob `AgentFramework/` na Infraestrutura.
2. Injetar a fábrica `IDbContextFactory<AgenticDbContext>` para buscar as skills registradas.
3. Se a query por Tenant não retornar registros, disparar o método privado `SeedDefaultSkillsAsync` para provisionar as habilidades padrão (`coding-assistant`, `productivity`, `creative-writing`, `data-analysis`) no banco de dados com `IsSystem = true`.
4. Substituir a referência antiga no `AgentSkillsProvider.cs`. Agora, ele injeta apenas o `DbAgentSkillsSource` e carrega do banco de forma 100% direta e unificada.

#### Etapa 3: Rota da API & Upload/Template
1. Implementar no `AgentSkillsController.cs` as rotas completas de escrita no banco operacional (`POST`, `PUT`, `DELETE`).
2. Adicionar regras rígidas que bloqueiam mutações se o registro possuir `IsSystem == true`.
3. Criar a rota de download `/template` que retorna um arquivo Markdown padrão contendo o esqueleto YAML Frontmatter + Markdown Prompt.
4. Atualizar `/upload` para realizar o parser regex da skill extraída do arquivo `.md` e gravá-la na tabela `DbSkillEntity` como `IsSystem = false`.
5. Criar a rota `/brainstorm` para apoiar a interface de prompt engineering do usuário.

#### Etapa 4: Tela de Gestão e Formulários (UI React)
1. Substituir a listagem em `SkillsPage.tsx` por uma visualização rica em grids, adicionando badges diferenciadores ("Sistema" vs "Customizada").
2. Habilitar o formulário completo de cadastro (ID, Nome, Domínio, Tipo e Editor de Prompt) com live markdown preview.
3. Desenhar a zona de upload aceitando arquivos `.md` e fornecendo o botão para download automático do arquivo de template.
4. Desativar botões de edição e exclusão para cards que possuam a indicação de skill de "Sistema".

#### Etapa 5: AI Brainstorming Canvas
1. Integrar um chat conversacional lateral com o assistente "Skill Forge" para gerar e calibrar instruções de prompt.
2. Ao obter uma boa versão, disponibilizar um botão de confirmação que preenche automaticamente o formulário do cadastro, facilitando o salvamento na base com 1 clique.

---

### Critérios de Aceite e SLOs
* [ ] **SLO de Latência de Chat:** O tempo gasto pela API na consulta e injeção de skills via banco PostgreSQL por requisição deve permanecer menor que **25ms**.
* [ ] **Segurança e Isolamento:** Filtros de Tenant devem garantir 100% de confidencialidade cross-tenant (Testado via Cypress/Playwright).
* [ ] **Read-Only System:** Tentativas de requisição HTTP DELETE ou PUT contra skills com `IsSystem = true` devem retornar `403 Forbidden` do backend.

---

### Riscos e Mitigações

| Risco | Mitigação |
|---|---|
| **Erros de Conectividade do Banco:** Falhas temporárias no PostgreSQL quebram o chat. | Implementar resiliência com Polly e cache em memória (MemoryCache) com tempo de expiração curto (e.g. 5 minutos) das skills ativas por Tenant. |
| **Colisão de IDs:** O usuário tenta criar uma skill customizada com o ID `coding-assistant`. | A API validará que IDs duplicados para o mesmo tenant retornem erro `409 Conflict`. |
| **Markdown Frontmatter Inválido no Upload:** Arquivos mal formatados quebram o parser. | O validador do parser regex possui tratamento estrito de exceções (`try-catch`), com mensagens específicas apontando as linhas com erros de parsing estrutural para a UI. |
