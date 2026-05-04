import { useState, useEffect, useCallback } from 'react'
import { llmApi } from '@/lib/api'
import type { LLMProviderInfo, UpdateProviderRequest } from '@/types/api'

export function useLLMProviders() {
  const [providers, setProviders] = useState<LLMProviderInfo[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  const refresh = useCallback(async () => {
    try {
      setError(null)
      setLoading(true)
      const data = await llmApi.providers()
      setProviders(data)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erro ao carregar providers')
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => { refresh() }, [refresh])

  const updateProvider = useCallback(async (name: string, req: UpdateProviderRequest) => {
    const updated = await llmApi.update(name, req)
    setProviders(prev => prev.map(p => p.name === name ? updated : p))
    return updated
  }, [])

  const testProvider = useCallback(async (name: string) => {
    return llmApi.test(name)
  }, [])

  return { providers, loading, error, refresh, updateProvider, testProvider }
}
