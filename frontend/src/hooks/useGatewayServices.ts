import { useState, useEffect, useCallback } from 'react'
import { gatewayApi } from '@/lib/api'
import type { ServiceStatus } from '@/types/api'

export function useGatewayServices() {
  const [services, setServices] = useState<ServiceStatus[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  const refresh = useCallback(async () => {
    try {
      setError(null)
      setLoading(true)
      const data = await gatewayApi.services()
      setServices(data)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erro ao carregar serviços')
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => { refresh() }, [refresh])

  const toggleService = useCallback(async (name: string, enable: boolean) => {
    if (enable) {
      await gatewayApi.enable(name)
    } else {
      await gatewayApi.disable(name)
    }
    setServices(prev =>
      prev.map(s => s.name === name ? { ...s, isEnabled: enable } : s)
    )
  }, [])

  return { services, loading, error, refresh, toggleService }
}
