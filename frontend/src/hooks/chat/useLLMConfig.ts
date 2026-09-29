import { useState, useCallback, useEffect } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'
import { llmApi } from '@/lib/api'
import { useAuthStore } from '@/store/authStore'
import { useKnowledgeStore } from '@/store/useKnowledgeStore'
import type { LLMProviderInfo } from '@/types/api'

type Selection = { tenantId: string; provider: string; model: string }

export function useLLMConfig() {
  const queryClient = useQueryClient()
  const tenantId = useKnowledgeStore(state => state.activeWorkspaceId)
  const userId = useAuthStore(state => state.principalId ?? state.user?.id ?? null)
  const [pending, setPending] = useState<Selection | null>(null)

  const { data: catalog, refetch: refreshAiConfiguration } = useQuery({
    queryKey: ['chat-configuration', tenantId, userId],
    queryFn: llmApi.chatConfiguration,
    enabled: Boolean(tenantId && userId),
  })

  const providers: LLMProviderInfo[] = (catalog?.providers ?? []).map(item => ({
    name: item.name,
    defaultModel: item.defaultModel,
    models: item.models,
    isEnabled: true,
    isAvailable: true,
    isDefault: item.name === catalog?.defaultProvider,
    hasApiKey: false,
    priority: 0,
  }))
  const preferredProvider = catalog?.preferredProvider ?? catalog?.defaultProvider ?? ''
  const selectedProvider = pending?.tenantId === tenantId ? pending.provider : preferredProvider
  const provider = providers.find(item => item.name === selectedProvider) ?? providers[0]
  const selectedModel = pending?.tenantId === tenantId ? pending.model
    : catalog?.preferredProvider === provider?.name && catalog?.preferredModel
      ? catalog.preferredModel : provider?.defaultModel ?? ''

  const save = useCallback(async (providerName: string, modelName: string) => {
    setPending({ tenantId, provider: providerName, model: modelName })
    try {
      await llmApi.saveChatSelection(providerName, modelName)
      await queryClient.invalidateQueries({ queryKey: ['chat-configuration', tenantId, userId] })
      setPending(null)
    } catch (error) {
      setPending(null)
      toast.error(error instanceof Error ? error.message : 'Não foi possível salvar a seleção de modelo')
    }
  }, [tenantId, userId, queryClient])

  const setSelectedProvider = useCallback((providerName: string) => {
    const next = providers.find(item => item.name === providerName)
    if (next) void save(next.name, next.defaultModel || next.models[0] || '')
  }, [providers, save])

  const setSelectedModel = useCallback((modelName: string) => {
    if (provider && (provider.models.includes(modelName) || provider.defaultModel === modelName))
      void save(provider.name, modelName)
  }, [provider, save])

  const setSessionSelection = useCallback((providerName?: string, modelName?: string) => {
    if (providerName && modelName)
      setPending({ tenantId, provider: providerName, model: modelName })
    else
      setPending(null)
  }, [tenantId])

  useEffect(() => {
    const handleRefresh = () => {
      queryClient.invalidateQueries({ queryKey: ['chat-configuration'] })
    }
    window.addEventListener('agentic:llm-config-updated', handleRefresh)
    return () => window.removeEventListener('agentic:llm-config-updated', handleRefresh)
  }, [queryClient])

  return { providers, selectedProvider, selectedModel, canManageTenant: catalog?.canManageTenant ?? false,
    setSelectedProvider, setSelectedModel,
    setSessionSelection, refreshAiConfiguration }
}
