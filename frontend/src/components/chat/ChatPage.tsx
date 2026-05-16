import { useState, useCallback } from 'react'
import type { ChatMessage } from '@/types/chat'
import type { LLMProviderInfo } from '@/types/api'
import { MessageList } from './MessageList'
import { ChatInput } from './ChatInput'
import { AISelectorBar } from './AISelectorBar'
import { SessionSidebar } from './SessionSidebar'
import { getConnection } from '@/lib/signalr'

interface ChatPageProps {
  messages: ChatMessage[]
  isProcessing: boolean
  isConnected: boolean
  onSend: (message: string) => void
  providers: LLMProviderInfo[]
  selectedProvider: string
  selectedModel: string
  onProviderChange: (providerName: string) => void
  onModelChange: (modelName: string) => void
  onClearMessages: () => void
}

export function ChatPage({
  messages,
  isProcessing,
  isConnected,
  onSend,
  providers,
  selectedProvider,
  selectedModel,
  onProviderChange,
  onModelChange,
  onClearMessages,
}: ChatPageProps) {
  const [activeSessionId, setActiveSessionId] = useState<string | undefined>()

  const handleSelectSession = useCallback((id: string) => {
    setActiveSessionId(id)
    onClearMessages()

    const conn = getConnection()
    conn.invoke('JoinSession', id).catch(err => {
      console.error('Failed to join session:', err)
    })
  }, [onClearMessages])

  const handleNewSession = useCallback(() => {
    setActiveSessionId(undefined)
  }, [])

  return (
    <div className="flex h-full">
      <div className="w-64 shrink-0">
        <SessionSidebar
          activeSessionId={activeSessionId}
          onSelectSession={handleSelectSession}
          onNewSession={handleNewSession}
          onClearMessages={onClearMessages}
        />
      </div>

      <div className="flex-1 flex flex-col min-w-0">
        <AISelectorBar
          providers={providers}
          selectedProvider={selectedProvider}
          selectedModel={selectedModel}
          onProviderChange={onProviderChange}
          onModelChange={onModelChange}
        />
        <MessageList messages={messages} isProcessing={isProcessing} />
        <ChatInput
          onSend={onSend}
          disabled={!isConnected && messages.length > 0}
          isProcessing={isProcessing}
        />
      </div>
    </div>
  )
}
