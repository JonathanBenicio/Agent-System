import { useState, useCallback, useRef, useEffect } from 'react'
import { sessionApi } from '@/lib/api'
import { useWorkflowStore } from '@/store/useWorkflowStore'
import { toast } from 'sonner'
import type { 
  SessionSummaryDto, 
  SessionInsightsDto, 
  ChatMessageDto, 
  WorkflowDefinition 
} from '@/types/api'
import type { ChatMessage } from '@/types/chat'

function generateId(): string {
  return crypto.randomUUID?.() ?? Date.now().toString(36) + Math.random().toString(36).slice(2, 7)
}

export function useChatState() {
  const [messagesMap, setMessagesMap] = useState<Record<string, ChatMessage[]>>({ general: [] })
  const [activeChannel, setActiveChannel] = useState<string>('general')
  const [isProcessing, setIsProcessing] = useState(false)
  const [sessionId, setSessionId] = useState<string>('')
  const [sessionEnded, setSessionEnded] = useState(false)
  const [activeSessionSummary, setActiveSessionSummary] = useState<SessionSummaryDto | undefined>()
  const [activeSessionInsights, setActiveSessionInsights] = useState<SessionInsightsDto | undefined>()
  
  const activeChannelRef = useRef(activeChannel)
  useEffect(() => {
    activeChannelRef.current = activeChannel
  }, [activeChannel])

  const setMessages = useCallback((newMessages: ChatMessage[] | ((prev: ChatMessage[]) => ChatMessage[])) => {
    setMessagesMap(prev => {
      const channel = activeChannelRef.current
      const currentList = prev[channel] || []
      const updatedList = typeof newMessages === 'function' ? newMessages(currentList) : newMessages
      return {
        ...prev,
        [channel]: updatedList
      }
    })
  }, [])

  const loadHistory = useCallback(async (id: string) => {
    setIsProcessing(true)
    try {
      const detail = await sessionApi.get(id)
      const mapped: ChatMessage[] = detail.messages.map((m: ChatMessageDto) => ({
        id: m.id,
        role: m.role as ChatMessage['role'],
        content: m.content,
        agentName: m.agentName,
        agentTier: m.agentTier,
        actions: m.actions,
        tools: m.tools,
        timestamp: m.timestamp,
        isHistory: true,
        memoryInjected: m.memoryInjected,
      }))
      setMessages(mapped)
      setSessionId(id)
      setSessionEnded(Boolean(detail.endedAt))
      return detail
    } catch (err) {
      console.error('Failed to load session history:', err)
      toast.error('Erro ao carregar histórico da sessão')
    } finally {
      setIsProcessing(false)
    }
  }, [setMessages])

  const addLocalMessage = useCallback((message: ChatMessage) => {
    setMessages(prev => [...prev, message])
  }, [setMessages])

  const handleWorkflowGenerated = useCallback((definition: WorkflowDefinition, name: string) => {
    useWorkflowStore.getState().fromWorkflowDefinition(definition)
    toast.success('Novo workflow gerado!', {
      description: `O fluxo "${name}" foi criado e está pronto para edição.`,
      action: {
        label: 'Ver Fluxo',
        onClick: () => { window.location.hash = '/workflows' },
      },
    })
  }, [])

  const clearMessages = useCallback(async () => {
    setMessagesMap(prev => ({ ...prev, [activeChannel]: [] }))
    setSessionId('')
    setSessionEnded(false)
    setActiveSessionSummary(undefined)
    setActiveSessionInsights(undefined)
  }, [activeChannel])

  return {
    messagesMap,
    activeChannel,
    setActiveChannel,
    messages: messagesMap[activeChannel] || [],
    setMessages,
    isProcessing,
    setIsProcessing,
    sessionId,
    setSessionId,
    sessionEnded,
    setSessionEnded,
    activeSessionSummary,
    setActiveSessionSummary,
    activeSessionInsights,
    setActiveSessionInsights,
    loadHistory,
    addLocalMessage,
    handleWorkflowGenerated,
    generateId,
    clearMessages
  }
}
