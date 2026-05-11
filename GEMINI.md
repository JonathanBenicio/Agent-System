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

### Limpeza & Qualidade
- **Clean Code**: Siga rigorosamente a skill `@[skills/clean-code]`.
- **Lint & Types**: Execute os comandos de verificação (ex: lint, type-check, build) antes de considerar uma tarefa concluída. **Zero tolerância para erros.**

### Comunicação
- Responda no idioma do usuário.
- Mantenha comentários e nomes de variáveis em Inglês, a menos que existam termos de domínio específicos já estabelecidos no projeto.

---

## 📁 Referência Rápida
- **Workflows**: `.agents/workflows/`
- **Scripts de Verificação**: `.agents/scripts/verify_all.py`
- **Documentação de Arquitetura**: `.agents/ARCHITECTURE.md`
