import { useState, useEffect, useCallback } from 'react'
import { agentApi } from '@/lib/api'
import type { AgentInfo, AgentSpecification } from '@/types/api'

export function useAgents() {
  const [agents, setAgents] = useState<AgentInfo[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  const refresh = useCallback(async () => {
    try {
      setError(null)
      setLoading(true)
      const data = await agentApi.listAll()
      setAgents(data)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erro ao carregar agents')
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => { refresh() }, [refresh])

  const createAgent = useCallback(async (spec: AgentSpecification) => {
    const created = await agentApi.create(spec)
    setAgents(prev => [...prev, created])
    return created
  }, [])

  const updateAgent = useCallback(async (name: string, spec: AgentSpecification) => {
    const updated = await agentApi.update(name, spec)
    setAgents(prev => prev.map(a => a.name === name ? updated : a))
    return updated
  }, [])

  const deleteAgent = useCallback(async (name: string) => {
    await agentApi.delete(name)
    setAgents(prev => prev.filter(a => a.name !== name))
  }, [])

  return { agents, loading, error, refresh, createAgent, updateAgent, deleteAgent }
}
