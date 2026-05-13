import { useState, useEffect } from 'react'
import { X, Plug } from 'lucide-react'
import { Badge } from '@/components/shared/Badge'
import { pluginApi } from '@/lib/api'
import type { PluginSummary } from '@/types/api'

interface Props {
  plugin: PluginSummary
  onClose: () => void
}

export function PluginDetailModal({ plugin, onClose }: Props) {
  const [resources, setResources] = useState<string[]>([])

  useEffect(() => {
    pluginApi.resources(plugin.id).then(setResources).catch(() => {})
  }, [plugin.id])

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
          <div className="flex gap-2">
            <Badge variant={plugin.isConnected ? 'success' : 'danger'}>
              {plugin.isConnected ? 'Conectado' : 'Desconectado'}
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
                    <p className="text-sm text-zinc-200">{t.name}</p>
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
        </div>
      </div>
    </div>
  )
}
