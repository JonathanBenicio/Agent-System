import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { knowledgeRoomApi } from '@/lib/api'
import type { KnowledgeRoom } from '@/types/api'
import { useKnowledgeStore } from '@/store/useKnowledgeStore'

export function useKnowledgeRooms() {
  const queryClient = useQueryClient()
  const [activeRoomId, setActiveRoomId] = useState<string | null>(null)
  const activeWorkspaceId = useKnowledgeStore(state => state.activeWorkspaceId)
  const setActiveWorkspaceId = useKnowledgeStore(state => state.setActiveWorkspace)

  const { data: rooms = [], isLoading: loading, error } = useQuery({
    queryKey: ['knowledge-rooms'],
    queryFn: () => knowledgeRoomApi.list(),
  })

  const createMutation = useMutation({
    mutationFn: (room: Partial<KnowledgeRoom>) => knowledgeRoomApi.create(room),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['knowledge-rooms'] })
    },
  })

  const updateMutation = useMutation({
    mutationFn: ({ id, data }: { id: string; data: Partial<KnowledgeRoom> }) => 
      knowledgeRoomApi.update(id, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['knowledge-rooms'] })
    },
  })

  const deleteMutation = useMutation({
    mutationFn: (id: string) => knowledgeRoomApi.delete(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['knowledge-rooms'] })
    },
  })

  return {
    rooms,
    loading,
    error: error?.message || null,
    activeRoomId,
    activeWorkspaceId,
    setActiveRoom: setActiveRoomId,
    setActiveWorkspace: setActiveWorkspaceId,
    createRoom: createMutation.mutateAsync,
    updateRoom: (id: string, data: Partial<KnowledgeRoom>) => updateMutation.mutateAsync({ id, data }),
    deleteRoom: async (id: string) => {
      await deleteMutation.mutateAsync(id);
      if (activeRoomId === id) setActiveRoomId(null);
    },
  }
}
