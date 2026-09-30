import { useQuery, useQueryClient } from '@tanstack/react-query'
import { llmApi } from '@/lib/api'
import type {
  LLMConfigurationInfo,
  UpdateDefaultLlmSelectionRequest,
  UpdateProviderRequest,
} from '@/types/api'

export function useLLMProviders() {
  const queryClient = useQueryClient()
  const queryKey = ['llm-configuration']
  const query = useQuery({ queryKey, queryFn: () => llmApi.configuration() })
  const configuration = query.data ?? null
  const providers = configuration?.providers ?? []
  const refresh = () => query.refetch()

  const updateProvider = async (name: string, req: UpdateProviderRequest) => {
    const updated = await llmApi.update(name, req)
    queryClient.setQueryData<LLMConfigurationInfo>(queryKey, prev => prev ? {
      ...prev,
      providers: prev.providers.map(p => p.name === name ? updated : p),
    } : prev)
    return updated
  }

  const updateDefaultSelection = async (req: UpdateDefaultLlmSelectionRequest) => {
    const updated = await llmApi.updateDefaultSelection(req)
    queryClient.setQueryData(queryKey, updated)
    return updated
  }

  const testProvider = async (name: string) => {
    return llmApi.test(name)
  }

  const discoverModels = async (name: string, apiKey: string) => {
    return llmApi.discoverModels(name, apiKey)
  }

  const syncQuotas = async () => {
    await llmApi.syncQuotas()
    await refresh()
  }

  return {
    providers,
    configuration,
    defaultProvider: configuration?.defaultProvider ?? '',
    defaultModel: configuration?.defaultModel ?? '',
    loading: query.isLoading,
    error: query.error instanceof Error ? query.error.message : query.error ? String(query.error) : null,
    refresh,
    updateProvider,
    updateDefaultSelection,
    testProvider,
    discoverModels,
    syncQuotas,
  }
}
