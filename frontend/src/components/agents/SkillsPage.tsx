import { useState } from 'react'
import { Sparkles, Search, Trash2 } from 'lucide-react'
import { useSkills } from '@/hooks/useSkills'
import { PageLoading, PageError } from '@/components/shared/Loading'
import { Badge } from '@/components/shared/Badge'
import { ConfirmModal } from '@/components/shared/ConfirmModal'
import { useToast } from '@/components/shared/Toast'

export function SkillsPage() {
  const { skills, loading, error, refresh, deleteSkill } = useSkills()
  const { addToast } = useToast()
  const [search, setSearch] = useState('')
  const [deleteTarget, setDeleteTarget] = useState<string | null>(null)

  if (loading) return <PageLoading />
  if (error) return <PageError message={error} onRetry={refresh} />

  const filtered = skills.filter(
    s => !search || s.name.toLowerCase().includes(search.toLowerCase()) ||
      s.domain?.toLowerCase().includes(search.toLowerCase())
  )

  const handleDelete = async () => {
    if (!deleteTarget) return
    try {
      await deleteSkill(deleteTarget)
      addToast('Skill removida', 'success')
    } catch {
      addToast('Erro ao remover skill', 'error')
    }
    setDeleteTarget(null)
  }

  return (
    <div className="h-full overflow-y-auto">
      <div className="max-w-7xl mx-auto px-6 py-6 space-y-6">
        <h1 className="text-xl font-semibold text-zinc-100">Skills ({skills.length})</h1>

        <div className="relative max-w-sm">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-zinc-500" />
          <input
            type="text"
            placeholder="Buscar skills..."
            value={search}
            onChange={e => setSearch(e.target.value)}
            className="w-full pl-10 pr-4 py-2 text-sm bg-zinc-900 border border-zinc-700 rounded-lg text-zinc-200 placeholder-zinc-500 focus:outline-none focus:border-teal-600"
          />
        </div>

        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
          {filtered.map(skill => (
            <div key={skill.id} className="bg-zinc-900 border border-zinc-800 rounded-xl p-4 hover:border-zinc-700 transition-colors">
              <div className="flex items-start justify-between mb-2">
                <div className="flex items-center gap-2">
                  <Sparkles className="w-4 h-4 text-amber-400" />
                  <h3 className="text-sm font-semibold text-zinc-100">{skill.name}</h3>
                </div>
                <button
                  onClick={() => setDeleteTarget(skill.id)}
                  className="p-1.5 rounded-lg text-zinc-500 hover:bg-zinc-800 hover:text-red-400"
                >
                  <Trash2 className="w-3.5 h-3.5" />
                </button>
              </div>
              <p className="text-xs text-zinc-400 mb-3 line-clamp-2">{skill.description}</p>
              <div className="flex gap-2">
                {skill.domain && <Badge>{skill.domain}</Badge>}
                {skill.agentName && <Badge variant="teal">{skill.agentName}</Badge>}
              </div>
            </div>
          ))}
          {filtered.length === 0 && (
            <div className="col-span-full text-center py-12 text-zinc-500 text-sm">
              Nenhuma skill encontrada.
            </div>
          )}
        </div>
      </div>

      <ConfirmModal
        open={!!deleteTarget}
        title="Remover Skill"
        message="Tem certeza que deseja remover esta skill?"
        variant="danger"
        confirmLabel="Remover"
        onConfirm={handleDelete}
        onCancel={() => setDeleteTarget(null)}
      />
    </div>
  )
}
