import { useQuery } from '@tanstack/react-query'
import { gatewayApi } from '@/lib/api'

export function useDashboard(pollInterval = 30000) {
  const query = useQuery({
    queryKey: ['gateway-dashboard'],
    queryFn: () => gatewayApi.dashboard(),
    refetchInterval: pollInterval,
  })

  return {
    data: query.data ?? null,
    loading: query.isLoading,
    error: query.error instanceof Error ? query.error.message : query.error ? String(query.error) : null,
    refresh: () => query.refetch(),
  }
}
