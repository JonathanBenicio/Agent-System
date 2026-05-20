import { useState, useCallback, useEffect } from 'react'
import { llmApi } from '@/lib/api'
import type {
  LLMProviderApiKey,
  RegisterApiKeyRequest,
  UpdateApiKeyRequest,
} from '@/types/api'

export function useLLMProviderApiKeys(providerName: string) {
  const [keys, setKeys] = useState<LLMProviderApiKey[]>([])
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const fetchKeys = useCallback(async () => {
    if (!providerName) return
    try {
      setLoading(true)
      setError(null)
      const data = await llmApi.getKeys(providerName)
      setKeys(data)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erro ao carregar chaves')
    } finally {
      setLoading(false)
    }
  }, [providerName])

  useEffect(() => {
    fetchKeys()
  }, [fetchKeys])

  const registerKey = useCallback(async (req: RegisterApiKeyRequest) => {
    const key = await llmApi.registerKey(providerName, req)
    setKeys(prev => [...prev, key])
    return key
  }, [providerName])

  const updateKey = useCallback(async (id: string, req: UpdateApiKeyRequest) => {
    const updated = await llmApi.updateKey(providerName, id, req)
    setKeys(prev => prev.map(k => k.id === id ? updated : k))
    return updated
  }, [providerName])

  const deleteKey = useCallback(async (id: string) => {
    await llmApi.deleteKey(providerName, id)
    setKeys(prev => prev.filter(k => k.id !== id))
  }, [providerName])

  const setDefaultKey = useCallback(async (id: string) => {
    await llmApi.setDefaultKey(providerName, id)
    // Refresh list to update all default flags
    await fetchKeys()
  }, [providerName, fetchKeys])

  const testKey = useCallback(async (id: string) => {
    return llmApi.testKey(providerName, id)
  }, [providerName])

  const discoverModelsForKey = useCallback(async (id: string) => {
    const res = await llmApi.discoverModelsForKey(providerName, id)
    if (res.success) {
      await fetchKeys() // Fetch again to get updated models
    }
    return res
  }, [providerName, fetchKeys])

  return {
    keys,
    loading,
    error,
    refresh: fetchKeys,
    registerKey,
    updateKey,
    deleteKey,
    setDefaultKey,
    testKey,
    discoverModelsForKey,
  }
}
