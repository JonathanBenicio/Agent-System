import { useState } from 'react'
import { Settings2, Save, Loader2, Upload } from 'lucide-react'
import { useSettings } from '@/hooks/useSettings'
import { PageLoading, PageError } from '@/components/shared/Loading'
import { useToast } from '@/components/shared/Toast'
import type { GatewaySettings, MemorySettings, RerankingSettings } from '@/types/api'

type Tab = 'gateway' | 'memory' | 'reranking'

export function SettingsPage() {
  const { settings, loading, error, saving, refresh, saveGateway, saveMemory, saveReranking, uploadRerankingAssets } = useSettings()
  const { addToast } = useToast()
  const [tab, setTab] = useState<Tab>('gateway')
  const [gwForm, setGwForm] = useState<GatewaySettings | null>(null)
  const [memForm, setMemForm] = useState<MemorySettings | null>(null)
  const [rerankForm, setRerankForm] = useState<RerankingSettings | null>(null)
  const [modelFile, setModelFile] = useState<File | null>(null)
  const [vocabularyFile, setVocabularyFile] = useState<File | null>(null)
  const [packageFile, setPackageFile] = useState<File | null>(null)
  const [uploadInputKey, setUploadInputKey] = useState(0)

  if (loading) return <PageLoading />
  if (error || !settings) return <PageError message={error ?? 'Sem dados'} onRetry={refresh} />

  const gw = gwForm ?? settings.gateway
  const mem = memForm ?? settings.memory
  const rerank = rerankForm ?? settings.reranking

  const handleSaveGateway = async () => {
    if (!gw) return
    try {
      await saveGateway(gw)
      setGwForm(null)
      addToast('Configurações de Gateway salvas', 'success')
    } catch {
      addToast('Erro ao salvar configurações', 'error')
    }
  }

  const handleSaveMemory = async () => {
    if (!mem) return
    try {
      await saveMemory(mem)
      setMemForm(null)
      addToast('Configurações de Memória salvas', 'success')
    } catch {
      addToast('Erro ao salvar configurações', 'error')
    }
  }

  const handleSaveReranking = async () => {
    if (!rerank) return
    try {
      await saveReranking(rerank)
      setRerankForm(null)
      addToast('Configurações de Rerank salvas', 'success')
    } catch {
      addToast('Erro ao salvar configurações de rerank', 'error')
    }
  }

  const handleUploadRerankingAssets = async () => {
    if (!modelFile && !vocabularyFile && !packageFile) return

    try {
      const updated = await uploadRerankingAssets(modelFile ?? undefined, vocabularyFile ?? undefined, packageFile ?? undefined)
      setRerankForm(prev => prev
        ? {
            ...prev,
            hasUploadedLocalOnnxModel: updated.hasUploadedLocalOnnxModel,
            uploadedLocalOnnxModelFileName: updated.uploadedLocalOnnxModelFileName,
            hasUploadedLocalOnnxVocabulary: updated.hasUploadedLocalOnnxVocabulary,
            uploadedLocalOnnxVocabularyFileName: updated.uploadedLocalOnnxVocabularyFileName,
            localOnnxModelPath: updated.localOnnxModelPath,
            localOnnxVocabularyPath: updated.localOnnxVocabularyPath,
          }
        : null)
      setModelFile(null)
      setVocabularyFile(null)
      setPackageFile(null)
      setUploadInputKey(key => key + 1)
      addToast('Assets de rerank enviados', 'success')
    } catch {
      addToast('Erro ao enviar assets de rerank', 'error')
    }
  }

  const updateRerank = <K extends keyof RerankingSettings>(key: K, value: RerankingSettings[K]) => {
    setRerankForm({ ...rerank, [key]: value })
  }

  const tabs: { id: Tab; label: string }[] = [
    { id: 'gateway', label: 'Gateway' },
    { id: 'memory', label: 'Memória' },
    { id: 'reranking', label: 'Rerank' },
  ]

  return (
    <div className="h-full overflow-y-auto">
      <div className="max-w-3xl mx-auto px-6 py-6 space-y-6">
        <div className="flex items-center gap-3">
          <Settings2 className="w-5 h-5 text-zinc-400" />
          <h1 className="text-xl font-semibold text-zinc-100">Configurações</h1>
        </div>

        {/* Tabs */}
        <div className="flex gap-1 border-b border-zinc-800">
          {tabs.map(t => (
            <button
              key={t.id}
              onClick={() => setTab(t.id)}
              className={`px-4 py-2 text-sm font-medium border-b-2 transition-colors ${
                tab === t.id
                  ? 'border-teal-500 text-teal-400'
                  : 'border-transparent text-zinc-500 hover:text-zinc-300'
              }`}
            >
              {t.label}
            </button>
          ))}
        </div>

        {/* Gateway Tab */}
        {tab === 'gateway' && gw && (
          <div className="space-y-4">
            <FormField label="Daily Budget ($)">
              <input
                type="number"
                min={0}
                step={0.01}
                value={gw.defaultDailyBudget}
                onChange={e => setGwForm({ ...gw, defaultDailyBudget: Number(e.target.value) })}
                className="input"
              />
            </FormField>
            <FormField label="Failure Threshold">
              <input
                type="number"
                min={1}
                max={50}
                value={gw.defaultFailureThreshold}
                onChange={e => setGwForm({ ...gw, defaultFailureThreshold: Number(e.target.value) })}
                className="input"
              />
            </FormField>
            <FormField label="Break Duration (s)">
              <input
                type="number"
                min={5}
                max={300}
                value={gw.defaultBreakDurationSeconds}
                onChange={e => setGwForm({ ...gw, defaultBreakDurationSeconds: Number(e.target.value) })}
                className="input"
              />
            </FormField>
            <FormField label="Requests Per Minute">
              <input
                type="number"
                min={1}
                max={1000}
                value={gw.defaultRequestsPerMinute}
                onChange={e => setGwForm({ ...gw, defaultRequestsPerMinute: Number(e.target.value) })}
                className="input"
              />
            </FormField>
            <SaveButton onClick={handleSaveGateway} saving={saving} disabled={!gwForm} />
          </div>
        )}

        {/* Memory Tab */}
        {tab === 'memory' && mem && (
          <div className="space-y-4">
            <FormField label="Obsidian Vault Path">
              <input
                type="text"
                value={mem.obsidianVaultPath}
                onChange={e => setMemForm({ ...mem, obsidianVaultPath: e.target.value })}
                className="input"
                placeholder="/path/to/vault"
              />
            </FormField>
            <FormField label="Vector Store Type">
              <select
                value={mem.vectorStoreType}
                onChange={e => setMemForm({ ...mem, vectorStoreType: e.target.value })}
                className="input"
              >
                <option value="InMemory">InMemory</option>
                <option value="PostgreSQL">PostgreSQL</option>
                <option value="Qdrant">Qdrant</option>
              </select>
            </FormField>
            <FormField label="Connection String">
              <input
                type="text"
                value={mem.connectionString ?? ''}
                onChange={e => setMemForm({ ...mem, connectionString: e.target.value || undefined })}
                className="input"
                placeholder="Host=localhost;Database=..."
              />
            </FormField>
            <SaveButton onClick={handleSaveMemory} saving={saving} disabled={!memForm} />
          </div>
        )}

        {tab === 'reranking' && rerank && (
          <div className="space-y-6">
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              <ToggleField
                label="Reranking Ativo"
                checked={rerank.enabled}
                onChange={checked => updateRerank('enabled', checked)}
              />
              <ToggleField
                label="Usar Provider Dedicado"
                checked={rerank.useDedicatedProvider}
                onChange={checked => updateRerank('useDedicatedProvider', checked)}
              />
              <ToggleField
                label="Fallback por Embeddings"
                checked={rerank.useEmbeddingReRanking}
                onChange={checked => updateRerank('useEmbeddingReRanking', checked)}
              />
              <ToggleField
                label="Fallback por LLM"
                checked={rerank.useLlmReRanking}
                onChange={checked => updateRerank('useLlmReRanking', checked)}
              />
            </div>

            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              <FormField label="Provider Dedicado">
                <select
                  value={rerank.dedicatedProvider}
                  onChange={e => updateRerank('dedicatedProvider', e.target.value)}
                  className="input"
                >
                  <option value="LocalOnnxCrossEncoder">Local ONNX Cross-Encoder</option>
                  <option value="jina-reranker">Jina</option>
                </select>
              </FormField>
              <FormField label="Peso Neural">
                <input
                  type="number"
                  min={0}
                  max={1}
                  step={0.05}
                  value={rerank.neuralScoreWeight}
                  onChange={e => updateRerank('neuralScoreWeight', Number(e.target.value))}
                  className="input"
                />
              </FormField>
              <FormField label="Candidate Pool Size">
                <input
                  type="number"
                  min={2}
                  max={50}
                  value={rerank.candidatePoolSize}
                  onChange={e => updateRerank('candidatePoolSize', Number(e.target.value))}
                  className="input"
                />
              </FormField>
              <FormField label="Min Candidate Count For LLM">
                <input
                  type="number"
                  min={2}
                  max={50}
                  value={rerank.minCandidateCountForLlm}
                  onChange={e => updateRerank('minCandidateCountForLlm', Number(e.target.value))}
                  className="input"
                />
              </FormField>
            </div>

            {rerank.dedicatedProvider === 'LocalOnnxCrossEncoder' && (
              <div className="space-y-4 rounded-xl border border-zinc-800 bg-zinc-900/50 p-4">
                <h2 className="text-sm font-semibold text-zinc-200">Provider Local ONNX</h2>
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  <AssetStatusCard
                    label="Modelo ONNX"
                    uploaded={rerank.hasUploadedLocalOnnxModel}
                    fileName={rerank.uploadedLocalOnnxModelFileName}
                  />
                  <AssetStatusCard
                    label="Vocabulário"
                    uploaded={rerank.hasUploadedLocalOnnxVocabulary}
                    fileName={rerank.uploadedLocalOnnxVocabularyFileName}
                  />
                </div>

                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  <FormField label="Pacote ZIP (.zip)">
                    <input
                      key={`package-${uploadInputKey}`}
                      type="file"
                      accept=".zip"
                      onChange={e => setPackageFile(e.target.files?.[0] ?? null)}
                      className="input file:mr-3 file:rounded-md file:border-0 file:bg-zinc-800 file:px-3 file:py-2 file:text-sm file:text-zinc-200"
                    />
                  </FormField>
                  <div className="rounded-lg border border-dashed border-zinc-700 bg-zinc-950/40 px-4 py-3 text-sm text-zinc-400">
                    Envie um ZIP contendo model.onnx e vocab.txt ou envie os arquivos separadamente abaixo.
                  </div>
                </div>

                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  <FormField label="Upload do Modelo (.onnx)">
                    <input
                      key={`model-${uploadInputKey}`}
                      type="file"
                      accept=".onnx"
                      onChange={e => setModelFile(e.target.files?.[0] ?? null)}
                      className="input file:mr-3 file:rounded-md file:border-0 file:bg-zinc-800 file:px-3 file:py-2 file:text-sm file:text-zinc-200"
                    />
                  </FormField>
                  <FormField label="Upload do Vocabulário (vocab.txt)">
                    <input
                      key={`vocab-${uploadInputKey}`}
                      type="file"
                      accept=".txt"
                      onChange={e => setVocabularyFile(e.target.files?.[0] ?? null)}
                      className="input file:mr-3 file:rounded-md file:border-0 file:bg-zinc-800 file:px-3 file:py-2 file:text-sm file:text-zinc-200"
                    />
                  </FormField>
                </div>

                <div className="flex flex-wrap items-center gap-3 rounded-lg border border-dashed border-zinc-700 bg-zinc-950/40 px-4 py-3">
                  <div className="text-sm text-zinc-400">
                    {packageFile?.name ?? 'Nenhum pacote ZIP selecionado'}
                    {' | '}
                    {modelFile?.name ?? 'Nenhum model.onnx selecionado'}
                    {' | '}
                    {vocabularyFile?.name ?? 'Nenhum vocab.txt selecionado'}
                  </div>
                  <button
                    type="button"
                    onClick={handleUploadRerankingAssets}
                    disabled={saving || (!modelFile && !vocabularyFile && !packageFile)}
                    className="inline-flex items-center gap-2 rounded-lg border border-zinc-700 px-3 py-2 text-sm text-zinc-200 hover:bg-zinc-800 disabled:opacity-50"
                  >
                    {saving ? <Loader2 className="w-4 h-4 animate-spin" /> : <Upload className="w-4 h-4" />}
                    {saving ? 'Enviando...' : 'Enviar Assets'}
                  </button>
                </div>

                <div className="grid grid-cols-1 gap-4">
                  <FormField label="Model Path (.onnx)">
                    <input
                      type="text"
                      value={rerank.localOnnxModelPath ?? ''}
                      onChange={e => updateRerank('localOnnxModelPath', e.target.value || undefined)}
                      className="input"
                      placeholder="C:/models/rerank/model.onnx"
                    />
                  </FormField>
                  <FormField label="Vocabulary Path (vocab.txt)">
                    <input
                      type="text"
                      value={rerank.localOnnxVocabularyPath ?? ''}
                      onChange={e => updateRerank('localOnnxVocabularyPath', e.target.value || undefined)}
                      className="input"
                      placeholder="C:/models/rerank/vocab.txt"
                    />
                  </FormField>
                </div>

                <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
                  <FormField label="Max Sequence Length">
                    <input
                      type="number"
                      min={32}
                      max={1024}
                      value={rerank.localOnnxMaxSequenceLength}
                      onChange={e => updateRerank('localOnnxMaxSequenceLength', Number(e.target.value))}
                      className="input"
                    />
                  </FormField>
                  <FormField label="Max Query Tokens">
                    <input
                      type="number"
                      min={8}
                      max={512}
                      value={rerank.localOnnxMaxQueryTokens}
                      onChange={e => updateRerank('localOnnxMaxQueryTokens', Number(e.target.value))}
                      className="input"
                    />
                  </FormField>
                  <ToggleField
                    label="Lower Case"
                    checked={rerank.localOnnxLowerCase}
                    onChange={checked => updateRerank('localOnnxLowerCase', checked)}
                  />
                </div>

                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  <FormField label="Input IDs Name">
                    <input
                      type="text"
                      value={rerank.localOnnxInputIdsName}
                      onChange={e => updateRerank('localOnnxInputIdsName', e.target.value)}
                      className="input"
                    />
                  </FormField>
                  <FormField label="Attention Mask Name">
                    <input
                      type="text"
                      value={rerank.localOnnxAttentionMaskName}
                      onChange={e => updateRerank('localOnnxAttentionMaskName', e.target.value)}
                      className="input"
                    />
                  </FormField>
                  <FormField label="Token Type IDs Name">
                    <input
                      type="text"
                      value={rerank.localOnnxTokenTypeIdsName}
                      onChange={e => updateRerank('localOnnxTokenTypeIdsName', e.target.value)}
                      className="input"
                    />
                  </FormField>
                  <FormField label="Output Name">
                    <input
                      type="text"
                      value={rerank.localOnnxOutputName}
                      onChange={e => updateRerank('localOnnxOutputName', e.target.value)}
                      className="input"
                    />
                  </FormField>
                  <FormField label="Positive Label Index">
                    <input
                      type="number"
                      min={0}
                      max={10}
                      value={rerank.localOnnxPositiveLabelIndex}
                      onChange={e => updateRerank('localOnnxPositiveLabelIndex', Number(e.target.value))}
                      className="input"
                    />
                  </FormField>
                </div>
              </div>
            )}

            {rerank.dedicatedProvider === 'jina-reranker' && (
              <div className="space-y-4 rounded-xl border border-zinc-800 bg-zinc-900/50 p-4">
                <h2 className="text-sm font-semibold text-zinc-200">Provider Jina</h2>
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  <FormField label="Base URL">
                    <input
                      type="text"
                      value={rerank.dedicatedProviderBaseUrl}
                      onChange={e => updateRerank('dedicatedProviderBaseUrl', e.target.value)}
                      className="input"
                    />
                  </FormField>
                  <FormField label="Model">
                    <input
                      type="text"
                      value={rerank.dedicatedProviderModel}
                      onChange={e => updateRerank('dedicatedProviderModel', e.target.value)}
                      className="input"
                    />
                  </FormField>
                  <FormField label="Timeout (s)">
                    <input
                      type="number"
                      min={1}
                      max={120}
                      value={rerank.dedicatedProviderTimeoutSeconds}
                      onChange={e => updateRerank('dedicatedProviderTimeoutSeconds', Number(e.target.value))}
                      className="input"
                    />
                  </FormField>
                  <FormField label="API Key">
                    <input
                      type="password"
                      value={rerank.dedicatedProviderApiKey ?? ''}
                      onChange={e => updateRerank('dedicatedProviderApiKey', e.target.value || undefined)}
                      className="input"
                      placeholder={rerank.hasDedicatedProviderApiKey ? 'Deixe em branco para manter a atual' : 'Bearer token'}
                    />
                  </FormField>
                </div>
              </div>
            )}

            <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
              <FormField label="Heuristic Confidence Threshold">
                <input
                  type="number"
                  min={0}
                  max={1}
                  step={0.01}
                  value={rerank.heuristicConfidenceThreshold}
                  onChange={e => updateRerank('heuristicConfidenceThreshold', Number(e.target.value))}
                  className="input"
                />
              </FormField>
              <FormField label="Heuristic Confidence Gap">
                <input
                  type="number"
                  min={0}
                  max={1}
                  step={0.01}
                  value={rerank.heuristicConfidenceGap}
                  onChange={e => updateRerank('heuristicConfidenceGap', Number(e.target.value))}
                  className="input"
                />
              </FormField>
              <FormField label="LLM Weight">
                <input
                  type="number"
                  min={0}
                  max={1}
                  step={0.05}
                  value={rerank.llmScoreWeight}
                  onChange={e => updateRerank('llmScoreWeight', Number(e.target.value))}
                  className="input"
                />
              </FormField>
              <FormField label="Max Snippet Characters">
                <input
                  type="number"
                  min={50}
                  max={4000}
                  value={rerank.maxSnippetCharacters}
                  onChange={e => updateRerank('maxSnippetCharacters', Number(e.target.value))}
                  className="input"
                />
              </FormField>
              <FormField label="Max Output Tokens (LLM)">
                <input
                  type="number"
                  min={16}
                  max={2048}
                  value={rerank.maxOutputTokens}
                  onChange={e => updateRerank('maxOutputTokens', Number(e.target.value))}
                  className="input"
                />
              </FormField>
              <FormField label="Temperature (LLM)">
                <input
                  type="number"
                  min={0}
                  max={2}
                  step={0.05}
                  value={rerank.temperature}
                  onChange={e => updateRerank('temperature', Number(e.target.value))}
                  className="input"
                />
              </FormField>
            </div>

            <SaveButton onClick={handleSaveReranking} saving={saving} disabled={!rerankForm} />
          </div>
        )}
      </div>
    </div>
  )
}

function ToggleField({
  label,
  checked,
  onChange,
}: {
  label: string
  checked: boolean
  onChange: (checked: boolean) => void
}) {
  return (
    <label className="flex items-center justify-between rounded-lg border border-zinc-800 bg-zinc-900/50 px-4 py-3">
      <span className="text-sm text-zinc-300">{label}</span>
      <input
        type="checkbox"
        checked={checked}
        onChange={e => onChange(e.target.checked)}
        className="h-4 w-4 rounded border-zinc-700 bg-zinc-950 text-teal-500"
      />
    </label>
  )
}

function FormField({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <label className="block">
      <span className="block text-xs font-medium text-zinc-400 mb-1">{label}</span>
      {children}
    </label>
  )
}

function AssetStatusCard({
  label,
  uploaded,
  fileName,
}: {
  label: string
  uploaded: boolean
  fileName?: string
}) {
  return (
    <div className="rounded-lg border border-zinc-800 bg-zinc-950/60 px-4 py-3">
      <div className="text-xs font-medium uppercase tracking-wide text-zinc-500">{label}</div>
      <div className={`mt-2 text-sm font-medium ${uploaded ? 'text-emerald-400' : 'text-amber-400'}`}>
        {uploaded ? 'Persistido' : 'Pendente'}
      </div>
      <div className="mt-1 text-xs text-zinc-400">{fileName ?? 'Nenhum arquivo enviado'}</div>
    </div>
  )
}

function SaveButton({ onClick, saving, disabled }: { onClick: () => void; saving: boolean; disabled: boolean }) {
  return (
    <div className="flex justify-end pt-4 border-t border-zinc-800">
      <button
        onClick={onClick}
        disabled={saving || disabled}
        className="flex items-center gap-2 px-4 py-2 text-sm rounded-lg bg-teal-600 text-white hover:bg-teal-500 disabled:opacity-50"
      >
        {saving ? <Loader2 className="w-4 h-4 animate-spin" /> : <Save className="w-4 h-4" />}
        {saving ? 'Salvando...' : 'Salvar'}
      </button>
    </div>
  )
}
