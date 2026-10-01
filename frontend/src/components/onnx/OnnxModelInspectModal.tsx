import { useEffect } from 'react'
import { X, Copy, Cpu } from 'lucide-react'
import { useInspectOnnxModel } from '@/hooks/useOnnxModels'

interface Props {
  modelId: string
  onClose: () => void
}

export function OnnxModelInspectModal({ modelId, onClose }: Props) {
  const inspectMutation = useInspectOnnxModel()
  const inspectModel = inspectMutation.mutate

  useEffect(() => {
    inspectModel(modelId)
  }, [modelId, inspectModel])

  const copyToClipboard = () => {
    if (inspectMutation.data) {
      navigator.clipboard.writeText(JSON.stringify(inspectMutation.data, null, 2))
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-sm">
      <div className="relative w-full max-w-lg max-h-[80vh] overflow-y-auto bg-zinc-900 border border-zinc-800 rounded-2xl shadow-2xl">
        {/* Header */}
        <div className="sticky top-0 z-10 flex items-center justify-between px-6 py-4 bg-zinc-900/95 backdrop-blur border-b border-zinc-800 rounded-t-2xl">
          <div className="flex items-center gap-2">
            <Cpu className="w-5 h-5 text-cyan-400" />
            <h2 className="text-lg font-semibold text-zinc-100">Inspeção do Modelo</h2>
          </div>
          <div className="flex items-center gap-1">
            <button
              onClick={copyToClipboard}
              disabled={!inspectMutation.data}
              className="p-1.5 rounded-lg text-zinc-500 hover:text-zinc-300 hover:bg-zinc-800 transition-colors"
              title="Copiar informações"
            >
              <Copy className="w-4 h-4" />
            </button>
            <button onClick={onClose} className="p-1.5 rounded-lg text-zinc-500 hover:text-zinc-300 hover:bg-zinc-800 transition-colors">
              <X className="w-5 h-5" />
            </button>
          </div>
        </div>

        <div className="p-6 flex flex-col gap-5">
          {/* Loading */}
          {inspectMutation.isPending && (
            <div className="flex items-center justify-center py-12">
              <div className="w-8 h-8 border-2 border-cyan-500/30 border-t-cyan-500 rounded-full animate-spin" />
            </div>
          )}

          {/* Error */}
          {inspectMutation.isError && (
            <div className="p-4 rounded-xl bg-red-500/10 border border-red-500/20 text-red-400 text-sm">
              {inspectMutation.error?.message || 'Falha ao inspecionar modelo.'}
            </div>
          )}

          {/* Results */}
          {inspectMutation.data && (
            <>
              {/* Input Nodes */}
              <div>
                <h3 className="text-sm font-semibold text-zinc-300 mb-2">Input Nodes</h3>
                <div className="rounded-xl overflow-hidden border border-zinc-800">
                  <table className="w-full text-sm">
                    <thead>
                      <tr className="bg-zinc-800/50">
                        <th className="text-left px-4 py-2 text-xs font-medium text-zinc-500">Nome</th>
                        <th className="text-left px-4 py-2 text-xs font-medium text-zinc-500">Shape</th>
                        <th className="text-left px-4 py-2 text-xs font-medium text-zinc-500">Tipo</th>
                      </tr>
                    </thead>
                    <tbody>
                      {inspectMutation.data.inputNodes.map((node, i) => (
                        <tr key={i} className="border-t border-zinc-800/50">
                          <td className="px-4 py-2.5 text-cyan-400 font-mono text-xs">{node.name}</td>
                          <td className="px-4 py-2.5 text-zinc-300 font-mono text-xs">
                            [{node.shape.join(', ')}]
                          </td>
                          <td className="px-4 py-2.5 text-zinc-500 text-xs">{node.type}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </div>

              {/* Output Nodes */}
              <div>
                <h3 className="text-sm font-semibold text-zinc-300 mb-2">Output Nodes</h3>
                <div className="rounded-xl overflow-hidden border border-zinc-800">
                  <table className="w-full text-sm">
                    <thead>
                      <tr className="bg-zinc-800/50">
                        <th className="text-left px-4 py-2 text-xs font-medium text-zinc-500">Nome</th>
                        <th className="text-left px-4 py-2 text-xs font-medium text-zinc-500">Shape</th>
                        <th className="text-left px-4 py-2 text-xs font-medium text-zinc-500">Tipo</th>
                      </tr>
                    </thead>
                    <tbody>
                      {inspectMutation.data.outputNodes.map((node, i) => (
                        <tr key={i} className="border-t border-zinc-800/50">
                          <td className="px-4 py-2.5 text-emerald-400 font-mono text-xs">{node.name}</td>
                          <td className="px-4 py-2.5 text-zinc-300 font-mono text-xs">
                            [{node.shape.join(', ')}]
                          </td>
                          <td className="px-4 py-2.5 text-zinc-500 text-xs">{node.type}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </div>
            </>
          )}
        </div>
      </div>
    </div>
  )
}
