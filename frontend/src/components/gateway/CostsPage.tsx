import { DollarSign, TrendingUp, AlertTriangle } from 'lucide-react'
import { useState, useEffect } from 'react'
import { gatewayApi } from '@/lib/api'
import { PageLoading, PageError } from '@/components/shared/Loading'
import { Badge } from '@/components/shared/Badge'
import { cn } from '@/lib/utils'
import type { CostReport } from '@/types/api'

export function CostsPage() {
  const [data, setData] = useState<CostReport | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  const refresh = async () => {
    try {
      setError(null)
      setLoading(true)
      const costs = await gatewayApi.costs()
      setData(costs)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erro ao carregar custos')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => { refresh() }, [])

  if (loading) return <PageLoading />
  if (error || !data) return <PageError message={error ?? 'Sem dados'} onRetry={refresh} />

  const pct = data.usagePercent * 100
  const sortedServices = Object.entries(data.costByService).sort(([, a], [, b]) => b - a)
  const sortedModels = Object.entries(data.costByModel).sort(([, a], [, b]) => b - a)

  return (
    <div className="h-full overflow-y-auto">
      <div className="max-w-7xl mx-auto px-6 py-6 space-y-6">
        <div className="flex items-center justify-between">
          <h1 className="text-xl font-semibold text-zinc-100">Custos</h1>
          {data.budgetAlert && (
            <Badge variant="danger">
              <AlertTriangle className="w-3 h-3 mr-1" />
              Alerta de Budget
            </Badge>
          )}
        </div>

        {/* Summary */}
        <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
          <SummaryCard
            icon={DollarSign}
            label="Custo Total"
            value={`$${data.totalCost.toFixed(4)}`}
            color="text-teal-400"
          />
          <SummaryCard
            icon={TrendingUp}
            label="Budget Diário"
            value={`$${data.dailyBudget.toFixed(2)}`}
            color="text-blue-400"
          />
          <div className="bg-zinc-900 border border-zinc-800 rounded-xl p-4">
            <p className="text-xs text-zinc-500 mb-2">Uso do Budget</p>
            <p className={cn('text-2xl font-semibold', pct > 80 ? 'text-red-400' : 'text-emerald-400')}>
              {pct.toFixed(1)}%
            </p>
            <div className="w-full bg-zinc-800 rounded-full h-2 mt-3">
              <div
                className={cn('h-2 rounded-full', pct > 80 ? 'bg-red-500' : pct > 50 ? 'bg-amber-500' : 'bg-emerald-500')}
                style={{ width: `${Math.min(pct, 100)}%` }}
              />
            </div>
          </div>
        </div>

        {/* Cost by Service */}
        {sortedServices.length > 0 && (
          <div className="bg-zinc-900 border border-zinc-800 rounded-xl overflow-hidden">
            <div className="px-5 py-4 border-b border-zinc-800">
              <h2 className="text-sm font-semibold text-zinc-200">Custo por Serviço</h2>
            </div>
            <div className="divide-y divide-zinc-800/50">
              {sortedServices.map(([name, cost]) => (
                <CostRow key={name} name={name} cost={cost} total={data.totalCost} />
              ))}
            </div>
          </div>
        )}

        {/* Cost by Model */}
        {sortedModels.length > 0 && (
          <div className="bg-zinc-900 border border-zinc-800 rounded-xl overflow-hidden">
            <div className="px-5 py-4 border-b border-zinc-800">
              <h2 className="text-sm font-semibold text-zinc-200">Custo por Modelo</h2>
            </div>
            <div className="divide-y divide-zinc-800/50">
              {sortedModels.map(([name, cost]) => (
                <CostRow key={name} name={name} cost={cost} total={data.totalCost} />
              ))}
            </div>
          </div>
        )}
      </div>
    </div>
  )
}

function SummaryCard({
  icon: Icon,
  label,
  value,
  color,
}: {
  icon: React.ElementType
  label: string
  value: string
  color: string
}) {
  return (
    <div className="bg-zinc-900 border border-zinc-800 rounded-xl p-4">
      <div className="flex items-center gap-2 mb-2">
        <Icon className={cn('w-4 h-4', color)} />
        <p className="text-xs text-zinc-500">{label}</p>
      </div>
      <p className="text-2xl font-semibold text-zinc-100">{value}</p>
    </div>
  )
}

function CostRow({ name, cost, total }: { name: string; cost: number; total: number }) {
  const pct = total > 0 ? (cost / total) * 100 : 0
  return (
    <div className="flex items-center gap-4 px-5 py-3">
      <span className="text-sm text-zinc-300 flex-1">{name}</span>
      <div className="w-32 bg-zinc-800 rounded-full h-1.5">
        <div className="h-1.5 rounded-full bg-teal-500" style={{ width: `${pct}%` }} />
      </div>
      <span className="text-sm text-zinc-400 w-20 text-right">${cost.toFixed(4)}</span>
      <span className="text-xs text-zinc-500 w-14 text-right">{pct.toFixed(1)}%</span>
    </div>
  )
}
