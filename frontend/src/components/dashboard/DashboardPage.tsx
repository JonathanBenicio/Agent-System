import { RefreshCw, Activity, DollarSign, Server, AlertTriangle, CheckCircle2, XCircle } from 'lucide-react'
import { useDashboard } from '@/hooks/useDashboard'
import { PageLoading, PageError } from '@/components/shared/Loading'
import { Badge } from '@/components/shared/Badge'
import { cn } from '@/lib/utils'
import type { ServiceStatus, CostReport, HealthReport, GatewayMetrics } from '@/types/api'

export function DashboardPage() {
  const { data, loading, error, refresh } = useDashboard()

  if (loading) return <PageLoading />
  if (error || !data) return <PageError message={error ?? 'Sem dados'} onRetry={refresh} />

  return (
    <div className="h-full overflow-y-auto">
      <head>
        <title>Dashboard - Agentic System</title>
        <meta name="description" content="Dashboard de monitoramento e governança de agentes de IA." />
        <meta property="og:title" content="Dashboard - Agentic System" />
      </head>
      <label className="sr-only" aria-label="Dashboard Monitoring">Dashboard</label>
      <div className="max-w-7xl mx-auto px-6 py-6 space-y-6">
        <Header onRefresh={refresh} timestamp={data.timestamp} />
        <MetricsRow metrics={data.metrics} />
        <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
          <HealthCard health={data.health} />
          <CostCard costs={data.costs} />
        </div>
        <ServicesTable services={data.services} />
      </div>
    </div>
  )
}

function Header({ onRefresh, timestamp }: { onRefresh: () => void; timestamp: string }) {
  return (
    <div className="flex items-center justify-between">
      <div>
        <h1 className="text-xl font-semibold text-zinc-100">Dashboard</h1>
        <p className="text-sm text-zinc-500">
          Atualizado em {new Date(timestamp).toLocaleTimeString('pt-BR')}
        </p>
      </div>
      <button
        onClick={onRefresh}
        className="flex items-center gap-2 px-3 py-2 text-sm rounded-lg border border-zinc-700 text-zinc-300 hover:bg-zinc-800"
      >
        <RefreshCw className="w-4 h-4" />
        Atualizar
      </button>
    </div>
  )
}

function MetricsRow({ metrics }: { metrics: GatewayMetrics }) {
  const cards = [
    {
      label: 'Total Requests',
      value: metrics.totalRequests.toLocaleString('pt-BR'),
      icon: Activity,
      color: 'text-blue-400',
    },
    {
      label: 'Taxa de Sucesso',
      value: `${(metrics.overallSuccessRate * 100).toFixed(1)}%`,
      icon: CheckCircle2,
      color: metrics.overallSuccessRate >= 0.95 ? 'text-emerald-400' : 'text-amber-400',
    },
    {
      label: 'Falhas',
      value: metrics.totalFailures.toLocaleString('pt-BR'),
      icon: XCircle,
      color: metrics.totalFailures > 0 ? 'text-red-400' : 'text-zinc-400',
    },
    {
      label: 'Latência Média',
      value: metrics.averageLatency,
      icon: Activity,
      color: 'text-teal-400',
    },
  ]

  return (
    <div className="grid grid-cols-2 lg:grid-cols-4 gap-4">
      {cards.map(c => (
        <div key={c.label} className="bg-zinc-900 border border-zinc-800 rounded-xl p-4">
          <div className="flex items-center gap-2 mb-2">
            <c.icon className={cn('w-4 h-4', c.color)} />
            <span className="text-xs text-zinc-500">{c.label}</span>
          </div>
          <p className="text-2xl font-semibold text-zinc-100">{c.value}</p>
        </div>
      ))}
    </div>
  )
}

function HealthCard({ health }: { health: HealthReport }) {
  return (
    <div className="bg-zinc-900 border border-zinc-800 rounded-xl p-5">
      <div className="flex items-center gap-2 mb-4">
        <Server className="w-4 h-4 text-zinc-400" />
        <h2 className="text-sm font-semibold text-zinc-200">Saúde dos Serviços</h2>
        <Badge variant={health.overallHealthy ? 'success' : 'danger'}>
          {health.overallHealthy ? 'Saudável' : 'Degradado'}
        </Badge>
      </div>
      <div className="grid grid-cols-3 gap-4 mb-4">
        <Stat label="Total" value={health.totalServices} />
        <Stat label="Saudáveis" value={health.healthyServices} color="text-emerald-400" />
        <Stat label="Problemáticos" value={health.unhealthyServices} color="text-red-400" />
      </div>
      {health.services.filter(s => !s.isHealthy).length > 0 && (
        <div className="space-y-2 mt-3 pt-3 border-t border-zinc-800">
          {health.services
            .filter(s => !s.isHealthy)
            .map(s => (
              <div key={s.name} className="flex items-center gap-2 text-sm">
                <AlertTriangle className="w-3.5 h-3.5 text-red-400" />
                <span className="text-zinc-300">{s.name}</span>
                <span className="text-zinc-500 text-xs">— {s.lastError ?? 'sem detalhes'}</span>
              </div>
            ))}
        </div>
      )}
    </div>
  )
}

function CostCard({ costs }: { costs: CostReport }) {
  const pct = costs.usagePercent * 100
  return (
    <div className="bg-zinc-900 border border-zinc-800 rounded-xl p-5">
      <div className="flex items-center gap-2 mb-4">
        <DollarSign className="w-4 h-4 text-zinc-400" />
        <h2 className="text-sm font-semibold text-zinc-200">Custos</h2>
        {costs.budgetAlert && <Badge variant="danger">Alerta de Budget</Badge>}
      </div>
      <div className="grid grid-cols-3 gap-4 mb-4">
        <Stat label="Total" value={`$${costs.totalCost.toFixed(2)}`} />
        <Stat label="Budget Diário" value={`$${costs.dailyBudget.toFixed(2)}`} />
        <Stat label="Uso" value={`${pct.toFixed(1)}%`} color={pct > 80 ? 'text-red-400' : 'text-emerald-400'} />
      </div>
      <div className="w-full bg-zinc-800 rounded-full h-2 mt-2">
        <div
          className={cn('h-2 rounded-full', pct > 80 ? 'bg-red-500' : pct > 50 ? 'bg-amber-500' : 'bg-emerald-500')}
          style={{ width: `${Math.min(pct, 100)}%` }}
        />
      </div>
      {Object.keys(costs.costByService).length > 0 && (
        <div className="mt-4 pt-3 border-t border-zinc-800 space-y-1">
          {Object.entries(costs.costByService)
            .sort(([, a], [, b]) => b - a)
            .slice(0, 5)
            .map(([name, cost]) => (
              <div key={name} className="flex justify-between text-sm">
                <span className="text-zinc-400">{name}</span>
                <span className="text-zinc-300">${cost.toFixed(4)}</span>
              </div>
            ))}
        </div>
      )}
    </div>
  )
}

function ServicesTable({ services }: { services: ServiceStatus[] }) {
  return (
    <div className="bg-zinc-900 border border-zinc-800 rounded-xl overflow-hidden">
      <div className="px-5 py-4 border-b border-zinc-800">
        <h2 className="text-sm font-semibold text-zinc-200">Serviços ({services.length})</h2>
      </div>
      <div className="overflow-x-auto">
        <table className="w-full text-sm">
          <thead>
            <tr className="text-xs text-zinc-500 border-b border-zinc-800">
              <th className="text-left px-5 py-3 font-medium">Serviço</th>
              <th className="text-left px-3 py-3 font-medium">Categoria</th>
              <th className="text-center px-3 py-3 font-medium">Status</th>
              <th className="text-center px-3 py-3 font-medium">Circuit</th>
              <th className="text-right px-3 py-3 font-medium">Requests</th>
              <th className="text-right px-3 py-3 font-medium">Sucesso</th>
              <th className="text-right px-5 py-3 font-medium">Custo</th>
            </tr>
          </thead>
          <tbody>
            {services.map(s => (
              <tr key={s.name} className="border-b border-zinc-800/50 hover:bg-zinc-800/30">
                <td className="px-5 py-3 text-zinc-200 font-medium">{s.name}</td>
                <td className="px-3 py-3 text-zinc-400">{s.category}</td>
                <td className="px-3 py-3 text-center">
                  <Badge variant={s.isEnabled && s.isHealthy ? 'success' : s.isEnabled ? 'warning' : 'default'}>
                    {!s.isEnabled ? 'Desativado' : s.isHealthy ? 'OK' : 'Problema'}
                  </Badge>
                </td>
                <td className="px-3 py-3 text-center">
                  <Badge
                    variant={
                      s.circuitState === 'Closed' ? 'success' : s.circuitState === 'HalfOpen' ? 'warning' : 'danger'
                    }
                  >
                    {s.circuitState}
                  </Badge>
                </td>
                <td className="px-3 py-3 text-right text-zinc-300">{s.requestCount.toLocaleString('pt-BR')}</td>
                <td className="px-3 py-3 text-right text-zinc-300">{(s.successRate * 100).toFixed(1)}%</td>
                <td className="px-5 py-3 text-right text-zinc-300">${s.totalCost.toFixed(4)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  )
}

function Stat({ label, value, color = 'text-zinc-100' }: { label: string; value: string | number; color?: string }) {
  return (
    <div>
      <p className="text-xs text-zinc-500 mb-0.5">{label}</p>
      <p className={cn('text-lg font-semibold', color)}>{value}</p>
    </div>
  )
}
