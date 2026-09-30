import { useQuery, useQueryClient } from '@tanstack/react-query'
import { pluginApi } from '@/lib/api'
import type { PluginSummary, LoadPluginRequest, MCPToolInfo } from '@/types/api'

export function usePlugins() {
  const queryClient = useQueryClient()
  const queryKey = ['plugins']
  const query = useQuery({
    queryKey,
    queryFn: async () => {
      const [plugins, allTools] = await Promise.all([pluginApi.list(), pluginApi.tools()])
      return { plugins, allTools }
    },
  })
  const plugins = query.data?.plugins ?? []
  const allTools = query.data?.allTools ?? []
  const refresh = () => query.refetch()

  const loadPlugin = async (req: LoadPluginRequest) => {
    const loaded = await pluginApi.load(req)
    queryClient.setQueryData(queryKey, (prev: { plugins: PluginSummary[]; allTools: MCPToolInfo[] } | undefined) => ({
      plugins: [...(prev?.plugins ?? []), loaded],
      allTools: prev?.allTools ?? [],
    }))
    return loaded
  }

  const deletePlugin = async (id: string) => {
    await pluginApi.delete(id)
    queryClient.setQueryData(queryKey, (prev: { plugins: PluginSummary[]; allTools: MCPToolInfo[] } | undefined) => ({
      plugins: (prev?.plugins ?? []).filter(plugin => plugin.id !== id),
      allTools: prev?.allTools ?? [],
    }))
  }

  const updatePlugin = async (id: string, req: LoadPluginRequest) => {
    await pluginApi.update(id, req)
    await refresh()
  }

  const getPluginDetails = async (id: string) => {
    return await pluginApi.get(id)
  }

  return {
    plugins,
    allTools,
    loading: query.isLoading,
    error: query.error instanceof Error ? query.error.message : query.error ? String(query.error) : null,
    refresh,
    loadPlugin,
    deletePlugin,
    updatePlugin,
    getPluginDetails,
  }
}
