import * as signalR from '@microsoft/signalr'
import { getAuthToken, getApiKey } from '@/lib/auth'

const CHAT_HUB_URL = '/hubs/chat'

let connection: signalR.HubConnection | null = null

export function getConnection(): signalR.HubConnection {
  if (!connection) {
    connection = new signalR.HubConnectionBuilder()
      .withUrl(CHAT_HUB_URL, {
        accessTokenFactory: () => getAuthToken() ?? '',
        headers: getApiKey() && !getAuthToken() ? { 'X-Api-Key': getApiKey()! } : {},
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(signalR.LogLevel.Information)
      .build()
  }
  return connection
}

let startPromise: Promise<void> | null = null

export async function startConnection(): Promise<void> {
  const conn = getConnection()
  if (conn.state === signalR.HubConnectionState.Disconnected) {
    startPromise = conn.start()
    await startPromise
    startPromise = null
  }
}

export async function stopConnection(): Promise<void> {
  if (startPromise) {
    await startPromise.catch(() => {})
  }
  if (connection && connection.state !== signalR.HubConnectionState.Disconnected) {
    await connection.stop()
  }
}

export function getConnectionState(): signalR.HubConnectionState {
  return connection?.state ?? signalR.HubConnectionState.Disconnected
}

export { signalR }
