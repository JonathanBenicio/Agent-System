import { useState } from 'react'
import { Plug, Plus, Trash2, Eye, Search, Edit2, Wrench } from 'lucide-react'
import { usePlugins } from '@/hooks/usePlugins'
import { PageLoading, PageError } from '@/components/shared/Loading'
import { Badge } from '@/components/shared/Badge'
import { ConfirmModal } from '@/components/shared/ConfirmModal'
import { useToast } from '@/components/shared/Toast'
import { PluginDetailModal } from './PluginDetailModal'
import { PluginLoadModal } from './PluginLoadModal'
import { cn } from '@/lib/utils'
import type { PluginSummary } from '@/types/api'

export function PluginsPage() {
  const { plugins, allTools, loading, error, refresh, loadPlugin, deletePlugin, updatePlugin, getPluginDetails } = usePlugins()
  const { addToast } = useToast()
  const [activeTab, setActiveTab] = useState<'plugins' | 'tools'>('plugins')
  const [search, setSearch] = useState('')
  const [loadOpen, setLoadOpen] = useState(false)
  const [editing, setEditing] = useState<PluginSummary | null>(null)
  const [viewing, setViewing] = useState<PluginSummary | null>(null)
  const [deleteTarget, setDeleteTarget] = useState<string | null>(null)

  if (loading && plugins.length === 0) return <PageLoading />
  if (error && plugins.length === 0) return <PageError message={error} onRetry={refresh} />

  const filteredPlugins = plugins.filter(
    p => !search || p.name.toLowerCase().includes(search.toLowerCase())
  )

  const filteredTools = allTools.filter(
    t => !search || t.name.toLowerCase().includes(search.toLowerCase()) || t.description?.toLowerCase().includes(search.toLowerCase())
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

  const handleEdit = async (plugin: PluginSummary) => {
    try {
      const full = await getPluginDetails(plugin.id)
      setEditing(full)
    } catch {
      addToast('Erro ao carregar configurações do plugin', 'error')
    }
  }

  return (
    <div className="h-full flex flex-col overflow-hidden">
      <div className="flex-none max-w-7xl w-full mx-auto px-6 pt-6 space-y-6">
        <div className="flex items-center justify-between">
          <h1 className="text-xl font-semibold text-zinc-100">
            Plugins MCP
          </h1>
          <button
            onClick={() => setLoadOpen(true)}
            className="flex items-center gap-2 px-3 py-2 text-sm rounded-lg bg-teal-600 text-white hover:bg-teal-500"
          >
            <Plus className="w-4 h-4" />
            Carregar Plugin
          </button>
        </div>

        <div className="flex items-center gap-1 p-1 bg-zinc-900 border border-zinc-800 rounded-lg w-fit">
          <button
            onClick={() => setActiveTab('plugins')}
            className={cn(
              "flex items-center gap-2 px-3 py-1.5 text-sm font-medium rounded-md transition-colors",
              activeTab === 'plugins' ? "bg-zinc-800 text-zinc-100" : "text-zinc-500 hover:text-zinc-300"
            )}
          >
            <Plug className="w-4 h-4" />
            Plugins ({plugins.length})
          </button>
          <button
            onClick={() => setActiveTab('tools')}
            className={cn(
              "flex items-center gap-2 px-3 py-1.5 text-sm font-medium rounded-md transition-colors",
              activeTab === 'tools' ? "bg-zinc-800 text-zinc-100" : "text-zinc-500 hover:text-zinc-300"
            )}
          >
            <Wrench className="w-4 h-4" />
            Explorer ({allTools.length})
          </button>
        </div>

        <div className="relative max-w-sm">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-zinc-500" />
          <input
            type="text"
            placeholder={activeTab === 'plugins' ? "Buscar plugins..." : "Buscar tools em todos os plugins..."}
            value={search}
            onChange={e => setSearch(e.target.value)}
            className="w-full pl-10 pr-4 py-2 text-sm bg-zinc-900 border border-zinc-700 rounded-lg text-zinc-200 placeholder-zinc-500 focus:outline-none focus:border-teal-600"
          />
        </div>
      </div>

      <div className="flex-1 overflow-y-auto mt-6">
        <div className="max-w-7xl mx-auto px-6 pb-12">
          {activeTab === 'plugins' ? (
            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
              {filteredPlugins.map(plugin => (
                <div key={plugin.id} className="group bg-zinc-900 border border-zinc-800 rounded-xl p-5 hover:border-zinc-700 transition-colors">
                  <div className="flex items-start justify-between mb-3">
                    <div className="flex items-center gap-2">
                      <Plug className="w-4 h-4 text-teal-400" />
                      <h3 className="text-sm font-semibold text-zinc-100 truncate max-w-[150px]" title={plugin.name}>{plugin.name}</h3>
                    </div>
                    <Badge variant={plugin.isEnabled ? 'success' : 'danger'}>
                      {plugin.status === 'Running' ? 'Conectado' : plugin.status || (plugin.isEnabled ? 'Conectado' : 'Desconectado')}
                    </Badge>
                  </div>
                  {plugin.description && (
                    <p className="text-xs text-zinc-400 mb-4 line-clamp-2 min-h-[32px]">{plugin.description}</p>
                  )}
                  <div className="flex items-center justify-between">
                    <div className="flex gap-2">
                      <Badge variant="default">{plugin.toolCount} tools</Badge>
                      <Badge variant="default" className="uppercase">{plugin.transport}</Badge>                    </div>
                    <div className="flex gap-1">
                      <button
                        onClick={() => setViewing(plugin)}
                        className="p-1.5 rounded-lg text-zinc-500 hover:bg-zinc-800 hover:text-zinc-300 transition-colors"
                        title="Ver detalhes"
                      >
                        <Eye className="w-4 h-4" />
                      </button>
                      <button
                        onClick={() => handleEdit(plugin)}
                        className="p-1.5 rounded-lg text-zinc-500 hover:bg-zinc-800 hover:text-zinc-300 transition-colors"
                        title="Editar"
                      >
                        <Edit2 className="w-4 h-4" />
                      </button>
                      <button
                        onClick={() => setDeleteTarget(plugin.id)}
                        className="p-1.5 rounded-lg text-zinc-500 hover:bg-zinc-800 hover:text-red-400 transition-colors"
                        title="Remover"
                      >
                        <Trash2 className="w-4 h-4" />
                      </button>
                    </div>
                  </div>
                </div>
              ))}
              {filteredPlugins.length === 0 && (
                <div className="col-span-full text-center py-12 text-zinc-500 text-sm">
                  Nenhum plugin encontrado.
                </div>
              )}
            </div>
          ) : (
            <div className="bg-zinc-900 border border-zinc-800 rounded-xl overflow-hidden shadow-lg">
              <div className="divide-y divide-zinc-800/50">
                {filteredTools.map(tool => (
                  <div key={`${tool.pluginId}-${tool.name}`} className="px-5 py-4 flex items-start justify-between hover:bg-zinc-800/30 transition-colors">
                    <div className="space-y-1 pr-4">
                      <div className="flex items-center gap-2">
                        <p className="text-sm font-medium text-zinc-100">{tool.name}</p>
                        <span className="text-[10px] text-zinc-600 font-mono">from {tool.pluginName}</span>
                      </div>
                      <p className="text-xs text-zinc-400 leading-relaxed">{tool.description}</p>
                    </div>
                    <Badge variant="teal" className="flex-none">{tool.pluginName}</Badge>
                  </div>
                ))}
                {filteredTools.length === 0 && (
                  <div className="text-center py-20 text-zinc-500 text-sm">
                    Nenhuma tool encontrada.
                  </div>
                )}
              </div>
            </div>
          )}
        </div>
      </div>

      {loadOpen && (
        <PluginLoadModal
          onLoad={async (req) => {
            try {
              await loadPlugin(req)
              addToast('Plugin carregado', 'success')
              setLoadOpen(false)
            } catch (err) {
              let msg = 'Erro ao carregar plugin'
              if (err instanceof Error) {
                try {
                  const parsed = JSON.parse(err.message)
                  if (parsed.error) msg = parsed.error
                  else if (parsed.errors) msg = `Erro de validação: ${Object.values(parsed.errors).flat().join(', ')}`
                  else if (parsed.title) msg = parsed.title
                } catch {
                  msg = err.message
                }
              }
              addToast(msg, 'error')
            }
          }}
          onClose={() => setLoadOpen(false)}
        />
      )}

      {editing && (
        <PluginLoadModal
          initialValues={editing.config}
          onLoad={async (req) => {
            try {
              await updatePlugin(editing.id, req)
              addToast('Plugin atualizado', 'success')
              setEditing(null)
            } catch (err) {
              addToast(err instanceof Error ? err.message : 'Erro ao atualizar plugin', 'error')
            }
          }}
          onClose={() => setEditing(null)}
        />
      )}

      {viewing && (
        <PluginDetailModal plugin={viewing} onClose={() => setViewing(null)} />
      )}

      <ConfirmModal
        open={!!deleteTarget}
        title="Remover Plugin"
        message="Tem certeza que deseja remover este plugin? Esta ação não pode ser desfeita e removerá o plugin permanentemente do banco de dados."
        variant="danger"
        confirmLabel="Remover Permanentemente"
        onConfirm={handleDelete}
        onCancel={() => setDeleteTarget(null)}
      />
    </div>
  )
}
