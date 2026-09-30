import { Link } from 'react-router-dom'
import { 
  Cpu, 
  Settings2, 
  Brain,
  Sliders
} from 'lucide-react'
import type { LLMProviderInfo } from '@/types/api'
import { useChat } from '@/hooks/useChat'
import { cn } from '@/lib/utils'

interface AISelectorBarProps {
  providers: LLMProviderInfo[]
  selectedProvider: string
  selectedModel: string
  onProviderChange: (providerName: string) => void
  onModelChange: (modelName: string) => void
  showConfigSidebar?: boolean
  onToggleConfig?: () => void
  showInsights?: boolean
  onToggleInsights?: () => void
}

export function AISelectorBar({
  providers,
  selectedProvider,
  selectedModel,
  showConfigSidebar = false,
  onToggleConfig,
  showInsights = false,
  onToggleInsights,
}: AISelectorBarProps) {
  const { sessionId } = useChat()

  const activeProvider = providers.find(provider => provider.name === selectedProvider) ?? providers[0]

  return (
    <div className="relative border-b border-zinc-800 bg-zinc-950/85 backdrop-blur-md px-4 py-2 select-none flex items-center justify-between h-14">
      {/* Background Teal Gradient Glow */}
      <div className="absolute inset-0 -z-10 bg-[radial-gradient(circle_at_20%_0%,_rgba(20,184,166,0.06),_transparent_40%),radial-gradient(circle_at_80%_0%,_rgba(16,185,129,0.04),_transparent_35%)]" />

      {/* Left Block: Session and Model Info */}
      <div className="flex items-center gap-3">
        <div className="flex h-8 w-8 items-center justify-center rounded-lg border border-teal-800 bg-teal-950/40 text-teal-400">
          <Cpu className="h-4 w-4 animate-pulse" />
        </div>
        <div>
          <div className="flex items-center gap-2">
            <h2 className="text-xs font-bold tracking-widest text-zinc-200 uppercase font-mono">IA CORE WORKSPACE</h2>
            {activeProvider && selectedModel && (
              <span className="bg-teal-950/60 border border-teal-800/80 px-2 py-0.5 text-[10px] font-bold text-teal-400 tracking-wider font-mono uppercase rounded-lg">
                {activeProvider.name} : {selectedModel.split('/').pop() || selectedModel}
              </span>
            )}
          </div>
          <p className="text-[10px] tracking-wide text-zinc-500 font-mono">
            Sessão ativa: <span className="text-zinc-400 font-semibold">{sessionId ? sessionId.slice(0, 8) : 'Nova Sessão'}</span>
          </p>
        </div>
      </div>

      {/* Right Block: Sidebar Toggle Action Buttons */}
      <div className="flex items-center gap-2">
        {/* Toggle Config Sidebar */}
        {onToggleConfig && (
          <button
            onClick={onToggleConfig}
            title="Configurações de Parâmetros"
            className={cn(
              "flex h-9 items-center gap-2 px-3 rounded-xl text-xs font-semibold font-mono border transition-all active:scale-95 duration-150",
              showConfigSidebar
                ? "bg-teal-950/60 border-teal-700 text-teal-400 shadow-md shadow-teal-950/30"
                : "bg-zinc-900/60 border-zinc-800 text-zinc-400 hover:border-zinc-700 hover:bg-zinc-800 hover:text-zinc-200"
            )}
          >
            <Sliders className="w-3.5 h-3.5" />
            <span className="hidden sm:inline">Parâmetros</span>
          </button>
        )}

        {/* Toggle Session Insights */}
        {onToggleInsights && sessionId && (
          <button
            onClick={onToggleInsights}
            title="Memória da Sessão (Insights)"
            className={cn(
              "flex h-9 items-center gap-2 px-3 rounded-xl text-xs font-semibold font-mono border transition-all active:scale-95 duration-150",
              showInsights
                ? "bg-teal-950/60 border-teal-700 text-teal-400 shadow-md shadow-teal-950/30"
                : "bg-zinc-900/60 border-zinc-800 text-zinc-400 hover:border-zinc-700 hover:bg-zinc-800 hover:text-zinc-200"
            )}
          >
            <Brain className="w-3.5 h-3.5" />
            <span className="hidden sm:inline">Memória</span>
          </button>
        )}

        {/* Global LLM API Keys Configuration Link */}
        <Link
          to="/ai"
          title="Configurações do Servidor LLM"
          className="inline-flex h-9 w-9 items-center justify-center border border-zinc-800 bg-zinc-900/60 text-zinc-400 hover:text-zinc-200 hover:border-zinc-700 hover:bg-zinc-800 transition rounded-xl"
        >
          <Settings2 className="w-4 h-4" />
        </Link>
      </div>
    </div>
  )
}