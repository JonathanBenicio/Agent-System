# Plano de Mitigação e Resiliência LLM (Circuit Breaker Fix)

## Overview & Background
- **Objetivo:** Resolver falhas em cascata e `BrokenCircuitException` em operações de background (`SessionConsolidator`, `ContextAnalyzer`) causadas pela exaustão de retentativas e abertura do circuito no acesso aos provedores de LLM.
- **Contexto:** Quando requisições de background ou de usuários falham consecutivamente (por HTTP 429 ou falta de cota/chave), o Circuit Breaker entra em estado `Open`, rejeitando instantaneamente todas as chamadas subsequentes do sistema.

## Project Type
- **Type:** BACKEND (API e Serviços de Background)
- **Primary Agent:** `backend-specialist`

## Success Criteria
- [ ] Tarefas de background tratam `BrokenCircuitException` / `RateLimitRejectException` de forma resiliente sem interromper o serviço.
- [ ] O `LLMManager` realiza transição de fallback automática (ex: Gemini ➔ OpenRouter) quando o circuito do provedor principal abrir.
- [ ] O sistema passa em todas as validações de resiliência e testes automatizados.

## Tech Stack & Trade-offs
| Opção Arquitetural | Vantagens | Desvantagens | Decisão |
|--------------------|-----------|--------------|---------|
| **1. Fallback Automático via Polly (Wrap Policy)** | Transição transparente entre provedores em tempo de execução no `LLMManager`. | Complexidade na configuração de múltiplos clientes HTTP/Providers. | ✅ **Selecionada:** Maior resiliência e alinhamento com a arquitetura atual de injeção de dependência. |
| **2. Filas de Espera (RabbitMQ / Channel) em Background** | Desacopla totalmente a execução, controlando a vazão (rate limiting). | Exige refatoração estrutural no `SessionManager` e infraestrutura adicional. | ❌ Descartada para MVP imediato; foco na resiliência do cliente LLM. |

## Risk Assessment
| Risco | Probabilidade | Impacto | Mitigação |
|-------|---------------|---------|-----------|
| **Cascata de Falhas no Fallback** | Média | Alto | Configurar Circuit Breaker independente para o provedor de fallback com timeout ajustado. |
| **Esgotamento de Cota de Fallback** | Alta | Médio | Alertas de log claros (`ILogger`) e limitação diária de orçamento (`DailyBudget`). |

## File Structure & Paths
- `src/AgenticSystem.Infrastructure/LLM/LLMManager.cs` (Modificar: Adicionar suporte a fallback de provedor na política Polly)
- `src/AgenticSystem.Core/Services/SessionConsolidator.cs` (Modificar: Adicionar tratamento de exceção e espera em caso de circuito aberto)
- `src/AgenticSystem.Core/Services/ContextAnalyzer.cs` (Modificar: Adicionar resiliência local)

## Task Breakdown
- [ ] **Task 1:** Refatorar `LLMManager.cs` para implementar política de Fallback entre provedores (Gemini ➔ OpenRouter). 
  - **Agent:** `backend-specialist` | **Skill:** `api-patterns` | **Priority:** P0
  - **INPUT:** Clientes configurados ➔ **OUTPUT:** Wrap Policy com Fallback ➔ **VERIFY:** Teste simulando falha do provedor primário.
- [ ] **Task 2:** Adicionar tratamento de `BrokenCircuitException` e backoff em `SessionConsolidator.SummarizeSessionAsync`.
  - **Agent:** `backend-specialist` | **Skill:** `clean-code` | **Priority:** P1
  - **INPUT:** Eventos de sessão ➔ **OUTPUT:** Log de aviso e re-agendamento ➔ **VERIFY:** Execução de teste sem quebra do serviço.
- [ ] **Task 3:** Adicionar tratamento de exceção em `ContextAnalyzer.AnalyzeAsync`.
  - **Agent:** `backend-specialist` | **Skill:** `clean-code` | **Priority:** P1
  - **INPUT:** Input do usuário ➔ **OUTPUT:** Retorno gracioso / fallback sem erro ➔ **VERIFY:** Teste com injeção de falha no LLM.

## Rollback Strategy
- Em caso de instabilidade nas políticas do Polly, reverter os commits em `LLMManager.cs` para a versão anterior e manter `AgenticSystem__Gemini__Enabled=true` configurado no `docker-compose.yml`.

## Done When (Phase X Verification)
- [ ] Executar script de verificação: `python .agents/scripts/checklist.py .`
- [ ] Executar testes unitários do backend (`dotnet test`).
- [ ] Verificar logs sem ocorrência de interrupção abrupta no consolidador de sessão.
