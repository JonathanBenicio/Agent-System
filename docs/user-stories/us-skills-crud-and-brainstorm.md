# US-012: Gestão Dinâmica de Skills via Tela e Brainstorming assistido por IA

**Épico:** Orquestração Multi-Agent & Habilidades Dinâmicas  
**Prioridade:** Alta  
**Estimativa (Story Points):** 8

## Descrição

**Como um** administrador ou usuário da plataforma AgenticSystem,  
**Eu quero** cadastrar, listar, editar e excluir Skills (habilidades de prompt e instruções) diretamente pela interface web (formulário e drag-and-drop de arquivos) ou através de um chat conversacional assistido por IA,  
**Para que** eu possa calibrar e estender os comportamentos dos meus agentes em tempo real no banco de dados, sem qualquer dependência do sistema de arquivos físico local.

## Regras de Negócio e Contexto

* **Persistência 100% no PostgreSQL:** Todas as habilidades (estáticas/sistema e customizadas/usuário) devem ser armazenadas de forma unificada no banco PostgreSQL. Nenhuma leitura física de arquivos markdown locales é executada para compilar prompts de agentes no MAF.
* **Multi-tenancy Rígido:** Toda skill no banco deve ser isolada sob o `TenantId` do usuário logado. Nenhuma skill de um tenant pode ser visualizada, editada ou executada por outro tenant.
* **Habilidades do Sistema (System Skills):** Habilidades padrão fornecidas pela plataforma (ex: `coding-assistant`, `productivity`) são salvas na tabela com a flag `IsSystem = true`. Elas são de leitura estrita (read-only) na API e na UI para usuários comuns, impedindo deleções ou modificações acidentais, mas permitindo que sejam visualizadas e vinculadas a agentes.
* **Auto-Seeding:** Quando a lista de habilidades de um tenant for consultada pela primeira vez e estiver vazia, o sistema deve copiar e auto-semear as habilidades padrão de sistema para a base de dados sob o `TenantId` correspondente.
* **Prevenção de Colisões:** Ao criar uma nova skill customizada, a API deve rejeitar IDs que coincidam com IDs de skills do sistema para o mesmo tenant.
* **Importação/Exportação:** O template baixado na tela deve corresponder exatamente ao layout de arquivo Markdown esperado pela API (YAML Frontmatter + Prompt Fragment no corpo). O upload de arquivos Markdown converte e salva a skill diretamente na tabela `DbSkillEntity`.

## Critérios de Aceite (DoD)

- [ ] **Critério 1 (CRUD API):** A API expõe endpoints CRUD funcionais de skills (`GET`, `POST`, `PUT`, `DELETE` em `/api/agent/skills`) que persistem e alteram os registros exclusivamente no banco PostgreSQL sob o contexto do Tenant.
- [ ] **Critério 2 (Parsing de Markdown):** O endpoint `/api/agent/skills/upload` aceita arquivos Markdown `.md` válidos com frontmatter YAML, converte metadados (`id`, `name`, `domain`, `type`) e o prompt do corpo, e grava diretamente na tabela `DbSkillEntity` como `IsSystem = false`.
- [ ] **Critério 3 (Template de Download):** O endpoint `/api/agent/skills/template` fornece o download de um arquivo Markdown padrão contendo a estrutura explicativa detalhada de frontmatter de skill.
- [ ] **Critério 4 (Tela de Formulário & Preview):** O frontend React disponibiliza uma tela com formulário completo que inclui validações e um painel de live markdown preview do prompt.
- [ ] **Critério 5 (AI Chat Brainstorming):** Um painel lateral conversacional permite dialogar com a IA para estruturar o prompt da skill e apresenta um botão "Salvar como Skill" ao finalizar a geração.
- [ ] **Critério 6 (Integração MAF Nativa):** Ao enviar mensagens para o agente, o `AgentSkillsProvider` nativo do MAF 1.6.1 realiza a consulta de skills exclusivamente do banco PostgreSQL por meio do `DbAgentSkillsSource` e injeta corretamente o contexto no prompt enviado à LLM.

## Dependências Técnicas

* [x] ADR-027 (Migração para o MAF 1.6.1)
* [x] ADR 028 (Implementação do DbAgentSkillsSource e CRUD via Tela)
* [ ] Criação da tabela `DbSkillEntity` na base PostgreSQL via EF Core Migrations.
* [ ] Implementação do serviço `DbAgentSkillsSource` na camada de Infraestrutura do .NET 10.
