import { useState, useCallback, useRef } from 'react'
import { X, Play, Upload, ImageIcon, Clock, Maximize2 } from 'lucide-react'
import { cn } from '@/lib/utils'
import { useTestOnnxModel } from '@/hooks/useOnnxModels'

interface Props {
  modelId: string
  onClose: () => void
}

export function OnnxModelTestModal({ modelId, onClose }: Props) {
  const testMutation = useTestOnnxModel()
  const fileInputRef = useRef<HTMLInputElement>(null)

  const [file, setFile] = useState<File | null>(null)
  const [preview, setPreview] = useState<string | null>(null)
  const [dragOver, setDragOver] = useState(false)

  const handleFile = useCallback((f: File) => {
    setFile(f)
    const reader = new FileReader()
    reader.onload = (e) => setPreview(e.target?.result as string)
    reader.readAsDataURL(f)
  }, [])

  const handleDrop = useCallback((e: React.DragEvent) => {
    e.preventDefault()
    setDragOver(false)
    const dropped = e.dataTransfer.files[0]
    if (dropped?.type.startsWith('image/')) handleFile(dropped)
  }, [handleFile])

  const handleFileSelect = (e: React.ChangeEvent<HTMLInputElement>) => {
    const selected = e.target.files?.[0]
    if (selected) handleFile(selected)
  }

  const handleTest = async () => {
    if (!file) return
    const formData = new FormData()
    formData.append('image', file)
    testMutation.mutate({ id: modelId, formData })
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-sm">
      <div className="relative w-full max-w-3xl max-h-[90vh] overflow-y-auto bg-zinc-900 border border-zinc-800 rounded-2xl shadow-2xl">
        {/* Header */}
        <div className="sticky top-0 z-10 flex items-center justify-between px-6 py-4 bg-zinc-900/95 backdrop-blur border-b border-zinc-800 rounded-t-2xl">
          <div className="flex items-center gap-2">
            <Play className="w-5 h-5 text-cyan-400" />
            <h2 className="text-lg font-semibold text-zinc-100">Teste de Inferência</h2>
          </div>
          <button onClick={onClose} className="p-1.5 rounded-lg text-zinc-500 hover:text-zinc-300 hover:bg-zinc-800 transition-colors">
            <X className="w-5 h-5" />
          </button>
        </div>

        <div className="p-6 flex flex-col gap-5">
          {/* Upload test image */}
          <div
            className={cn(
              'flex flex-col items-center justify-center gap-3 p-6 rounded-xl border-2 border-dashed transition-all cursor-pointer',
              dragOver
                ? 'border-cyan-400 bg-cyan-500/5'
                : file
                  ? 'border-emerald-500/40 bg-emerald-500/5'
                  : 'border-zinc-700 bg-zinc-800/30 hover:border-zinc-600'
            )}
            onDragOver={(e) => { e.preventDefault(); setDragOver(true) }}
            onDragLeave={() => setDragOver(false)}
            onDrop={handleDrop}
            onClick={() => fileInputRef.current?.click()}
          >
            <Upload className={cn('w-6 h-6', file ? 'text-emerald-400' : 'text-zinc-500')} />
            <p className="text-sm text-zinc-400">
              {file ? file.name : 'Arraste uma imagem de teste ou clique para selecionar'}
            </p>
            <input
              ref={fileInputRef}
              type="file"
              accept="image/*"
              onChange={handleFileSelect}
              className="hidden"
              aria-label="Selecionar imagem de teste"
            />
          </div>

          {/* Side-by-side preview */}
          {(preview || testMutation.data?.outputImage) && (
            <div className="grid grid-cols-2 gap-4">
              {/* Input */}
              <div className="flex flex-col gap-2">
                <div className="flex items-center gap-1.5 text-xs font-medium text-zinc-500">
                  <ImageIcon className="w-3.5 h-3.5" /> Input
                </div>
                {preview && (
                  <div className="rounded-xl overflow-hidden border border-zinc-800 bg-zinc-950/50">
                    <img src={preview} alt="Input" className="w-full h-auto object-contain max-h-64" />
                  </div>
                )}
              </div>

              {/* Output */}
              <div className="flex flex-col gap-2">
                <div className="flex items-center gap-1.5 text-xs font-medium text-zinc-500">
                  <Maximize2 className="w-3.5 h-3.5" /> Output
                </div>
                {testMutation.data?.outputImage ? (
                  <div className="rounded-xl overflow-hidden border border-zinc-800 bg-zinc-950/50">
                    <img
                      src={`data:image/png;base64,${testMutation.data.outputImage}`}
                      alt="Output"
                      className="w-full h-auto object-contain max-h-64"
                    />
                  </div>
                ) : (
                  <div className="flex items-center justify-center h-32 rounded-xl border border-zinc-800 bg-zinc-950/30">
                    <p className="text-xs text-zinc-600">Aguardando resultado...</p>
                  </div>
                )}
              </div>
            </div>
          )}

          {/* Metrics */}
          {testMutation.data && (
            <div className="flex flex-wrap gap-3">
              <div className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-zinc-800/60 text-xs text-zinc-400">
                <Clock className="w-3.5 h-3.5 text-cyan-400" />
                {testMutation.data.latencyMs}ms
              </div>
              <div className="px-3 py-1.5 rounded-lg bg-zinc-800/60 text-xs text-zinc-400">
                Input: [{testMutation.data.inputShape.join(', ')}]
              </div>
              <div className="px-3 py-1.5 rounded-lg bg-zinc-800/60 text-xs text-zinc-400">
                Output: [{testMutation.data.outputShape.join(', ')}]
              </div>
            </div>
          )}

          {/* Error */}
          {testMutation.isError && (
            <div className="p-3 rounded-xl bg-red-500/10 border border-red-500/20 text-red-400 text-sm">
              {testMutation.error?.message || 'Falha ao executar teste.'}
            </div>
          )}

          {/* Run button */}
          <div className="flex justify-end">
            <button
              onClick={handleTest}
              disabled={!file || testMutation.isPending}
              className={cn(
                'flex items-center gap-2 px-5 py-2.5 rounded-xl text-sm font-medium transition-all',
                !file || testMutation.isPending
                  ? 'bg-zinc-700 text-zinc-500 cursor-not-allowed'
                  : 'bg-gradient-to-r from-cyan-600 to-teal-600 text-white hover:from-cyan-500 hover:to-teal-500 shadow-lg shadow-cyan-500/20'
              )}
            >
              {testMutation.isPending ? (
                <>
                  <div className="w-4 h-4 border-2 border-white/30 border-t-white rounded-full animate-spin" />
                  Processando...
                </>
              ) : (
                <>
                  <Play className="w-4 h-4" />
                  Executar Teste
                </>
              )}
            </button>
          </div>
        </div>
      </div>
    </div>
  )
}
