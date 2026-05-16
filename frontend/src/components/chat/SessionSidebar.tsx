import { useState } from 'react'
import { MessageSquare, Plus, MoreHorizontal, Pencil, Trash2, Loader2 } from 'lucide-react'
import { useSessions } from '@/hooks/useSessions'
import { cn } from '@/lib/utils'
import type { SessionListItem } from '@/types/api'

interface SessionSidebarProps {
  activeSessionId?: string
  onSelectSession: (id: string) => void
  onNewSession: () => void
  onClearMessages: () => void
}

export function SessionSidebar({
  activeSessionId,
  onSelectSession,
  onNewSession,
  onClearMessages,
}: SessionSidebarProps) {
  const { sessions, isLoading, error, deleteSession, renameSession } = useSessions()
  const [editingId, setEditingId] = useState<string | null>(null)
  const [editTitle, setEditTitle] = useState('')
  const [menuId, setMenuId] = useState<string | null>(null)

  const handleNewSession = () => {
    onClearMessages()
    onNewSession()
  }

  const handleSelect = (id: string) => {
    onSelectSession(id)
    setMenuId(null)
  }

  const startEdit = (session: SessionListItem) => {
    setEditingId(session.id)
    setEditTitle(session.title)
    setMenuId(null)
  }

  const saveEdit = async (id: string) => {
    if (editTitle.trim()) {
      await renameSession(id, editTitle.trim())
    }
    setEditingId(null)
  }

  const handleDelete = async (id: string) => {
    await deleteSession(id)
    setMenuId(null)
  }

  if (isLoading && sessions.length === 0) {
    return (
      <div className="flex items-center justify-center h-full text-zinc-400">
        <Loader2 className="w-5 h-5 animate-spin" />
      </div>
    )
  }

  return (
    <div className="flex flex-col h-full bg-zinc-900 border-r border-zinc-800">
      <div className="p-3 border-b border-zinc-800">
        <button
          onClick={handleNewSession}
          className="w-full flex items-center gap-2 px-3 py-2 rounded-lg bg-zinc-800 hover:bg-zinc-700 text-zinc-200 text-sm transition-colors"
        >
          <Plus className="w-4 h-4" />
          Nova Conversa
        </button>
      </div>

      {error && (
        <div className="px-3 py-2 text-xs text-red-400 bg-red-900/20">
          Erro ao carregar sessões
        </div>
      )}

      <div className="flex-1 overflow-y-auto">
        {sessions.length === 0 ? (
          <div className="px-3 py-8 text-center text-zinc-500 text-sm">
            Nenhuma conversa anterior
          </div>
        ) : (
          <div className="p-2 space-y-1">
            {sessions.map(session => (
              <div
                key={session.id}
                className={cn(
                  'group relative flex items-center gap-2 px-3 py-2 rounded-lg cursor-pointer text-sm transition-colors',
                  activeSessionId === session.id
                    ? 'bg-zinc-700 text-white'
                    : 'text-zinc-300 hover:bg-zinc-800',
                )}
                onClick={() => handleSelect(session.id)}
              >
                <MessageSquare className="w-4 h-4 shrink-0" />

                {editingId === session.id ? (
                  <input
                    autoFocus
                    value={editTitle}
                    onChange={e => setEditTitle(e.target.value)}
                    onBlur={() => saveEdit(session.id)}
                    onKeyDown={e => {
                      if (e.key === 'Enter') saveEdit(session.id)
                      if (e.key === 'Escape') setEditingId(null)
                    }}
                    className="flex-1 bg-zinc-700 text-white px-1 py-0.5 rounded text-sm outline-none"
                    onClick={e => e.stopPropagation()}
                  />
                ) : (
                  <span className="flex-1 truncate">{session.title}</span>
                )}

                <div className="relative">
                  <button
                    onClick={e => {
                      e.stopPropagation()
                      setMenuId(menuId === session.id ? null : session.id)
                    }}
                    className="opacity-0 group-hover:opacity-100 p-1 rounded hover:bg-zinc-600 transition-opacity"
                  >
                    <MoreHorizontal className="w-4 h-4" />
                  </button>

                  {menuId === session.id && (
                    <div className="absolute right-0 top-full z-50 w-36 rounded-lg bg-zinc-800 border border-zinc-700 shadow-xl py-1">
                      <button
                        onClick={e => {
                          e.stopPropagation()
                          startEdit(session)
                        }}
                        className="w-full flex items-center gap-2 px-3 py-1.5 text-sm text-zinc-300 hover:bg-zinc-700"
                      >
                        <Pencil className="w-3.5 h-3.5" />
                        Renomear
                      </button>
                      <button
                        onClick={e => {
                          e.stopPropagation()
                          handleDelete(session.id)
                        }}
                        className="w-full flex items-center gap-2 px-3 py-1.5 text-sm text-red-400 hover:bg-zinc-700"
                      >
                        <Trash2 className="w-3.5 h-3.5" />
                        Excluir
                      </button>
                    </div>
                  )}
                </div>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  )
}
