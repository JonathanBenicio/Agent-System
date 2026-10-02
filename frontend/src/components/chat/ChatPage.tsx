import { useState, useCallback, useEffect } from 'react'
import { Brain, X } from 'lucide-react'
import type { ChatMessage } from '@/types/chat'
import type { LLMProviderInfo } from '@/types/api'
import { MessageList } from './MessageList'
import { ChatInput } from './ChatInput'
import { AISelectorBar } from './AISelectorBar'
import { SessionSidebar } from './SessionSidebar'
import { SessionInsights } from './SessionInsights'
import { ChatConfigSidebar } from './ChatConfigSidebar'
import { getConnection } from '@/lib/signalr'
import { useChat } from '@/hooks/useChat'
import { useSessions } from '@/hooks/useSessions'
import { cn } from '@/lib/utils'
import { ragApi, sessionApi } from '@/lib/api'
import { toast } from 'sonner'

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
  const [showConfigSidebar, setShowConfigSidebar] = useState(false)
  const [isDragging, setIsDragging] = useState(false)
  const [uploadingFiles, setUploadingFiles] = useState(false)

  const { 
    activeSessionInsights, 
    activeSessionSummary, 
    loadHistory,
    selectedRoomId,
    associateToRoom,
    sessionId,
    setSessionId,
    sessionEnded,
    setSessionEnded,
    addLocalMessage,
    activeChannel,
    setActiveChannel
  } = useChat()
  const { endSession } = useSessions()

  const handleEndSession = useCallback(async (id: string) => {
    try {
      await endSession(id)
      if (id === sessionId) setSessionEnded(true)
    } catch (err) {
      toast.error('Não foi possível encerrar a sessão')
      console.error('Failed to end session:', err)
    }
  }, [endSession, sessionId, setSessionEnded])

  useEffect(() => {
    setActiveChannel('general')
  }, [setActiveChannel])

  const handleSelectSession = useCallback((id: string) => {
    onClearMessages()
    setActiveSessionId(id)

    // Fire-and-forget carregar histórico da API
    loadHistory(id).catch(console.error)

    const conn = getConnection()
    conn.invoke('JoinSession', id).catch(err => {
      console.error('Failed to join session:', err)
    })
  }, [onClearMessages, loadHistory])

  const handleNewSession = useCallback(async () => {
    setActiveSessionId(undefined)
    setShowInsights(false)
    setShowConfigSidebar(false)
    try {
      const created = await sessionApi.create()
      setSessionId(created.id)
      setActiveSessionId(created.id)
    } catch (err) {
      toast.error('Não foi possível criar a sessão')
      console.error('Failed to create session:', err)
    }
  }, [setSessionId])
  // Sync activeSessionId with the chat hook's sessionId once we have messages
  useEffect(() => {
    if (messages.length > 0 && activeSessionId !== sessionId) {
      // eslint-disable-next-line react-hooks/set-state-in-effect
      setActiveSessionId(sessionId)
    }
  }, [messages.length, sessionId, activeSessionId])

  const handleDragOver = useCallback((e: React.DragEvent) => {
    e.preventDefault()
    e.stopPropagation()
    setIsDragging(true)
  }, [])

  const handleDragLeave = useCallback((e: React.DragEvent) => {
    e.preventDefault()
    e.stopPropagation()
    setIsDragging(false)
  }, [])

  const handleDrop = useCallback(async (e: React.DragEvent) => {
    e.preventDefault()
    e.stopPropagation()
    setIsDragging(false)

    const files = e.dataTransfer.files
    if (!files || files.length === 0) return

    setUploadingFiles(true)
    let source = sessionId
    const roomId = associateToRoom && selectedRoomId ? selectedRoomId : undefined
    
    try {
      if (!source) {
        const created = await sessionApi.create()
        source = created.id
        setSessionId(created.id)
        setActiveSessionId(created.id)
      }
      const res = await ragApi.ingestBatch(files, source, roomId)
      toast.success("Documentos ingeridos com sucesso!", {
        description: `${res.succeeded} de ${res.total} arquivos foram indexados no Vector Store.`,
      })
      addLocalMessage({
        id: crypto.randomUUID?.() ?? Math.random().toString(36).substring(2),
        role: 'system',
        content: `Sucesso: ${res.succeeded} de ${res.total} documento(s) indexado(s) na ${associateToRoom && selectedRoomId ? 'sala de conhecimento' : 'sessão temporária'}.`,
        timestamp: new Date().toISOString(),
      })
    } catch (err) {
      console.error('Failed to ingest dropped files:', err)
      toast.error("Erro na ingestão de documentos", {
        description: err instanceof Error ? err.message : "Erro desconhecido ao processar arquivos.",
      })
    } finally {
      setUploadingFiles(false)
    }
  }, [associateToRoom, selectedRoomId, sessionId, setSessionId, addLocalMessage])

  return (
    <div className="flex h-full overflow-hidden">
      <div className="w-64 shrink-0 h-full">
        <SessionSidebar
          activeSessionId={activeSessionId}
          onSelectSession={handleSelectSession}
          onNewSession={handleNewSession}
          onClearMessages={onClearMessages}
          onEndSession={handleEndSession}
        />
      </div>

      <div 
        className="flex-1 flex flex-col min-w-0 h-full relative"
        onDragOver={handleDragOver}
        onDragLeave={handleDragLeave}
        onDrop={handleDrop}
      >
        {isDragging && (
          <div className="absolute inset-0 z-50 flex flex-col items-center justify-center bg-zinc-950/80 backdrop-blur-md border-2 border-dashed border-teal-500/50 m-4 transition-all duration-200">
            <div className="text-center p-6 bg-zinc-900 border border-zinc-800 shadow-2xl max-w-sm">
              <div className="w-16 h-16 bg-teal-950/40 border border-teal-800/40 flex items-center justify-center mx-auto mb-4 animate-pulse">
                <Brain className="w-8 h-8 text-teal-400" />
              </div>
              <h3 className="text-sm font-bold text-zinc-100 uppercase tracking-wider mb-2">Ingestão RAG Contextual</h3>
              <p className="text-xs text-zinc-400 leading-relaxed mb-4">
                Solte os seus arquivos aqui para processá-los e indexá-los instantaneamente no Vector Store.
              </p>
              <div className="text-[10px] text-zinc-500 uppercase tracking-widest font-mono">
                {associateToRoom && selectedRoomId 
                  ? "Modo: Associação à Sala" 
                  : "Modo: Sessão de Chat Temporária"}
              </div>
            </div>
          </div>
        )}

        {uploadingFiles && (
          <div className="absolute inset-0 z-50 flex flex-col items-center justify-center bg-zinc-950/70 backdrop-blur-sm m-4">
            <div className="text-center p-6 bg-zinc-900 border border-zinc-800 shadow-2xl max-w-sm flex flex-col items-center">
              <div className="w-10 h-10 border-2 border-teal-500/20 border-t-teal-500 animate-spin mb-4" />
              <h3 className="text-xs font-bold text-zinc-200 uppercase tracking-wider mb-1">Processando Documentos</h3>
              <p className="text-[11px] text-zinc-400 animate-pulse">
                Extraindo texto, gerando embeddings e indexando...
              </p>
            </div>
          </div>
        )}

        <AISelectorBar
          providers={providers}
          selectedProvider={selectedProvider}
          selectedModel={selectedModel}
          onProviderChange={onProviderChange}
          onModelChange={onModelChange}
          showConfigSidebar={showConfigSidebar}
          onToggleConfig={() => {
            setShowConfigSidebar(!showConfigSidebar)
            setShowInsights(false)
          }}
          showInsights={showInsights}
          onToggleInsights={() => {
            setShowInsights(!showInsights)
            setShowConfigSidebar(false)
          }}
        />

        <div className="flex-1 flex overflow-hidden relative">
          <div className={cn(
            "flex-1 flex flex-col min-w-0 transition-all duration-300",
            (showInsights || showConfigSidebar) && "opacity-40 grayscale-[0.5] pointer-events-none scale-[0.99] origin-left"
          )}>
            <MessageList messages={activeChannel === 'general' ? messages : []} isProcessing={isProcessing} />
            <ChatInput
              onSend={onSend}
              disabled={sessionEnded}
              isProcessing={isProcessing}
            />
          </div>

          {/* Insights Panel Overlay/Sidebar */}
          {showInsights && activeSessionInsights && (
            <div className="absolute inset-y-0 right-0 w-80 bg-zinc-900 border-l border-zinc-800 shadow-2xl flex flex-col animate-in slide-in-from-right duration-300 z-40">
              <div className="p-4 border-b border-zinc-800 flex items-center justify-between">
                <div className="flex items-center gap-2 text-teal-400 font-semibold">
                  <Brain className="w-4 h-4" />
                  <span className="text-sm font-bold uppercase tracking-wider font-mono">Memória da Sessão</span>
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

          {/* Configuration Sidebar */}
          {showConfigSidebar && (
            <ChatConfigSidebar onClose={() => setShowConfigSidebar(false)} />
          )}
        </div>
      </div>
    </div>
  )
}
