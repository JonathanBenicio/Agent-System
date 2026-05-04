import { useState, useEffect, useCallback } from 'react'
import { skillApi } from '@/lib/api'
import type { SkillSummary } from '@/types/api'

export function useSkills() {
  const [skills, setSkills] = useState<SkillSummary[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  const refresh = useCallback(async () => {
    try {
      setError(null)
      setLoading(true)
      const data = await skillApi.listAll()
      setSkills(data)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erro ao carregar skills')
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => { refresh() }, [refresh])

  const deleteSkill = useCallback(async (id: string) => {
    await skillApi.delete(id)
    setSkills(prev => prev.filter(s => s.id !== id))
  }, [])

  return { skills, loading, error, refresh, deleteSkill }
}
