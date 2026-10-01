import { get, post, postForm, put, del } from './client'
import type {
  AgentInfo,
  AgentSpecification,
  AgentVersion,
  YamlValidationResult,
  ToolSummary,
  ToolInput,
  ToolResult,
  SkillSummary
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
  setEnabled: (id: string, enabled: boolean) =>
    put<{ id: string; isEnabled: boolean }>(`/api/agent/skills/${encodeURIComponent(id)}/enabled`, { enabled }),
  get: (id: string) => get<SkillSummary & { systemPrompt?: string; examples?: string; metadata?: Record<string, string> }>(`/api/agent/skills/${encodeURIComponent(id)}`),
  create: (data: { id: string; name: string; domain: string; type: string; systemPromptFragment: string; fewShotExamples?: string; metadata?: Record<string, string> }) => 
    post<SkillSummary>('/api/agent/skills', data),
  update: (id: string, data: { name?: string; domain?: string; systemPromptFragment?: string; fewShotExamples?: string; metadata?: Record<string, string> }) => 
    put<SkillSummary>(`/api/agent/skills/${encodeURIComponent(id)}`, data),
  delete: (id: string) => del(`/api/agent/skills/${encodeURIComponent(id)}`),
  upload: (file: File) => {
    const formData = new FormData()
    formData.append('file', file)
    return postForm<{ message: string; id: string }>('/api/agent/skills/upload', formData)
  },
  brainstorm: (description: string) => 
    post<{ suggestedId: string; suggestedName: string; systemPromptFragment: string; fewShotExamples?: string }>('/api/agent/skills/brainstorm', { description })
}
