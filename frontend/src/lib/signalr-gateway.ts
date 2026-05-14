import * as signalR from '@microsoft/signalr'
import { getAuthToken, getApiKey } from '@/lib/auth'

const GATEWAY_HUB_URL = '/hubs/gateway'

let connection: signalR.HubConnection | null = null

export function getGatewayConnection(): signalR.HubConnection {
  if (!connection) {
    connection = new signalR.HubConnectionBuilder()
      .withUrl(GATEWAY_HUB_URL, {
        accessTokenFactory: () => getAuthToken() ?? '',
        headers: getApiKey() && getApiKey() !== 'admin' && !getAuthToken() ? { 'X-Api-Key': getApiKey()! } : {},
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(signalR.LogLevel.Warning)
      .build()
  }
  return connection
}

export async function startGatewayConnection(): Promise<void> {
  const conn = getGatewayConnection()
  if (conn.state === signalR.HubConnectionState.Disconnected) {
    await conn.start()
  }
}

export async function stopGatewayConnection(): Promise<void> {
  if (connection && connection.state !== signalR.HubConnectionState.Disconnected) {
    await connection.stop()
  }
}
