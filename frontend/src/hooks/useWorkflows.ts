import { useState, useEffect, useCallback } from 'react'
import { useQuery } from '@tanstack/react-query'
import { workflowApi } from '@/lib/api'
import type { WorkflowDefinitionSummary, WorkflowDefinition, WorkflowExecution } from '@/types/api'

export function useWorkflows() {
  const [workflows, setWorkflows] = useState<WorkflowDefinitionSummary[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  const refresh = useCallback(async () => {
    try {
      setLoading(true)
      setError(null)
      const data = await workflowApi.listDefinitions()
      setWorkflows(data)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erro ao carregar workflows')
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    refresh()
  }, [refresh])

  const getWorkflow = async (id: string) => {
    return await workflowApi.getDefinition(id)
  }

  const saveWorkflow = async (definition: WorkflowDefinition) => {
    try {
      const saved = await workflowApi.saveDefinition(definition)
      await refresh()
      return saved
    } catch (err) {
      throw new Error('Falha ao salvar workflow')
    }
  }

  const deleteWorkflow = async (id: string) => {
    try {
      await workflowApi.deleteDefinition(id)
      await refresh()
    } catch (err) {
      throw new Error('Falha ao deletar workflow')
    }
  }

  const executeWorkflow = async (id: string) => {
    try {
      return await workflowApi.startWorkflow(id)
    } catch (err) {
      throw new Error('Falha ao iniciar execução do workflow')
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
    loading,
    error,
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
  return useQuery({
    queryKey: ['workflow-execution', executionId],
    queryFn: () => executionId ? workflowApi.getExecution(executionId) : Promise.resolve(null),
    enabled: !!executionId,
    refetchInterval: (query) => {
      // Poll every 2 seconds if running
      const data = query.state.data as WorkflowExecution | null
      return data?.status === 0 ? 2000 : false
    }
  })
}
