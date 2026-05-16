import { useState, useCallback, useEffect } from 'react'
import { sessionApi } from '@/lib/api'
import type { SessionListItem, ChatMessageDto } from '@/types/api'
import type { ChatMessage } from '@/types/chat'

export function useSessions() {
  const [sessions, setSessions] = useState<SessionListItem[]>([])
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const loadSessions = useCallback(async () => {
    setIsLoading(true)
    setError(null)
    try {
      const data = await sessionApi.list(50)
      setSessions(data)
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to load sessions'
      setError(message)
    } finally {
      setIsLoading(false)
    }
  }, [])

  const loadSessionMessages = useCallback(async (id: string): Promise<ChatMessage[]> => {
    const messages = await sessionApi.messages(id)
    return messages.map((m: ChatMessageDto) => ({
      id: m.id,
      role: m.role as ChatMessage['role'],
      content: m.content,
      agentName: m.agentName,
      agentTier: m.agentTier,
      actions: m.actions,
      tools: m.tools,
      timestamp: m.timestamp,
    }))
  }, [])

  const deleteSession = useCallback(async (id: string) => {
    await sessionApi.delete(id)
    setSessions(prev => prev.filter(s => s.id !== id))
  }, [])

  const renameSession = useCallback(async (id: string, title: string) => {
    const result = await sessionApi.updateTitle(id, title)
    setSessions(prev => prev.map(s => s.id === id ? { ...s, title: result.title } : s))
  }, [])

  useEffect(() => {
    void loadSessions()
  }, [loadSessions])

  return {
    sessions,
    isLoading,
    error,
    refresh: loadSessions,
    loadSessionMessages,
    deleteSession,
    renameSession,
  }
}
