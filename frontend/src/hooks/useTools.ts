import { useQuery, useQueryClient } from '@tanstack/react-query'
import { toolApi } from '@/lib/api'
import type { ToolSummary, ToolInput, ToolResult } from '@/types/api'

export function useTools() {
  const queryClient = useQueryClient()
  const queryKey = ['tools']
  const query = useQuery({ queryKey, queryFn: () => toolApi.list() })
  const tools = query.data ?? []

  const executeTool = async (id: string, input: ToolInput): Promise<ToolResult> => {
    return toolApi.execute(id, input)
  }

  const deleteTool = async (id: string) => {
    await toolApi.delete(id)
    queryClient.setQueryData<ToolSummary[]>(queryKey, prev => (prev ?? []).filter(tool => tool.id !== id))
  }

  return {
    tools,
    loading: query.isLoading,
    error: query.error instanceof Error ? query.error.message : query.error ? String(query.error) : null,
    refresh: () => query.refetch(),
    executeTool,
    deleteTool,
  }
}
