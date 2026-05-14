---
title: "Plano de Implementação: Auditoria & Sincronização da Documentação (Docs Drift Audit & Sync)"
status: PLANNED
tier: TIER 0 / TIER 1
agent: project-planner
updated: 2026-05-14
---

# 📑 Plano de Implementação: Auditoria & Sincronização da Documentação (Docs Drift Audit & Sync)

## 🎯 Overview & Context
Ao longo da evolução da solução **AgenticSystem**, certas decisões arquiteturais e implementações (como a adoção de um motor de agendamento customizado no lugar de dependências externas como o Quartz.NET) divergiram dos planos e documentações originais (*Architectural Drift*). 

Este plano estabelece uma metodologia de 4 fases para auditar rigorosamente todos os artefatos de documentação (`CONSOLIDATED_DOCS.md`, `README.md`, `ARCHITECTURE.md`, `CODEBASE.md`, `GEMINI.md` e a pasta `docs/plan/`), mapear as discrepâncias contra a base de código real e consolidar uma documentação 100% fidedigna e acionável para o ecossistema de agentes.

---

## 🏗️ Project Type
**FULLSTACK / ARCHITECTURE** (Agnóstico com impacto direto em Frontend, Backend e Infraestrutura de Agentes).

---

## 🏁 Measurable Success Criteria (AAA Pattern)
1. **Arrange**: Varredura completa automatizada via scripts de análise sintática e mapeamento semântico de símbolos do projeto (C# e TypeScript) contra textos de documentação.
2. **Act**: Atualização e consolidação dos arquivos mestre de documentação (`CONSOLIDATED_DOCS.md`, `README.md`, `ARCHITECTURE.md`), retificando referências obsoletas (ex: *Quartz*, *Polly Circuit Breaker exceptions*, etc.).
3. **Assert**: Verificação de integridade via execução de `python .agents/scripts/checklist.py .` e validação semântica com zero links quebrados ou ambiguidades arquiteturais.

---

## ⚖️ Tech Stack & Trade-off Analysis (Socratic Gate)

Para a automação da auditoria e sincronização das documentações, avaliamos as seguintes abordagens de ferramentas e processos:

| Opção | Descrição | Prós | Contras | Decisão |
| :--- | :--- | :--- | :--- | :--- |
| **Opção A: LLM-Driven AST Semantic Matching** | Uso de scripts customizados com RAG local combinando buscas ripgrep/AST da base de código com a documentação existente. | Alta precisão na detecção de *drift* arquitetural; entende contexto implícito de C# DDD e React. | Requer orquestração estruturada do LLM para evitar alucinações nas substituições. | **SELECIONADA**. Garante alinhamento profundo com as *skills* e regras do projeto. |
| **Opção B: Manual Text & Regex Grep Audit** | Revisão manual auxiliada por buscas textuais e expressões regulares nos diretórios do repositório. | Simplicidade imediata de configuração. | Propenso a falha humana; baixa escalabilidade à medida que o monorepo cresce. | Rejeitada. Não atende aos padrões de automação de TIER 0 do projeto. |

---

## ⚠️ Risk Assessment & Mitigation

| Risco | Probabilidade | Impacto | Estratégia de Mitigação |
| :--- | :--- | :--- | :--- |
| **Perda de Histórico de Decisões (ADRs)** | Média | Alto | Em vez de deletar planos antigos divergentes, adicionar uma seção de `Status de Evolução/Retificação` no cabeçalho ou notas de rodapé de cada arquivo. |
| **Quebra de Referências e Links Internos** | Alta | Médio | Utilizar verificação estricta de links relativos e âncoras markdown antes de aprovar a consolidação dos arquivos. |
| **Dessincronização Concorrente durante Refatorações** | Média | Alto | Congelar *merges* estruturais na `main` durante a execução da Fase 3 e 4 deste plano. |

---

## 📁 File Structure & Mapping

```markdown
Agent-System/
├── CONSOLIDATED_DOCS.md               # [Alvo de Atualização Mestre]
├── README.md                          # [Alvo de Alinhamento de Roadmap/Arquitetura]
├── ARCHITECTURE.md                    # [Alvo de Retificação de Decisões/Motores]
├── CODEBASE.md                        # [Alvo de Mapeamento de Diretórios]
├── docs/
│   └── plan/
│       ├── p3-extensibility-automation.md # [Alvo de Inserção de Errata/Retificação]
│       └── docs-audit-sync.md         # [Este Plano]
└── .agents/
    └── scripts/
        └── checklist.py               # [Ferramenta de Auditoria Final]
```

---

## 📋 Detalhamento das Tarefas (Task Breakdown)

### Phase 1: ANALYSIS (Descoberta & Mapeamento de Drift)

#### Task 1.1: Mapeamento de Entidades e Serviços do Backend
- **Agent**: `backend-specialist` | **Skill**: `api-patterns`, `clean-code`
- **Priority**: P0 | **Dependencies**: Nenhuma
- **Input**: Estrutura atual de `src/AgenticSystem.Core` e `src/AgenticSystem.Infrastructure`.
- **Output**: Relatório de inventário de serviços reais (ex: `ScheduledTaskHostedService`, `LLMManager`, `PostgresScheduledTaskStore`).
- **Verify**: Executar `ripgrep` nos símbolos exportados de `src/` e validar equivalência.

#### Task 1.2: Mapeamento de Componentes e Telas do Frontend
- **Agent**: `frontend-specialist` | **Skill**: `frontend-design`, `modern-react-nextjs`
- **Priority**: P0 | **Dependencies**: Nenhuma
- **Input**: Diretório `frontend/src`.
- **Output**: Mapeamento de telas ativas e estado de implementação (Zustand, React 19).
- **Verify**: Garantir que todas as rotas listadas no frontend possuem correspondência na documentação.

#### Task 1.3: Auditoria Comparativa da Documentação (Drift Analysis)
- **Agent**: `orchestrator` | **Skill**: `agentic-brainstorming`, `plan-writing`
- **Priority**: P0 | **Dependencies**: Task 1.1, Task 1.2
- **Input**: Arquivos `CONSOLIDATED_DOCS.md`, `README.md`, `ARCHITECTURE.md` e `docs/plan/*.md`.
- **Output**: Lista consolidada de *drift* (pontos onde a doc cita bibliotecas/padrões não utilizados na prática).
- **Verify**: Cruzamento completo e identificação exata das linhas e parágrafos defasados.

---

### Phase 2: PLANNING (Consolidação do Plano de Refatoração Textual)

#### Task 2.1: Estruturação das Emendas de Documentação
- **Agent**: `project-planner` | **Skill**: `plan-writing`, `documentation-templates`
- **Priority**: P1 | **Dependencies**: Task 1.3
- **Input**: Lista de *drifts* identificados.
- **Output**: Minuta das atualizações para cada arquivo mestre.
- **Verify**: Revisão humana/socrática de aprovação da minuta antes de aplicar alterações.

---

### Phase 3: SOLUTIONING (Design de Retificação e ADRs)

#### Task 3.1: Elaboração de Erratas e ADRs para Planos Antigos
- **Agent**: `architecture` | **Skill**: `architecture`, `documentation-templates`
- **Priority**: P1 | **Dependencies**: Task 2.1
- **Input**: `docs/plan/p3-extensibility-automation.md` e similares.
- **Output**: Cabeçalhos e seções de errata explicando a substituição de componentes (ex: *Quartz* pelo *Motor Customizado em C#*).
- **Verify**: Garantir a preservação do histórico de intenções originais com clareza da evolução.

---

### Phase 4: IMPLEMENTATION (Atualização e Consolidação)

#### Task 4.1: Atualização do `ARCHITECTURE.md` e `CODEBASE.md`
- **Agent**: `orchestrator` | **Skill**: `documentation-templates`
- **Priority**: P1 | **Dependencies**: Task 3.1
- **Input**: Novo mapa arquitetural verificado.
- **Output**: Arquivos de arquitetura atualizados com os fluxos reais de dados, serviços e MCP.
- **Verify**: Visualização limpa da renderização markdown.

#### Task 4.2: Atualização do `README.md` e `CONSOLIDATED_DOCS.md`
- **Agent**: `orchestrator` | **Skill**: `documentation-templates`
- **Priority**: P1 | **Dependencies**: Task 4.1
- **Input**: O conteúdo consolidado do sistema.
- **Output**: Documentação mestre unificada, totalmente limpa, coerente e com índice remissivo funcional.
- **Verify**: Inspeção de leitura fluida e links internos.

---

## 🔄 Rollback Strategy
* **Mecanismo**: Cada tarefa de modificação textual será precedida por um commit de *checkpoint* ou backup local dos arquivos `.md` na pasta `scratch/`.
* **Critério de Restauração**: Caso ocorra corrupção na formatação markdown ou perda de seções críticas, reverter via `git checkout HEAD -- <arquivo>` ou restaurar o backup de `scratch/`.

---

## 🏁 Phase X: Final Verification (Checklist & Automation Gate)

```bash
# 1. Validação de regras e auditoria geral do projeto via script mestre
python .agents/scripts/checklist.py .

# 2. Verificação de integridade de links e formatação (Lint de Markdown se configurado)
npm run lint
```

### ✅ PHASE X COMPLETE
- Lint: `[ ]` Pendente de execução
- Security/Secrets na Doc: `[ ]` Pendente de execução
- Consistência Semântica: `[ ]` Pendente de execução
- Date: 2026-05-14
