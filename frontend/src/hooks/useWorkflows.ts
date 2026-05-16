import { useState, useEffect, useCallback } from 'react'
import { workflowApi } from '@/lib/api'
import type { WorkflowDefinitionSummary, WorkflowDefinition } from '@/types/api'

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

  return {
    workflows,
    loading,
    error,
    refresh,
    getWorkflow,
    saveWorkflow,
    deleteWorkflow,
    executeWorkflow,
  }
}
