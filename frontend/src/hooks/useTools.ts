import { useState, useEffect, useCallback } from 'react'
import { toolApi } from '@/lib/api'
import type { ToolSummary, ToolInput, ToolResult } from '@/types/api'

export function useTools() {
  const [tools, setTools] = useState<ToolSummary[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  const refresh = useCallback(async () => {
    try {
      setError(null)
      setLoading(true)
      const data = await toolApi.list()
      setTools(data)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erro ao carregar tools')
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => { refresh() }, [refresh])

  const executeTool = useCallback(async (id: string, input: ToolInput): Promise<ToolResult> => {
    return toolApi.execute(id, input)
  }, [])

  const deleteTool = useCallback(async (id: string) => {
    await toolApi.delete(id)
    setTools(prev => prev.filter(t => t.id !== id))
  }, [])

  return { tools, loading, error, refresh, executeTool, deleteTool }
}
