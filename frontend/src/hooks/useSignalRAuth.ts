import { useEffect } from 'react'
import { useAuthStore } from '../store/authStore'
import { stopConnection, startConnection, stopOnnxConnection, startOnnxConnection } from '../lib/signalr'
import { stopGatewayConnection, startGatewayConnection } from '../lib/signalr-gateway'

export function useSignalRAuth() {
  const isAuthenticated = useAuthStore((state) => state.isAuthenticated)

  useEffect(() => {
    if (isAuthenticated) {
      startConnection().catch(console.error)
      startOnnxConnection().catch(console.error)
      startGatewayConnection().catch(console.error)
    } else {
      stopConnection().catch(console.error)
      stopOnnxConnection().catch(console.error)
      stopGatewayConnection().catch(console.error)
    }
  }, [isAuthenticated])
}

