import { useState, useEffect } from 'react'
import { CheckCircle2, XCircle, RefreshCw } from 'lucide-react'
import { gatewayApi } from '@/lib/api'
import { PageLoading, PageError } from '@/components/shared/Loading'
import { Badge } from '@/components/shared/Badge'
import { cn } from '@/lib/utils'
import type { HealthReport } from '@/types/api'

export function HealthPage() {
  const [data, setData] = useState<HealthReport | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  const refresh = async () => {
    try {
      setError(null)
      setLoading(true)
      const health = await gatewayApi.health()
      setData(health)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erro ao carregar saúde')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => { refresh() }, [])

  if (loading) return <PageLoading />
  if (error || !data) return <PageError message={error ?? 'Sem dados'} onRetry={refresh} />

  return (
    <div className="h-full overflow-y-auto">
      <label className="sr-only" aria-label="Saúde do Gateway">Saúde</label>
      <div className="max-w-7xl mx-auto px-6 py-6 space-y-6">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-3">
            <h1 className="text-xl font-semibold text-zinc-100">Saúde do Sistema</h1>
            <Badge variant={data.overallHealthy ? 'success' : 'danger'}>
              {data.overallHealthy ? 'Saudável' : 'Degradado'}
            </Badge>
          </div>
          <button
            onClick={refresh}
            className="flex items-center gap-2 px-3 py-2 text-sm rounded-lg border border-zinc-700 text-zinc-300 hover:bg-zinc-800"
          >
            <RefreshCw className="w-4 h-4" />
            Atualizar
          </button>
        </div>

        {/* Summary */}
        <div className="grid grid-cols-3 gap-4">
          <StatCard label="Total de Serviços" value={data.totalServices} />
          <StatCard label="Saudáveis" value={data.healthyServices} color="text-emerald-400" />
          <StatCard label="Problemáticos" value={data.unhealthyServices} color={data.unhealthyServices > 0 ? 'text-red-400' : 'text-zinc-400'} />
        </div>

        {/* Service List */}
        <div className="space-y-3">
          {data.services.map(svc => (
            <div
              key={svc.name}
              className={cn(
                'bg-zinc-900 border rounded-xl p-4',
                svc.isHealthy ? 'border-zinc-800' : 'border-red-900/50'
              )}
            >
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-3">
                  {svc.isHealthy ? (
                    <CheckCircle2 className="w-5 h-5 text-emerald-400" />
                  ) : (
                    <XCircle className="w-5 h-5 text-red-400" />
                  )}
                  <div>
                    <h3 className="text-sm font-semibold text-zinc-100">{svc.name}</h3>
                    <p className="text-xs text-zinc-500">{svc.category}</p>
                  </div>
                </div>
                <div className="flex items-center gap-3">
                  <div className="text-right">
                    <p className="text-xs text-zinc-500">Latência</p>
                    <p className="text-sm text-zinc-300">{svc.latency}</p>
                  </div>
                  <Badge variant={svc.isHealthy ? 'success' : 'danger'}>
                    {svc.isHealthy ? 'OK' : 'Falha'}
                  </Badge>
                </div>
              </div>
              {svc.lastError && (
                <p className="mt-3 text-xs text-red-400 bg-red-900/20 border border-red-900/50 rounded-lg px-3 py-2">
                  {svc.lastError}
                </p>
              )}
              {svc.lastCheck && (
                <p className="mt-2 text-xs text-zinc-500">
                  Última verificação: {new Date(svc.lastCheck).toLocaleString('pt-BR')}
                </p>
              )}
            </div>
          ))}
        </div>
      </div>
    </div>
  )
}

function StatCard({ label, value, color = 'text-zinc-100' }: { label: string; value: number; color?: string }) {
  return (
    <div className="bg-zinc-900 border border-zinc-800 rounded-xl p-4">
      <p className="text-xs text-zinc-500 mb-1">{label}</p>
      <p className={cn('text-2xl font-semibold', color)}>{value}</p>
    </div>
  )
}
