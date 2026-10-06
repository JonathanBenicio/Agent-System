import { useState, useEffect } from 'react'
import { X, Save, Settings } from 'lucide-react'
import { cn } from '@/lib/utils'
import { useUpdateOnnxModel } from '@/hooks/useOnnxModels'
import { onnxModelApi } from '@/lib/api'

interface Props {
  modelId: string
  onClose: () => void
  onSuccess: () => void
}

export function OnnxModelEditModal({ modelId, onClose, onSuccess }: Props) {
  const updateMutation = useUpdateOnnxModel()

  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  const [name, setName] = useState('')
  const [description, setDescription] = useState('')
  const [inputNodeName, setInputNodeName] = useState('input')
  const [outputNodeName, setOutputNodeName] = useState('output')
  const [inputWidth, setInputWidth] = useState(512)
  const [inputHeight, setInputHeight] = useState(512)
  const [channels, setChannels] = useState(3)
  const [scaleFactor, setScaleFactor] = useState(0.0039216)
  const [meanR, setMeanR] = useState(0)
  const [meanG, setMeanG] = useState(0)
  const [meanB, setMeanB] = useState(0)
  const [outputFormat, setOutputFormat] = useState('image')
  const [isActive, setIsActive] = useState(true)

  useEffect(() => {
    async function loadModel() {
      try {
        setLoading(true)
        setError(null)
        const detail = await onnxModelApi.get(modelId)
        setName(detail.name)
        setDescription(detail.description ?? '')
        setInputNodeName(detail.inputNodeName)
        setOutputNodeName(detail.outputNodeName)
        setInputWidth(detail.inputWidth)
        setInputHeight(detail.inputHeight)
        setChannels(detail.channels)
        setScaleFactor(detail.scaleFactor)
        setMeanR(detail.meanRed)
        setMeanG(detail.meanGreen)
        setMeanB(detail.meanBlue)
        setOutputFormat(detail.outputFormat)
        setIsActive(detail.isActive)
      } catch (err: unknown) {
        setError(err instanceof Error ? err.message : 'Falha ao carregar detalhes do modelo.')
      } finally {
        setLoading(false)
      }
    }
    loadModel()
  }, [modelId])

  const handleSubmit = async () => {
    if (!name || !inputNodeName || !outputNodeName) {
      setError('Preencha todos os campos obrigatórios.')
      return
    }

    try {
      setError(null)
      await updateMutation.mutateAsync({
        id: modelId,
        data: {
          name,
          description: description || null,
          inputNodeName,
          outputNodeName,
          inputWidth,
          inputHeight,
          channels,
          scaleFactor,
          meanRed: meanR,
          meanGreen: meanG,
          meanBlue: meanB,
          outputFormat,
          isActive,
        },
      })
      onSuccess()
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Falha ao atualizar modelo.')
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-sm">
      <div className="relative w-full max-w-2xl max-h-[90vh] overflow-y-auto bg-zinc-900 border border-zinc-800 rounded-2xl shadow-2xl">
        {/* Header */}
        <div className="sticky top-0 z-10 flex items-center justify-between px-6 py-4 bg-zinc-900/95 backdrop-blur border-b border-zinc-800 rounded-t-2xl">
          <div className="flex items-center gap-2">
            <Settings className="w-5 h-5 text-cyan-400 animate-pulse" />
            <h2 className="text-lg font-semibold text-zinc-100">Editar Parâmetros do Modelo</h2>
          </div>
          <button onClick={onClose} className="p-1.5 rounded-lg text-zinc-500 hover:text-zinc-300 hover:bg-zinc-800 transition-colors">
            <X className="w-5 h-5" />
          </button>
        </div>

        <div className="flex flex-col gap-5 p-6">
          {loading ? (
            <div className="flex items-center justify-center py-20">
              <div className="w-8 h-8 border-2 border-cyan-500/30 border-t-cyan-500 rounded-full animate-spin" />
            </div>
          ) : (
            <>
              {/* Form fields */}
              <div className="grid grid-cols-2 gap-4">
                <div className="col-span-2">
                  <label className="block text-xs font-medium text-zinc-400 mb-1.5">Nome *</label>
                  <input
                    value={name}
                    onChange={(e) => setName(e.target.value)}
                    className="w-full px-3 py-2 rounded-xl bg-zinc-800 border border-zinc-700 text-zinc-200 text-sm focus:outline-none focus:border-cyan-500/50 transition-colors"
                    placeholder="Real-ESRGAN x4"
                  />
                </div>
                <div className="col-span-2">
                  <label className="block text-xs font-medium text-zinc-400 mb-1.5">Descrição</label>
                  <input
                    value={description}
                    onChange={(e) => setDescription(e.target.value)}
                    className="w-full px-3 py-2 rounded-xl bg-zinc-800 border border-zinc-700 text-zinc-200 text-sm focus:outline-none focus:border-cyan-500/50 transition-colors"
                    placeholder="Upscaling 4x para imagens"
                  />
                </div>
                <div>
                  <label className="block text-xs font-medium text-zinc-400 mb-1.5">Input Node Name *</label>
                  <input
                    value={inputNodeName}
                    onChange={(e) => setInputNodeName(e.target.value)}
                    className="w-full px-3 py-2 rounded-xl bg-zinc-800 border border-zinc-700 text-zinc-200 text-sm focus:outline-none focus:border-cyan-500/50 transition-colors"
                  />
                </div>
                <div>
                  <label className="block text-xs font-medium text-zinc-400 mb-1.5">Output Node Name *</label>
                  <input
                    value={outputNodeName}
                    onChange={(e) => setOutputNodeName(e.target.value)}
                    className="w-full px-3 py-2 rounded-xl bg-zinc-800 border border-zinc-700 text-zinc-200 text-sm focus:outline-none focus:border-cyan-500/50 transition-colors"
                  />
                </div>
                <div>
                  <label className="block text-xs font-medium text-zinc-400 mb-1.5">Input Width</label>
                  <input
                    type="number"
                    value={inputWidth}
                    onChange={(e) => setInputWidth(Number(e.target.value))}
                    className="w-full px-3 py-2 rounded-xl bg-zinc-800 border border-zinc-700 text-zinc-200 text-sm focus:outline-none focus:border-cyan-500/50 transition-colors"
                  />
                </div>
                <div>
                  <label className="block text-xs font-medium text-zinc-400 mb-1.5">Input Height</label>
                  <input
                    type="number"
                    value={inputHeight}
                    onChange={(e) => setInputHeight(Number(e.target.value))}
                    className="w-full px-3 py-2 rounded-xl bg-zinc-800 border border-zinc-700 text-zinc-200 text-sm focus:outline-none focus:border-cyan-500/50 transition-colors"
                  />
                </div>
                <div>
                  <label className="block text-xs font-medium text-zinc-400 mb-1.5">Channels</label>
                  <select
                    value={channels}
                    onChange={(e) => setChannels(Number(e.target.value))}
                    className="w-full px-3 py-2 rounded-xl bg-zinc-800 border border-zinc-700 text-zinc-200 text-sm focus:outline-none focus:border-cyan-500/50 transition-colors"
                  >
                    <option value={1}>1 (Grayscale)</option>
                    <option value={3}>3 (RGB)</option>
                    <option value={4}>4 (RGBA)</option>
                  </select>
                </div>
                <div>
                  <label className="block text-xs font-medium text-zinc-400 mb-1.5">Output Format</label>
                  <select
                    value={outputFormat}
                    onChange={(e) => setOutputFormat(e.target.value)}
                    className="w-full px-3 py-2 rounded-xl bg-zinc-800 border border-zinc-700 text-zinc-200 text-sm focus:outline-none focus:border-cyan-500/50 transition-colors"
                  >
                    <option value="image">Image</option>
                    <option value="tensor">Tensor</option>
                    <option value="text">Text</option>
                  </select>
                </div>
                <div>
                  <label className="block text-xs font-medium text-zinc-400 mb-1.5">Scale Factor</label>
                  <input
                    type="number"
                    step="0.0001"
                    value={scaleFactor}
                    onChange={(e) => setScaleFactor(Number(e.target.value))}
                    className="w-full px-3 py-2 rounded-xl bg-zinc-800 border border-zinc-700 text-zinc-200 text-sm focus:outline-none focus:border-cyan-500/50 transition-colors"
                  />
                </div>
                <div className="flex gap-2">
                  <div className="flex-1">
                    <label className="block text-xs font-medium text-zinc-400 mb-1.5">Mean R</label>
                    <input
                      type="number" step="0.001" value={meanR}
                      onChange={(e) => setMeanR(Number(e.target.value))}
                      className="w-full px-3 py-2 rounded-xl bg-zinc-800 border border-zinc-700 text-zinc-200 text-sm focus:outline-none focus:border-cyan-500/50 transition-colors"
                    />
                  </div>
                  <div className="flex-1">
                    <label className="block text-xs font-medium text-zinc-400 mb-1.5">G</label>
                    <input
                      type="number" step="0.001" value={meanG}
                      onChange={(e) => setMeanG(Number(e.target.value))}
                      className="w-full px-3 py-2 rounded-xl bg-zinc-800 border border-zinc-700 text-zinc-200 text-sm focus:outline-none focus:border-cyan-500/50 transition-colors"
                    />
                  </div>
                  <div className="flex-1">
                    <label className="block text-xs font-medium text-zinc-400 mb-1.5">B</label>
                    <input
                      type="number" step="0.001" value={meanB}
                      onChange={(e) => setMeanB(Number(e.target.value))}
                      className="w-full px-3 py-2 rounded-xl bg-zinc-800 border border-zinc-700 text-zinc-200 text-sm focus:outline-none focus:border-cyan-500/50 transition-colors"
                    />
                  </div>
                </div>
                <div className="col-span-2 flex items-center gap-3 py-2">
                  <input
                    type="checkbox"
                    id="isActive"
                    checked={isActive}
                    onChange={(e) => setIsActive(e.target.checked)}
                    className="w-4 h-4 rounded border-zinc-700 bg-zinc-800 text-cyan-600 focus:ring-cyan-500/20 focus:ring-offset-zinc-900"
                  />
                  <label htmlFor="isActive" className="text-sm font-medium text-zinc-300 select-none cursor-pointer">
                    Modelo Ativo para Execução e Workflows
                  </label>
                </div>
              </div>

              {/* Error */}
              {error && (
                <div className="p-3 rounded-xl bg-red-500/10 border border-red-500/20 text-red-400 text-sm">
                  {error}
                </div>
              )}

              {/* Actions */}
              <div className="flex justify-end gap-3 pt-2">
                <button
                  onClick={onClose}
                  className="px-4 py-2 rounded-xl text-sm text-zinc-400 hover:text-zinc-200 border border-zinc-700 hover:border-zinc-600 transition-colors"
                >
                  Cancelar
                </button>
                <button
                  onClick={handleSubmit}
                  disabled={updateMutation.isPending || !name}
                  className={cn(
                    'flex items-center gap-2 px-5 py-2 rounded-xl text-sm font-medium transition-all',
                    updateMutation.isPending || !name
                      ? 'bg-zinc-700 text-zinc-500 cursor-not-allowed'
                      : 'bg-gradient-to-r from-cyan-600 to-teal-600 text-white hover:from-cyan-500 hover:to-teal-500 shadow-lg shadow-cyan-500/20'
                  )}
                >
                  {updateMutation.isPending ? (
                    <>
                      <div className="w-4 h-4 border-2 border-white/30 border-t-white rounded-full animate-spin" />
                      Salvando...
                    </>
                  ) : (
                    <>
                      <Save className="w-4 h-4" />
                      Salvar Alterações
                    </>
                  )}
                </button>
              </div>
            </>
          )}
        </div>
      </div>
    </div>
  )
}
