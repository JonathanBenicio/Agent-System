# ADR 032: Contratos REST para CRUD de Golden Sets da Evaluation Suite

> Revisão operacional 2026-09-28: Schemas abaixo são proposta histórica, não resposta atual. Controller possui três aliases, lista array, agentName obrigatório, <=20 casos retorna suiteId/results; >20 retorna 202/runId e GET /runs/{runId} com cache local. Ver [contrato atual](../../backend/api-core.md).

**Status:** Proposto
**Data:** 04 de Junho de 2026
**Autor(es):** Antigravity AI & Jonathan Benicio

---

## Contexto

A suíte de avaliação contínua do AgenticSystem (`RuntimeEvaluatorService`) está operacional e integrada com `Microsoft.Extensions.AI.Evaluation`. O sistema é capaz de executar avaliações heurísticas e baseadas em LLM, detectar regressões e persistir resultados.

No entanto, a auditoria arquitetural de Junho/2026 identificou que **não existem endpoints REST para gerenciar os Golden Sets** — os conjuntos de casos de teste (input/output esperado) utilizados nas avaliações. Atualmente, os dados só podem ser inseridos diretamente no banco de dados, o que é inviável em produção e viola a premissa arquitetural do sistema de ser totalmente gerenciável via API.

A documentação em `docs/planejamento/Agent_Runtime_State_Machine.md` lista a evaluation suite como entregável da Track 4 do Q2 2026, mas não especifica os contratos de API para Golden Sets.

**Alternativas consideradas:**

- *Opção A (adotada):* REST CRUD padrão com 6 endpoints JSON, seguindo o padrão dos controllers existentes.
- *Opção B:* GraphQL — introduz complexidade desnecessária para um CRUD simples sem queries relacionais complexas.
- *Opção C:* Endpoint único com operação PATCH — dificulta geração de SDK e documentação Swagger.

---

## Decisão

Implementar um `GoldenSetController` com os seguintes contratos REST, respeitando os padrões existentes do sistema (envelope de resposta, paginação, `X-Tenant-Id`, `X-Correlation-Id`):

### Endpoints

| Método | Rota | Descrição |
|--------|------|-----------|
| `POST` | `/api/evaluation/golden-sets` | Criar novo golden set |
| `GET` | `/api/evaluation/golden-sets` | Listar golden sets do tenant (paginado) |
| `GET` | `/api/evaluation/golden-sets/{id}` | Detalhar golden set por ID |
| `PUT` | `/api/evaluation/golden-sets/{id}` | Atualizar golden set completo |
| `DELETE` | `/api/evaluation/golden-sets/{id}` | Remover golden set |
| `POST` | `/api/evaluation/golden-sets/{id}/run` | Executar avaliação contra o golden set |

### Schema de Dados

```json
// GoldenSetDto
{
  "id": "uuid",
  "name": "string",
  "description": "string",
  "tenantId": "string",
  "cases": [
    {
      "id": "uuid",
      "input": "string",
      "expectedOutput": "string",
      "tags": ["string"]
    }
  ],
  "createdAt": "datetime",
  "updatedAt": "datetime"
}

// RunResultDto (resposta do POST .../run)
{
  "runId": "uuid",
  "goldenSetId": "uuid",
  "executedAt": "datetime",
  "overallScore": 0.92,
  "cases": [
    {
      "caseId": "uuid",
      "passed": true,
      "actualOutput": "string",
      "score": 0.95,
      "evaluatorNotes": "string"
    }
  ]
}
```

### Regras de Negócio

- Golden Sets são tenant-isolated — filtro automático via global query filter EF Core por `TenantId`.
- Máximo de 500 casos por Golden Set (validação no DTO com `[MaxLength(500)]`).
- `POST .../run` é síncrono para golden sets com ≤ 20 casos; assíncrono (retorna `runId` com polling) para > 20 casos.
- Apenas o tenant proprietário pode ler, editar ou deletar seus Golden Sets.

---

## Justificativa

1. **Completude da Track 4:** O Roadmap Q2 2026 lista a Evaluation Suite como entregável. Sem endpoints CRUD, a feature é inutilizável por times externos ao desenvolvimento.

2. **Padrão REST Existente:** Os controllers existentes (ex: `KnowledgeRoomController`, `WorkflowController`) seguem o mesmo padrão de CRUD REST com `X-Tenant-Id`. Manter consistência reduz curva de aprendizado da API.

3. **Gerenciamento via Chat (Premissa Arquitetural):** A premissa do AgenticSystem é que tudo é gerenciável via API/chat sem acesso direto ao banco. Golden Sets não são exceção.

4. **Testabilidade:** Endpoints REST permitem testes E2E com Cypress/Playwright, diferente de inserções diretas no banco que não validam a lógica de negócio.

---

## Consequências

### Positivas

* **Evaluation Suite completa e utilizável:** Times de produto podem criar e gerenciar conjuntos de testes via API ou UI.
* **Integração com CI/CD:** Endpoints permitem que pipelines de integração contínua criem e executem Golden Sets automaticamente.
* **Auditoria de qualidade:** Histórico de runs com scores permite tracking de regressão de qualidade dos agentes ao longo do tempo.
* **Track 4 do Q2 2026 completa:** Cumpre o critério de aceite do Roadmap.

### Desafios / Pontos de Atenção (Negativas)

* **Latência do `/run` para sets grandes:** Avaliações com muitos casos e chamadas LLM podem demorar. Mitigado com modelo assíncrono para > 20 casos.
* **Custo de LLM em avaliações:** Cada run de avaliação baseada em AI consome tokens. Recomenda-se integração futura com o `QuotaEnforcer` para debitar o custo das avaliações ao tenant.
* **Schema evolution dos casos:** Adicionar novos campos aos `GoldenSetCase` futuramente exige migration sem perda de dados históricos.
