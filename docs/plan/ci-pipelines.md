# Roadmap: Configuração de Pipelines de CI (GitHub Actions)

> **Status documental:** Draft
> **Escopo:** Criação de workflows separados para Backend (.NET 10) e Frontend (React + Vite + Playwright) acionados em Pull Requests.
> **Fonte de verdade operacional:** `AGENTS.md` e `GEMINI.md`
> **Gerado em:** 2026-05-18
> **Projeto:** AgenticSystem

---

## Objetivo

Implementar uma esteira de Integração Contínua (CI) usando GitHub Actions para garantir que todo Pull Request (PR) seja validado automaticamente antes de ser mesclado. Isso inclui compilação, testes unitários, linting e testes end-to-end (E2E).

## Princípios de Implantação

1. **Isolamento**: Workflows de Backend e Frontend devem ser separados para evitar execuções desnecessárias.
2. **Modernidade**: Usar .NET 10 para o backend e Playwright para testes E2E no frontend.
3. **Bloqueio de Qualidade**: Falhas no build, lint ou testes devem impedir o merge do PR.
4. **Resiliência**: Tratar erros de tipos e dependências antes de subir a pipeline.

## Fases e Sequenciamento

| Ordem | Frente/Fase | Motivo do sequenciamento |
|---|---|---|
| 1 | Correção do Build do Frontend | O build atual está quebrado com erros de TS. A pipeline falharia imediatamente. |
| 2 | Workflow do Backend | Mais simples de isolar e configurar com .NET 10. |
| 3 | Workflow do Frontend | Requer configuração de ambiente Node e Playwright (mais complexo). |

---

## Detalhamento: CI Pipelines

### Por que implementar?
Para evitar que código quebrado ou sem testes seja mesclado na branch principal, garantindo a estabilidade do sistema.

### Componentes propostos
| Componente | Papel |
|---|---|
| `.github/workflows/backend.yml` | Valida o código C# (.NET 10) em PRs. |
| `.github/workflows/frontend.yml` | Valida o código React/TypeScript e roda Playwright em PRs. |

### Plano por etapas

#### Fase 1: Correção do Build do Frontend
1. Corrigir o erro de variável não utilizada em `RoomAccessModal.tsx`.
2. Corrigir os tipos ausentes (`KnowledgeRoomPermission`, `KnowledgeRoomRole`) em `api.ts`.
3. Validar o build localmente com `npm run build` na pasta `frontend`.

#### Fase 2: Workflow do Backend (.NET 10)
1. Criar `.github/workflows/backend.yml`.
2. Configurar trigger em `pull_request` afetando arquivos do backend.
3. Passos: Checkout, Setup .NET 10, Restore, Build, Test.
4. Adicionar coleta de cobertura de código (limite de 80%).

#### Fase 3: Workflow do Frontend (React + Playwright)
1. Criar `.github/workflows/frontend.yml`.
2. Configurar trigger em `pull_request` afetando arquivos do frontend.
3. Passos: Checkout, Setup Node 20, Install, Lint, Build.
4. Configurar instalação de browsers do Playwright e execução dos testes E2E.

### Critérios de Aceite e SLOs
* [ ] Frontend passa em `npm run build` sem erros de TypeScript.
* [ ] Workflow do Backend executa com sucesso em PRs.
* [ ] Workflow do Frontend executa com sucesso em PRs, incluindo testes Playwright.
* [ ] Cobertura de testes do backend atinge o mínimo de 80% (conforme regra do projeto).

### Riscos e Mitigações
| Risco | Mitigação |
|---|---|
| Testes E2E lentos ou instáveis (flaky) | Usar shards ou rodar apenas testes críticos em PRs se necessário. |
| Versão do .NET no CI incompatível | Forçar o uso do SDK 10.0 explicitamente no workflow. |
