import { useRef, useEffect } from 'react'
import type { ChatMessage } from '@/types/chat'
import { MessageBubble } from './MessageBubble'
import { ProcessingIndicator } from './ProcessingIndicator'

interface MessageListProps {
  messages: ChatMessage[]
  isProcessing: boolean
}

export function MessageList({ messages, isProcessing }: MessageListProps) {
  const bottomRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: 'smooth' })
  }, [messages, isProcessing])

  if (messages.length === 0 && !isProcessing) {
    return (
      <div className="flex-1 flex items-center justify-center">
        <div className="text-center max-w-md px-4">
          <div className="w-16 h-16 mx-auto mb-6 rounded-2xl bg-teal-600/20 flex items-center justify-center">
            <svg className="w-8 h-8 text-teal-400" fill="none" viewBox="0 0 24 24" strokeWidth={1.5} stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" d="M9.813 15.904L9 18.75l-.813-2.846a4.5 4.5 0 00-3.09-3.09L2.25 12l2.846-.813a4.5 4.5 0 003.09-3.09L9 5.25l.813 2.846a4.5 4.5 0 003.09 3.09L15.75 12l-2.846.813a4.5 4.5 0 00-3.09 3.09zM18.259 8.715L18 9.75l-.259-1.035a3.375 3.375 0 00-2.455-2.456L14.25 6l1.036-.259a3.375 3.375 0 002.455-2.456L18 2.25l.259 1.035a3.375 3.375 0 002.455 2.456L21.75 6l-1.036.259a3.375 3.375 0 00-2.455 2.456z" />
            </svg>
          </div>
          <h2 className="text-xl font-semibold text-zinc-200 mb-2">
            AgenticSystem
          </h2>
          <p className="text-zinc-400 text-sm leading-relaxed">
            Envie uma mensagem para interagir com os agents. O sistema
            selecionará automaticamente o melhor agent para sua solicitação.
          </p>
        </div>
      </div>
    )
  }

  return (
    <div className="flex-1 overflow-y-auto">
      <div className="max-w-3xl mx-auto px-4 py-6 space-y-1">
        {messages.map((msg) => (
          <MessageBubble key={msg.id} message={msg} />
        ))}
        {isProcessing && <ProcessingIndicator />}
        <div ref={bottomRef} />
      </div>
    </div>
  )
}
