/* eslint-disable react-refresh/only-export-components */
import { createContext, useContext, useState, useEffect, useCallback, useRef, type ReactNode } from 'react'
import { getConnection, signalR } from '@/lib/signalr'
import { request, ApiError } from '@/lib/api/client'
import type { 
  LLMProviderInfo, 
  SessionSummaryDto, 
  SessionInsightsDto,
  WorkflowDefinition,
} from '@/types/api'
import type { ChatMessage, SignalRMessage, AgentResponse } from '@/types/chat'

// Specialized Hooks
import { useLLMConfig } from './chat/useLLMConfig'
import { useSignalR } from './chat/useSignalR'
import { useChatState } from './chat/useChatState'

interface ChatContextValue {
  messages: ChatMessage[]
  isConnected: boolean
  isProcessing: boolean
  connectionState: string
  sessionId: string
  setSessionId: (id: string) => void
  sessionEnded: boolean
  setSessionEnded: (ended: boolean) => void
  activeSessionSummary?: SessionSummaryDto
  activeSessionInsights?: SessionInsightsDto
  providers: LLMProviderInfo[]
  selectedProvider: string
  selectedModel: string
  selectedRoomId: string
  selectedAgentId: string
  associateToRoom: boolean
  activeChannel: string
  setActiveChannel: (channel: string) => void
  setSelectedProvider: (providerName: string) => void
  setSelectedModel: (modelName: string) => void
  setSelectedRoomId: (roomId: string) => void
  setSelectedAgentId: (agentId: string) => void
  setAssociateToRoom: (associate: boolean) => void
  refreshAiConfiguration: () => Promise<unknown>
  sendMessage: (text: string, targetAgent?: string) => Promise<void>
  clearMessages: () => Promise<void>
  loadHistory: (sessionId: string) => Promise<void>
  addLocalMessage: (message: ChatMessage) => void
}

const ChatContext = createContext<ChatContextValue | null>(null)

export function ChatProvider({ children }: { children: ReactNode }) {
  const { isConnected, connectionState } = useSignalR()
  const { 
    providers, selectedProvider, selectedModel, 
    setSelectedProvider, setSelectedModel, setSessionSelection, refreshAiConfiguration
  } = useLLMConfig()
  
  const {
    messages, setMessages, isProcessing, setIsProcessing,
    sessionId, setSessionId, sessionEnded, setSessionEnded, activeSessionSummary, setActiveSessionSummary,
    activeSessionInsights, setActiveSessionInsights, loadHistory: loadHistoryState,
    addLocalMessage, handleWorkflowGenerated, generateId,
    activeChannel, setActiveChannel, clearMessages: clearState
  } = useChatState()

  const [selectedRoomId, setSelectedRoomId] = useState<string>('')
  const [selectedAgentId, setSelectedAgentId] = useState<string>('')
  const [associateToRoom, setAssociateToRoom] = useState<boolean>(true)
  const sendingRef = useRef(false)

  const loadHistory = useCallback(async (id: string) => {
    const detail = await loadHistoryState(id)
    if (detail) setSessionSelection(detail.provider, detail.model)
  }, [loadHistoryState, setSessionSelection])

  // Safety Timeout US-25
  useEffect(() => {
    let timeoutId: ReturnType<typeof setTimeout> | undefined
    if (isProcessing) {
      timeoutId = setTimeout(() => {
        setIsProcessing(false)
        console.warn('⚠️ Processing timeout reached.')
        setMessages(prev => [
          ...prev,
          {
            id: generateId(),
            role: 'system',
            content: 'Aviso do Sistema: Tempo limite excedido ao aguardar resposta do agente.',
            timestamp: new Date().toISOString(),
          }
        ])
      }, 120000)
    }
    return () => {
      if (timeoutId) clearTimeout(timeoutId)
    }
  }, [isProcessing, setIsProcessing, setMessages, generateId])

  // SignalR Event Handlers
  useEffect(() => {
    const conn = getConnection()

    const onStreamEvent = (evt: { type?: number; data?: { artifactType?: string; definition?: WorkflowDefinition | string; workflowName?: string } }) => {
      if (evt?.type === 20) {
        const data = evt.data
        if (data?.artifactType === 'Plan' && data?.definition) {
          const definition = typeof data.definition === 'string'
            ? JSON.parse(data.definition) as WorkflowDefinition : data.definition
          handleWorkflowGenerated(definition, data.workflowName ?? 'Workflow')
        }
      }
    }

    const onReceiveMessage = (msg: SignalRMessage) => {
      if (!msg.content?.trim()) {
        setIsProcessing(false)
        return
      }

      const chatMsg: ChatMessage = {
        id: generateId(),
        role: msg.agentName ? 'assistant' : 'user',
        content: msg.content,
        agentName: msg.agentName,
        agentTier: msg.agentTier,
        actions: msg.actions,
        tools: msg.tools,
        success: msg.success,
        sessionId: msg.sessionId,
        timestamp: msg.timestamp,
        isHistory: msg.isHistory,
        memoryInjected: msg.memoryInjected,
        citations: msg.citations,
      }

      setMessages(prev => {
        if (!msg.isHistory && msg.memoryInjected && msg.agentName) {
          const lastUserIdx = prev.findLastIndex(m => m.role === 'user')
          if (lastUserIdx !== -1) {
            const newMessages = [...prev]
            newMessages[lastUserIdx] = { ...newMessages[lastUserIdx], memoryInjected: true }
            return [...newMessages, chatMsg]
          }
        }
        return [...prev, chatMsg]
      })

      if (!msg.isHistory) setIsProcessing(false)
      if (msg.sessionId) setSessionId(msg.sessionId)
    }

    const onProcessingStarted = () => setIsProcessing(true)
    
    const onReceiveError = (data: { error: string; timestamp: string }) => {
      setMessages(prev => [...prev, {
        id: generateId(),
        role: 'system',
        content: `Erro: ${data.error}`,
        timestamp: data.timestamp,
      }])
      setIsProcessing(false)
    }

    const onSessionJoined = (data: { summary?: SessionSummaryDto; insights?: SessionInsightsDto }) => {
      setActiveSessionSummary(data.summary)
      setActiveSessionInsights(data.insights)
    }

    conn.on('StreamEvent', onStreamEvent)
    conn.on('ReceiveMessage', onReceiveMessage)
    conn.on('ProcessingStarted', onProcessingStarted)
    conn.on('ReceiveError', onReceiveError)
    conn.on('SessionJoined', onSessionJoined)

    return () => {
      conn.off('StreamEvent', onStreamEvent)
      conn.off('ReceiveMessage', onReceiveMessage)
      conn.off('ProcessingStarted', onProcessingStarted)
      conn.off('ReceiveError', onReceiveError)
      conn.off('SessionJoined', onSessionJoined)
    }
  }, [handleWorkflowGenerated, setMessages, setIsProcessing, setSessionId, setActiveSessionSummary, setActiveSessionInsights, generateId])

  const sendViaRest = useCallback(async (text: string, targetAgent?: string) => {
    setIsProcessing(true)
    try {
      const activeAgent = targetAgent ?? selectedAgentId
      const data = await request<AgentResponse>('/api/chat', {
        method: 'POST',
        body: JSON.stringify({
          message: text,
          targetAgent: activeAgent || null,
          provider: selectedProvider || null,
          model: selectedModel || null,
          sessionId: sessionId || null,
          context: selectedRoomId ? { 'rag.knowledgeRoomId': selectedRoomId } : null,
        }),
      })
      if (data.sessionId) {
        setSessionId(data.sessionId)
        window.dispatchEvent(new Event('agentic:session-updated'))
      }
      setMessages(prev => [...prev, {
        id: generateId(),
        role: data.success ? 'assistant' : 'system',
        content: data.success ? data.content : (data.errorMessage || data.content || 'O agente não conseguiu responder.'),
        agentName: data.agentName,
        agentTier: data.agentTier,
        actions: data.actionsPerformed,
        tools: data.toolsUsed,
        success: data.success,
        sessionId: data.sessionId,
        timestamp: data.timestamp || new Date().toISOString(),
      }])
    } catch (err) {
      console.error('REST error:', err)
      setMessages(prev => [...prev, {
        id: generateId(),
        role: 'system',
        content: err instanceof ApiError ? `Erro ao enviar mensagem (${err.status}): ${err.message}` : 'Erro ao enviar mensagem.',
        timestamp: new Date().toISOString(),
      }])
    }
    setIsProcessing(false)
  }, [selectedModel, selectedProvider, selectedAgentId, selectedRoomId, sessionId, setSessionId, setMessages, setIsProcessing, generateId])

  const sendMessage = useCallback(async (text: string, targetAgent?: string) => {
    if (!text.trim() || sendingRef.current) return
    sendingRef.current = true

    setMessages(prev => [...prev, {
      id: generateId(),
      role: 'user',
      content: text,
      timestamp: new Date().toISOString(),
    }])

    const conn = getConnection()
    const activeAgent = targetAgent ?? selectedAgentId
    if (conn.state === signalR.HubConnectionState.Connected) {
      setIsProcessing(true)
      try {
        await conn.invoke(
          'SendMessage',
          text,
          activeAgent || null,
          selectedProvider || null,
          selectedModel || null,
          null,
          sessionId || null,
          selectedRoomId || null
        )
      } catch (err) {
        console.error('SendMessage error:', err)
        await sendViaRest(text, activeAgent)
      }
    } else {
      await sendViaRest(text, activeAgent)
    }
    sendingRef.current = false
  }, [selectedModel, selectedProvider, selectedAgentId, selectedRoomId, sessionId, sendViaRest, setMessages, setIsProcessing, generateId])

  const clearMessages = useCallback(async () => {
    await clearState()
    setSessionSelection()
    setSelectedRoomId('')
    setSelectedAgentId('')
    setAssociateToRoom(true)
  }, [clearState, setSessionSelection])

  return (
    <ChatContext.Provider
      value={{
        messages, isConnected, isProcessing, connectionState, sessionId, setSessionId, sessionEnded, setSessionEnded,
        activeSessionSummary, activeSessionInsights, providers,
        selectedProvider, selectedModel, selectedRoomId, selectedAgentId,
        associateToRoom, activeChannel, setActiveChannel,
        setSelectedProvider, setSelectedModel, setSelectedRoomId, setSelectedAgentId,
        setAssociateToRoom, refreshAiConfiguration, sendMessage,
        clearMessages, loadHistory, addLocalMessage
      }}
    >
      {children}
    </ChatContext.Provider>
  )
}

export function useChat(targetAgent?: string) {
  const ctx = useContext(ChatContext)
  if (!ctx) throw new Error('useChat must be used within <ChatProvider>')
  const sendMessage = ctx.sendMessage

  const boundSend = useCallback(
    (text: string) => sendMessage(text, targetAgent),
    [sendMessage, targetAgent],
  )

  return { ...ctx, sendMessage: boundSend }
}
