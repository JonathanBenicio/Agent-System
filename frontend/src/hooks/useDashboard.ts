import { useState, useEffect, useCallback } from 'react'
import { gatewayApi } from '@/lib/api'
import type { GatewayDashboard } from '@/types/api'

export function useDashboard(pollInterval = 30000) {
  const [data, setData] = useState<GatewayDashboard | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  const refresh = useCallback(async () => {
    try {
      setError(null)
      const dashboard = await gatewayApi.dashboard()
      setData(dashboard)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erro ao carregar dashboard')
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    refresh()
    const id = setInterval(refresh, pollInterval)
    return () => clearInterval(id)
  }, [refresh, pollInterval])

  return { data, loading, error, refresh }
}
