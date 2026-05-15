import { useEffect } from 'react'
import { getGatewayConnection, startGatewayConnection } from '@/lib/signalr-gateway'
import { useToast } from '@/components/shared/Toast'

export function QuotaAlertListener() {
  const { addToast } = useToast()

  useEffect(() => {
    const conn = getGatewayConnection()
    startGatewayConnection().catch(err => console.error('Erro ao iniciar Gateway Hub:', err))

    const handleQuotaThresholdReached = (payload: any) => {
      // O SignalR pode camelCasear as chaves do dicionário dependendo da configuração
      const providerName = payload.providerName || payload.ProviderName
      const type = payload.type || payload.Type
      const percentage = payload.percentage || payload.Percentage
      const remaining = payload.remaining || payload.Remaining
      const limit = payload.limit || payload.Limit
      const currency = payload.currency || payload.Currency

      const message = `🚨 Alerta de ${type}: ${providerName} está com ${Number(percentage).toFixed(1)}% restante (${remaining}/${limit}${currency ? ' ' + currency : ''})`
      
      addToast(message, 'error')
    }

    conn.on('QuotaThresholdReached', handleQuotaThresholdReached)

    return () => {
      conn.off('QuotaThresholdReached', handleQuotaThresholdReached)
    }
  }, [addToast])

  return null
}
