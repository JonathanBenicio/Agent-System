---
title: "Plano de Validação Contínua: Código vs Documentação (Code vs Docs Validation Gate)"
status: COMPLETED
tier: TIER 0 / TIER 1
agent: project-planner
updated: 2026-05-14
---

# 📑 Plano de Validação Contínua: Código vs Documentação (Code vs Docs Validation Gate)

## 🎯 Overview & Context
Com a conclusão da auditoria de *drift* documental na pasta `docs/`, é imprescindível estabelecer uma ponte de verificação contínua e bidirecional entre os contratos de código reais em C# .NET 10 DDD / React 19 e a documentação arquitetural e de produto (incluindo `CONSOLIDATED_DOCS.md`, `ARCHITECTURE.md` e `USER-STORIES.md`).

Este plano define a estratégia de 4 fases para automatizar e sistematizar o **Validation Gate** de Código x Documentação, assegurando que futuras refatorações (ex: mudanças em serviços de IA ou rotas de frontend) exijam atualização imediata na documentação aplicável.

---

## 🏗️ Project Type
**FULLSTACK / ARCHITECTURE** (Validação transversal de C# Backend, React Frontend e Artefatos Documentais).

---

## 🏁 Measurable Success Criteria (AAA Pattern)
1. **Arrange**: Implementação e/ou configuração de *hooks* de CI e scripts de auditoria de reflexão que extraiam a assinatura de serviços C# e rotas do React.
2. **Act**: Execução de rotinas de comparação semântica e estrutural entre o código ativo e os índices de `CONSOLIDATED_DOCS.md` e `USER-STORIES.md`.
3. **Assert**: Falha de build imediata (via `checklist.py` ou extensão de validação) caso seja detectada uma funcionalidade pública ou endpoint sem cobertura na documentação viva.

---

## ⚖️ Tech Stack & Trade-off Analysis (Socratic Gate)

| Opção | Descrição | Prós | Contras | Decisão |
| :--- | :--- | :--- | :--- | :--- |
| **Opção A: Roslyn & AST Extraction combined with Grep** | Uso de scripts Python/C# para inspecionar árvores sintáticas e comparar com títulos de seções markdown. | Extremamente robusto e insensível a mudanças superficiais de estilo. | Exige manutenção do motor de extração em caso de novas sintaxes de linguagem. | **SELECIONADA**. Integra-se perfeitamente com a extensão de superpowers e TIER 0. |
| **Opção B: LLM RAG-based Continuous Scan** | Envio diário de diffs de código para o modelo LLM avaliar se a documentação ficou defasada. | Capacidade analítica profunda para inferir intenções arquiteturais. | Latência e custo recorrente na esteira de CI/CD. | Rejeitada para *blocking gates*; mantida apenas para auditorias mensais de alto nível. |

---

## ⚠️ Risk Assessment & Mitigation

| Risco | Probabilidade | Impacto | Estratégia de Mitigação |
| :--- | :--- | :--- | :--- |
| **Falsos Positivos em Refatorações Menores** | Alta | Médio | Criar anotações ou diretrizes de ignorar (ex: `[DocsIgnore]`) para métodos puramente utilitários ou internos. |
| **Sobrecarga na Esteira de Build** | Média | Médio | Otimizar os scripts para analisar apenas arquivos modificados no diff (Incremental Validation). |

---

## 📁 File Structure & Mapping (Cobertura Estendida)

```markdown
Agent-System/
├── src/
│   └── AgenticSystem.Core/            # [Alvo de Extração de Assinaturas Backend]
├── frontend/src/                      # [Alvo de Extração de Rotas Frontend]
├── docs/
│   ├── architecture/                  # [Alvo: Validação Canônica de Backend e Registry]
│   ├── bdd/                           # [Alvo: Validação Executável de Cenários]
│   ├── historico/                     # [Alvo: Preservação de Rastreabilidade]
│   ├── planejamento/                  # [Alvo: Paridade de Roadmaps e Gaps]
│   ├── referencia-externa/            # [Alvo: Rastreio de Fornecedor MAF]
│   ├── superpowers/plans/             # [Alvo: Verificação de Extensions]
│   ├── USER-STORIES.md                # [Alvo: Validação de Cobertura Funcional]
│   ├── CONSOLIDATED_DOCS.md           # [Alvo: Validação de Cobertura Arquitetural]
│   └── plan/
│       └── code-vs-docs-validation.md # [Este Plano]
└── .agents/
    └── scripts/
        └── checklist.py               # [Ponto de Integração do Validation Gate]
```

---

## 📋 Detalhamento das Tarefas (Task Breakdown)

### Phase 1: ANALYSIS (Modelagem de Contratos e Extração)

#### Task 1.1: Mapeamento de Pontos de Entrada no Backend e Frontend
- [x] **Agent**: `backend-specialist` | **Skill**: `api-patterns`
- **Priority**: P0 | **Dependencies**: Nenhuma
- **Input**: `src/` e `frontend/src/`.
- **Output**: Definição do formato JSON/YAML intermediário de inventário de código ativo.
- **Verify**: Teste de extração bem-sucedido na máquina de desenvolvimento local.

---

### Phase 2: PLANNING (Design da Regra de Validação)

#### Task 2.1: Especificação das Regras de Casamento (Matching Rules)
- [x] **Agent**: `project-planner` | **Skill**: `plan-writing`
- **Priority**: P1 | **Dependencies**: Task 1.1
- **Input**: Inventário de código vs Catálogo de `USER-STORIES.md`.
- **Output**: Documento de regras de tolerância e casamento semântico.
- **Verify**: Revisão por pares de arquitetura.

---

### Phase 3: SOLUTIONING (Implementação do Motor de Validação)

#### Task 3.1: Integração do Verificador no `checklist.py`
- [x] **Agent**: `orchestrator` | **Skill**: `clean-code`
- **Priority**: P1 | **Dependencies**: Task 2.1
- **Input**: Motor de extração e regras de casamento.
- **Output**: Novo passo no `checklist.py` (ex: `Running: Docs Coverage Check`).
- **Verify**: Execução isolada do novo script comprovando detecção de *drift*.

---

### Phase 4: IMPLEMENTATION (Rollout na Esteira)

#### Task 4.1: Ativação Global do Gate de Documentação
- [x] **Agent**: `orchestrator` | **Skill**: `deployment-procedures`
- **Priority**: P2 | **Dependencies**: Task 3.1
- **Input**: Sistema completo com validação ativa.
- **Output**: Esteira de desenvolvimento blindada contra *docs drift*.
- **Verify**: Execução final de `python .agents/scripts/checklist.py .`.

---

## 🔄 Rollback Strategy
* **Mecanismo**: Desabilitar a flag de validação de documentação no script mestre caso bloqueios indevidos ocorram em produção.
* **Critério de Restauração**: Reversão do commit que introduziu a checagem no `checklist.py`.

---

## 🏁 Phase X: Final Verification (Checklist & Automation Gate)

```bash
python .agents/scripts/checklist.py .
```

## ✅ PHASE X COMPLETE
- Lint: ✅ Pass
- Security: ✅ No critical issues
- Build & Coverage: ✅ Success
- Date: 2026-05-14
