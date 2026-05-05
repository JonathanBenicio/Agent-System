import { useState } from 'react'
import { Wrench, Search, Play, Trash2, ChevronRight } from 'lucide-react'
import { useTools } from '@/hooks/useTools'
import { PageLoading, PageError } from '@/components/shared/Loading'
import { Badge } from '@/components/shared/Badge'
import { ConfirmModal } from '@/components/shared/ConfirmModal'
import { useToast } from '@/components/shared/Toast'
import type { ToolResult } from '@/types/api'

export function ToolsPage() {
  const { tools, loading, error, refresh, executeTool, deleteTool } = useTools()
  const { addToast } = useToast()
  const [search, setSearch] = useState('')
  const [catFilter, setCatFilter] = useState('')
  const [deleteTarget, setDeleteTarget] = useState<string | null>(null)
  const [executingId, setExecutingId] = useState<string | null>(null)
  const [result, setResult] = useState<ToolResult | null>(null)

  if (loading) return <PageLoading />
  if (error) return <PageError message={error} onRetry={refresh} />

  const categories = [...new Set(tools.map(t => t.category))].sort()
  const filtered = tools.filter(t => {
    const matchSearch = !search || t.name.toLowerCase().includes(search.toLowerCase())
    const matchCat = !catFilter || t.category === catFilter
    return matchSearch && matchCat
  })

  const handleExecute = async (id: string) => {
    setExecutingId(id)
    try {
      const r = await executeTool(id, { action: '', parameters: {} })
      setResult(r)
      addToast(r.success ? 'Tool executada com sucesso' : 'Tool retornou erro', r.success ? 'success' : 'error')
    } catch {
      addToast('Erro ao executar tool', 'error')
    }
    setExecutingId(null)
  }

  const handleDelete = async () => {
    if (!deleteTarget) return
    try {
      await deleteTool(deleteTarget)
      addToast('Tool removida', 'success')
    } catch {
      addToast('Erro ao remover tool', 'error')
    }
    setDeleteTarget(null)
  }

  return (
    <div className="h-full overflow-y-auto">
      <div className="max-w-7xl mx-auto px-6 py-6 space-y-6">
        <h1 className="text-xl font-semibold text-zinc-100">Tools ({tools.length})</h1>

        <div className="flex items-center gap-3">
          <div className="relative flex-1 max-w-sm">
            <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-zinc-500" />
            <input
              type="text"
              placeholder="Buscar tools..."
              value={search}
              onChange={e => setSearch(e.target.value)}
              className="w-full pl-10 pr-4 py-2 text-sm bg-zinc-900 border border-zinc-700 rounded-lg text-zinc-200 placeholder-zinc-500 focus:outline-none focus:border-violet-600"
            />
          </div>
          <select
            value={catFilter}
            onChange={e => setCatFilter(e.target.value)}
            className="px-3 py-2 text-sm bg-zinc-900 border border-zinc-700 rounded-lg text-zinc-300"
          >
            <option value="">Todas categorias</option>
            {categories.map(c => <option key={c} value={c}>{c}</option>)}
          </select>
        </div>

        <div className="space-y-3">
          {filtered.map(tool => (
            <div key={tool.id} className="bg-zinc-900 border border-zinc-800 rounded-xl p-4 hover:border-zinc-700 transition-colors">
              <div className="flex items-start justify-between">
                <div className="flex items-start gap-3">
                  <div className="flex items-center justify-center w-9 h-9 rounded-lg bg-zinc-800 mt-0.5">
                    <Wrench className="w-4 h-4 text-violet-400" />
                  </div>
                  <div>
                    <h3 className="text-sm font-semibold text-zinc-100">{tool.name}</h3>
                    <p className="text-xs text-zinc-400 mt-0.5">{tool.description}</p>
                    <div className="flex gap-2 mt-2">
                      <Badge>{tool.category}</Badge>
                      {tool.isAsync && <Badge variant="warning">Async</Badge>}
                    </div>
                  </div>
                </div>
                <div className="flex gap-1">
                  <button
                    onClick={() => handleExecute(tool.id)}
                    disabled={executingId === tool.id}
                    title="Executar"
                    className="p-1.5 rounded-lg text-zinc-500 hover:bg-zinc-800 hover:text-emerald-400 disabled:opacity-50"
                  >
                    <Play className="w-3.5 h-3.5" />
                  </button>
                  <button
                    onClick={() => setDeleteTarget(tool.id)}
                    title="Remover"
                    className="p-1.5 rounded-lg text-zinc-500 hover:bg-zinc-800 hover:text-red-400"
                  >
                    <Trash2 className="w-3.5 h-3.5" />
                  </button>
                </div>
              </div>
            </div>
          ))}
          {filtered.length === 0 && (
            <div className="text-center py-12 text-zinc-500 text-sm">Nenhuma tool encontrada.</div>
          )}
        </div>

        {result && (
          <div className="bg-zinc-900 border border-zinc-800 rounded-xl p-4">
            <div className="flex items-center gap-2 mb-3">
              <ChevronRight className="w-4 h-4 text-zinc-400" />
              <h3 className="text-sm font-semibold text-zinc-200">Resultado</h3>
              <Badge variant={result.success ? 'success' : 'danger'}>{result.success ? 'Sucesso' : 'Erro'}</Badge>
            </div>
            <pre className="text-xs text-zinc-400 bg-zinc-950 border border-zinc-800 rounded-lg p-3 overflow-x-auto whitespace-pre-wrap">
              {(() => {
                const output = result.output ?? result.data
                return typeof output === 'string' ? output : JSON.stringify(output, null, 2) ?? ''
              })()}
            </pre>
          </div>
        )}
      </div>

      <ConfirmModal
        open={!!deleteTarget}
        title="Remover Tool"
        message={`Tem certeza que deseja remover esta tool?`}
        variant="danger"
        confirmLabel="Remover"
        onConfirm={handleDelete}
        onCancel={() => setDeleteTarget(null)}
      />
    </div>
  )
}
