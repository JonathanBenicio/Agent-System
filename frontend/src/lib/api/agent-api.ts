import { get, post, put, del } from './client'
import type {
  AgentInfo,
  AgentSpecification,
  AgentVersion,
  YamlValidationResult,
  ToolSummary,
  ToolInput,
  ToolResult,
  SkillSummary,
  SkillContent
} from '@/types/api'

export const agentApi = {
  list: () => get<AgentInfo[]>('/api/agent/agents'),
  listAll: () => get<AgentInfo[]>('/api/agent/agents/all'),
  listByTier: (tier: number) => get<AgentInfo[]>(`/api/agent/agents/tier/${tier}`),
  get: (name: string) => get<AgentInfo>(`/api/agent/agents/${encodeURIComponent(name)}`),
  create: (spec: AgentSpecification) => post<AgentInfo>('/api/agent/agents', spec),
  update: (name: string, spec: AgentSpecification) =>
    put<AgentInfo>(`/api/agent/agents/${encodeURIComponent(name)}`, spec),
  delete: (name: string) => del(`/api/agent/agents/${encodeURIComponent(name)}`),
  validateYaml: (yaml: string) => post<YamlValidationResult>('/api/agent/agents/validate-yaml', { yaml }),
  saveYaml: (yaml: string) => post<{ agent: AgentInfo; version?: AgentVersion }>('/api/agent/agents/save-yaml', { yaml }),
  getHistory: (name: string, limit?: number) => get<AgentVersion[]>(`/api/agent/agents/${encodeURIComponent(name)}/history${limit ? `?limit=${limit}` : ''}`),
  rollback: (name: string, versionId: string) => post<{ message: string; agent: AgentInfo; version: AgentVersion }>(`/api/agent/agents/${encodeURIComponent(name)}/rollback/${encodeURIComponent(versionId)}`),
  getRooms: (name: string) => get<string[]>(`/api/agent/agents/${encodeURIComponent(name)}/rooms`),
  setRooms: (name: string, roomIds: string[]) => put<void>(`/api/agent/agents/${encodeURIComponent(name)}/rooms`, roomIds),
}

export const toolApi = {
  list: (category?: string) =>
    get<ToolSummary[]>(`/api/agent/tools${category ? `?category=${encodeURIComponent(category)}` : ''}`),
  get: (id: string) => get<ToolSummary>(`/api/agent/tools/${encodeURIComponent(id)}`),
  execute: (id: string, input: ToolInput) =>
    post<ToolResult>(`/api/agent/tools/${encodeURIComponent(id)}/execute`, input),
  delete: (id: string) => del(`/api/agent/tools/${encodeURIComponent(id)}`),
}

export const skillApi = {
  listAll: () => get<SkillSummary[]>('/api/agent/skills/all'),
  list: (agent?: string, domain?: string) => {
    const params = new URLSearchParams()
    if (agent) params.set('agent', agent)
    if (domain) params.set('domain', domain)
    const qs = params.toString()
    return get<SkillContent[]>(`/api/agent/skills${qs ? `?${qs}` : ''}`)
  },
  delete: (id: string) => del(`/api/agent/skills/${encodeURIComponent(id)}`),
}
