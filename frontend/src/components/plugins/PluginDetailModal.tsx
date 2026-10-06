import { useState, useEffect } from 'react'
import { X, Plug, Loader2 } from 'lucide-react'
import { Badge } from '@/components/shared/Badge'
import { pluginApi } from '@/lib/api'
import type { PluginSummary } from '@/types/api'

interface Props {
  plugin: PluginSummary
  onClose: () => void
}

export function PluginDetailModal({ plugin: initialPlugin, onClose }: Props) {
  const [plugin, setPlugin] = useState<PluginSummary>(initialPlugin)
  const [loadedPluginId, setLoadedPluginId] = useState<string | null>(null)
  const loading = loadedPluginId !== initialPlugin.id
  const [resources, setResources] = useState<string[]>([])

  useEffect(() => {
    let active = true
    Promise.all([
      pluginApi.get(initialPlugin.id),
      pluginApi.resources(initialPlugin.id).catch(() => [] as string[])
    ]).then(([details, res]) => {
      if (!active) return
      setPlugin(details)
      setResources(res)
    }).catch(error => {
      console.error('Erro ao carregar detalhes do plugin:', error)
    }).finally(() => {
      if (active) setLoadedPluginId(initialPlugin.id)
    })
    return () => { active = false }
  }, [initialPlugin.id])

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center">
      <div className="absolute inset-0 bg-black/60" onClick={onClose} />
      <div className="relative bg-zinc-900 border border-zinc-700 rounded-xl w-full max-w-lg max-h-[90vh] overflow-y-auto shadow-2xl">
        <div className="sticky top-0 bg-zinc-900 px-6 py-4 border-b border-zinc-800 flex items-center justify-between">
          <div className="flex items-center gap-3">
            <Plug className="w-5 h-5 text-teal-400" />
            <h2 className="text-lg font-semibold text-zinc-100">{plugin.name}</h2>
          </div>
          <button onClick={onClose} className="text-zinc-500 hover:text-zinc-300">
            <X className="w-5 h-5" />
          </button>
        </div>
        <div className="p-6 space-y-5">
          {loading ? (
            <div className="flex flex-col items-center justify-center py-12 gap-3">
              <Loader2 className="w-6 h-6 text-teal-500 animate-spin" />
              <p className="text-sm text-zinc-500">Carregando detalhes...</p>
            </div>
          ) : (
            <>
              <div className="flex gap-2">
                <Badge variant={plugin.isEnabled ? 'success' : 'danger'}>
                  {plugin.status === 'Running' ? 'Conectado' : plugin.status || (plugin.isEnabled ? 'Conectado' : 'Desconectado')}
                </Badge>
                <Badge>{plugin.transport}</Badge>
                <Badge>{plugin.toolCount} tools</Badge>
              </div>

              {plugin.description && (
                <div>
                  <h3 className="text-xs font-semibold text-zinc-500 uppercase mb-1">Descrição</h3>
                  <p className="text-sm text-zinc-300">{plugin.description}</p>
                </div>
              )}

              <div>
                <h3 className="text-xs font-semibold text-zinc-500 uppercase mb-1">ID</h3>
                <p className="text-sm text-zinc-400 font-mono">{plugin.id}</p>
              </div>

              {plugin.tools && plugin.tools.length > 0 && (
                <div>
                  <h3 className="text-xs font-semibold text-zinc-500 uppercase mb-2">Tools</h3>
                  <div className="space-y-2">
                    {plugin.tools.map(t => (
                      <div key={t.name} className="bg-zinc-950 border border-zinc-800 rounded-lg px-3 py-2">
                        <p className="text-sm text-zinc-200 font-medium">{t.name}</p>
                        {t.description && <p className="text-xs text-zinc-500 mt-0.5">{t.description}</p>}
                      </div>
                    ))}
                  </div>
                </div>
              )}

              {resources.length > 0 && (
                <div>
                  <h3 className="text-xs font-semibold text-zinc-500 uppercase mb-2">Resources</h3>
                  <div className="flex flex-wrap gap-1">
                    {resources.map(r => <Badge key={r}>{r}</Badge>)}
                  </div>
                </div>
              )}
            </>
          )}
        </div>
      </div>
    </div>
  )
}
