import { useState } from 'react'
import { Server, Power, RefreshCw, Search } from 'lucide-react'
import { useGatewayServices } from '@/hooks/useGatewayServices'
import { PageLoading, PageError } from '@/components/shared/Loading'
import { Badge } from '@/components/shared/Badge'
import { useToast } from '@/components/shared/Toast'
import { cn } from '@/lib/utils'

export function ServicesPage() {
  const { services, loading, error, refresh, toggleService } = useGatewayServices()
  const { addToast } = useToast()
  const [search, setSearch] = useState('')
  const [catFilter, setCatFilter] = useState('')
  const [toggling, setToggling] = useState<string | null>(null)

  if (loading) return <PageLoading />
  if (error) return <PageError message={error} onRetry={refresh} />

  const categories = [...new Set(services.map(s => s.category))].sort()
  const filtered = services.filter(s => {
    const matchSearch = !search || s.name.toLowerCase().includes(search.toLowerCase())
    const matchCat = !catFilter || s.category === catFilter
    return matchSearch && matchCat
  })

  const handleToggle = async (name: string, enable: boolean) => {
    setToggling(name)
    try {
      await toggleService(name, enable)
      addToast(`${name} ${enable ? 'ativado' : 'desativado'}`, 'success')
    } catch {
      addToast(`Erro ao ${enable ? 'ativar' : 'desativar'} ${name}`, 'error')
    }
    setToggling(null)
  }

  return (
    <div className="h-full overflow-y-auto">
      <div className="max-w-7xl mx-auto px-6 py-6 space-y-6">
        <div className="flex items-center justify-between">
          <h1 className="text-xl font-semibold text-zinc-100">Serviços ({services.length})</h1>
          <button
            onClick={refresh}
            className="flex items-center gap-2 px-3 py-2 text-sm rounded-lg border border-zinc-700 text-zinc-300 hover:bg-zinc-800"
          >
            <RefreshCw className="w-4 h-4" />
            Atualizar
          </button>
        </div>

        <div className="flex items-center gap-3">
          <div className="relative flex-1 max-w-sm">
            <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-zinc-500" />
            <input
              type="text"
              placeholder="Buscar serviços..."
              value={search}
              onChange={e => setSearch(e.target.value)}
              className="w-full pl-10 pr-4 py-2 text-sm bg-zinc-900 border border-zinc-700 rounded-lg text-zinc-200 placeholder-zinc-500 focus:outline-none focus:border-teal-600"
            />
          </div>
          <select
            value={catFilter}
            onChange={e => setCatFilter(e.target.value)}
            className="px-3 py-2 text-sm bg-zinc-900 border border-zinc-700 rounded-lg text-zinc-300"
          >
            <option value="">Todas categorias</option>
            {categories.map(c => <option key={c} value={c}>{c}</option>)}
          </select>
        </div>

        <div className="space-y-3">
          {filtered.map(svc => (
            <div key={svc.name} className="bg-zinc-900 border border-zinc-800 rounded-xl p-5 hover:border-zinc-700 transition-colors">
              <div className="flex items-start justify-between">
                <div className="flex items-start gap-3">
                  <div className={cn(
                    'flex items-center justify-center w-10 h-10 rounded-lg',
                    svc.isEnabled && svc.isHealthy ? 'bg-emerald-900/50' : svc.isEnabled ? 'bg-amber-900/50' : 'bg-zinc-800'
                  )}>
                    <Server className={cn(
                      'w-5 h-5',
                      svc.isEnabled && svc.isHealthy ? 'text-emerald-400' : svc.isEnabled ? 'text-amber-400' : 'text-zinc-500'
                    )} />
                  </div>
                  <div>
                    <h3 className="text-sm font-semibold text-zinc-100">{svc.name}</h3>
                    <p className="text-xs text-zinc-400 mt-0.5">{svc.category}</p>
                    <div className="flex gap-2 mt-2">
                      <Badge variant={svc.isHealthy ? 'success' : 'danger'}>
                        {svc.isHealthy ? 'Saudável' : 'Problema'}
                      </Badge>
                      <Badge variant={
                        svc.circuitState === 'Closed' ? 'success' :
                        svc.circuitState === 'HalfOpen' ? 'warning' : 'danger'
                      }>
                        Circuit: {svc.circuitState}
                      </Badge>
                    </div>
                  </div>
                </div>
                <button
                  onClick={() => handleToggle(svc.name, !svc.isEnabled)}
                  disabled={toggling === svc.name}
                  className={cn(
                    'flex items-center gap-2 px-3 py-2 text-sm rounded-lg border transition-colors disabled:opacity-50',
                    svc.isEnabled
                      ? 'border-red-800 text-red-400 hover:bg-red-900/30'
                      : 'border-emerald-800 text-emerald-400 hover:bg-emerald-900/30'
                  )}
                >
                  <Power className="w-4 h-4" />
                  {svc.isEnabled ? 'Desativar' : 'Ativar'}
                </button>
              </div>
              <div className="grid grid-cols-3 gap-4 mt-4 pt-4 border-t border-zinc-800">
                <StatMini label="Requests" value={svc.requestCount.toLocaleString('pt-BR')} />
                <StatMini label="Sucesso" value={`${(svc.successRate * 100).toFixed(1)}%`} />
                <StatMini label="Custo" value={`$${svc.totalCost.toFixed(4)}`} />
              </div>
            </div>
          ))}
          {filtered.length === 0 && (
            <div className="text-center py-12 text-zinc-500 text-sm">Nenhum serviço encontrado.</div>
          )}
        </div>
      </div>
    </div>
  )
}

function StatMini({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <p className="text-xs text-zinc-500">{label}</p>
      <p className="text-sm font-medium text-zinc-200">{value}</p>
    </div>
  )
}
