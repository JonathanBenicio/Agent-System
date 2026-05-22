import { useState, useCallback, useRef } from 'react'
import { X, Play, Upload, ImageIcon, CheckCircle } from 'lucide-react'
import { cn } from '@/lib/utils'
import { useTestOnnxModel } from '@/hooks/useOnnxModels'

interface Props {
  modelId: string
  onClose: () => void
  onViewGallery?: () => void
}

export function OnnxModelTestModal({ modelId, onClose, onViewGallery }: Props) {
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

  if (testMutation.data) {
    const jobId = testMutation.data.jobId
    return (
      <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/70 backdrop-blur-md animate-in fade-in duration-200">
        <div className="relative w-full max-w-md bg-zinc-900 border border-zinc-800 rounded-2xl shadow-2xl p-6 flex flex-col items-center text-center gap-5">
          <div className="flex items-center justify-center w-14 h-14 rounded-full bg-emerald-500/10 border border-emerald-500/30 text-emerald-400">
            <CheckCircle className="w-8 h-8 animate-bounce" />
          </div>
          <div>
            <h2 className="text-xl font-bold text-zinc-100">Tarefa Enfileirada!</h2>
            <p className="text-sm text-zinc-400 mt-2 leading-relaxed">
              O processamento foi iniciado em segundo plano no servidor para evitar timeouts de gateway.
            </p>
          </div>
          <div className="w-full bg-zinc-950/60 border border-zinc-800/80 rounded-xl p-4 text-left">
            <div className="text-xs text-zinc-500 font-semibold tracking-wider uppercase">ID da Tarefa:</div>
            <div className="text-sm font-mono text-cyan-400 truncate mt-1 select-all" title={jobId}>{jobId}</div>
            
            <div className="text-xs text-zinc-500 font-semibold tracking-wider uppercase mt-3.5">Status:</div>
            <div className="inline-flex items-center gap-2 mt-1 px-2.5 py-1 rounded-lg bg-amber-500/10 border border-amber-500/25 text-amber-400 text-xs font-medium">
              <span className="w-1.5 h-1.5 rounded-full bg-amber-400 animate-ping" />
              Na Fila / Pendente
            </div>
          </div>
          <p className="text-xs text-zinc-500 leading-relaxed">
            Você pode fechar esta tela e continuar navegando pelo sistema. Um pop-up global avisará assim que a imagem for gerada.
          </p>
          <div className="flex w-full gap-3 mt-2">
            <button
              onClick={() => {
                onClose()
                if (onViewGallery) onViewGallery()
              }}
              className="flex-1 px-4 py-2.5 rounded-xl bg-gradient-to-r from-cyan-600 to-teal-600 hover:from-cyan-500 hover:to-teal-500 text-white text-sm font-medium transition-all shadow-lg shadow-cyan-500/20 active:scale-95"
            >
              Ver na Galeria
            </button>
            <button
              onClick={onClose}
              className="px-4 py-2.5 rounded-xl bg-zinc-800 border border-zinc-700 hover:bg-zinc-750 text-zinc-300 text-sm font-medium transition-all active:scale-95"
            >
              Fechar
            </button>
          </div>
        </div>
      </div>
    )
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-sm animate-in fade-in duration-200">
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
              'flex flex-col items-center justify-center gap-3 p-8 rounded-xl border-2 border-dashed transition-all cursor-pointer',
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
            <Upload className={cn('w-8 h-8 transition-transform group-hover:-translate-y-0.5', file ? 'text-emerald-400' : 'text-zinc-500')} />
            <p className="text-sm font-medium text-zinc-300">
              {file ? file.name : 'Arraste uma imagem de teste ou clique para selecionar'}
            </p>
            <p className="text-xs text-zinc-500">
              PNG, JPG, JPEG de qualquer tamanho. O processamento ocorrerá sequencialmente.
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
          {preview && (
            <div className="flex flex-col gap-2">
              <div className="flex items-center gap-1.5 text-xs font-semibold text-zinc-500 uppercase tracking-wider">
                <ImageIcon className="w-3.5 h-3.5" /> Preview da Imagem de Entrada
              </div>
              <div className="rounded-xl overflow-hidden border border-zinc-800 bg-zinc-950/50 p-2 flex justify-center max-h-80">
                <img src={preview} alt="Input Preview" className="w-full h-auto object-contain max-h-72 rounded-lg" />
              </div>
            </div>
          )}

          {/* Error */}
          {testMutation.isError && (
            <div className="p-3.5 rounded-xl bg-red-500/10 border border-red-500/20 text-red-400 text-sm font-medium">
              {testMutation.error?.message || 'Falha ao executar teste.'}
            </div>
          )}

          {/* Run button */}
          <div className="flex justify-end gap-3 pt-2">
            <button
              onClick={onClose}
              className="px-4 py-2 rounded-xl bg-zinc-800 border border-zinc-700 hover:bg-zinc-750 text-zinc-400 hover:text-zinc-200 text-sm font-medium transition-all"
            >
              Cancelar
            </button>
            <button
              onClick={handleTest}
              disabled={!file || testMutation.isPending}
              className={cn(
                'flex items-center gap-2 px-5 py-2.5 rounded-xl text-sm font-medium transition-all active:scale-95',
                !file || testMutation.isPending
                  ? 'bg-zinc-700 text-zinc-500 cursor-not-allowed'
                  : 'bg-gradient-to-r from-cyan-600 to-teal-600 text-white hover:from-cyan-500 hover:to-teal-500 shadow-lg shadow-cyan-500/20'
              )}
            >
              {testMutation.isPending ? (
                <>
                  <div className="w-4 h-4 border-2 border-white/30 border-t-white rounded-full animate-spin" />
                  Enfileirando...
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

