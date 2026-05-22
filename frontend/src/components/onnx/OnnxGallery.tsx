import { useState } from 'react'
import {
  Clock,
  Trash2,
  Download,
  AlertTriangle,
  ChevronLeft,
  ChevronRight,
  Sparkles,
  RefreshCw,
  Sliders,
  X,
  FileImage
} from 'lucide-react'
import { cn } from '@/lib/utils'
import { useOnnxJobsList, useDeleteOnnxJob } from '@/hooks/useOnnxModels'
import type { OnnxInferenceJob } from '@/types/api'

interface BeforeAfterModalProps {
  job: OnnxInferenceJob
  onClose: () => void
}

function BeforeAfterSliderModal({ job, onClose }: BeforeAfterModalProps) {
  const [sliderPosition, setSliderPosition] = useState(50)
  const [isDragging, setIsDragging] = useState(false)

  const inputUrl = job.inputImagePath ? job.inputImagePath : ''
  const outputUrl = job.outputImagePath ? job.outputImagePath : ''

  const handleMove = (clientX: number, rect: DOMRect) => {
    const x = clientX - rect.left
    const percentage = Math.max(0, Math.min(100, (x / rect.width) * 100))
    setSliderPosition(percentage)
  }

  const handleTouchMove = (e: React.TouchEvent) => {
    const container = e.currentTarget
    const rect = container.getBoundingClientRect()
    if (e.touches[0]) {
      handleMove(e.touches[0].clientX, rect)
    }
  }

  const handleMouseMove = (e: React.MouseEvent) => {
    if (!isDragging && e.buttons !== 1) return
    const container = e.currentTarget
    const rect = container.getBoundingClientRect()
    handleMove(e.clientX, rect)
  }

  const handleDownload = async () => {
    if (!outputUrl) return
    try {
      const response = await fetch(outputUrl)
      const blob = await response.blob()
      const url = window.URL.createObjectURL(blob)
      const a = document.createElement('a')
      a.href = url
      a.download = `onnx-result-${job.id.substring(0, 8)}.png`
      document.body.appendChild(a)
      a.click()
      document.body.removeChild(a)
      window.URL.revokeObjectURL(url)
    } catch (err) {
      console.error('Failed to download image:', err)
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/85 backdrop-blur-md p-4 animate-in fade-in duration-200">
      <div className="relative w-full max-w-5xl bg-zinc-900 border border-zinc-800 rounded-3xl overflow-hidden shadow-2xl flex flex-col max-h-[92vh]">
        {/* Header */}
        <div className="flex items-center justify-between px-6 py-4 border-b border-zinc-800/80 bg-zinc-900/95 sticky top-0 z-10">
          <div className="flex items-center gap-2.5">
            <Sliders className="w-5 h-5 text-cyan-400" />
            <div>
              <h2 className="text-base font-bold text-zinc-100">Comparação Antes / Depois</h2>
              <p className="text-xs text-zinc-500 font-mono mt-0.5">Job: {job.id}</p>
            </div>
          </div>
          <div className="flex items-center gap-3">
            <button
              onClick={handleDownload}
              className="flex items-center gap-1.5 px-4 py-2 rounded-xl bg-zinc-800 hover:bg-zinc-750 border border-zinc-700 text-zinc-200 text-xs font-semibold transition-all active:scale-95"
            >
              <Download className="w-3.5 h-3.5" />
              Download Resultado
            </button>
            <button
              onClick={onClose}
              aria-label="Fechar comparação"
              className="p-2 rounded-xl text-zinc-400 hover:text-zinc-200 hover:bg-zinc-800 transition-colors"
            >
              <X className="w-5 h-5" />
            </button>
          </div>
        </div>

        {/* Modal Content */}
        <div className="flex-1 overflow-y-auto p-6 flex flex-col lg:flex-row gap-6 items-center justify-center">
          {/* Main Interactive Slider */}
          <div className="flex-1 flex flex-col items-center justify-center w-full">
            <div
              className="relative w-full aspect-[4/3] max-w-2xl bg-zinc-950 rounded-2xl overflow-hidden border border-zinc-800 select-none cursor-ew-resize shadow-2xl"
              onMouseDown={() => setIsDragging(true)}
              onMouseUp={() => setIsDragging(false)}
              onMouseLeave={() => setIsDragging(false)}
              onMouseMove={handleMouseMove}
              onTouchMove={handleTouchMove}
            >
              {/* Bottom Image (After/Output) */}
              <img
                src={outputUrl}
                alt="Depois (Output)"
                className="absolute inset-0 w-full h-full object-contain pointer-events-none"
              />

              {/* Top Image Container (Before/Input) */}
              <div
                className="absolute inset-0 h-full overflow-hidden border-r-2 border-cyan-400"
                style={{ width: `${sliderPosition}%` }}
              >
                <img
                  src={inputUrl}
                  alt="Antes (Input)"
                  className="absolute inset-0 w-full h-full object-contain max-w-none pointer-events-none"
                  style={{ width: '100%', height: '100%' }}
                />
                {/* Badge Left */}
                <div className="absolute top-4 left-4 px-2.5 py-1 rounded-lg bg-black/70 border border-white/10 text-white text-[10px] font-semibold uppercase tracking-wider">
                  Original (Antes)
                </div>
              </div>

              {/* Badge Right */}
              <div className="absolute top-4 right-4 px-2.5 py-1 rounded-lg bg-cyan-950/80 border border-cyan-500/20 text-cyan-400 text-[10px] font-semibold uppercase tracking-wider">
                Processado (Depois)
              </div>

              {/* Drag Handle Bar */}
              <div
                className="absolute top-0 bottom-0 w-0.5 bg-cyan-400 pointer-events-none"
                style={{ left: `${sliderPosition}%` }}
              >
                <div className="absolute top-1/2 -translate-y-1/2 -translate-x-1/2 w-8 h-8 rounded-full bg-zinc-900 border-2 border-cyan-400 shadow-xl flex items-center justify-center text-cyan-400">
                  <Sliders className="w-3.5 h-3.5 rotate-90" />
                </div>
              </div>
            </div>
            <p className="text-xs text-zinc-500 mt-3 text-center">
              Arraste o mouse ou passe o dedo sobre a imagem para comparar o antes e depois.
            </p>
          </div>

          {/* Metadata Card Side Panel */}
          <div className="w-full lg:w-80 shrink-0 flex flex-col gap-4">
            <div className="bg-zinc-950/60 border border-zinc-800/80 rounded-2xl p-5 flex flex-col gap-4">
              <h3 className="text-sm font-semibold text-zinc-300 border-b border-zinc-800 pb-2">Detalhes da Inferência</h3>
              
              <div className="flex flex-col gap-1">
                <span className="text-[10px] text-zinc-500 font-semibold uppercase tracking-wider">Modelo Utilizado:</span>
                <span className="text-sm text-zinc-200 font-medium">{job.modelName}</span>
              </div>

              <div className="flex flex-col gap-1">
                <span className="text-[10px] text-zinc-500 font-semibold uppercase tracking-wider">Latência do Servidor:</span>
                <span className="text-sm text-cyan-400 font-mono font-medium">{job.latencyMs ? `${job.latencyMs} ms` : 'N/A'}</span>
              </div>

              <div className="flex flex-col gap-1">
                <span className="text-[10px] text-zinc-500 font-semibold uppercase tracking-wider">Data de Criação:</span>
                <span className="text-xs text-zinc-400">{new Date(job.createdAt).toLocaleString()}</span>
              </div>

              {job.completedAt && (
                <div className="flex flex-col gap-1">
                  <span className="text-[10px] text-zinc-500 font-semibold uppercase tracking-wider">Concluído em:</span>
                  <span className="text-xs text-zinc-400">{new Date(job.completedAt).toLocaleString()}</span>
                </div>
              )}
            </div>
          </div>
        </div>
      </div>
    </div>
  )
}

export function OnnxGallery() {
  const [page, setPage] = useState(1)
  const selectedModelId = undefined
  const [activeJobForSlider, setActiveJobForSlider] = useState<OnnxInferenceJob | null>(null)

  const { data, isLoading, isError, refetch } = useOnnxJobsList(selectedModelId, page, 8)
  const deleteMutation = useDeleteOnnxJob()

  const handleDownloadDirect = async (job: OnnxInferenceJob, e: React.MouseEvent) => {
    e.stopPropagation()
    if (!job.outputImagePath) return
    try {
      const response = await fetch(job.outputImagePath)
      const blob = await response.blob()
      const url = window.URL.createObjectURL(blob)
      const a = document.createElement('a')
      a.href = url
      a.download = `onnx-${job.id.substring(0, 8)}.png`
      document.body.appendChild(a)
      a.click()
      document.body.removeChild(a)
      window.URL.revokeObjectURL(url)
    } catch (err) {
      console.error('Failed to download image:', err)
    }
  }

  const handleDelete = async (jobId: string, e: React.MouseEvent) => {
    e.stopPropagation()
    if (!confirm('Tem certeza que deseja remover este resultado permanentemente?')) return
    try {
      await deleteMutation.mutateAsync(jobId)
    } catch (err) {
      console.error('Failed to delete job:', err)
    }
  }

  return (
    <div className="flex flex-col gap-5">
      {/* Filters & Controls */}
      <div className="flex flex-col sm:flex-row items-stretch sm:items-center justify-between gap-3 bg-zinc-900/40 p-4 border border-zinc-800 rounded-2xl">
        <div className="flex items-center gap-2">
          <Sparkles className="w-4 h-4 text-cyan-400" />
          <h2 className="text-sm font-semibold text-zinc-200">Resultados Recentes</h2>
        </div>
        <div className="flex items-center gap-2">
          <button
            onClick={() => { refetch().catch(console.error) }}
            className="p-2 rounded-xl bg-zinc-800 hover:bg-zinc-750 text-zinc-400 hover:text-zinc-200 border border-zinc-700 transition-colors"
            title="Atualizar lista"
          >
            <RefreshCw className="w-4 h-4" />
          </button>
        </div>
      </div>

      {/* Loading & Error */}
      {isLoading && (
        <div className="flex items-center justify-center py-24">
          <div className="flex flex-col items-center gap-3">
            <div className="w-9 h-9 border-2 border-cyan-500/20 border-t-cyan-400 rounded-full animate-spin" />
            <p className="text-xs text-zinc-500 font-medium">Carregando histórico de jobs...</p>
          </div>
        </div>
      )}

      {isError && (
        <div className="p-4 rounded-2xl bg-red-500/10 border border-red-500/20 text-red-400 text-sm font-medium flex items-center gap-2.5">
          <AlertTriangle className="w-5 h-5 shrink-0 text-red-400" />
          Erro ao carregar a galeria de inferências.
        </div>
      )}

      {/* Empty State */}
      {!isLoading && !isError && (!data?.jobs || data.jobs.length === 0) && (
        <div className="flex flex-col items-center justify-center py-20 gap-4 bg-zinc-900/10 border border-dashed border-zinc-800/80 rounded-2xl">
          <div className="w-14 h-14 rounded-2xl bg-zinc-900 border border-zinc-800 flex items-center justify-center text-zinc-650">
            <FileImage className="w-6 h-6 text-zinc-600" />
          </div>
          <div className="text-center">
            <p className="text-sm font-semibold text-zinc-400">Nenhum teste de processamento encontrado</p>
            <p className="text-xs text-zinc-650 mt-1 max-w-sm">
              Use a opção "Testar" em um de seus modelos ONNX acima para enviar imagens para a fila.
            </p>
          </div>
        </div>
      )}

      {/* Jobs Grid */}
      {!isLoading && !isError && data?.jobs && data.jobs.length > 0 && (
        <>
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
            {data.jobs.map((job) => {
              const isCompleted = job.status === 'Completed'
              const isFailed = job.status === 'Failed'
              const isProcessing = job.status === 'Processing'
              const isPending = job.status === 'Pending'

              return (
                <div
                  key={job.id}
                  onClick={() => isCompleted && setActiveJobForSlider(job)}
                  className={cn(
                    'group relative flex flex-col rounded-2xl bg-zinc-900/60 border border-zinc-800 overflow-hidden transition-all duration-200 shadow-sm hover:shadow-lg',
                    isCompleted ? 'cursor-pointer hover:border-zinc-700' : 'cursor-default'
                  )}
                >
                  {/* Status Overlay or Preview */}
                  <div className="relative aspect-[4/3] w-full bg-zinc-950 flex items-center justify-center overflow-hidden border-b border-zinc-800/50">
                    {isCompleted && job.outputImagePath ? (
                      <div className="relative w-full h-full">
                        <img
                          src={job.outputImagePath}
                          alt="Output Preview"
                          className="w-full h-full object-cover transition-transform duration-350 group-hover:scale-105"
                        />
                        {/* Hover Overlay info */}
                        <div className="absolute inset-0 bg-black/40 opacity-0 group-hover:opacity-100 flex items-center justify-center transition-opacity duration-200">
                          <span className="flex items-center gap-1.5 px-3 py-1.5 rounded-xl bg-zinc-900/90 border border-zinc-800 text-xs font-semibold text-zinc-200">
                            <Sliders className="w-3.5 h-3.5 text-cyan-400" />
                            Comparar
                          </span>
                        </div>
                      </div>
                    ) : isFailed ? (
                      <div className="flex flex-col items-center gap-2 text-center p-4">
                        <AlertTriangle className="w-8 h-8 text-red-500/80 animate-pulse" />
                        <span className="text-xs font-semibold text-red-400">Falha na Inferência</span>
                        <p className="text-[10px] text-zinc-500 line-clamp-2 max-w-[180px]">{job.errorMessage || 'Erro de CPU/Modelo'}</p>
                      </div>
                    ) : isProcessing ? (
                      <div className="flex flex-col items-center gap-2">
                        <div className="w-7 h-7 border-2 border-cyan-500/10 border-t-cyan-400 rounded-full animate-spin" />
                        <span className="text-xs font-semibold text-cyan-400 animate-pulse">Processando...</span>
                      </div>
                    ) : (
                      <div className="flex flex-col items-center gap-2">
                        <div className="w-6 h-6 rounded-full border border-dashed border-amber-500/40 flex items-center justify-center text-amber-500">
                          <span className="w-2 h-2 rounded-full bg-amber-400 animate-ping" />
                        </div>
                        <span className="text-xs font-medium text-amber-400">Na Fila (Pendente)</span>
                      </div>
                    )}

                    {/* Status Badge */}
                    <div className="absolute top-2 left-2">
                      <span
                        className={cn(
                          'inline-flex items-center gap-1.5 px-2 py-0.5 rounded-md text-[10px] font-semibold uppercase tracking-wider border backdrop-blur-md',
                          isCompleted && 'bg-emerald-500/10 border-emerald-500/20 text-emerald-400',
                          isFailed && 'bg-red-500/10 border-red-500/20 text-red-400',
                          isProcessing && 'bg-cyan-500/10 border-cyan-500/20 text-cyan-400',
                          isPending && 'bg-amber-500/10 border-amber-500/20 text-amber-400'
                        )}
                      >
                        {isCompleted && 'Concluído'}
                        {isFailed && 'Falha'}
                        {isProcessing && 'Processando'}
                        {isPending && 'Pendente'}
                      </span>
                    </div>
                  </div>

                  {/* Body details */}
                  <div className="flex flex-col flex-1 p-3.5 gap-2">
                    <div className="flex flex-col">
                      <span className="text-[10px] text-zinc-500 font-semibold uppercase tracking-wider">Modelo:</span>
                      <span className="text-xs font-bold text-zinc-300 truncate" title={job.modelName}>{job.modelName}</span>
                    </div>

                    <div className="flex items-center justify-between text-[11px] text-zinc-500 font-mono border-t border-zinc-800/40 pt-2 mt-auto">
                      <span className="flex items-center gap-1" title="Data de Envio">
                        <Clock className="w-3 h-3 text-zinc-650" />
                        {new Date(job.createdAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}
                      </span>
                      <span>
                        {job.latencyMs ? `${job.latencyMs}ms` : ''}
                      </span>
                    </div>
                  </div>

                  {/* Absolute Top-Right Card Menu */}
                  <div className="absolute top-2 right-2 flex gap-1">
                    {isCompleted && (
                      <button
                        onClick={(e) => handleDownloadDirect(job, e)}
                        className="p-1.5 rounded-lg bg-zinc-900/80 border border-zinc-800/80 text-zinc-400 hover:text-zinc-200 transition-all hover:bg-zinc-850"
                        title="Download de imagem"
                      >
                        <Download className="w-3.5 h-3.5" />
                      </button>
                    )}
                    <button
                      onClick={(e) => handleDelete(job.id, e)}
                      disabled={deleteMutation.isPending}
                      className="p-1.5 rounded-lg bg-zinc-900/80 border border-zinc-800/80 text-zinc-450 hover:text-red-400 hover:bg-red-500/10 hover:border-red-500/20 transition-all"
                      title="Deletar resultado"
                    >
                      <Trash2 className="w-3.5 h-3.5" />
                    </button>
                  </div>
                </div>
              )
            })}
          </div>

          {/* Pagination Controls */}
          {data.totalPages > 1 && (
            <div className="flex items-center justify-between border-t border-zinc-800/50 pt-5 mt-2">
              <span className="text-xs text-zinc-500 font-medium">
                Página <strong className="text-zinc-300">{data.page}</strong> de <strong className="text-zinc-300">{data.totalPages}</strong> ({data.totalItems} resultados)
              </span>
              <div className="flex items-center gap-2">
                <button
                  onClick={() => setPage((p) => Math.max(1, p - 1))}
                  disabled={page === 1}
                  className={cn(
                    'p-2 rounded-xl border text-zinc-400 transition-all',
                    page === 1
                      ? 'bg-zinc-900/20 border-zinc-850 text-zinc-650 cursor-not-allowed'
                      : 'bg-zinc-800 border-zinc-700 hover:bg-zinc-750 hover:text-zinc-200 active:scale-95'
                  )}
                >
                  <ChevronLeft className="w-4 h-4" />
                </button>
                <button
                  onClick={() => setPage((p) => Math.min(data.totalPages, p + 1))}
                  disabled={page === data.totalPages}
                  className={cn(
                    'p-2 rounded-xl border text-zinc-400 transition-all',
                    page === data.totalPages
                      ? 'bg-zinc-900/20 border-zinc-850 text-zinc-650 cursor-not-allowed'
                      : 'bg-zinc-800 border-zinc-700 hover:bg-zinc-750 hover:text-zinc-200 active:scale-95'
                  )}
                >
                  <ChevronRight className="w-4 h-4" />
                </button>
              </div>
            </div>
          )}
        </>
      )}

      {/* Comparer Slider Modal */}
      {activeJobForSlider && (
        <BeforeAfterSliderModal
          job={activeJobForSlider}
          onClose={() => setActiveJobForSlider(null)}
        />
      )}
    </div>
  )
}
