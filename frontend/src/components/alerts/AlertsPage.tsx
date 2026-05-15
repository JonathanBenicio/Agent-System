import { useState, useEffect } from 'react'
import { alertsApi } from '@/lib/api'
import type { SystemAlert } from '@/types/api'
import { PageLoading, PageError } from '@/components/shared/Loading'
import { AlertTriangle, Check, Inbox } from 'lucide-react'
import { cn } from '@/lib/utils'

export default function AlertsPage() {
  const [alerts, setAlerts] = useState<SystemAlert[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  const fetchAlerts = async () => {
    try {
      setLoading(true)
      const data = await alertsApi.getAlerts()
      setAlerts(data)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erro ao carregar alertas')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    fetchAlerts()
  }, [])

  const handleMarkAsRead = async (id: string) => {
    try {
      await alertsApi.markAsRead(id)
      setAlerts(prev => prev.map(a => a.id === id ? { ...a, isRead: true } : a))
    } catch (err) {
      console.error('Erro ao marcar como lido:', err)
    }
  }

  const formatDate = (dateStr: string) => {
    try {
      return new Date(dateStr).toLocaleString('pt-BR', {
        day: '2-digit',
        month: '2-digit',
        year: 'numeric',
        hour: '2-digit',
        minute: '2-digit'
      })
    } catch (e) {
      return dateStr
    }
  }

  if (loading) return <PageLoading />
  if (error) return <PageError message={error} />

  return (
    <div className="p-6 space-y-6 max-w-5xl mx-auto">
      <div className="flex justify-between items-center">
        <div>
          <h1 className="text-3xl font-bold tracking-tight bg-gradient-to-r from-red-500 to-orange-500 bg-clip-text text-transparent">
            Histórico de Alertas
          </h1>
          <p className="text-muted-foreground mt-1">
            Acompanhe os alertas de cota e saldo do sistema.
          </p>
        </div>
        <button 
          onClick={fetchAlerts}
          className="px-4 py-2 bg-secondary text-secondary-foreground rounded-lg hover:bg-secondary/80 transition-colors"
        >
          Atualizar
        </button>
      </div>

      <div className="grid gap-4">
        {alerts.length === 0 ? (
          <div className="flex flex-col items-center justify-center p-12 bg-card rounded-xl border border-border/50 text-center space-y-4">
            <Inbox className="w-12 h-12 text-muted-foreground/50" />
            <div className="space-y-1">
              <p className="font-medium text-lg">Nenhum alerta encontrado</p>
              <p className="text-sm text-muted-foreground">Tudo certo por aqui!</p>
            </div>
          </div>
        ) : (
          alerts.map(alert => (
            <div 
              key={alert.id}
              className={cn(
                "p-5 bg-card rounded-xl border border-border/50 hover:border-border transition-all flex items-start gap-4 backdrop-blur-sm",
                !alert.isRead && "border-l-4 border-l-red-500 bg-red-500/5 shadow-sm"
              )}
            >
              <div className={cn(
                "p-3 rounded-full shrink-0",
                alert.isRead ? "bg-muted text-muted-foreground" : "bg-red-500/10 text-red-500"
              )}>
                <AlertTriangle className="w-5 h-5" />
              </div>
              
              <div className="flex-1 space-y-1 min-w-0">
                <div className="flex justify-between items-start gap-2">
                  <div className="flex items-center gap-2 flex-wrap">
                    <span className="font-semibold text-lg">{alert.providerName}</span>
                    <span className={cn(
                      "text-xs px-2 py-0.5 rounded-full font-medium",
                      alert.isRead ? "bg-secondary text-secondary-foreground" : "bg-red-500/10 text-red-500"
                    )}>
                      {alert.type}
                    </span>
                    <span className="text-xs px-2 py-0.5 rounded-full font-medium border border-border">
                      {alert.percentage.toFixed(1)}% restante
                    </span>
                  </div>
                  <span className="text-sm text-muted-foreground whitespace-nowrap">
                    {formatDate(alert.createdAt)}
                  </span>
                </div>
                
                <p className="text-muted-foreground text-sm md:text-base">
                  {alert.message}
                </p>
              </div>

              {!alert.isRead && (
                <button
                  onClick={() => handleMarkAsRead(alert.id)}
                  className="p-2 hover:bg-muted rounded-lg transition-colors group"
                  title="Marcar como lido"
                >
                  <Check className="w-5 h-5 text-muted-foreground group-hover:text-primary" />
                </button>
              )}
            </div>
          ))
        )}
      </div>
    </div>
  )
}
