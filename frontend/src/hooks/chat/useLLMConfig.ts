import { useState, useCallback, useEffect } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { llmApi } from '@/lib/api'
import type { LLMProviderInfo } from '@/types/api'

const ProviderStorageKey = 'agentic.chat.provider'
const ModelStorageKey = 'agentic.chat.model'

export function useLLMConfig() {
  const queryClient = useQueryClient()
  const [selectedProvider, setSelectedProviderState] = useState('')
  const [selectedModel, setSelectedModelState] = useState('')

  const { data: providers = [], refetch: refreshAiConfiguration } = useQuery({
    queryKey: ['llm-configuration'],
    queryFn: async () => {
      const configuration = await llmApi.configuration()
      const enabledProviders = configuration.providers.filter(p => p.isEnabled)
      
      // Initial selection logic
      if (enabledProviders.length > 0 && !selectedProvider) {
        const preferredProvider = window.localStorage.getItem(ProviderStorageKey)
        const nextProvider = resolveProvider(enabledProviders, preferredProvider, configuration.defaultProvider)
        const preferredModel = window.localStorage.getItem(ModelStorageKey)
        const nextModel = resolveModel(
          nextProvider,
          preferredModel,
          nextProvider.name === configuration.defaultProvider ? configuration.defaultModel : nextProvider.defaultModel,
        )

        setSelectedProviderState(nextProvider.name)
        setSelectedModelState(nextModel)
      }
      
      return enabledProviders
    }
  })

  const setSelectedProvider = useCallback((providerName: string) => {
    const provider = providers.find(item => item.name === providerName)
    if (!provider) return

    const nextModel = resolveModel(provider, undefined, provider.defaultModel)
    setSelectedProviderState(provider.name)
    setSelectedModelState(nextModel)
    persistAiSelection(provider.name, nextModel)
  }, [providers])

  const setSelectedModel = useCallback((modelName: string) => {
    setSelectedModelState(modelName)
    persistAiSelection(selectedProvider, modelName)
  }, [selectedProvider])

  // Sync with global updates
  useEffect(() => {
    const handleRefresh = () => {
      queryClient.invalidateQueries({ queryKey: ['llm-configuration'] })
    }
    window.addEventListener('agentic:llm-config-updated', handleRefresh)
    return () => window.removeEventListener('agentic:llm-config-updated', handleRefresh)
  }, [queryClient])

  return {
    providers,
    selectedProvider,
    selectedModel,
    setSelectedProvider,
    setSelectedModel,
    refreshAiConfiguration
  }
}

function resolveProvider(providers: LLMProviderInfo[], preferredProvider?: string | null, defaultProvider?: string) {
  return providers.find(provider => provider.name === preferredProvider)
    ?? providers.find(provider => provider.name === defaultProvider)
    ?? providers[0]
}

function resolveModel(provider: LLMProviderInfo, preferredModel?: string | null, defaultModel?: string) {
  return provider.models.find(model => model === preferredModel)
    ?? provider.models.find(model => model === defaultModel)
    ?? provider.defaultModel
    ?? provider.models[0]
    ?? ''
}

function persistAiSelection(providerName: string, modelName: string) {
  if (!providerName) return
  window.localStorage.setItem(ProviderStorageKey, providerName)
  if (modelName) {
    window.localStorage.setItem(ModelStorageKey, modelName)
  }
}
