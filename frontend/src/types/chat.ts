export interface ChatMessage {
  id: string
  role: 'user' | 'assistant' | 'system'
  content: string
  agentName?: string
  agentTier?: number
  actions?: string[]
  tools?: string[]
  success?: boolean
  sessionId?: string
  timestamp: string
  isStreaming?: boolean
  isHistory?: boolean
}

export interface ChatSession {
  id: string
  title: string
  lastMessage: string
  timestamp: string
  messageCount: number
}

export interface ChatRequest {
  message: string
  userId?: string
  userName?: string
  targetAgent?: string
  context?: string
}

export interface ChatResponse {
  response: string
  agentUsed: string
  agentTier: number
  actionsPerformed: string[]
  metadata?: Record<string, unknown>
}

export interface SignalRMessage {
  content: string
  agentName: string
  agentTier: number
  actions: string[]
  tools: string[]
  success: boolean
  sessionId: string
  timestamp: string
  isHistory?: boolean
}
