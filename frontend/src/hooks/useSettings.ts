import { useState, useEffect, useCallback } from 'react'
import { settingsApi } from '@/lib/api'
import type { SystemSettings, GatewaySettings, MemorySettings } from '@/types/api'

export function useSettings() {
  const [settings, setSettings] = useState<SystemSettings | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)

  const refresh = useCallback(async () => {
    try {
      setError(null)
      setLoading(true)
      const data = await settingsApi.getAll()
      setSettings(data)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erro ao carregar configurações')
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => { refresh() }, [refresh])

  const saveGateway = useCallback(async (s: GatewaySettings) => {
    setSaving(true)
    try {
      const updated = await settingsApi.updateGateway(s)
      setSettings(prev => prev ? { ...prev, gateway: updated } : null)
    } finally {
      setSaving(false)
    }
  }, [])

  const saveMemory = useCallback(async (s: MemorySettings) => {
    setSaving(true)
    try {
      const updated = await settingsApi.updateMemory(s)
      setSettings(prev => prev ? { ...prev, memory: updated } : null)
    } finally {
      setSaving(false)
    }
  }, [])

  return { settings, loading, error, saving, refresh, saveGateway, saveMemory }
}
