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


  // Fetch the tenant catalog on mount, then persist subsequent edits through explicit actions.
  // eslint-disable-next-line react-hooks/set-state-in-effect
  useEffect(() => { refresh() }, [refresh])

  const deleteSkill = useCallback(async (id: string) => {
    await skillApi.delete(id)
    setSkills(prev => prev.filter(s => s.id !== id))
  }, [])

  const setSkillEnabled = useCallback(async (id: string, enabled: boolean) => {
    await skillApi.setEnabled(id, enabled)
    setSkills(prev => prev.map(skill => skill.id === id ? { ...skill, isEnabled: enabled } : skill))
  }, [])

  const createSkill = useCallback(async (data: { id: string; name: string; domain: string; type: string; systemPromptFragment: string; fewShotExamples?: string; metadata?: Record<string, string> }) => {
    const newSkill = await skillApi.create(data)
    setSkills(prev => [...prev, newSkill])
    return newSkill
  }, [])

  const updateSkill = useCallback(async (id: string, data: { name?: string; domain?: string; systemPromptFragment?: string; fewShotExamples?: string; metadata?: Record<string, string> }) => {
    const updated = await skillApi.update(id, data)
    setSkills(prev => prev.map(s => s.id === id ? { ...s, ...updated } : s))
    return updated;
  }, [])

  const uploadSkill = useCallback(async (file: File) => {
    const res = await skillApi.upload(file)
    await refresh()
    return res
  }, [refresh])

  return { skills, loading, error, refresh, deleteSkill, setSkillEnabled, createSkill, updateSkill, uploadSkill }
}
