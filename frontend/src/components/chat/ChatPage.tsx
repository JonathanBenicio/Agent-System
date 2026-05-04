import type { ChatMessage } from '@/types/chat'
import { MessageList } from './MessageList'
import { ChatInput } from './ChatInput'

interface ChatPageProps {
  messages: ChatMessage[]
  isProcessing: boolean
  isConnected: boolean
  onSend: (message: string) => void
}

export function ChatPage({ messages, isProcessing, isConnected, onSend }: ChatPageProps) {
  return (
    <div className="flex flex-col h-full">
      <MessageList messages={messages} isProcessing={isProcessing} />
      <ChatInput
        onSend={onSend}
        disabled={!isConnected && messages.length > 0}
        isProcessing={isProcessing}
      />
    </div>
  )
}
