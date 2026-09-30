import { useState, useEffect, useCallback } from 'react'
import { MessageSquare, Plus, MoreHorizontal, Pencil, Trash2, Loader2, Search, X } from 'lucide-react'
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
  const { sessions, isLoading, error, deleteSession, renameSession, refresh } = useSessions()
  const [editingId, setEditingId] = useState<string | null>(null)
  const [editTitle, setEditTitle] = useState('')
  const [menuId, setMenuId] = useState<string | null>(null)
  const [searchQuery, setSearchQuery] = useState('')

  // Debounced search trigger
  useEffect(() => {
    const delayDebounceFn = setTimeout(() => {
      void refresh()
    }, 300)

    return () => clearTimeout(delayDebounceFn)
  }, [searchQuery, refresh])

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
    if (id === activeSessionId) {
      handleNewSession()
    }
  }

  // Lógica para divisão cronológica das conversas
  const groupSessionsByDate = useCallback((items: SessionListItem[]) => {
    const groups: { [key: string]: SessionListItem[] } = {
      'Hoje': [],
      'Ontem': [],
      'Últimos 7 dias': [],
      'Mais antigas': [],
    }

    const now = new Date()
    const today = new Date(now.getFullYear(), now.getMonth(), now.getDate())
    const yesterday = new Date(today)
    yesterday.setDate(yesterday.getDate() - 1)
    const sevenDaysAgo = new Date(today)
    sevenDaysAgo.setDate(sevenDaysAgo.getDate() - 7)

    items.forEach(session => {
      const date = new Date(session.lastActivity)
      if (date >= today) {
        groups['Hoje'].push(session)
      } else if (date >= yesterday) {
        groups['Ontem'].push(session)
      } else if (date >= sevenDaysAgo) {
        groups['Últimos 7 dias'].push(session)
      } else {
        groups['Mais antigas'].push(session)
      }
    })

    return Object.entries(groups).filter(([_, list]) => list.length > 0)
  }, [])

  const groupedSessions = groupSessionsByDate(sessions)

  return (
    <div className="flex flex-col h-full bg-zinc-950 border-r border-zinc-800">
      {/* Botão de Nova Conversa (Refatorado com rounded-xl) */}
      <div className="p-3 space-y-2 border-b border-zinc-900 bg-zinc-900/20">
        <button
          onClick={handleNewSession}
          className="w-full flex items-center justify-center gap-2 px-3 py-2.5 rounded-xl bg-zinc-900 border border-zinc-800 hover:bg-zinc-800 text-zinc-100 hover:text-white text-sm font-medium transition-all shadow-sm"
        >
          <Plus className="w-4 h-4 text-teal-400" />
          Nova Conversa
        </button>
      </div>

      {/* Barra de Pesquisa Integrada com Debounce */}
      <div className="px-3 pt-3 pb-2 relative">
        <Search className="absolute left-6 top-1/2 -translate-y-1/2 w-4 h-4 text-zinc-500" />
        <input
          type="text"
          placeholder="Pesquisar histórico..."
          value={searchQuery}
          onChange={e => setSearchQuery(e.target.value)}
          className="w-full pl-9 pr-8 py-2 bg-zinc-900 text-zinc-100 rounded-xl text-xs border border-zinc-800 focus:border-teal-500/50 focus:outline-none placeholder-zinc-500 transition-all font-mono"
        />
        {searchQuery && (
          <button
            onClick={() => setSearchQuery('')}
            className="absolute right-5 top-1/2 -translate-y-1/2 p-0.5 rounded-full hover:bg-zinc-850 text-zinc-500 hover:text-zinc-300"
          >
            <X className="w-3.5 h-3.5" />
          </button>
        )}
      </div>

      {error && (
        <div className="mx-3 my-1 px-3 py-2 text-xs text-red-400 bg-red-950/20 border border-red-900/30 rounded-xl">
          Erro ao carregar sessões
        </div>
      )}

      {/* Lista de Sessões Agrupadas Cronologicamente */}
      <div className="flex-1 overflow-y-auto px-2 pb-4 scrollbar-thin scrollbar-thumb-zinc-800 scrollbar-track-transparent">
        {isLoading && sessions.length === 0 ? (
          <div className="flex items-center justify-center py-12 text-zinc-500">
            <Loader2 className="w-5 h-5 animate-spin text-teal-500" />
          </div>
        ) : sessions.length === 0 ? (
          <div className="px-3 py-12 text-center text-zinc-550 text-xs italic font-mono">
            {searchQuery ? 'Nenhuma conversa encontrada' : 'Nenhuma conversa anterior'}
          </div>
        ) : (
          <div className="space-y-4 pt-1">
            {groupedSessions.map(([groupName, groupItems]) => (
              <div key={groupName} className="space-y-1.5">
                {/* Divisor Visual de Intervalo de Datas */}
                <div className="px-2 text-[10px] font-bold text-zinc-500 uppercase tracking-widest font-mono select-none">
                  {groupName}
                </div>

                <div className="space-y-0.5">
                  {groupItems.map(session => (
                    <div
                      key={session.id}
                      className={cn(
                        'group relative flex items-center gap-2.5 px-3 py-2 rounded-xl cursor-pointer text-sm transition-all border select-none',
                        activeSessionId === session.id
                          ? 'bg-zinc-800 border-zinc-700 text-white shadow-md'
                          : 'bg-transparent border-transparent text-zinc-400 hover:text-zinc-200 hover:bg-zinc-900/60 hover:border-zinc-850',
                      )}
                      onClick={() => handleSelect(session.id)}
                    >
                      <MessageSquare className="w-4 h-4 shrink-0 text-zinc-500 group-hover:text-teal-400 transition-colors" />

                      {editingId === session.id ? (
                        <input
                          autoFocus
                          aria-label="Renomear conversa"
                          value={editTitle}
                          onChange={e => setEditTitle(e.target.value)}
                          onBlur={() => saveEdit(session.id)}
                          onKeyDown={e => {
                            if (e.key === 'Enter') saveEdit(session.id)
                            if (e.key === 'Escape') setEditingId(null)
                          }}
                          className="flex-1 bg-zinc-900 text-white px-2 py-0.5 rounded-lg text-xs border border-zinc-800 outline-none"
                          onClick={e => e.stopPropagation()}
                        />
                      ) : (
                        <div className="flex-1 min-w-0 flex items-center justify-between gap-1.5">
                          <div className="truncate font-medium text-xs leading-5">
                            {session.title}
                          </div>

                          {/* Metadados (Contagem de Mensagens) exibido de forma minimalista */}
                          {session.messageCount > 0 && (
                            <span className="text-[9px] bg-zinc-900/80 border border-zinc-850 px-1.5 py-0.5 rounded-full shrink-0 text-zinc-500 font-mono group-hover:border-zinc-800 transition-colors">
                              {session.messageCount} msg
                            </span>
                          )}
                        </div>
                      )}

                      {/* Tooltip com Sumário Executivo do LLM no Hover (Premium e Fluido) */}
                      {session.summary && !editingId && (
                        <div className="absolute left-[calc(100%+8px)] top-0 w-64 p-3 bg-zinc-950/95 border border-zinc-850 rounded-xl shadow-2xl opacity-0 scale-95 pointer-events-none group-hover:opacity-100 group-hover:scale-100 group-hover:pointer-events-auto transition-all duration-200 z-50 origin-left backdrop-blur-md">
                          <div className="text-[9px] text-zinc-500 font-bold uppercase tracking-wider mb-1 font-mono">
                            Resumo da Conversa
                          </div>
                          <p className="text-[11px] text-zinc-300 leading-relaxed italic">
                            "{session.summary}"
                          </p>
                        </div>
                      )}

                      {/* Menu de Ações Renomear/Excluir */}
                      <div className="relative shrink-0">
                        <button
                          onClick={e => {
                            e.stopPropagation()
                            setMenuId(menuId === session.id ? null : session.id)
                          }}
                          className="opacity-0 group-hover:opacity-100 p-1 rounded-lg hover:bg-zinc-800 text-zinc-400 hover:text-zinc-200 transition-all"
                        >
                          <MoreHorizontal className="w-3.5 h-3.5" />
                        </button>

                        {menuId === session.id && (
                          <div className="absolute right-0 top-full mt-1 z-50 w-32 rounded-xl bg-zinc-900 border border-zinc-800 shadow-2xl py-1 animate-in fade-in slide-in-from-top-1 duration-150">
                            <button
                              onClick={e => {
                                e.stopPropagation()
                                startEdit(session)
                              }}
                              className="w-full flex items-center gap-2 px-3 py-1.5 text-xs text-zinc-350 hover:bg-zinc-800 hover:text-white"
                            >
                              <Pencil className="w-3 h-3 text-zinc-400" />
                              Renomear
                            </button>
                            <button
                              onClick={e => {
                                e.stopPropagation()
                                handleDelete(session.id)
                              }}
                              className="w-full flex items-center gap-2 px-3 py-1.5 text-xs text-red-400 hover:bg-zinc-800 hover:text-red-300"
                            >
                              <Trash2 className="w-3 h-3 text-red-500" />
                              Excluir
                            </button>
                          </div>
                        )}
                      </div>
                    </div>
                  ))}
                </div>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  )
}
