import { useState } from 'react'
import { Brain, Upload, Search, TestTube, Trash2, HardDrive, Database, Power, PowerOff, Edit } from 'lucide-react'
import { cn } from '@/lib/utils'
import { useOnnxModelsList, useDeleteOnnxModel, useUpdateOnnxModel } from '@/hooks/useOnnxModels'
import type { OnnxModelSummary } from '@/types/api'
import { OnnxModelUploadModal } from './OnnxModelUploadModal'
import { OnnxModelInspectModal } from './OnnxModelInspectModal'
import { OnnxModelTestModal } from './OnnxModelTestModal'
import { OnnxModelEditModal } from './OnnxModelEditModal'

function formatFileSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  if (bytes < 1024 * 1024 * 1024) return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
  return `${(bytes / (1024 * 1024 * 1024)).toFixed(2)} GB`
}

export function OnnxModelsPage() {
  const { data: models = [], isLoading, error } = useOnnxModelsList()
  const deleteMutation = useDeleteOnnxModel()
  const updateMutation = useUpdateOnnxModel()

  const [showUpload, setShowUpload] = useState(false)
  const [inspectModelId, setInspectModelId] = useState<string | null>(null)
  const [testModelId, setTestModelId] = useState<string | null>(null)
  const [editModelId, setEditModelId] = useState<string | null>(null)
  const [toast, setToast] = useState<{ type: 'success' | 'error'; message: string } | null>(null)

  const showToast = (type: 'success' | 'error', message: string) => {
    setToast({ type, message })
    setTimeout(() => setToast(null), 4000)
  }

  const handleDelete = async (model: OnnxModelSummary) => {
    if (!confirm(`Deletar modelo "${model.name}"? Esta ação não pode ser desfeita.`)) return
    try {
      await deleteMutation.mutateAsync(model.id)
      showToast('success', `Modelo "${model.name}" deletado.`)
    } catch {
      showToast('error', 'Falha ao deletar modelo.')
    }
  }

  const handleToggleActive = async (model: OnnxModelSummary) => {
    try {
      await updateMutation.mutateAsync({ id: model.id, data: { isActive: !model.isActive } })
      showToast('success', `Modelo ${model.isActive ? 'desativado' : 'ativado'}.`)
    } catch {
      showToast('error', 'Falha ao atualizar status.')
    }
  }

  return (
    <div className="flex flex-col gap-6 p-6 max-w-7xl mx-auto">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-3">
          <div className="flex items-center justify-center w-10 h-10 rounded-xl bg-gradient-to-br from-cyan-500/20 to-teal-500/20 border border-cyan-500/30">
            <Brain className="w-5 h-5 text-cyan-400" />
          </div>
          <div>
            <h1 className="text-xl font-bold text-zinc-100">Modelos IA</h1>
            <p className="text-sm text-zinc-500">Upload e gerenciamento de modelos ONNX para inferência local</p>
          </div>
        </div>
        <button
          onClick={() => setShowUpload(true)}
          className="flex items-center gap-2 px-4 py-2.5 rounded-xl bg-gradient-to-r from-cyan-600 to-teal-600 text-white text-sm font-medium hover:from-cyan-500 hover:to-teal-500 transition-all shadow-lg shadow-cyan-500/20"
        >
          <Upload className="w-4 h-4" />
          Upload Modelo
        </button>
      </div>

      {/* Toast */}
      {toast && (
        <div
          className={cn(
            'fixed top-4 right-4 z-50 px-4 py-3 rounded-xl text-sm font-medium shadow-xl border animate-in slide-in-from-top-2',
            toast.type === 'success'
              ? 'bg-emerald-500/10 border-emerald-500/30 text-emerald-400'
              : 'bg-red-500/10 border-red-500/30 text-red-400'
          )}
        >
          {toast.message}
        </div>
      )}

      {/* Loading */}
      {isLoading && (
        <div className="flex items-center justify-center py-20">
          <div className="w-8 h-8 border-2 border-cyan-500/30 border-t-cyan-500 rounded-full animate-spin" />
        </div>
      )}

      {/* Error */}
      {error && (
        <div className="p-4 rounded-xl bg-red-500/10 border border-red-500/20 text-red-400 text-sm">
          Erro ao carregar modelos: {error.message}
        </div>
      )}

      {/* Empty State */}
      {!isLoading && !error && models.length === 0 && (
        <div className="flex flex-col items-center justify-center py-20 gap-4">
          <div className="w-16 h-16 rounded-2xl bg-zinc-800/50 border border-zinc-700/50 flex items-center justify-center">
            <Brain className="w-8 h-8 text-zinc-600" />
          </div>
          <div className="text-center">
            <p className="text-zinc-400 font-medium">Nenhum modelo ONNX cadastrado</p>
            <p className="text-zinc-600 text-sm mt-1">
              Faça upload de um modelo pré-treinado para habilitar inferência local.
            </p>
          </div>
          <button
            onClick={() => setShowUpload(true)}
            className="mt-2 px-4 py-2 rounded-xl bg-zinc-800 border border-zinc-700 text-zinc-300 text-sm hover:bg-zinc-750 hover:border-zinc-600 transition-colors"
          >
            <Upload className="w-4 h-4 inline mr-2" />
            Upload Modelo
          </button>
        </div>
      )}

      {/* Model Grid */}
      {!isLoading && models.length > 0 && (
        <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
          {models.map((model) => (
            <div
              key={model.id}
              className={cn(
                'group relative p-5 rounded-2xl border transition-all duration-200',
                model.isActive
                  ? 'bg-zinc-900/60 border-zinc-800 hover:border-zinc-700'
                  : 'bg-zinc-900/30 border-zinc-800/50 opacity-60 hover:opacity-80'
              )}
            >
              {/* Status dot */}
              <div className="absolute top-4 right-4 flex items-center gap-2">
                <span
                  className={cn(
                    'w-2 h-2 rounded-full',
                    model.isActive ? 'bg-emerald-400 shadow-lg shadow-emerald-400/50' : 'bg-zinc-600'
                  )}
                />
                <span className={cn('text-xs', model.isActive ? 'text-emerald-400' : 'text-zinc-600')}>
                  {model.isActive ? 'Ativo' : 'Inativo'}
                </span>
              </div>

              {/* Model info */}
              <div className="flex flex-col gap-3">
                <div>
                  <h3 className="text-base font-semibold text-zinc-200 pr-20">{model.name}</h3>
                  {model.description && (
                    <p className="text-sm text-zinc-500 mt-0.5 line-clamp-2">{model.description}</p>
                  )}
                </div>

                {/* Metadata chips */}
                <div className="flex flex-wrap gap-2">
                  <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-lg bg-zinc-800/80 text-zinc-400 text-xs">
                    📐 {model.inputWidth}×{model.inputHeight}
                  </span>
                  <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-lg bg-zinc-800/80 text-zinc-400 text-xs">
                    🎨 {model.channels}ch
                  </span>
                  <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-lg bg-zinc-800/80 text-zinc-400 text-xs">
                    📤 {model.outputFormat}
                  </span>
                  <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-lg bg-zinc-800/80 text-zinc-400 text-xs">
                    {model.storedOnDisk ? (
                      <><HardDrive className="w-3 h-3" /> {formatFileSize(model.fileSizeBytes)} (disco)</>
                    ) : (
                      <><Database className="w-3 h-3" /> {formatFileSize(model.fileSizeBytes)}</>
                    )}
                  </span>
                </div>

                {/* Actions */}
                <div className="flex items-center gap-1.5 pt-1">
                  <button
                    onClick={() => setInspectModelId(model.id)}
                    className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs text-zinc-400 bg-zinc-800/60 hover:bg-zinc-800 hover:text-zinc-200 transition-colors"
                    title="Inspecionar"
                  >
                    <Search className="w-3.5 h-3.5" />
                    Inspecionar
                  </button>
                  <button
                    onClick={() => setEditModelId(model.id)}
                    className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs text-zinc-400 bg-zinc-800/60 hover:bg-zinc-800 hover:text-zinc-200 transition-colors"
                    title="Editar"
                  >
                    <Edit className="w-3.5 h-3.5" />
                    Editar
                  </button>
                  <button
                    onClick={() => setTestModelId(model.id)}
                    className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs text-zinc-400 bg-zinc-800/60 hover:bg-zinc-800 hover:text-zinc-200 transition-colors"
                    title="Testar"
                  >
                    <TestTube className="w-3.5 h-3.5" />
                    Testar
                  </button>
                  <button
                    onClick={() => handleToggleActive(model)}
                    className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs text-zinc-400 bg-zinc-800/60 hover:bg-zinc-800 hover:text-zinc-200 transition-colors"
                    title={model.isActive ? 'Desativar' : 'Ativar'}
                  >
                    {model.isActive ? <PowerOff className="w-3.5 h-3.5" /> : <Power className="w-3.5 h-3.5" />}
                    {model.isActive ? 'Desativar' : 'Ativar'}
                  </button>
                  <button
                    onClick={() => handleDelete(model)}
                    className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs text-zinc-400 bg-zinc-800/60 hover:bg-red-500/10 hover:text-red-400 transition-colors ml-auto"
                    title="Deletar"
                  >
                    <Trash2 className="w-3.5 h-3.5" />
                  </button>
                </div>
              </div>
            </div>
          ))}
        </div>
      )}

      {/* Modals */}
      {showUpload && (
        <OnnxModelUploadModal
          onClose={() => setShowUpload(false)}
          onSuccess={() => {
            setShowUpload(false)
            showToast('success', 'Modelo enviado com sucesso!')
          }}
        />
      )}
      {inspectModelId && (
        <OnnxModelInspectModal
          modelId={inspectModelId}
          onClose={() => setInspectModelId(null)}
        />
      )}
      {editModelId && (
        <OnnxModelEditModal
          modelId={editModelId}
          onClose={() => setEditModelId(null)}
          onSuccess={() => {
            setEditModelId(null)
            showToast('success', 'Parâmetros do modelo atualizados com sucesso!')
          }}
        />
      )}
      {testModelId && (
        <OnnxModelTestModal
          modelId={testModelId}
          onClose={() => setTestModelId(null)}
        />
      )}
    </div>
  )
}

export default OnnxModelsPage
