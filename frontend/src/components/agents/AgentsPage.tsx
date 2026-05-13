import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { Bot, Plus, Search, Trash2, Edit, Eye, MessageSquare } from 'lucide-react'
import { useAgents } from '@/hooks/useAgents'
import { PageLoading, PageError } from '@/components/shared/Loading'
import { Badge } from '@/components/shared/Badge'
import { ConfirmModal } from '@/components/shared/ConfirmModal'
import { useToast } from '@/components/shared/Toast'
import { AgentFormModal } from './AgentFormModal'
import { AgentDetailModal } from './AgentDetailModal'
import { cn } from '@/lib/utils'
import { TierLabels, TierColors } from '@/types/api'
import type { AgentInfo } from '@/types/api'

export function AgentsPage() {
  const { agents, loading, error, refresh, createAgent, updateAgent, deleteAgent } = useAgents()
  const { addToast } = useToast()
  const navigate = useNavigate()
  const [search, setSearch] = useState('')
  const [tierFilter, setTierFilter] = useState<number | null>(null)
  const [formOpen, setFormOpen] = useState(false)
  const [editingAgent, setEditingAgent] = useState<AgentInfo | null>(null)
  const [viewingAgent, setViewingAgent] = useState<AgentInfo | null>(null)
  const [deleteTarget, setDeleteTarget] = useState<string | null>(null)

  if (loading) return <PageLoading />
  if (error) return <PageError message={error} onRetry={refresh} />

  const filtered = agents.filter(a => {
    const matchSearch = !search || a.name.toLowerCase().includes(search.toLowerCase()) ||
      a.description.toLowerCase().includes(search.toLowerCase())
    const matchTier = tierFilter === null || a.tier === tierFilter
    return matchSearch && matchTier
  })

  const handleDelete = async () => {
    if (!deleteTarget) return
    try {
      await deleteAgent(deleteTarget)
      addToast(`Agent "${deleteTarget}" removido`, 'success')
    } catch {
      addToast('Erro ao remover agent', 'error')
    }
    setDeleteTarget(null)
  }

  return (
    <div className="h-full overflow-y-auto">
      <div className="max-w-7xl mx-auto px-6 py-6 space-y-6">
        <div className="flex items-center justify-between">
          <h1 className="text-xl font-semibold text-zinc-100">Agents ({agents.length})</h1>
          <button
            onClick={() => { setEditingAgent(null); setFormOpen(true) }}
            className="flex items-center gap-2 px-3 py-2 text-sm rounded-lg bg-teal-600 text-white hover:bg-teal-500"
          >
            <Plus className="w-4 h-4" />
            Novo Agent
          </button>
        </div>

        {/* Filters */}
        <div className="flex items-center gap-3">
          <div className="relative flex-1 max-w-sm">
            <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-zinc-500" />
            <input
              type="text"
              placeholder="Buscar agents..."
              value={search}
              onChange={e => setSearch(e.target.value)}
              className="w-full pl-10 pr-4 py-2 text-sm bg-zinc-900 border border-zinc-700 rounded-lg text-zinc-200 placeholder-zinc-500 focus:outline-none focus:border-teal-600"
            />
          </div>
          <div className="flex gap-1">
            <FilterBtn label="Todos" active={tierFilter === null} onClick={() => setTierFilter(null)} />
            {[0, 1, 2, 3].map(t => (
              <FilterBtn key={t} label={TierLabels[t]} active={tierFilter === t} onClick={() => setTierFilter(t)} />
            ))}
          </div>
        </div>

        {/* Grid */}
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
          {filtered.map(agent => (
            <AgentCard
              key={agent.name}
              agent={agent}
              onView={() => setViewingAgent(agent)}
              onChat={() => navigate(`/chat/${agent.name}`)}
              onEdit={() => { setEditingAgent(agent); setFormOpen(true) }}
              onDelete={() => setDeleteTarget(agent.name)}
            />
          ))}
          {filtered.length === 0 && (
            <div className="col-span-full text-center py-12 text-zinc-500 text-sm">
              Nenhum agent encontrado.
            </div>
          )}
        </div>
      </div>

      {formOpen && (
        <AgentFormModal
          agent={editingAgent}
          onSave={async (spec) => {
            try {
              if (editingAgent) {
                await updateAgent(editingAgent.name, spec)
                addToast('Agent atualizado', 'success')
              } else {
                await createAgent(spec)
                addToast('Agent criado', 'success')
              }
              setFormOpen(false)
            } catch {
              addToast('Erro ao salvar agent', 'error')
            }
          }}
          onClose={() => setFormOpen(false)}
        />
      )}

      {viewingAgent && (
        <AgentDetailModal agent={viewingAgent} onClose={() => setViewingAgent(null)} />
      )}

      <ConfirmModal
        open={!!deleteTarget}
        title="Remover Agent"
        message={`Tem certeza que deseja remover o agent "${deleteTarget}"?`}
        variant="danger"
        confirmLabel="Remover"
        onConfirm={handleDelete}
        onCancel={() => setDeleteTarget(null)}
      />
    </div>
  )
}

function AgentCard({
  agent,
  onView,
  onChat,
  onEdit,
  onDelete,
}: {
  agent: AgentInfo
  onView: () => void
  onChat: () => void
  onEdit: () => void
  onDelete: () => void
}) {
  return (
    <div className="bg-zinc-900 border border-zinc-800 rounded-xl p-5 hover:border-zinc-700 transition-colors">
      <div className="flex items-start justify-between mb-3">
        <div className="flex items-center gap-2">
          <div
            className={cn(
              'flex items-center justify-center w-8 h-8 rounded-lg',
              TierColors[agent.tier] ?? 'bg-zinc-600'
            )}
          >
            <Bot className="w-4 h-4 text-white" />
          </div>
          <div>
            <h3 className="text-sm font-semibold text-zinc-100">{agent.name}</h3>
            <span className="text-xs text-zinc-500">{agent.domain}</span>
          </div>
        </div>
        <Badge variant={agent.isActive ? 'success' : 'default'}>
          {agent.isActive ? 'Ativo' : 'Inativo'}
        </Badge>
      </div>
      <p className="text-xs text-zinc-400 mb-3 line-clamp-2">{agent.description}</p>
      <div className="flex items-center justify-between">
        <Badge variant="teal">{TierLabels[agent.tier] ?? `T${agent.tier}`}</Badge>
        <div className="flex gap-1">
          <IconBtn icon={MessageSquare} onClick={onChat} title="Chat direto" />
          <IconBtn icon={Eye} onClick={onView} title="Ver detalhes" />
          <IconBtn icon={Edit} onClick={onEdit} title="Editar" />
          <IconBtn icon={Trash2} onClick={onDelete} title="Remover" className="hover:text-red-400" />
        </div>
      </div>
    </div>
  )
}

function FilterBtn({ label, active, onClick }: { label: string; active: boolean; onClick: () => void }) {
  return (
    <button
      onClick={onClick}
      className={cn(
        'px-3 py-1.5 text-xs rounded-lg border transition-colors',
        active ? 'bg-teal-600 border-teal-500 text-white' : 'border-zinc-700 text-zinc-400 hover:bg-zinc-800'
      )}
    >
      {label}
    </button>
  )
}

function IconBtn({
  icon: Icon,
  onClick,
  title,
  className,
}: {
  icon: React.ElementType
  onClick: () => void
  title: string
  className?: string
}) {
  return (
    <button
      onClick={onClick}
      title={title}
      className={cn('p-1.5 rounded-lg text-zinc-500 hover:bg-zinc-800 hover:text-zinc-300', className)}
    >
      <Icon className="w-3.5 h-3.5" />
    </button>
  )
}
