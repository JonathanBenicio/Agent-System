import { useState, useCallback } from 'react'
import { Brain, X } from 'lucide-react'
import type { ChatMessage } from '@/types/chat'
import type { LLMProviderInfo } from '@/types/api'
import { MessageList } from './MessageList'
import { ChatInput } from './ChatInput'
import { AISelectorBar } from './AISelectorBar'
import { SessionSidebar } from './SessionSidebar'
import { SessionInsights } from './SessionInsights'
import { getConnection } from '@/lib/signalr'
import { useChat } from '@/hooks/useChat'
import { cn } from '@/lib/utils'

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
  const [showInsights, setShowInsights] = useState(false)
  const { activeSessionInsights, activeSessionSummary } = useChat()

  const handleSelectSession = useCallback((id: string) => {
    onClearMessages()
    setActiveSessionId(id)

    const conn = getConnection()
    conn.invoke('JoinSession', id).catch(err => {
      console.error('Failed to join session:', err)
    })
  }, [onClearMessages])

  const handleNewSession = useCallback(() => {
    setActiveSessionId(undefined)
    setShowInsights(false)
  }, [])

  return (
    <div className="flex h-full overflow-hidden">
      <div className="w-64 shrink-0 h-full">
        <SessionSidebar
          activeSessionId={activeSessionId}
          onSelectSession={handleSelectSession}
          onNewSession={handleNewSession}
          onClearMessages={onClearMessages}
        />
      </div>

      <div className="flex-1 flex flex-col min-w-0 h-full relative">
        <div className="flex items-center justify-between pr-4 bg-zinc-900 border-b border-zinc-800">
          <div className="flex-1">
            <AISelectorBar
              providers={providers}
              selectedProvider={selectedProvider}
              selectedModel={selectedModel}
              onProviderChange={onProviderChange}
              onModelChange={onModelChange}
            />
          </div>
          
          {activeSessionId && (
            <button
              onClick={() => setShowInsights(!showInsights)}
              className={cn(
                "flex items-center gap-2 px-3 py-1.5 rounded-full text-xs font-medium transition-all",
                showInsights 
                  ? "bg-teal-600 text-white shadow-lg shadow-teal-900/20" 
                  : "bg-zinc-800 text-zinc-400 hover:bg-zinc-700"
              )}
            >
              <Brain className="w-3.5 h-3.5" />
              Insights
            </button>
          )}
        </div>

        <div className="flex-1 flex overflow-hidden">
          <div className={cn(
            "flex-1 flex flex-col min-w-0 transition-all duration-300",
            showInsights && "opacity-40 grayscale-[0.5] pointer-events-none scale-[0.99] origin-left"
          )}>
            <MessageList messages={messages} isProcessing={isProcessing} />
            <ChatInput
              onSend={onSend}
              disabled={!isConnected && messages.length > 0}
              isProcessing={isProcessing}
            />
          </div>

          {/* Insights Panel Overlay/Sidebar */}
          {showInsights && activeSessionInsights && (
            <div className="absolute inset-y-0 right-0 w-80 bg-zinc-900 border-l border-zinc-800 shadow-2xl flex flex-col animate-in slide-in-from-right duration-300">
              <div className="p-4 border-b border-zinc-800 flex items-center justify-between">
                <div className="flex items-center gap-2 text-teal-400 font-semibold">
                  <Brain className="w-4 h-4" />
                  <span>Memória da Sessão</span>
                </div>
                <button 
                  onClick={() => setShowInsights(false)}
                  className="p-1 rounded-md hover:bg-zinc-800 text-zinc-500 hover:text-zinc-300"
                >
                  <X className="w-4 h-4" />
                </button>
              </div>
              
              <div className="flex-1 overflow-y-auto custom-scrollbar">
                {activeSessionSummary && (
                  <div className="p-4 bg-teal-950/20 border-b border-zinc-800/50">
                    <h4 className="text-[10px] uppercase tracking-wider text-zinc-500 font-bold mb-2">Resumo Executivo</h4>
                    <p className="text-sm text-zinc-300 leading-relaxed italic">
                      "{activeSessionSummary.summary}"
                    </p>
                  </div>
                )}
                <SessionInsights insights={activeSessionInsights} />
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  )
}
