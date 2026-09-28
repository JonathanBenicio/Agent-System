# ADR 006: Padronização do Skills Framework com MAF Nativo

**Status:** Aprovado  
**Data:** 24 de Maio de 2026  
**Autor(es):** Antigravity

---

## Contexto

No `AgenticSystem`, as habilidades (skills) dos agentes vinham sendo tratadas como registros estáticos em banco de dados ou como injeções manuais de prompts dentro do ciclo de vida da sessão. Com a adoção do **Microsoft Agent Framework (MAF) 1.6.1**, o framework passou a oferecer um modelo arquitetural unificado e file-based para Skills através do uso de `AgentFileSkillsSource`, `AgentFileSkill` e `AgentSkillsProvider`. A abordagem antiga acoplava fortemente a infraestrutura de agentes ao repositório de dados transacional (`ISkillManager`), dificultando o versionamento via controle de código fonte (Git) e a extensão declarativa (Zero-Code) das habilidades.

## Decisão

Adotar as abstrações nativas do MAF para o gerenciamento de Skills.

1. O armazenamento de skills baseadas em instruções e artefatos será migrado para um modelo de arquivos `.md` (Markdown) ou estruturados, gerenciados pelo `AgentFileSkillsSource`.
2. O carregamento dessas skills nos agentes será orquestrado nativamente pelo `AgentSkillsProvider` integrado no pipeline do `AIAgentBuilder`.
3. O `ISkillManager` legado será refatorado ou substituído para atuar apenas como ponte (caso haja necessidade de persistência dinâmica) ou removido em prol da fonte de arquivos estáticos.

## Justificativa

1. **Desacoplamento de Banco de Dados:** Tratar as definições de Skills como código/recurso (Docs-as-Code) permite PRs (Pull Requests), auditoria de versão e facilita CI/CD, alinhando com a governança da plataforma.
2. **Integração com Ferramentas Nativas:** O uso do provedor nativo automatiza a injeção do contexto das skills no prompt do LLM e o registro de `AIFunctions` derivadas sem a necessidade de código C# customizado.
3. **Escalabilidade (Zero-Code):** Facilita a criação de novas skills por especialistas de domínio (ex: editando um arquivo Markdown), sem a necessidade de desenvolvedores recompilarem o backend.

## Consequências

### Positivas
* **Versionamento nativo:** Ciclo de vida das skills atrelado via Git.
* **Aderência arquitetural:** Alinhamento total aos padrões propostos pela Microsoft.
* **Desacoplamento:** Redução de dependência do banco de dados na inicialização dos Agentes.

### Desafios / Pontos de Atenção (Negativas)
* **Migração de Dados:** Necessidade de exportação dos dados legados (tabelas de skills) para o sistema de arquivos.
* **Adaptação de Testes:** Adaptação dos testes unitários que usavam mocks restritos ao contrato anterior (`ISkillManager`).