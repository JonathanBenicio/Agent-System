import { Link } from 'react-router-dom'
import { ChevronRight, Cpu, Settings2 } from 'lucide-react'
import type { LLMProviderInfo } from '@/types/api'
import { Badge } from '@/components/shared/Badge'

interface AISelectorBarProps {
  providers: LLMProviderInfo[]
  selectedProvider: string
  selectedModel: string
  onProviderChange: (providerName: string) => void
  onModelChange: (modelName: string) => void
}

export function AISelectorBar({
  providers,
  selectedProvider,
  selectedModel,
  onProviderChange,
  onModelChange,
}: AISelectorBarProps) {
  const activeProvider = providers.find(provider => provider.name === selectedProvider) ?? providers[0]
  const models = activeProvider?.models ?? []

  return (
    <div className="border-b border-zinc-800 bg-[radial-gradient(circle_at_top_left,_rgba(34,197,94,0.16),_transparent_32%),radial-gradient(circle_at_top_right,_rgba(59,130,246,0.18),_transparent_28%),#09090b] px-4 py-3">
      <div className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
        <div className="flex items-start gap-3">
          <div className="flex h-10 w-10 items-center justify-center rounded-2xl border border-emerald-900/80 bg-emerald-950/70 text-emerald-300">
            <Cpu className="h-5 w-5" />
          </div>
          <div>
            <div className="flex items-center gap-2">
              <h2 className="text-sm font-semibold text-zinc-100">IA ativa no chat</h2>
              {activeProvider ? <Badge variant="violet">{activeProvider.name}</Badge> : null}
            </div>
            <p className="mt-0.5 text-xs text-zinc-400">
              Selecione a IA para esta conversa ou ajuste a configuração completa na área dedicada.
            </p>
          </div>
        </div>

        <div className="flex flex-col gap-2 sm:flex-row sm:items-center">
          <label className="flex min-w-[180px] flex-col gap-1">
            <span className="text-[11px] font-medium uppercase tracking-[0.14em] text-zinc-500">Provider</span>
            <select
              value={selectedProvider}
              onChange={(event) => onProviderChange(event.target.value)}
              className="rounded-xl border border-zinc-700 bg-zinc-900 px-3 py-2 text-sm text-zinc-100 outline-none transition focus:border-emerald-500"
            >
              {providers.map(provider => (
                <option key={provider.name} value={provider.name}>{provider.name}</option>
              ))}
            </select>
          </label>

          <label className="flex min-w-[220px] flex-col gap-1">
            <span className="text-[11px] font-medium uppercase tracking-[0.14em] text-zinc-500">Modelo</span>
            <select
              value={selectedModel}
              onChange={(event) => onModelChange(event.target.value)}
              className="rounded-xl border border-zinc-700 bg-zinc-900 px-3 py-2 text-sm text-zinc-100 outline-none transition focus:border-emerald-500"
            >
              {models.map(model => (
                <option key={model} value={model}>{model}</option>
              ))}
            </select>
          </label>

          <Link
            to="/ai"
            className="inline-flex items-center justify-center gap-2 rounded-xl border border-zinc-700 bg-zinc-900 px-4 py-2 text-sm font-medium text-zinc-200 transition hover:border-emerald-500/60 hover:bg-zinc-800"
          >
            <Settings2 className="h-4 w-4" />
            Configurar IA
            <ChevronRight className="h-4 w-4" />
          </Link>
        </div>
      </div>
    </div>
  )
}