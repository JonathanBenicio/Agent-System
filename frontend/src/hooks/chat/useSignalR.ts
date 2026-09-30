import { useState, useEffect } from 'react'
import { getConnection, startConnection } from '@/lib/signalr'

export function useSignalR() {
  const [isConnected, setIsConnected] = useState(false)
  const [connectionState, setConnectionState] = useState<string>('Disconnected')

  useEffect(() => {
    const conn = getConnection()

    const onConnected = (data: { connectionId: string }) => {
      console.log('SignalR connected:', data.connectionId)
      setIsConnected(true)
      setConnectionState('Connected')
    }

    conn.on('Connected', onConnected)

    conn.onreconnecting(() => {
      setIsConnected(false)
      setConnectionState('Reconnecting')
    })

    conn.onreconnected(() => {
      setIsConnected(true)
      setConnectionState('Connected')
    })

    conn.onclose(() => {
      setIsConnected(false)
      setConnectionState('Disconnected')
    })

    startConnection()
      .then(() => {
        setIsConnected(true)
        setConnectionState('Connected')
      })
      .catch((err) => {
        console.error('SignalR connection error:', err)
        setConnectionState('Error')
      })

    return () => {
      conn.off('Connected', onConnected)
    }
  }, [])

  return { isConnected, connectionState }
}
