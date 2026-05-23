import { get, post, put, del } from './client'
import type {
  SessionListItem,
  SessionDetail,
  ChatMessageDto,
  AgentEvent
} from '@/types/api'

export const sessionApi = {
  list: (limit?: number, search?: string) => {
    const params = new URLSearchParams()
    if (limit) params.set('limit', limit.toString())
    if (search) params.set('search', search)
    const qs = params.toString()
    return get<SessionListItem[]>(`/api/session${qs ? `?${qs}` : ''}`)
  },
  get: (id: string) => get<SessionDetail>(`/api/session/${encodeURIComponent(id)}`),
  messages: (id: string) => get<ChatMessageDto[]>(`/api/session/${encodeURIComponent(id)}/messages`),
  delete: (id: string) => del(`/api/session/${encodeURIComponent(id)}`),
  updateTitle: (id: string, title: string) =>
    put<{ id: string; title: string }>(`/api/session/${encodeURIComponent(id)}/title`, { title }),
  getEvents: (sessionId: string, count?: number) =>
    get<AgentEvent[]>(`/api/agent/sessions/${encodeURIComponent(sessionId)}/events${count ? `?count=${count}` : ''}`),
  cleanup: () => post<{ message: string; timestamp: string }>('/api/agent/maintenance/cleanup'),
}
