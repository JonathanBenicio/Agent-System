# Plano de Ação - Atualização de User Stories

## Objetivo
Atualizar o arquivo `docs/USER-STORIES.md` com as capacidades e recursos ausentes identificados (Smart Triage, Platform Capabilities, Advanced RAG e FinOps), garantindo que o documento reflita o estado atual da arquitetura do sistema.

## Conductor Blueprint

### ⚖️ Trade-offs de Abordagem

| Caminho | Prós | Contras |
| :--- | :--- | :--- |
| **A: Atualizar `USER-STORIES.md` existente** | Mantém a fonte única de verdade; histórico preservado no Git. | O arquivo já é grande (quase 2000 linhas); pode ficar mais complexo. |
| **B: Criar `USER-STORIES-EXTENSIONS.md`** | Mantém o arquivo original intacto; isola recursos novos. | Fragmenta a documentação; exige que o leitor consulte múltiplos arquivos. |

*Decisão proposta*: **Caminho A**, pois o objetivo é ter um catálogo *consolidado*.

### ⚡ Matriz de Risco

| Risco | Impacto | Probabilidade | Mitigação |
| :--- | :--- | :--- | :--- |
| Quebrar links internos no arquivo gigante. | Alto | Média | Usar ferramentas de busca para verificar âncoras e referências. |
| Perder o padrão de escrita (Como/Quero/Para). | Médio | Baixa | Seguir rigorosamente o template existente no arquivo. |
| Desalinhamento com a implementação real. | Alto | Baixa | Basear-se estritamente no `backend-architecture-explained.md` e `smart-routing-triage.md`. |

### ↩️ Estratégia de Rollback
Caso a atualização cause problemas de legibilidade ou erros:
1.  Reverter o commit específico usando `git revert`.
2.  Manter a versão anterior como backup local se necessário.

### 📍 Mapeamento de Arquivos
*   **Destino**: `c:\Users\Jonathan\Documents\Developer\GitHub\Agent-System\docs\USER-STORIES.md`
*   **Fontes de Verdade**:
    *   `c:\Users\Jonathan\Documents\Developer\GitHub\Agent-System\docs\architecture\backend-architecture-explained.md`
    *   `c:\Users\Jonathan\Documents\Developer\GitHub\Agent-System\docs\architecture\smart-routing-triage.md`

---

## 📋 Tarefas

### Fase 1: Preparação e Aprovação
- [x] Tarefa 1: Obter resposta do usuário às questões socráticas → Verificar: Confirmação no chat.

### Fase 2: Criação de Issues no GitHub
- [x] Tarefa 2: Criar Épico/Issues no GitHub para rastrear a atualização → Verificar: Issues listadas no repositório.

### Fase 3: Atualização do Arquivo
- [x] Tarefa 3: Adicionar User Stories de **Smart Triage** (Já existia como ML40) → Verificar: Presença dos termos `MlFastPathInterceptor`, `TriageService`.
- [x] Tarefa 4: Adicionar User Stories de **Platform Capabilities** (Já existia como ML26-ML34) → Verificar: Presença de `Quality Gates`, `Vision`, `MCP`.
- [x] Tarefa 5: Adicionar User Stories de **RAG Avançado** (Adicionado como ML38) → Verificar: Presença de `Cross-Encoder ReRanker`.
- [x] Tarefa 6: Adicionar User Stories de **Recursos Recentes** (Adicionado como ML39) → Verificar: Presença de `Proactive LLM Quotas`.

## 🏁 Critérios de Conclusão (Done When)
- [ ] O arquivo `USER-STORIES.md` contém todas as novas capacidades mapeadas como User Stories válidas.
- [ ] As issues no GitHub foram criadas e vinculadas (se aplicável).
- [ ] Nenhum link interno foi quebrado.

---

## 🧠 Questões Socráticas para o Usuário
1.  **Formato**: Deseja que as novas capacidades sejam adicionadas como novos níveis de maturidade (ex: ML36+) ou como uma nova seção de "Capacidades Avançadas"?
2.  **Mapeamento de Issues**: Deseja que eu crie um único Épico no GitHub com sub-issues para cada grupo (Triagem, RAG, etc.) ou issues separadas?
3.  **Execução**: Após aprovação do plano e criação das issues, posso prosseguir diretamente com a edição do arquivo?
