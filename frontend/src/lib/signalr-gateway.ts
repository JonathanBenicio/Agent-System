import * as signalR from '@microsoft/signalr'
import { getAuthToken } from '@/lib/auth'
import { useKnowledgeStore } from '@/store/useKnowledgeStore'

const GATEWAY_HUB_URL = '/hubs/gateway'

let connection: signalR.HubConnection | null = null
let lastTenantId: string | null = null

export function getGatewayConnection(): signalR.HubConnection {
  const currentTenantId = useKnowledgeStore.getState().activeWorkspaceId

  if (connection && lastTenantId !== currentTenantId) {
    const oldConn = connection
    connection = null
    oldConn.stop().catch(err => console.warn('Failed to stop old hub connection on tenant switch:', err))
  }

  if (!connection) {
    lastTenantId = currentTenantId
    const url = currentTenantId ? `${GATEWAY_HUB_URL}?X-Tenant-Id=${encodeURIComponent(currentTenantId)}` : GATEWAY_HUB_URL
    connection = new signalR.HubConnectionBuilder()
      .withUrl(url, {
        withCredentials: true,
        accessTokenFactory: () => getAuthToken() ?? '',
        headers: {
          ...(currentTenantId ? { 'X-Tenant-Id': currentTenantId } : {}),
        },
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(signalR.LogLevel.Warning)
      .build()
  }
  return connection
}

let startGatewayPromise: Promise<void> | null = null

export async function startGatewayConnection(): Promise<void> {
  const conn = getGatewayConnection()
  if (conn.state === signalR.HubConnectionState.Disconnected) {
    startGatewayPromise = conn.start()
    await startGatewayPromise
    startGatewayPromise = null
  }
}

export async function stopGatewayConnection(): Promise<void> {
  if (startGatewayPromise) {
    await startGatewayPromise.catch(() => {})
  }
  if (connection && connection.state !== signalR.HubConnectionState.Disconnected) {
    await connection.stop()
  }
}
