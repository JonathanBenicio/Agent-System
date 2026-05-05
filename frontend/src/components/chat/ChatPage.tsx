import type { ChatMessage } from '@/types/chat'
import type { LLMProviderInfo } from '@/types/api'
import { MessageList } from './MessageList'
import { ChatInput } from './ChatInput'
import { AISelectorBar } from './AISelectorBar'

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
}: ChatPageProps) {
  return (
    <div className="flex flex-col h-full">
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
  )
}
