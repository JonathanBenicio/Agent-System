import { createContext, useContext, useState, useEffect, useCallback, useRef, type ReactNode } from 'react'
import { llmApi } from '@/lib/api'
import { getConnection, startConnection, signalR } from '@/lib/signalr'
import { getAuthHeaders } from '@/lib/auth'
import type { LLMProviderInfo } from '@/types/api'
import type { ChatMessage, SignalRMessage } from '@/types/chat'

const ProviderStorageKey = 'agentic.chat.provider'
const ModelStorageKey = 'agentic.chat.model'

function generateId(): string {
  return crypto.randomUUID?.() ?? Date.now().toString(36) + Math.random().toString(36).slice(2, 7)
}

interface ChatContextValue {
  messages: ChatMessage[]
  isConnected: boolean
  isProcessing: boolean
  connectionState: string
  sessionId: string
  providers: LLMProviderInfo[]
  selectedProvider: string
  selectedModel: string
  setSelectedProvider: (providerName: string) => void
  setSelectedModel: (modelName: string) => void
  refreshAiConfiguration: () => Promise<void>
  sendMessage: (text: string, targetAgent?: string) => Promise<void>
  clearMessages: () => void
}

const ChatContext = createContext<ChatContextValue | null>(null)

/**
 * Single provider that owns the SignalR connection and event handlers.
 * Mount once at the app root — all consumers share the same state.
 */
export function ChatProvider({ children }: { children: ReactNode }) {
  const [messages, setMessages] = useState<ChatMessage[]>([])
  const [isConnected, setIsConnected] = useState(false)
  const [isProcessing, setIsProcessing] = useState(false)
  const [connectionState, setConnectionState] = useState<string>('Disconnected')
  const [sessionId, setSessionId] = useState<string>('')
  const [providers, setProviders] = useState<LLMProviderInfo[]>([])
  const [selectedProvider, setSelectedProviderState] = useState('')
  const [selectedModel, setSelectedModelState] = useState('')
  const sendingRef = useRef(false)

  const refreshAiConfiguration = useCallback(async () => {
    try {
      const configuration = await llmApi.configuration()
      const enabledProviders = configuration.providers.filter(provider => provider.isEnabled)
      setProviders(enabledProviders)

      if (enabledProviders.length === 0) {
        setSelectedProviderState('')
        setSelectedModelState('')
        return
      }

      const preferredProvider = window.localStorage.getItem(ProviderStorageKey)
      const nextProvider = resolveProvider(enabledProviders, preferredProvider, configuration.defaultProvider)
      const preferredModel = window.localStorage.getItem(ModelStorageKey)
      const nextModel = resolveModel(
        nextProvider,
        preferredModel,
        nextProvider.name === configuration.defaultProvider ? configuration.defaultModel : nextProvider.defaultModel,
      )

      setSelectedProviderState(nextProvider.name)
      setSelectedModelState(nextModel)
      persistAiSelection(nextProvider.name, nextModel)
    } catch (err) {
      console.error('AI configuration error:', err)
    }
  }, [])

  // Register handlers ONCE — no cleanup-per-instance
  useEffect(() => {
    const conn = getConnection()

    conn.on('ReceiveMessage', (msg: SignalRMessage) => {
      const chatMsg: ChatMessage = {
        id: generateId(),
        role: 'assistant',
        content: msg.content,
        agentName: msg.agentName,
        agentTier: msg.agentTier,
        actions: msg.actions,
        tools: msg.tools,
        success: msg.success,
        sessionId: msg.sessionId,
        timestamp: msg.timestamp,
      }
      setMessages(prev => [...prev, chatMsg])
      setIsProcessing(false)
      if (msg.sessionId) setSessionId(msg.sessionId)
    })

    conn.on('ProcessingStarted', () => {
      setIsProcessing(true)
    })

    conn.on('ReceiveError', (data: { error: string; timestamp: string }) => {
      const errorMsg: ChatMessage = {
        id: generateId(),
        role: 'system',
        content: `Erro: ${data.error}`,
        timestamp: data.timestamp,
      }
      setMessages(prev => [...prev, errorMsg])
      setIsProcessing(false)
    })

    conn.on('Connected', (data: { connectionId: string; timestamp: string }) => {
      console.log('SignalR connected:', data.connectionId)
    })

    conn.onreconnecting(() => {
      setIsConnected(false)
      setConnectionState('Reconnecting')
    })

    conn.onreconnected(() => {
      setIsConnected(true)
      setConnectionState('Connected')
    })

    conn.onclose(() => {
      setIsConnected(false)
      setConnectionState('Disconnected')
    })

    startConnection()
      .then(() => {
        setIsConnected(true)
        setConnectionState('Connected')
      })
      .catch((err) => {
        console.error('SignalR connection error:', err)
        setConnectionState('Error')
      })

    // Cleanup only on full unmount (app teardown)
    return () => {
      conn.off('ReceiveMessage')
      conn.off('ProcessingStarted')
      conn.off('ReceiveError')
      conn.off('Connected')
    }
  }, [])

  useEffect(() => {
    void refreshAiConfiguration()

    const handleRefresh = () => {
      void refreshAiConfiguration()
    }

    window.addEventListener('agentic:llm-config-updated', handleRefresh)
    return () => window.removeEventListener('agentic:llm-config-updated', handleRefresh)
  }, [refreshAiConfiguration])

  const setSelectedProvider = useCallback((providerName: string) => {
    const provider = providers.find(item => item.name === providerName)
    if (!provider) return

    const nextModel = resolveModel(provider, undefined, provider.defaultModel)
    setSelectedProviderState(provider.name)
    setSelectedModelState(nextModel)
    persistAiSelection(provider.name, nextModel)
  }, [providers])

  const setSelectedModel = useCallback((modelName: string) => {
    setSelectedModelState(modelName)
    persistAiSelection(selectedProvider, modelName)
  }, [selectedProvider])

  const sendViaRest = useCallback(async (text: string, targetAgent?: string) => {
    setIsProcessing(true)
    try {
      const res = await fetch('/api/chat', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', ...getAuthHeaders() },
        body: JSON.stringify({
          message: text,
          targetAgent: targetAgent ?? null,
          provider: selectedProvider || null,
          model: selectedModel || null,
        }),
      })
      const data = await res.json()
      setMessages(prev => [...prev, {
        id: generateId(),
        role: 'assistant',
        content: data.response,
        agentName: data.agentUsed,
        agentTier: data.agentTier,
        actions: data.actionsPerformed,
        timestamp: new Date().toISOString(),
      }])
    } catch (err) {
      console.error('REST error:', err)
      setMessages(prev => [...prev, {
        id: generateId(),
        role: 'system',
        content: 'Erro ao enviar mensagem. Verifique a conexão com o servidor.',
        timestamp: new Date().toISOString(),
      }])
    }
    setIsProcessing(false)
  }, [selectedModel, selectedProvider])

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
    if (conn.state === signalR.HubConnectionState.Connected) {
      setIsProcessing(true)
      try {
        await conn.invoke('SendMessage', text, targetAgent ?? null, selectedProvider || null, selectedModel || null, null)
      } catch (err) {
        console.error('SendMessage error:', err)
        await sendViaRest(text, targetAgent)
      }
    } else {
      await sendViaRest(text, targetAgent)
    }
    sendingRef.current = false
  }, [selectedModel, selectedProvider, sendViaRest])

  const clearMessages = useCallback(() => {
    setMessages([])
    setSessionId('')
  }, [])

  return (
    <ChatContext.Provider
      value={{
        messages,
        isConnected,
        isProcessing,
        connectionState,
        sessionId,
        providers,
        selectedProvider,
        selectedModel,
        setSelectedProvider,
        setSelectedModel,
        refreshAiConfiguration,
        sendMessage,
        clearMessages,
      }}
    >
      {children}
    </ChatContext.Provider>
  )
}

/**
 * Hook to access the shared chat state.
 * @param targetAgent — optional; when provided, sendMessage automatically routes to this agent.
 */
export function useChat(targetAgent?: string) {
  const ctx = useContext(ChatContext)
  if (!ctx) throw new Error('useChat must be used within <ChatProvider>')

  const boundSend = useCallback(
    (text: string) => ctx.sendMessage(text, targetAgent),
    [ctx.sendMessage, targetAgent],
  )

  return {
    messages: ctx.messages,
    isConnected: ctx.isConnected,
    isProcessing: ctx.isProcessing,
    connectionState: ctx.connectionState,
    sessionId: ctx.sessionId,
    providers: ctx.providers,
    selectedProvider: ctx.selectedProvider,
    selectedModel: ctx.selectedModel,
    setSelectedProvider: ctx.setSelectedProvider,
    setSelectedModel: ctx.setSelectedModel,
    refreshAiConfiguration: ctx.refreshAiConfiguration,
    sendMessage: boundSend,
    clearMessages: ctx.clearMessages,
  }
}

function resolveProvider(providers: LLMProviderInfo[], preferredProvider?: string | null, defaultProvider?: string) {
  return providers.find(provider => provider.name === preferredProvider)
    ?? providers.find(provider => provider.name === defaultProvider)
    ?? providers[0]
}

function resolveModel(provider: LLMProviderInfo, preferredModel?: string | null, defaultModel?: string) {
  return provider.models.find(model => model === preferredModel)
    ?? provider.models.find(model => model === defaultModel)
    ?? provider.defaultModel
    ?? provider.models[0]
    ?? ''
}

function persistAiSelection(providerName: string, modelName: string) {
  if (!providerName) return

  window.localStorage.setItem(ProviderStorageKey, providerName)
  if (modelName) {
    window.localStorage.setItem(ModelStorageKey, modelName)
  }
}
