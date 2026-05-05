import { useState, useEffect, useCallback } from 'react'
import { llmApi } from '@/lib/api'
import type {
  LLMConfigurationInfo,
  LLMProviderInfo,
  UpdateDefaultLlmSelectionRequest,
  UpdateProviderRequest,
} from '@/types/api'

export function useLLMProviders() {
  const [configuration, setConfiguration] = useState<LLMConfigurationInfo | null>(null)
  const [providers, setProviders] = useState<LLMProviderInfo[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  const refresh = useCallback(async () => {
    try {
      setError(null)
      setLoading(true)
      const data = await llmApi.configuration()
      setConfiguration(data)
      setProviders(data.providers)
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
    setConfiguration(prev => prev ? {
      ...prev,
      providers: prev.providers.map(p => p.name === name ? updated : p),
    } : prev)
    return updated
  }, [])

  const updateDefaultSelection = useCallback(async (req: UpdateDefaultLlmSelectionRequest) => {
    const updated = await llmApi.updateDefaultSelection(req)
    setConfiguration(updated)
    setProviders(updated.providers)
    return updated
  }, [])

  const testProvider = useCallback(async (name: string) => {
    return llmApi.test(name)
  }, [])

  return {
    providers,
    configuration,
    defaultProvider: configuration?.defaultProvider ?? '',
    defaultModel: configuration?.defaultModel ?? '',
    loading,
    error,
    refresh,
    updateProvider,
    updateDefaultSelection,
    testProvider,
  }
}
