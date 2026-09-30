import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { agentApi } from '@/lib/api'
import type { AgentSpecification } from '@/types/api'

export function useAgents() {
  const queryClient = useQueryClient()

  const { data: agents = [], isLoading: loading, error } = useQuery({
    queryKey: ['agents'],
    queryFn: () => agentApi.listAll()
  })

  const createAgentMutation = useMutation({
    mutationFn: (spec: AgentSpecification) => agentApi.create(spec),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['agents'] })
  })

  const updateAgentMutation = useMutation({
    mutationFn: ({ name, spec }: { name: string; spec: AgentSpecification }) => 
      agentApi.update(name, spec),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['agents'] })
  })

  const deleteAgentMutation = useMutation({
    mutationFn: (name: string) => agentApi.delete(name),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['agents'] })
  })

  return { 
    agents, 
    loading, 
    error: error ? (error as Error).message : null, 
    refresh: () => queryClient.invalidateQueries({ queryKey: ['agents'] }), 
    createAgent: createAgentMutation.mutateAsync, 
    updateAgent: (name: string, spec: AgentSpecification) => updateAgentMutation.mutateAsync({ name, spec }), 
    deleteAgent: deleteAgentMutation.mutateAsync 
  }
}
