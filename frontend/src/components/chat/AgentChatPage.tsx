import { useParams, useNavigate } from 'react-router-dom'
import { Bot, ArrowLeft } from 'lucide-react'
import { useChat } from '@/hooks/useChat'
import { MessageList } from './MessageList'
import { ChatInput } from './ChatInput'
import { AISelectorBar } from './AISelectorBar'

export function AgentChatPage() {
  const { agentName } = useParams<{ agentName: string }>()
  const navigate = useNavigate()
  const {
    messages,
    isConnected,
    isProcessing,
    providers,
    selectedProvider,
    selectedModel,
    setSelectedProvider,
    setSelectedModel,
    sendMessage,
  } = useChat(agentName)

  return (
    <div className="flex flex-col h-full">
      {/* Agent header */}
      <div className="flex items-center gap-3 px-4 py-3 border-b border-zinc-800 bg-zinc-950/50">
        <button
          onClick={() => navigate('/agents')}
          className="p-1.5 rounded-lg text-zinc-400 hover:bg-zinc-800 hover:text-zinc-200 transition-colors"
          title="Voltar para agents"
        >
          <ArrowLeft className="w-4 h-4" />
        </button>
        <div className="flex items-center gap-2">
          <div className="flex items-center justify-center w-7 h-7 rounded-lg bg-violet-600">
            <Bot className="w-3.5 h-3.5 text-white" />
          </div>
          <div>
            <h2 className="text-sm font-semibold text-zinc-100">Chat com {agentName}</h2>
            <span className="text-xs text-zinc-500">Mensagens vão direto para este agent</span>
          </div>
        </div>
      </div>

      <AISelectorBar
        providers={providers}
        selectedProvider={selectedProvider}
        selectedModel={selectedModel}
        onProviderChange={setSelectedProvider}
        onModelChange={setSelectedModel}
      />

      <MessageList messages={messages} isProcessing={isProcessing} />
      <ChatInput
        onSend={sendMessage}
        disabled={!isConnected && messages.length > 0}
        isProcessing={isProcessing}
        placeholder={`Envie uma mensagem para ${agentName}...`}
      />
    </div>
  )
}
