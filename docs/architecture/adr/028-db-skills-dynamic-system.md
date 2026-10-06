# ADR 028: Implementação do DbAgentSkillsSource e CRUD de Skills via Tela

**Status:** Aprovado  
**Data:** 24 de Maio de 2026  
**Autor(es):** Principal .NET Architect & Frontend Specialist (Antigravity Swarm)

---

## Contexto

No **AgenticSystem**, as habilidades (skills) dos agentes são essenciais para modularizar comportamentos específicos (ex: prompts de programação, regras de produtividade). A transição para o **Microsoft Agent Framework (MAF) 1.6.1** propôs a padronização do catálogo de skills por meio de arquivos Markdown físicos gerenciados pelo `AgentFileSkillsSource`.

Embora a abordagem baseada em arquivos (Docs-as-Code) funcione para versionamento sob controle do time de desenvolvimento, ela cria um grande gargalo para a operação da plataforma e para a sua principal finalidade: **permitir que usuários finais e administradores criem, gerenciem, testem e calibrem novas skills dinamicamente via tela (UI) através de endpoints de API**.

Ambientes de nuvem modernos (como Azure Container Apps ou AWS ECS) operam sob instâncias horizontais escaladas com discos efêmeros e de leitura estrita. Gravar arquivos `.md` fisicamente nesses locais em tempo de execução é insustentável. Além disso, ter parte das skills em disco e parte no banco adiciona complexidade técnica desnecessária (overhead de sincronização, risco de colisão de ID, e duplicação de lógicas de carregamento e ordenação).

## Decisão

1. **Persistência 100% em Banco de Dados (Sem Provedor Híbrido):** Desativar e remover completamente o `AgentFileSkillsSource` e o escaneamento físico de arquivos Markdown locales no pipeline do MessageAIContextProvider do MAF 1.6.1. Todas as habilidades (Skills) passam a ser armazenadas e gerenciadas no banco de dados operacional PostgreSQL.
2. **Criação da Entidade `DbSkillEntity` com Flags de Controle:** Mapear a tabela física no `AgenticDbContext` herdando as garantias de multi-tenancy estrito (`ITenantEntity`) usando a coluna `TenantId`. Adicionar a flag booleana `IsSystem` para diferenciar skills nativas (semeadas) de skills customizadas.
3. **Mapeamento do `DbAgentSkillsSource`:** Implementar o provedor de infraestrutura C# que lê as skills ativas diretamente do PostgreSQL usando `IDbContextFactory<AgenticDbContext>`. O isolamento de Tenant será aplicado de forma transparente por meio dos filtros globais do EF Core.
4. **Mecanismo de Auto-Seeding Dinâmico:** Ao provisionar um Tenant ou no primeiro carregamento de skills de um Tenant ativo, se a base estiver vazia, o sistema executará o provisionamento automático das skills padrão do sistema (`coding-assistant`, `productivity`, `creative-writing`, `data-analysis`) no banco de dados.
5. **CRUD Completo e Template na API (`AgentSkillsController`):**
   * Ampliar a API REST para oferecer suporte completo a persistência física no banco (Create, Read, Update, Delete).
   * Fornecer um endpoint de download de template de estrutura oficial Markdown (`/api/agent/skills/template`).
   * Oferecer um endpoint de upload de arquivos Markdown com parsing dinâmico de Frontmatter YAML para persistência no banco.
   * Criar um endpoint de brainstorming assistido por IA (`/api/agent/skills/brainstorm`) para apoiar o chat do usuário no frontend.
6. **Interface Web Avançada (Frontend React):**
   * **Tela de Listagem e Grid:** Exibe as skills marcando-as claramente como "Sistema" (estáticas em banco, semeadas, read-only) ou "Customizadas" (criadas por usuários, totalmente editáveis).
   * **Formulário Dinâmico:** Cadastro com validação de formato, editor de prompt com live preview markdown e seletor de domínios.
   * **Upload Drag-and-Drop:** Zona interativa de upload do arquivo `.md` com botão de download do template pré-configurado.
   * **AI Brainstorming Chat:** Painel lateral onde um agente especialista ajuda a gerar e calibrar prompts de skills, permitindo salvar o resultado na base com um clique.

## Justificativa

1. **Simplicidade de Arquitetura (Single Source of Truth):** Elimina a complexidade do modelo híbrido de fusão. Todas as habilidades residem no PostgreSQL.
2. **Zero Dependência do Sistema de Arquivos (Cloud Native):** Ideal para contêineres e serverless. Sem escrita ou leitura no disco efêmero do host, o que garante 100% de consistência entre múltiplas réplicas da API.
3. **Total Alinhamento com a Finalidade de Tela/API:** Habilita a gerência e o ciclo de vida completo de skills No-Code via tela.
4. **Resiliência a Falhas:** Toda transação é atômica no banco, herdando a consistência do PostgreSQL e garantindo facilidade de backups e migrações.

## Consequências

### Positivas
* **Consistência em Ambientes Multi-Instância:** Sem riscos de drift de arquivos locais entre servidores web por trás de balanceadores de carga.
* **Segurança e Conformidade:** Isolamento rigoroso a nível de banco único, mitigando riscos de vazamento cross-tenant de prompts corporativos estratégicos.
* **Flexibilidade de Gestão:** Habilidades padrão (System) e customizadas (Custom) são tratadas sob o mesmo pipeline de dados, unificando a API.

### Desafios / Pontos de Atenção (Negativas)
* **Custo de Startup do Tenant:** O primeiro acesso de um tenant possui um overhead mínimo de frações de segundos para semear as skills padrão no banco.
* **Read-Only Restrict:** O frontend e o backend devem garantir que registros com a flag `IsSystem = true` sejam bloqueados contra mutações (atualizações ou deleções por usuários comuns), permitindo apenas que sejam inspecionados ou associados a agentes.
