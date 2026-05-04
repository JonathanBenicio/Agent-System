import { useState, useEffect, useCallback, useRef } from 'react'
import { getConnection, startConnection, signalR } from '@/lib/signalr'
import type { ChatMessage, SignalRMessage } from '@/types/chat'

function generateId(): string {
  return crypto.randomUUID?.() ?? Date.now().toString(36) + Math.random().toString(36).slice(2, 7)
}

export function useChat(targetAgent?: string) {
  const [messages, setMessages] = useState<ChatMessage[]>([])
  const [isConnected, setIsConnected] = useState(false)
  const [isProcessing, setIsProcessing] = useState(false)
  const [connectionState, setConnectionState] = useState<string>('Disconnected')
  const [sessionId, setSessionId] = useState<string>('')
  const reconnectRef = useRef(false)
  const sendingRef = useRef(false)

  useEffect(() => {
    const conn = getConnection()

    conn.on('ReceiveMessage', (msg: SignalRMessage) => {
      const chatMsg: ChatMessage = {
        id: generateId(),
        role: 'assistant',
        content: msg.content,
        agentName: msg.agentName,
        agentTier: msg.agentTier,
        actions: msg.actions,
        tools: msg.tools,
        success: msg.success,
        sessionId: msg.sessionId,
        timestamp: msg.timestamp,
      }
      setMessages(prev => [...prev, chatMsg])
      setIsProcessing(false)
      if (msg.sessionId) setSessionId(msg.sessionId)
    })

    conn.on('ProcessingStarted', (_data: { timestamp: string }) => {
      setIsProcessing(true)
    })

    conn.on('ReceiveError', (data: { error: string; timestamp: string }) => {
      const errorMsg: ChatMessage = {
        id: generateId(),
        role: 'system',
        content: `Erro: ${data.error}`,
        timestamp: data.timestamp,
      }
      setMessages(prev => [...prev, errorMsg])
      setIsProcessing(false)
    })

    conn.on('Connected', (data: { connectionId: string; timestamp: string }) => {
      console.log('SignalR connected:', data.connectionId)
    })

    conn.onreconnecting(() => {
      setIsConnected(false)
      setConnectionState('Reconnecting')
      reconnectRef.current = true
    })

    conn.onreconnected(() => {
      setIsConnected(true)
      setConnectionState('Connected')
      reconnectRef.current = false
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
      conn.off('ReceiveMessage')
      conn.off('ProcessingStarted')
      conn.off('ReceiveError')
      conn.off('Connected')
    }
  }, [])

  const sendMessage = useCallback(async (text: string) => {
    if (!text.trim() || sendingRef.current) return
    sendingRef.current = true

    const userMsg: ChatMessage = {
      id: generateId(),
      role: 'user',
      content: text,
      timestamp: new Date().toISOString(),
    }
    setMessages(prev => [...prev, userMsg])

    const conn = getConnection()
    if (conn.state === signalR.HubConnectionState.Connected) {
      setIsProcessing(true)
      try {
        await conn.invoke('SendMessage', 'user-1', text, targetAgent ?? null)
      } catch (err) {
        console.error('SendMessage error:', err)
        // Fallback to REST API
        try {
          const res = await fetch('/api/chat', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ message: text, userId: 'user-1', targetAgent: targetAgent ?? null }),
          })
          const data = await res.json()
          const assistantMsg: ChatMessage = {
            id: generateId(),
            role: 'assistant',
            content: data.response,
            agentName: data.agentUsed,
            agentTier: data.agentTier,
            actions: data.actionsPerformed,
            timestamp: new Date().toISOString(),
          }
          setMessages(prev => [...prev, assistantMsg])
        } catch (restErr) {
          console.error('REST fallback error:', restErr)
          setMessages(prev => [...prev, {
            id: generateId(),
            role: 'system',
            content: 'Erro ao enviar mensagem. Verifique a conexão com o servidor.',
            timestamp: new Date().toISOString(),
          }])
        }
        setIsProcessing(false)
      }
    } else {
      // Use REST API directly
      setIsProcessing(true)
      try {
        const res = await fetch('/api/chat', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ message: text, userId: 'user-1', targetAgent: targetAgent ?? null }),
        })
        const data = await res.json()
        const assistantMsg: ChatMessage = {
          id: generateId(),
          role: 'assistant',
          content: data.response,
          agentName: data.agentUsed,
          agentTier: data.agentTier,
          actions: data.actionsPerformed,
          timestamp: new Date().toISOString(),
        }
        setMessages(prev => [...prev, assistantMsg])
      } catch (err) {
        console.error('REST error:', err)
        setMessages(prev => [...prev, {
          id: generateId(),
          role: 'system',
          content: 'Erro ao enviar mensagem. Verifique a conexão com o servidor.',
          timestamp: new Date().toISOString(),
        }])
      }
      setIsProcessing(false)
    }
    sendingRef.current = false
  }, [])

  const clearMessages = useCallback(() => {
    setMessages([])
    setSessionId('')
  }, [])

  return {
    messages,
    isConnected,
    isProcessing,
    connectionState,
    sessionId,
    sendMessage,
    clearMessages,
  }
}
