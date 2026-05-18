# GEMINI.md - Global Orchestrator

Este é o arquivo central de instruções para o agente neste workspace.

---

## 🏗️ Estrutura do Projeto & Roteamento de Regras

O projeto é um monorepo dividido em Frontend e Backend. As regras específicas de cada domínio estão localizadas em seus respectivos diretórios:

- **Frontend**: `frontend/GEMINI.md`
- **Backend**: `src/GEMINI.md`

### 🔴 REGRA DE OURO
**Sempre consulte o `GEMINI.md` do diretório em que você está trabalhando.**
- Se estiver editando arquivos em `frontend/`, as regras de `frontend/GEMINI.md` têm precedência.
- Se estiver editando arquivos em `src/`, as regras de `src/GEMINI.md` têm precedência.

---

## 🤖 Sistema de Agentes & Skills

Todas as definições de agentes, skills e workflows estão centralizadas no diretório:
👉 **`.agents/`**

### Instruções para Uso de Agentes
1. **Identificação**: Determine se a tarefa é de Frontend, Backend ou Geral.
2. **Ativação**: Leia o arquivo do agente em `.agents/agents/{agent-name}.md`.
3. **Skills**: Carregue as skills listadas no frontmatter do agente a partir de `.agents/skills/`.

---

## 🧹 Regras Globais (TIER 0)

### Limpeza & Qualidade (Superpowers-Driven)
- **Superpowers Engine**: O desenvolvimento local é guiado pelas regras de qualidade extrema da extensão `obra/superpowers`. Todo código escrito deve ser modular, sem placeholders, totalmente testável (padrão AAA) e livre de acoplamento desnecessário.
- **Clean Code**: Siga rigorosamente a skill `@[skills/clean-code]`.
- **Lint & Types**: Execute os comandos de verificação (ex: lint, type-check, build) antes de considerar uma tarefa concluída. **Zero tolerância para erros.**

### Pipeline de Execução de Agentes
1. **Pensamento (Conductor)**: Todo planejamento de tarefas complexas é guiado pela extensão `gemini-cli-extensions/conductor`, exigindo mapeamento socrático profundo, estimativas de risco e tabelas detalhadas de trade-offs antes de qualquer código ser escrito.
2. **Orquestração Concorrente (Antigravity Swarm)**: O gerenciamento de subagentes paralelos é feito através da orquestração concurrente do Swarm, garantindo que as fronteiras de cada agente sejam rigorosamente respeitadas.
3. **Barreira de Qualidade (Security Gate)**: Todo build, deploy ou finalização de tarefa deve passar pela validação de segurança da extensão `gemini-cli-extensions/security`, que bloqueia a conclusão em caso de vulnerabilidades críticas.

### 📜 Governança de Documentação (Roadmap Q2 2026)
Toda nova funcionalidade estratégica deve seguir rigorosamente esta ordem:
1. **GitHub Issue**: Registro da necessidade. **Obrigatório atualizar a descrição da Issue com links para o ADR, Story e Plan assim que criados.**
2. **ADR (Architectural Decision Record)**: Definição de padrões técnicos em `docs/architecture/adr/`.
3. **User Story**: Detalhamento dos critérios de aceite em `docs/USER-STORIES.md`.
4. **Implementation Plan**: Roteiro técnico detalhado em `plan/`.
5. **Rastreabilidade**: Commits vinculados à issue (ex: `feat: ... Closes #ID`).
6. **Sincronização de Índices**: Atualizar `README.md`, `INDEX.md` e `CONSOLIDATED_DOCS.md`.

Consulte o [Master Roadmap Q2 2026](plan/master-roadmap-2026.md) para prioridades correntes.

### Comunicação
- Responda no idioma do usuário.
- Mantenha comentários e nomes de variáveis em Inglês, a menos que existam termos de domínio específicos já estabelecidos no projeto.

---

## 🗄️ Banco de Dados & Migrações
- **Localização**: As migrations do EF Core devem ser geradas no projeto `AgenticSystem.Infrastructure` na pasta `Persistence/Migrations`.
- **Comando**: Sempre use o comando abaixo para garantir a pasta correta:
  ```bash
  dotnet ef migrations add <Name> --project src/AgenticSystem.Infrastructure --startup-project src/AgenticSystem.Api --output-dir Persistence/Migrations
  ```

---

## 📁 Referência Rápida
- **Workflows**: `.agents/workflows/`
- **Scripts de Verificação**: `.agents/scripts/verify_all.py`
- **Documentação de Arquitetura**: `.agents/ARCHITECTURE.md`
