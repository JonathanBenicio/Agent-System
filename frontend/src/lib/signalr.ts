import * as signalR from '@microsoft/signalr'
import { getAuthToken, getApiKey } from '@/lib/auth'
import { useKnowledgeStore } from '@/store/useKnowledgeStore'

const CHAT_HUB_URL = '/hubs/chat'
const ONNX_HUB_URL = '/hubs/onnx'

let connection: signalR.HubConnection | null = null
let lastTenantId: string | null = null

export function getConnection(): signalR.HubConnection {
  const currentTenantId = useKnowledgeStore.getState().activeWorkspaceId

  if (connection && lastTenantId !== currentTenantId) {
    const oldConn = connection
    connection = null
    oldConn.stop().catch(err => console.warn('Failed to stop old hub connection on tenant switch:', err))
  }

  if (!connection) {
    lastTenantId = currentTenantId
    const url = currentTenantId ? `${CHAT_HUB_URL}?X-Tenant-Id=${encodeURIComponent(currentTenantId)}` : CHAT_HUB_URL
    connection = new signalR.HubConnectionBuilder()
      .withUrl(url, {
        accessTokenFactory: () => getAuthToken() ?? '',
        headers: {
          ...(getApiKey() && !getAuthToken() ? { 'X-Api-Key': getApiKey()! } : {}),
          ...(currentTenantId ? { 'X-Tenant-Id': currentTenantId } : {}),
        },
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

// ==========================================
// 🔌 ONNX Hub Connection Helpers
// ==========================================
let onnxConnection: signalR.HubConnection | null = null
let lastOnnxTenantId: string | null = null

export function getOnnxConnection(): signalR.HubConnection {
  const currentTenantId = useKnowledgeStore.getState().activeWorkspaceId

  if (onnxConnection && lastOnnxTenantId !== currentTenantId) {
    const oldConn = onnxConnection
    onnxConnection = null
    oldConn.stop().catch(err => console.warn('Failed to stop old onnx hub connection on tenant switch:', err))
  }

  if (!onnxConnection) {
    lastOnnxTenantId = currentTenantId
    const url = currentTenantId ? `${ONNX_HUB_URL}?X-Tenant-Id=${encodeURIComponent(currentTenantId)}` : ONNX_HUB_URL
    onnxConnection = new signalR.HubConnectionBuilder()
      .withUrl(url, {
        accessTokenFactory: () => getAuthToken() ?? '',
        headers: {
          ...(getApiKey() && !getAuthToken() ? { 'X-Api-Key': getApiKey()! } : {}),
          ...(currentTenantId ? { 'X-Tenant-Id': currentTenantId } : {}),
        },
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(signalR.LogLevel.Information)
      .build()
  }
  return onnxConnection
}

let startOnnxPromise: Promise<void> | null = null

export async function startOnnxConnection(): Promise<void> {
  const conn = getOnnxConnection()
  if (conn.state === signalR.HubConnectionState.Disconnected) {
    startOnnxPromise = conn.start().then(async () => {
      const currentTenantId = useKnowledgeStore.getState().activeWorkspaceId || 'default'
      await conn.invoke('SubscribeToTenant', currentTenantId)
        .catch(err => console.error('Failed to subscribe to tenant on OnnxHub:', err))
    })
    await startOnnxPromise
    startOnnxPromise = null
  }
}

export async function stopOnnxConnection(): Promise<void> {
  if (startOnnxPromise) {
    await startOnnxPromise.catch(() => {})
  }
  if (onnxConnection && onnxConnection.state !== signalR.HubConnectionState.Disconnected) {
    await onnxConnection.stop()
  }
}

export function getOnnxConnectionState(): signalR.HubConnectionState {
  return onnxConnection?.state ?? signalR.HubConnectionState.Disconnected
}

export { signalR }
