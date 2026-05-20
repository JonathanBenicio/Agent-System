import * as signalR from '@microsoft/signalr'
import { getAuthToken, getApiKey } from '@/lib/auth'

const GATEWAY_HUB_URL = '/hubs/gateway'

let connection: signalR.HubConnection | null = null

export function getGatewayConnection(): signalR.HubConnection {
  if (!connection) {
    connection = new signalR.HubConnectionBuilder()
      .withUrl(GATEWAY_HUB_URL, {
        accessTokenFactory: () => getAuthToken() ?? '',
        headers: getApiKey() && !getAuthToken() ? { 'X-Api-Key': getApiKey()! } : {},
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
