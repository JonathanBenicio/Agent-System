import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useEffect, useCallback } from 'react'
import { sessionApi } from '@/lib/api'
import { getConnection } from '@/lib/signalr'
import type { ChatMessageDto } from '@/types/api'
import type { ChatMessage } from '@/types/chat'

export function useSessions(search?: string) {
  const queryClient = useQueryClient()

  const { data: sessions = [], isLoading, error } = useQuery({
    queryKey: ['sessions', search],
    queryFn: () => sessionApi.list(50, search),
  })

  const deleteMutation = useMutation({
    mutationFn: (id: string) => sessionApi.delete(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['sessions'] })
  })

  const renameMutation = useMutation({
    mutationFn: ({ id, title }: { id: string; title: string }) => sessionApi.updateTitle(id, title),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['sessions'] })
  })

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

  // Reactive updates via SignalR
  useEffect(() => {
    const conn = getConnection()
    
    const handleUpdate = () => {
      queryClient.invalidateQueries({ queryKey: ['sessions'] })
    }

    conn.on('SessionCreated', handleUpdate)
    conn.on('SessionDeleted', handleUpdate)
    conn.on('SessionUpdated', handleUpdate)

    return () => {
      conn.off('SessionCreated', handleUpdate)
      conn.off('SessionDeleted', handleUpdate)
      conn.off('SessionUpdated', handleUpdate)
    }
  }, [queryClient])

  return {
    sessions,
    isLoading,
    error: error ? (error as Error).message : null,
    refresh: () => queryClient.invalidateQueries({ queryKey: ['sessions'] }),
    loadSessionMessages,
    deleteSession: deleteMutation.mutateAsync,
    renameSession: (id: string, title: string) => renameMutation.mutateAsync({ id, title }),
  }
}
