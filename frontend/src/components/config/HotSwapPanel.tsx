import { useState } from 'react'
import { Zap, RefreshCw, CheckCircle, XCircle, Clock } from 'lucide-react'
import { configApi, type HotSwapSubsystem, type HotSwapResult } from '@/lib/api'

type SubsystemConfig = {
  label: string
  description: string
  icon: string
  color: string
  bgColor: string
  borderColor: string
}

const SUBSYSTEMS: Record<HotSwapSubsystem, SubsystemConfig> = {
  vectorstore: {
    label: 'Vector Store',
    description: 'Recarrega o provider de busca vetorial (Postgres, Qdrant, Pinecone)',
    icon: '🗄️',
    color: 'text-cyan-400',
    bgColor: 'bg-cyan-900/20',
    borderColor: 'border-cyan-800/50',
  },
  llm: {
    label: 'LLM Provider',
    description: 'Recarrega o provider de linguagem (OpenAI, Ollama, Azure)',
    icon: '🧠',
    color: 'text-violet-400',
    bgColor: 'bg-violet-900/20',
    borderColor: 'border-violet-800/50',
  },
  embedding: {
    label: 'Embedding Provider',
    description: 'Recarrega o provider de embeddings vetoriais',
    icon: '⚡',
    color: 'text-amber-400',
    bgColor: 'bg-amber-900/20',
    borderColor: 'border-amber-800/50',
  },
  all: {
    label: 'Todos os Subsistemas',
    description: 'Invalida o cache de todos os providers simultaneamente (zero-downtime)',
    icon: '🔄',
    color: 'text-teal-400',
    bgColor: 'bg-teal-900/20',
    borderColor: 'border-teal-800/50',
  },
}

type OperationStatus = 'idle' | 'loading' | 'success' | 'error'

interface SubsystemState {
  status: OperationStatus
  result?: HotSwapResult
  error?: string
}

export function HotSwapPanel() {
  const [states, setStates] = useState<Record<string, SubsystemState>>({})

  async function handleHotSwap(subsystem: HotSwapSubsystem) {
    setStates(prev => ({ ...prev, [subsystem]: { status: 'loading' } }))

    try {
      const result = await configApi.hotSwap(subsystem)
      setStates(prev => ({ ...prev, [subsystem]: { status: 'success', result } }))

      // Auto-reset after 4 seconds
      setTimeout(() => {
        setStates(prev => ({ ...prev, [subsystem]: { status: 'idle' } }))
      }, 4000)
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Falha ao acionar hot-swap'
      setStates(prev => ({ ...prev, [subsystem]: { status: 'error', error: message } }))

      setTimeout(() => {
        setStates(prev => ({ ...prev, [subsystem]: { status: 'idle' } }))
      }, 6000)
    }
  }

  const subsystemEntries = Object.entries(SUBSYSTEMS) as [HotSwapSubsystem, SubsystemConfig][]

  return (
    <div className="space-y-4">
      {/* Header */}
      <div className="flex items-center gap-3 pb-3 border-b border-zinc-800">
        <div className="p-2 bg-teal-900/30 rounded-lg">
          <Zap className="w-5 h-5 text-teal-400" />
        </div>
        <div>
          <h2 className="text-sm font-semibold text-zinc-100">Hot-Swap de Configuração</h2>
          <p className="text-xs text-zinc-500 mt-0.5">
            Recarregue providers sem reiniciar a aplicação (zero-downtime)
          </p>
        </div>
      </div>

      {/* Warning Banner */}
      <div className="flex items-start gap-2 px-3 py-2.5 bg-amber-900/10 border border-amber-800/30 rounded-lg">
        <span className="text-amber-400 mt-0.5 flex-shrink-0">⚠️</span>
        <p className="text-xs text-amber-200/80">
          O hot-swap invalida o cache interno do provider. Requisições em andamento continuam usando
          a instância anterior até completar. A nova instância é criada na próxima requisição.
        </p>
      </div>

      {/* Subsystem Cards */}
      <div className="grid grid-cols-1 gap-3">
        {subsystemEntries.map(([key, cfg]) => {
          const state = states[key] ?? { status: 'idle' }
          const isLoading = state.status === 'loading'
          const isAll = key === 'all'

          return (
            <div
              key={key}
              className={`flex items-center justify-between p-4 rounded-xl border transition-all
                ${isAll
                  ? 'bg-teal-900/10 border-teal-800/40'
                  : `${cfg.bgColor} ${cfg.borderColor}`
                }`}
            >
              {/* Left: Info */}
              <div className="flex items-start gap-3 flex-1 min-w-0">
                <span className="text-xl leading-none mt-0.5 flex-shrink-0">{cfg.icon}</span>
                <div className="min-w-0">
                  <div className={`text-sm font-medium ${cfg.color}`}>{cfg.label}</div>
                  <div className="text-xs text-zinc-500 mt-0.5 line-clamp-1">{cfg.description}</div>

                  {/* Status Feedback */}
                  {state.status === 'success' && state.result && (
                    <div className="flex items-center gap-1.5 mt-1.5">
                      <CheckCircle className="w-3.5 h-3.5 text-green-400 flex-shrink-0" />
                      <span className="text-xs text-green-400">{state.result.message}</span>
                    </div>
                  )}
                  {state.status === 'error' && (
                    <div className="flex items-center gap-1.5 mt-1.5">
                      <XCircle className="w-3.5 h-3.5 text-red-400 flex-shrink-0" />
                      <span className="text-xs text-red-400">{state.error}</span>
                    </div>
                  )}
                  {state.status === 'loading' && (
                    <div className="flex items-center gap-1.5 mt-1.5">
                      <Clock className="w-3.5 h-3.5 text-zinc-400 animate-pulse flex-shrink-0" />
                      <span className="text-xs text-zinc-400">Acionando hot-swap...</span>
                    </div>
                  )}
                </div>
              </div>

              {/* Right: Action Button */}
              <button
                id={`hot-swap-${key}`}
                onClick={() => handleHotSwap(key)}
                disabled={isLoading}
                className={`ml-4 flex items-center gap-2 px-3 py-1.5 rounded-lg text-xs font-medium
                  transition-all border flex-shrink-0 disabled:opacity-60 disabled:cursor-not-allowed
                  ${isAll
                    ? 'bg-teal-600 hover:bg-teal-500 text-white border-teal-500 hover:shadow-lg hover:shadow-teal-900/30'
                    : 'bg-zinc-800 hover:bg-zinc-700 text-zinc-200 border-zinc-700 hover:border-zinc-600'
                  }`}
              >
                <RefreshCw className={`w-3.5 h-3.5 ${isLoading ? 'animate-spin' : ''}`} />
                {isLoading ? 'Recarregando...' : 'Hot-Swap'}
              </button>
            </div>
          )
        })}
      </div>

      {/* Footer: Last Operation Summary */}
      {Object.values(states).some(s => s.status === 'success' && s.result) && (
        <div className="pt-3 border-t border-zinc-800">
          <p className="text-xs text-zinc-500">
            Última operação em{' '}
            <span className="text-zinc-400">
              {new Date(
                Object.values(states)
                  .filter(s => s.status === 'success' && s.result)
                  .map(s => s.result!.timestamp)
                  .sort()
                  .at(-1) ?? ''
              ).toLocaleString('pt-BR')}
            </span>
          </p>
        </div>
      )}
    </div>
  )
}
