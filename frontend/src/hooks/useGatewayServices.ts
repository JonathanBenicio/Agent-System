import { useQuery, useQueryClient } from '@tanstack/react-query'
import { gatewayApi } from '@/lib/api'
import type { ServiceStatus } from '@/types/api'

export function useGatewayServices() {
  const queryClient = useQueryClient()
  const queryKey = ['gateway-services']
  const query = useQuery({ queryKey, queryFn: () => gatewayApi.services() })

  const toggleService = async (name: string, enable: boolean) => {
    if (enable) {
      await gatewayApi.enable(name)
    } else {
      await gatewayApi.disable(name)
    }
    queryClient.setQueryData<ServiceStatus[]>(queryKey, prev =>
      (prev ?? []).map(s => s.name === name ? { ...s, isEnabled: enable } : s)
    )
  }

  return {
    services: query.data ?? [],
    loading: query.isLoading,
    error: query.error instanceof Error ? query.error.message : query.error ? String(query.error) : null,
    refresh: () => query.refetch(),
    toggleService,
  }
}
