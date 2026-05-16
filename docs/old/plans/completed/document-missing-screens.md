# Plano de Ação - Documentação de Telas Ausentes

## Objetivo
Documentar as telas `WorkflowBuilder`, `WebhooksPage` e `AlertsPage` que estão implementadas no código mas ausentes no `USER-STORIES.md` e nos arquivos BDD.

## Escopo e Diretrizes
- **WorkflowBuilder**: Documentar o estado alvo (permitindo salvar e executar workflows), mesmo que a implementação atual seja apenas visual.
- **WebhooksPage**: Documentar as operações de CRUD existentes (Listar, Criar, Excluir) e o uso do webhook.
- **AlertsPage**: Documentar a visualização e marcação como lido, além de incluir cenários BDD que simulam a geração de alertas pelo backend (ex: atingir limites de cota).

## 📋 Tarefas

### Fase 1: Atualização de User Stories
- [x] Adicionar User Stories para `WorkflowBuilder` em `docs/USER-STORIES.md`.
- [x] Adicionar User Stories para `WebhooksPage` em `docs/USER-STORIES.md`.
- [x] Adicionar User Stories para `AlertsPage` em `docs/USER-STORIES.md`.

### Fase 2: Criação de Arquivos BDD (.feature)
- [x] Criar `docs/bdd/workflows.feature`.
- [x] Criar `docs/bdd/webhooks.feature`.
- [x] Criar `docs/bdd/alerts.feature`.

## 🏁 Critérios de Conclusão
- Arquivo `USER-STORIES.md` atualizado com as novas stories.
- 3 novos arquivos `.feature` criados em `docs/bdd/`.
- Documentação consistente com as decisões tomadas (estado alvo para workflows, geração de alertas no backend).
