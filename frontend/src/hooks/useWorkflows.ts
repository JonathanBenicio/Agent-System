import { useQuery, useQueryClient } from '@tanstack/react-query'
import { workflowApi } from '@/lib/api'
import { useKnowledgeStore } from '@/store/useKnowledgeStore'
import type { WorkflowDefinition, WorkflowExecution } from '@/types/api'

export function useWorkflows() {
  const queryClient = useQueryClient()
  const tenantId = useKnowledgeStore(state => state.activeWorkspaceId)
  const queryKey = ['workflow-definitions', tenantId]
  const query = useQuery({
    queryKey,
    queryFn: () => workflowApi.listDefinitions(),
    enabled: Boolean(tenantId),
  })
  const workflows = query.data ?? []
  const refresh = () => query.refetch()

  const getWorkflow = async (id: string) => {
    return await workflowApi.getDefinition(id)
  }

  const saveWorkflow = async (definition: WorkflowDefinition) => {
    try {
      const saved = await workflowApi.saveDefinition(definition)
      await queryClient.invalidateQueries({ queryKey })
      return saved
    } catch (err) {
      throw new Error('Falha ao salvar workflow', { cause: err })
    }
  }

  const deleteWorkflow = async (id: string) => {
    try {
      await workflowApi.deleteDefinition(id)
      await queryClient.invalidateQueries({ queryKey })
    } catch (err) {
      throw new Error('Falha ao deletar workflow', { cause: err })
    }
  }

  const executeWorkflow = async (id: string) => {
    try {
      return await workflowApi.startWorkflow(id)
    } catch (err) {
      throw new Error('Falha ao iniciar execução do workflow', { cause: err })
    }
  }

  const listExecutions = async () => {
    return await workflowApi.listExecutions()
  }

  const getExecution = async (id: string) => {
    return await workflowApi.getExecution(id)
  }

  return {
    workflows,
    loading: query.isLoading,
    error: query.error instanceof Error ? query.error.message : query.error ? String(query.error) : null,
    refresh,
    getWorkflow,
    saveWorkflow,
    deleteWorkflow,
    executeWorkflow,
    listExecutions,
    getExecution,
  }
}

export function useWorkflowExecution(executionId: string | null) {
  const tenantId = useKnowledgeStore(state => state.activeWorkspaceId)
  return useQuery({
    queryKey: ['workflow-execution', tenantId, executionId],
    queryFn: () => executionId ? workflowApi.getExecution(executionId) : Promise.resolve(null),
    enabled: Boolean(tenantId && executionId),
    refetchInterval: (query) => {
      // Poll every 2 seconds if running
      const data = query.state.data as WorkflowExecution | null
      return data?.status === 0 ? 2000 : false
    }
  })
}
