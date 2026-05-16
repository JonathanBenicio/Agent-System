# Plano: Monitoramento de Execução de Workflows (UX/Feedback)

## Objetivo
Criar uma interface para visualização do histórico e monitoramento em tempo real das execuções de um Workflow, garantindo feedback adequado (UX) sobre o progresso e o status de cada nó (Agente, Tool, Condição).

## Contexto
O motor de execução de workflows (`DefaultWorkflowEngine`) no backend já está implementado e salvando o histórico (entidades `WorkflowExecutionEntity` e `WorkflowStepExecutionEntity`). A API já possui os endpoints `GET /api/workflow/executions` e `GET /api/workflow/executions/{id}`.
A tela de edição `WorkflowBuilder.tsx` já permite criar e disparar o workflow, mas falta o feedback de sua execução.

## Tarefas a Implementar

### 1. Painel de Execuções (Drawer / Panel Lateral)
- [ ] Adicionar um botão no header (ao lado de "Run") chamado "Executions" ou "History".
- [ ] Criar um componente lateral direito (`ExecutionHistoryPanel.tsx`) que lista todas as execuções do workflow ativo (`useWorkflows().listExecutions()`).
- [ ] Cada item da lista deve mostrar: Status (Running, Completed, Failed), data de início e botão para ver detalhes.

### 2. Visão Detalhada da Execução
- [ ] Ao clicar em uma execução no painel, abrir os detalhes buscando a execução completa (`workflowApi.getExecution(id)`).
- [ ] Mostrar uma lista/timeline dos `StepExecutions` indicando se cada nó falhou ou concluiu, exibindo as variáveis de saída (`OutputJson`).
- [ ] Integrar feedback visual no Canvas do ReactFlow: Mudar as cores dos nós no canvas para verde (sucesso) ou vermelho (falha) com base nos passos carregados da execução selecionada.

### 3. Fetching Contínuo (Polling / React Query)
- [ ] Usar `useQuery` (do TanStack Query) com `refetchInterval` para atualizar os dados de uma execução enquanto o status dela for `Running`.
- [ ] Sincronizar o estado da store para atualizar as cores dos nós no Canvas sempre que a execução em andamento atualizar seu progresso.

## Integração / Arquivos Afetados
- `frontend/src/components/workflows/WorkflowBuilder.tsx`: Inclusão de um novo estado de UI para o painel de execuções.
- `frontend/src/components/workflows/ExecutionHistoryPanel.tsx` (Novo componente).
- `frontend/src/hooks/useWorkflows.ts`: Adicionar/verificar funções para buscar a lista e detalhes das execuções.
- `frontend/src/store/useWorkflowStore.ts`: Adicionar estado temporário (ex: `executionMode`) para alterar o estilo dos nós dinamicamente durante o "Run".

## Critérios de Aceite
- Quando o usuário clicar em "Run", o painel de execuções deve abrir automaticamente focando na nova execução.
- O painel deve atualizar sozinho (via polling ou SignalR futuro) mostrando o progresso (ex: "Passo 1 Finalizado", "Passo 2 Rodando").
- O diagrama visual deve refletir os mesmos status através de contornos/cores.
