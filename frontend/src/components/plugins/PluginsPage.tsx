import { useState } from 'react'
import { Plug, Plus, Trash2, Eye, Search } from 'lucide-react'
import { usePlugins } from '@/hooks/usePlugins'
import { PageLoading, PageError } from '@/components/shared/Loading'
import { Badge } from '@/components/shared/Badge'
import { ConfirmModal } from '@/components/shared/ConfirmModal'
import { useToast } from '@/components/shared/Toast'
import { PluginDetailModal } from './PluginDetailModal'
import { PluginLoadModal } from './PluginLoadModal'
import type { PluginSummary } from '@/types/api'

export function PluginsPage() {
  const { plugins, allTools, loading, error, refresh, loadPlugin, deletePlugin } = usePlugins()
  const { addToast } = useToast()
  const [search, setSearch] = useState('')
  const [loadOpen, setLoadOpen] = useState(false)
  const [viewing, setViewing] = useState<PluginSummary | null>(null)
  const [deleteTarget, setDeleteTarget] = useState<string | null>(null)

  if (loading) return <PageLoading />
  if (error) return <PageError message={error} onRetry={refresh} />

  const filtered = plugins.filter(
    p => !search || p.name.toLowerCase().includes(search.toLowerCase())
  )

  const handleDelete = async () => {
    if (!deleteTarget) return
    try {
      await deletePlugin(deleteTarget)
      addToast('Plugin removido', 'success')
    } catch {
      addToast('Erro ao remover plugin', 'error')
    }
    setDeleteTarget(null)
  }

  return (
    <div className="h-full overflow-y-auto">
      <div className="max-w-7xl mx-auto px-6 py-6 space-y-6">
        <div className="flex items-center justify-between">
          <h1 className="text-xl font-semibold text-zinc-100">
            Plugins MCP ({plugins.length})
          </h1>
          <button
            onClick={() => setLoadOpen(true)}
            className="flex items-center gap-2 px-3 py-2 text-sm rounded-lg bg-violet-600 text-white hover:bg-violet-500"
          >
            <Plus className="w-4 h-4" />
            Carregar Plugin
          </button>
        </div>

        <div className="relative max-w-sm">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-zinc-500" />
          <input
            type="text"
            placeholder="Buscar plugins..."
            value={search}
            onChange={e => setSearch(e.target.value)}
            className="w-full pl-10 pr-4 py-2 text-sm bg-zinc-900 border border-zinc-700 rounded-lg text-zinc-200 placeholder-zinc-500 focus:outline-none focus:border-violet-600"
          />
        </div>

        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          {filtered.map(plugin => (
            <div key={plugin.id} className="bg-zinc-900 border border-zinc-800 rounded-xl p-5 hover:border-zinc-700 transition-colors">
              <div className="flex items-start justify-between mb-3">
                <div className="flex items-center gap-2">
                  <Plug className="w-4 h-4 text-violet-400" />
                  <h3 className="text-sm font-semibold text-zinc-100">{plugin.name}</h3>
                </div>
                <Badge variant={plugin.isConnected ? 'success' : 'danger'}>
                  {plugin.isConnected ? 'Conectado' : 'Desconectado'}
                </Badge>
              </div>
              {plugin.description && (
                <p className="text-xs text-zinc-400 mb-3 line-clamp-2">{plugin.description}</p>
              )}
              <div className="flex items-center justify-between">
                <div className="flex gap-2">
                  <Badge>{plugin.toolCount} tools</Badge>
                  <Badge>{plugin.transport}</Badge>
                </div>
                <div className="flex gap-1">
                  <button
                    onClick={() => setViewing(plugin)}
                    className="p-1.5 rounded-lg text-zinc-500 hover:bg-zinc-800 hover:text-zinc-300"
                    title="Ver detalhes"
                  >
                    <Eye className="w-3.5 h-3.5" />
                  </button>
                  <button
                    onClick={() => setDeleteTarget(plugin.id)}
                    className="p-1.5 rounded-lg text-zinc-500 hover:bg-zinc-800 hover:text-red-400"
                    title="Remover"
                  >
                    <Trash2 className="w-3.5 h-3.5" />
                  </button>
                </div>
              </div>
            </div>
          ))}
          {filtered.length === 0 && (
            <div className="col-span-full text-center py-12 text-zinc-500 text-sm">
              Nenhum plugin encontrado.
            </div>
          )}
        </div>

        {allTools.length > 0 && (
          <div className="bg-zinc-900 border border-zinc-800 rounded-xl overflow-hidden">
            <div className="px-5 py-4 border-b border-zinc-800">
              <h2 className="text-sm font-semibold text-zinc-200">Todas as Tools MCP ({allTools.length})</h2>
            </div>
            <div className="divide-y divide-zinc-800/50">
              {allTools.map(tool => (
                <div key={`${tool.pluginId}-${tool.name}`} className="px-5 py-3 flex items-center justify-between">
                  <div>
                    <p className="text-sm text-zinc-200">{tool.name}</p>
                    <p className="text-xs text-zinc-500">{tool.description}</p>
                  </div>
                  <Badge>{tool.pluginName}</Badge>
                </div>
              ))}
            </div>
          </div>
        )}
      </div>

      {loadOpen && (
        <PluginLoadModal
          onLoad={async (req) => {
            try {
              await loadPlugin(req)
              addToast('Plugin carregado', 'success')
              setLoadOpen(false)
            } catch {
              addToast('Erro ao carregar plugin', 'error')
            }
          }}
          onClose={() => setLoadOpen(false)}
        />
      )}

      {viewing && (
        <PluginDetailModal plugin={viewing} onClose={() => setViewing(null)} />
      )}

      <ConfirmModal
        open={!!deleteTarget}
        title="Remover Plugin"
        message="Tem certeza que deseja remover este plugin?"
        variant="danger"
        confirmLabel="Remover"
        onConfirm={handleDelete}
        onCancel={() => setDeleteTarget(null)}
      />
    </div>
  )
}
