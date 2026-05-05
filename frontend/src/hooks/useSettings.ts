import { useState, useEffect, useCallback } from 'react'
import { settingsApi } from '@/lib/api'
import type { SystemSettings, GatewaySettings, MemorySettings, RerankingSettings } from '@/types/api'

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
      return updated
    } finally {
      setSaving(false)
    }
  }, [])

  const saveMemory = useCallback(async (s: MemorySettings) => {
    setSaving(true)
    try {
      const updated = await settingsApi.updateMemory(s)
      setSettings(prev => prev ? { ...prev, memory: updated } : null)
      return updated
    } finally {
      setSaving(false)
    }
  }, [])

  const saveReranking = useCallback(async (s: RerankingSettings) => {
    setSaving(true)
    try {
      const updated = await settingsApi.updateReranking(s)
      setSettings(prev => prev ? { ...prev, reranking: updated } : null)
      return updated
    } finally {
      setSaving(false)
    }
  }, [])

  const uploadRerankingAssets = useCallback(async (modelFile?: File, vocabularyFile?: File, packageFile?: File) => {
    setSaving(true)
    try {
      const updated = await settingsApi.uploadRerankingAssets(modelFile, vocabularyFile, packageFile)
      setSettings(prev => prev ? { ...prev, reranking: updated } : null)
      return updated
    } finally {
      setSaving(false)
    }
  }, [])

  return { settings, loading, error, saving, refresh, saveGateway, saveMemory, saveReranking, uploadRerankingAssets }
}
