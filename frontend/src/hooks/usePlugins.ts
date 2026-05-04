import { useState, useEffect, useCallback } from 'react'
import { pluginApi } from '@/lib/api'
import type { PluginSummary, LoadPluginRequest, MCPToolInfo } from '@/types/api'

export function usePlugins() {
  const [plugins, setPlugins] = useState<PluginSummary[]>([])
  const [allTools, setAllTools] = useState<MCPToolInfo[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  const refresh = useCallback(async () => {
    try {
      setError(null)
      setLoading(true)
      const [pluginData, toolData] = await Promise.all([
        pluginApi.list(),
        pluginApi.tools(),
      ])
      setPlugins(pluginData)
      setAllTools(toolData)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erro ao carregar plugins')
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => { refresh() }, [refresh])

  const loadPlugin = useCallback(async (req: LoadPluginRequest) => {
    const loaded = await pluginApi.load(req)
    setPlugins(prev => [...prev, loaded])
    return loaded
  }, [])

  const deletePlugin = useCallback(async (id: string) => {
    await pluginApi.delete(id)
    setPlugins(prev => prev.filter(p => p.id !== id))
  }, [])

  return { plugins, allTools, loading, error, refresh, loadPlugin, deletePlugin }
}
