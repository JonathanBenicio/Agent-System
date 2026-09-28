import { useState, useCallback, useRef } from 'react'
import { Upload, X, AlertTriangle } from 'lucide-react'
import { cn } from '@/lib/utils'
import { useUploadOnnxModel } from '@/hooks/useOnnxModels'

interface Props {
  onClose: () => void
  onSuccess: () => void
}

export function OnnxModelUploadModal({ onClose, onSuccess }: Props) {
  const uploadMutation = useUploadOnnxModel()
  const fileInputRef = useRef<HTMLInputElement>(null)
  const dataFileInputRef = useRef<HTMLInputElement>(null)

  const [file, setFile] = useState<File | null>(null)
  const [dataFile, setDataFile] = useState<File | null>(null)
  const [dragOver, setDragOver] = useState(false)
  const [dataDragOver, setDataDragOver] = useState(false)
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
  const [error, setError] = useState<string | null>(null)

  const totalSize = (file?.size ?? 0) + (dataFile?.size ?? 0)
  const isLarge = totalSize > 50 * 1024 * 1024

  const handleDrop = useCallback((e: React.DragEvent) => {
    e.preventDefault()
    setDragOver(false)
    const dropped = e.dataTransfer.files[0]
    if (dropped?.name.endsWith('.onnx')) {
      setFile(dropped)
      if (!name) setName(dropped.name.replace('.onnx', ''))
    }
  }, [name])

  const handleDataDrop = useCallback((e: React.DragEvent) => {
    e.preventDefault()
    setDataDragOver(false)
    const dropped = e.dataTransfer.files[0]
    if (dropped) {
      setDataFile(dropped)
    }
  }, [])

  const handleFileSelect = (e: React.ChangeEvent<HTMLInputElement>) => {
    const selected = e.target.files?.[0]
    if (selected) {
      setFile(selected)
      if (!name) setName(selected.name.replace('.onnx', ''))
    }
  }

  const handleSubmit = async () => {
    if (!file || !name || !inputNodeName || !outputNodeName) {
      setError('Preencha todos os campos obrigatórios.')
      return
    }

    const formData = new FormData()
    formData.append('file', file)
    formData.append('name', name)
    formData.append('description', description)
    formData.append('inputNodeName', inputNodeName)
    formData.append('outputNodeName', outputNodeName)
    formData.append('inputWidth', inputWidth.toString())
    formData.append('inputHeight', inputHeight.toString())
    formData.append('channels', channels.toString())
    formData.append('scaleFactor', scaleFactor.toString())
    formData.append('meanRed', meanR.toString())
    formData.append('meanGreen', meanG.toString())
    formData.append('meanBlue', meanB.toString())
    formData.append('outputFormat', outputFormat)
    if (dataFile) {
      formData.append('dataFile', dataFile)
    }

    try {
      setError(null)
      await uploadMutation.mutateAsync(formData)
      onSuccess()
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Falha ao enviar modelo.')
    }
  }

  const formatSize = (bytes: number) => {
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-sm">
      <div className="relative w-full max-w-2xl max-h-[90vh] overflow-y-auto bg-zinc-900 border border-zinc-800 rounded-2xl shadow-2xl">
        {/* Header */}
        <div className="sticky top-0 z-10 flex items-center justify-between px-6 py-4 bg-zinc-900/95 backdrop-blur border-b border-zinc-800 rounded-t-2xl">
          <h2 className="text-lg font-semibold text-zinc-100">Upload Modelo ONNX</h2>
          <button onClick={onClose} className="p-1.5 rounded-lg text-zinc-500 hover:text-zinc-300 hover:bg-zinc-800 transition-colors">
            <X className="w-5 h-5" />
          </button>
        </div>

        <div className="flex flex-col gap-5 p-6">
          {/* Drop zone */}
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
            <Upload className={cn('w-8 h-8', file ? 'text-emerald-400' : 'text-zinc-500')} />
            {file ? (
              <div className="text-center">
                <p className="text-sm font-medium text-zinc-200">{file.name}</p>
                <p className="text-xs text-zinc-500 mt-0.5">{formatSize(file.size)}</p>
              </div>
            ) : (
              <div className="text-center">
                <p className="text-sm text-zinc-400">Arraste o arquivo <code className="text-cyan-400">.onnx</code> aqui</p>
                <p className="text-xs text-zinc-600 mt-1">ou clique para selecionar</p>
              </div>
            )}
            <input
              ref={fileInputRef}
              type="file"
              accept=".onnx"
              onChange={handleFileSelect}
              className="hidden"
            />
          </div>

          {/* Companion weight file (.data) drop zone (only visible if .onnx file is selected) */}
          {file && (
            <div className="flex flex-col gap-2">
              <label className="block text-xs font-medium text-zinc-400">
                Arquivo de Pesos Companion (Opcional - ex: .data, .bin)
              </label>
              <div
                className={cn(
                  'flex items-center justify-between p-4 rounded-xl border border-dashed transition-all cursor-pointer bg-zinc-800/20',
                  dataDragOver
                    ? 'border-cyan-400 bg-cyan-500/5'
                    : dataFile
                      ? 'border-emerald-500/40 bg-emerald-500/5'
                      : 'border-zinc-700 hover:border-zinc-600'
                )}
                onDragOver={(e) => { e.preventDefault(); setDataDragOver(true) }}
                onDragLeave={() => setDataDragOver(false)}
                onDrop={handleDataDrop}
                onClick={() => dataFileInputRef.current?.click()}
              >
                <div className="flex items-center gap-3">
                  <Upload className={cn('w-5 h-5', dataFile ? 'text-emerald-400' : 'text-zinc-500')} />
                  {dataFile ? (
                    <div>
                      <p className="text-sm font-medium text-zinc-200">{dataFile.name}</p>
                      <p className="text-xs text-zinc-500">{formatSize(dataFile.size)}</p>
                    </div>
                  ) : (
                    <div className="text-left">
                      <p className="text-sm text-zinc-400">Arraste o arquivo de pesos aqui ou clique para selecionar</p>
                      <p className="text-xs text-zinc-600">Aceita arquivos .data, .bin, etc.</p>
                    </div>
                  )}
                </div>
                {dataFile && (
                  <button
                    type="button"
                    onClick={(e) => {
                      e.stopPropagation()
                      setDataFile(null)
                    }}
                    className="p-1.5 rounded-lg hover:bg-zinc-850 text-zinc-500 hover:text-zinc-300 transition-colors"
                  >
                    <X className="w-4 h-4" />
                  </button>
                )}
                <input
                  ref={dataFileInputRef}
                  type="file"
                  onChange={(e) => {
                    const selected = e.target.files?.[0]
                    if (selected) setDataFile(selected)
                  }}
                  className="hidden"
                />
              </div>
            </div>
          )}

          {/* Large file warning */}
          {isLarge && (
            <div className="flex items-start gap-3 p-3 rounded-xl bg-amber-500/10 border border-amber-500/20">
              <AlertTriangle className="w-5 h-5 text-amber-400 shrink-0 mt-0.5" />
              <div>
                <p className="text-sm font-medium text-amber-300">Modelo grande ({formatSize(totalSize)})</p>
                <p className="text-xs text-amber-400/70 mt-0.5">Será salvo em disco para melhor performance.</p>
              </div>
            </div>
          )}

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
              disabled={uploadMutation.isPending || !file || !name}
              className={cn(
                'flex items-center gap-2 px-5 py-2 rounded-xl text-sm font-medium transition-all',
                uploadMutation.isPending || !file || !name
                  ? 'bg-zinc-700 text-zinc-500 cursor-not-allowed'
                  : 'bg-gradient-to-r from-cyan-600 to-teal-600 text-white hover:from-cyan-500 hover:to-teal-500 shadow-lg shadow-cyan-500/20'
              )}
            >
              {uploadMutation.isPending ? (
                <>
                  <div className="w-4 h-4 border-2 border-white/30 border-t-white rounded-full animate-spin" />
                  Enviando...
                </>
              ) : (
                <>
                  <Upload className="w-4 h-4" />
                  Upload
                </>
              )}
            </button>
          </div>
        </div>
      </div>
    </div>
  )
}
