import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { settingsApi } from '@/lib/api'
import type { GatewaySettings, MemorySettings, RerankingSettings } from '@/types/api'

export function useSettings() {
  const queryClient = useQueryClient()

  const { data: settings, isLoading: loading, error } = useQuery({
    queryKey: ['settings'],
    queryFn: () => settingsApi.getAll(),
  })

  const updateGatewayMutation = useMutation({
    mutationFn: (s: GatewaySettings) => settingsApi.updateGateway(s),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['settings'] })
  })

  const updateMemoryMutation = useMutation({
    mutationFn: (s: MemorySettings) => settingsApi.updateMemory(s),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['settings'] })
  })

  const updateRerankingMutation = useMutation({
    mutationFn: (s: RerankingSettings) => settingsApi.updateReranking(s),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['settings'] })
  })

  const uploadAssetsMutation = useMutation({
    mutationFn: ({ modelFile, vocabularyFile, packageFile }: { modelFile?: File, vocabularyFile?: File, packageFile?: File }) => 
      settingsApi.uploadRerankingAssets(modelFile, vocabularyFile, packageFile),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['settings'] })
  })

  return {
    settings,
    loading,
    error: error ? (error as Error).message : null,
    saving: updateGatewayMutation.isPending || 
            updateMemoryMutation.isPending || 
            updateRerankingMutation.isPending || 
            uploadAssetsMutation.isPending,
    refresh: () => queryClient.invalidateQueries({ queryKey: ['settings'] }),
    saveGateway: updateGatewayMutation.mutateAsync,
    saveMemory: updateMemoryMutation.mutateAsync,
    saveReranking: updateRerankingMutation.mutateAsync,
    uploadRerankingAssets: (modelFile?: File, vocabularyFile?: File, packageFile?: File) => 
      uploadAssetsMutation.mutateAsync({ modelFile, vocabularyFile, packageFile })
  }
}
