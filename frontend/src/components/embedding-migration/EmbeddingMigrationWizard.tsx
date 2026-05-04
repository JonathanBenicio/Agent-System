import { useState, useEffect } from 'react'
import { ArrowRight, ArrowLeftRight, Play, Pause, RotateCcw, CheckCircle2, XCircle, Loader2, Cpu, Plus, Trash2 } from 'lucide-react'

interface EmbeddingModelConfig {
  id: string
  provider: string
  modelName: string
  dimensions: number
  baseUrl: string | null
  apiKey: string
  isActive: boolean
}

interface MigrationJob {
  id: string
  sourceCollection: string
  targetCollection: string
  sourceModel: EmbeddingModelConfig
  targetModel: EmbeddingModelConfig
  status: string
  totalDocuments: number
  processedDocuments: number
  failedDocuments: number
  progressPercentage: number
  startedAt: string | null
  completedAt: string | null
  errorMessage: string | null
}

interface MigrationStatusSummary {
  jobId: string
  status: string
  progressPercentage: number
  totalDocuments: number
  processedDocuments: number
  failedDocuments: number
  sourceModel: string
  targetModel: string
  elapsedTime: string | null
  estimatedTimeRemaining: string | null
}

const API_BASE = '/api/admin/embedding-migration'

const statusColors: Record<string, string> = {
  Pending: 'bg-yellow-900/30 text-yellow-400',
  InProgress: 'bg-blue-900/30 text-blue-400',
  Completed: 'bg-green-900/30 text-green-400',
  Failed: 'bg-red-900/30 text-red-400',
  Cancelled: 'bg-zinc-800 text-zinc-400',
  RollingBack: 'bg-orange-900/30 text-orange-400',
}

const providerOptions = ['OpenAI', 'Google', 'Ollama', 'Cohere', 'Custom']

export function EmbeddingMigrationWizard() {
  const [step, setStep] = useState(0) // 0=models, 1=start-migration, 2=jobs
  const [models, setModels] = useState<EmbeddingModelConfig[]>([])
  const [jobs, setJobs] = useState<MigrationJob[]>([])
  const [jobStatus, setJobStatus] = useState<MigrationStatusSummary | null>(null)
  const [selectedJobId, setSelectedJobId] = useState<string | null>(null)
  const [showModelForm, setShowModelForm] = useState(false)
  const [modelForm, setModelForm] = useState({ provider: 'OpenAI', modelName: '', dimensions: 1536, baseUrl: '', apiKey: '' })
  const [migrationForm, setMigrationForm] = useState({ sourceModelId: '', targetModelId: '', sourceCollection: '' })

  useEffect(() => { fetchModels(); fetchJobs() }, [])
  useEffect(() => {
    if (!selectedJobId) return
    const interval = setInterval(() => fetchJobStatus(selectedJobId), 3000)
    return () => clearInterval(interval)
  }, [selectedJobId])

  async function fetchModels() {
    try { const res = await fetch(`${API_BASE}/models`); setModels(await res.json()) } catch {}
  }
  async function fetchJobs() {
    try { const res = await fetch(`${API_BASE}/jobs`); setJobs(await res.json()) } catch {}
  }
  async function fetchJobStatus(jobId: string) {
    try { const res = await fetch(`${API_BASE}/jobs/${jobId}/status`); setJobStatus(await res.json()) } catch {}
  }

  async function saveModel(e: React.FormEvent) {
    e.preventDefault()
    await fetch(`${API_BASE}/models`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ ...modelForm, isActive: models.length === 0 }) })
    setShowModelForm(false)
    setModelForm({ provider: 'OpenAI', modelName: '', dimensions: 1536, baseUrl: '', apiKey: '' })
    fetchModels()
  }

  async function deleteModel(id: string) {
    if (!confirm('Remover modelo?')) return
    await fetch(`${API_BASE}/models/${id}`, { method: 'DELETE' })
    fetchModels()
  }

  async function activateModel(id: string) {
    await fetch(`${API_BASE}/models/${id}/activate`, { method: 'POST' })
    fetchModels()
  }

  async function startMigration(e: React.FormEvent) {
    e.preventDefault()
    const res = await fetch(`${API_BASE}/jobs`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(migrationForm) })
    const job = await res.json()
    setSelectedJobId(job.id)
    setStep(2)
    fetchJobs()
  }

  async function cancelJob(id: string) { await fetch(`${API_BASE}/jobs/${id}/cancel`, { method: 'POST' }); fetchJobs() }
  async function retryJob(id: string) { await fetch(`${API_BASE}/jobs/${id}/retry`, { method: 'POST' }); fetchJobs() }
  async function switchCollection(id: string) {
    if (!confirm('Ativar nova coleção? Isso altera o modelo ativo.')) return
    await fetch(`${API_BASE}/jobs/${id}/switch`, { method: 'POST' }); fetchJobs(); fetchModels()
  }

  return (
    <div className="p-6 space-y-6">
      <div>
        <h1 className="text-2xl font-bold text-zinc-100">Migração de Embeddings</h1>
        <p className="text-sm text-zinc-400 mt-1">Wizard para re-indexação com rotação de coleção sem downtime</p>
      </div>

      {/* Steps */}
      <div className="flex items-center gap-4 pb-4">
        {['Modelos', 'Iniciar Migração', 'Jobs'].map((label, i) => (
          <button key={i} onClick={() => setStep(i)}
            className={`flex items-center gap-2 px-4 py-2 rounded-lg text-sm transition-colors ${step === i ? 'bg-violet-600 text-white' : 'bg-zinc-800 text-zinc-400 hover:text-zinc-200'}`}>
            <span className="w-5 h-5 flex items-center justify-center rounded-full bg-zinc-700 text-xs">{i + 1}</span>
            {label}
          </button>
        ))}
      </div>

      {/* Step 0: Models */}
      {step === 0 && (
        <div className="space-y-4">
          <div className="flex justify-between items-center">
            <h2 className="text-lg text-zinc-200">Modelos de Embedding Configurados</h2>
            <button onClick={() => setShowModelForm(true)} className="flex items-center gap-2 px-3 py-2 bg-violet-600 text-white rounded-lg text-sm hover:bg-violet-700">
              <Plus className="w-4 h-4" /> Novo Modelo
            </button>
          </div>

          {showModelForm && (
            <form onSubmit={saveModel} className="bg-zinc-900 border border-zinc-800 rounded-xl p-4 space-y-4">
              <div className="grid grid-cols-2 gap-4">
                <div>
                  <label className="block text-xs text-zinc-400 mb-1">Provider</label>
                  <select value={modelForm.provider} onChange={e => setModelForm({ ...modelForm, provider: e.target.value })}
                    className="w-full bg-zinc-800 border border-zinc-700 rounded-lg px-3 py-2 text-sm text-zinc-100">
                    {providerOptions.map(p => <option key={p} value={p}>{p}</option>)}
                  </select>
                </div>
                <div>
                  <label className="block text-xs text-zinc-400 mb-1">Nome do Modelo</label>
                  <input value={modelForm.modelName} onChange={e => setModelForm({ ...modelForm, modelName: e.target.value })}
                    className="w-full bg-zinc-800 border border-zinc-700 rounded-lg px-3 py-2 text-sm text-zinc-100" placeholder="text-embedding-3-small" required />
                </div>
                <div>
                  <label className="block text-xs text-zinc-400 mb-1">Dimensões</label>
                  <input type="number" value={modelForm.dimensions} onChange={e => setModelForm({ ...modelForm, dimensions: parseInt(e.target.value) })}
                    className="w-full bg-zinc-800 border border-zinc-700 rounded-lg px-3 py-2 text-sm text-zinc-100" required />
                </div>
                <div>
                  <label className="block text-xs text-zinc-400 mb-1">Base URL (opcional)</label>
                  <input value={modelForm.baseUrl} onChange={e => setModelForm({ ...modelForm, baseUrl: e.target.value })}
                    className="w-full bg-zinc-800 border border-zinc-700 rounded-lg px-3 py-2 text-sm text-zinc-100" placeholder="https://api.openai.com" />
                </div>
                <div className="col-span-2">
                  <label className="block text-xs text-zinc-400 mb-1">API Key</label>
                  <input type="password" value={modelForm.apiKey} onChange={e => setModelForm({ ...modelForm, apiKey: e.target.value })}
                    className="w-full bg-zinc-800 border border-zinc-700 rounded-lg px-3 py-2 text-sm text-zinc-100" />
                </div>
              </div>
              <div className="flex gap-2 justify-end">
                <button type="button" onClick={() => setShowModelForm(false)} className="px-3 py-2 text-sm text-zinc-400 hover:text-zinc-200">Cancelar</button>
                <button type="submit" className="px-4 py-2 bg-violet-600 text-white rounded-lg text-sm hover:bg-violet-700">Salvar</button>
              </div>
            </form>
          )}

          <div className="space-y-2">
            {models.map(model => (
              <div key={model.id} className="flex items-center justify-between bg-zinc-900 border border-zinc-800 rounded-xl px-4 py-3">
                <div className="flex items-center gap-3">
                  <Cpu className={`w-5 h-5 ${model.isActive ? 'text-green-400' : 'text-zinc-500'}`} />
                  <div>
                    <div className="text-sm text-zinc-100 font-medium">{model.provider} / {model.modelName}</div>
                    <div className="text-xs text-zinc-500">{model.dimensions}d{model.isActive && <span className="ml-2 text-green-400">● Ativo</span>}</div>
                  </div>
                </div>
                <div className="flex gap-1">
                  {!model.isActive && <button onClick={() => activateModel(model.id)} className="px-3 py-1 text-xs bg-zinc-800 text-zinc-300 rounded hover:bg-zinc-700">Ativar</button>}
                  <button onClick={() => deleteModel(model.id)} className="p-2 text-zinc-400 hover:text-red-400"><Trash2 className="w-4 h-4" /></button>
                </div>
              </div>
            ))}
            {models.length === 0 && <div className="text-center py-8 text-zinc-500">Nenhum modelo configurado. Adicione um para começar.</div>}
          </div>
        </div>
      )}

      {/* Step 1: Start Migration */}
      {step === 1 && (
        <form onSubmit={startMigration} className="bg-zinc-900 border border-zinc-800 rounded-xl p-6 space-y-6">
          <h2 className="text-lg text-zinc-200 flex items-center gap-2"><ArrowLeftRight className="w-5 h-5 text-violet-400" /> Nova Migração</h2>
          <div className="grid grid-cols-1 md:grid-cols-3 gap-4 items-end">
            <div>
              <label className="block text-xs text-zinc-400 mb-1">Modelo Origem</label>
              <select value={migrationForm.sourceModelId} onChange={e => setMigrationForm({ ...migrationForm, sourceModelId: e.target.value })}
                className="w-full bg-zinc-800 border border-zinc-700 rounded-lg px-3 py-2 text-sm text-zinc-100" required>
                <option value="">Selecione...</option>
                {models.map(m => <option key={m.id} value={m.id}>{m.provider}/{m.modelName} ({m.dimensions}d)</option>)}
              </select>
            </div>
            <div className="flex justify-center"><ArrowRight className="w-6 h-6 text-zinc-500" /></div>
            <div>
              <label className="block text-xs text-zinc-400 mb-1">Modelo Destino</label>
              <select value={migrationForm.targetModelId} onChange={e => setMigrationForm({ ...migrationForm, targetModelId: e.target.value })}
                className="w-full bg-zinc-800 border border-zinc-700 rounded-lg px-3 py-2 text-sm text-zinc-100" required>
                <option value="">Selecione...</option>
                {models.filter(m => m.id !== migrationForm.sourceModelId).map(m => <option key={m.id} value={m.id}>{m.provider}/{m.modelName} ({m.dimensions}d)</option>)}
              </select>
            </div>
          </div>
          <div>
            <label className="block text-xs text-zinc-400 mb-1">Coleção Origem (opcional, default: "default")</label>
            <input value={migrationForm.sourceCollection} onChange={e => setMigrationForm({ ...migrationForm, sourceCollection: e.target.value })}
              className="w-full bg-zinc-800 border border-zinc-700 rounded-lg px-3 py-2 text-sm text-zinc-100" placeholder="default" />
          </div>
          <button type="submit" className="flex items-center gap-2 px-6 py-3 bg-violet-600 text-white rounded-lg hover:bg-violet-700">
            <Play className="w-4 h-4" /> Iniciar Migração
          </button>
        </form>
      )}

      {/* Step 2: Jobs */}
      {step === 2 && (
        <div className="space-y-4">
          <h2 className="text-lg text-zinc-200">Jobs de Migração</h2>

          {/* Status tracker for selected job */}
          {jobStatus && (
            <div className="bg-zinc-900 border border-violet-800 rounded-xl p-4 space-y-3">
              <div className="flex items-center justify-between">
                <span className="text-sm text-zinc-100 font-medium">{jobStatus.sourceModel} → {jobStatus.targetModel}</span>
                <span className={`px-2 py-0.5 rounded-full text-xs ${statusColors[jobStatus.status] || 'bg-zinc-800 text-zinc-400'}`}>{jobStatus.status}</span>
              </div>
              <div className="relative w-full h-3 bg-zinc-800 rounded-full overflow-hidden">
                <div className="absolute inset-y-0 left-0 bg-violet-600 rounded-full transition-all duration-500" style={{ width: `${jobStatus.progressPercentage}%` }} />
              </div>
              <div className="flex justify-between text-xs text-zinc-400">
                <span>{jobStatus.processedDocuments}/{jobStatus.totalDocuments} docs</span>
                <span>{jobStatus.progressPercentage.toFixed(1)}%</span>
                {jobStatus.estimatedTimeRemaining && <span>ETA: {jobStatus.estimatedTimeRemaining}</span>}
              </div>
              {jobStatus.failedDocuments > 0 && <div className="text-xs text-red-400">{jobStatus.failedDocuments} documentos falharam</div>}
            </div>
          )}

          {/* Job list */}
          <div className="space-y-2">
            {jobs.map(job => (
              <div key={job.id} className="bg-zinc-900 border border-zinc-800 rounded-xl px-4 py-3">
                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-3 cursor-pointer" onClick={() => { setSelectedJobId(job.id); fetchJobStatus(job.id) }}>
                    {job.status === 'InProgress' ? <Loader2 className="w-4 h-4 text-blue-400 animate-spin" /> :
                     job.status === 'Completed' ? <CheckCircle2 className="w-4 h-4 text-green-400" /> :
                     job.status === 'Failed' ? <XCircle className="w-4 h-4 text-red-400" /> :
                     <Pause className="w-4 h-4 text-zinc-400" />}
                    <div>
                      <div className="text-sm text-zinc-100">{job.sourceModel.modelName} → {job.targetModel.modelName}</div>
                      <div className="text-xs text-zinc-500">{job.sourceCollection} → {job.targetCollection}</div>
                    </div>
                  </div>
                  <div className="flex items-center gap-2">
                    <span className={`px-2 py-0.5 rounded-full text-xs ${statusColors[job.status]}`}>{job.status}</span>
                    {job.status === 'InProgress' && <button onClick={() => cancelJob(job.id)} className="p-1 text-zinc-400 hover:text-red-400"><Pause className="w-4 h-4" /></button>}
                    {job.status === 'Failed' && <button onClick={() => retryJob(job.id)} className="p-1 text-zinc-400 hover:text-blue-400"><RotateCcw className="w-4 h-4" /></button>}
                    {job.status === 'Completed' && <button onClick={() => switchCollection(job.id)} className="px-2 py-1 text-xs bg-green-900/30 text-green-400 rounded hover:bg-green-900/50">Ativar</button>}
                  </div>
                </div>
                {job.status === 'InProgress' && (
                  <div className="mt-2 relative w-full h-2 bg-zinc-800 rounded-full overflow-hidden">
                    <div className="absolute inset-y-0 left-0 bg-blue-500 rounded-full transition-all" style={{ width: `${job.progressPercentage}%` }} />
                  </div>
                )}
              </div>
            ))}
            {jobs.length === 0 && <div className="text-center py-8 text-zinc-500">Nenhum job de migração. Inicie uma migração no passo anterior.</div>}
          </div>
        </div>
      )}
    </div>
  )
}
