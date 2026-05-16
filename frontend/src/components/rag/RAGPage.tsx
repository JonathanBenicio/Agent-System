import React, { useState } from 'react'
import { Database, Search, FileText, Cpu, Upload, /* Layers, */ RefreshCw, Plus, Check, Play, AlertCircle, Settings2, Trash2, Box } from 'lucide-react'
import { Badge } from '@/components/shared/Badge'
import { useRAG } from '@/hooks/useRAG'
import type { IngestDocumentResponse } from '@/types/api'
import { KnowledgeRooms } from './KnowledgeRooms'

const PRESET_MODELS: Record<string, { name: string; dim: number; desc: string }[]> = {
  OpenAI: [
    { name: 'text-embedding-3-small', dim: 1536, desc: 'Padrão rápido e eficiente' },
    { name: 'text-embedding-3-large', dim: 3072, desc: 'Alta precisão e maior capacidade' },
    { name: 'text-embedding-ada-002', dim: 1536, desc: 'Modelo legado v2' },
  ],
  Ollama: [
    { name: 'nomic-embed-text', dim: 768, desc: 'Excelente modelo geral open-source' },
    { name: 'mxbai-embed-large', dim: 1024, desc: 'State-of-the-art para busca semântica' },
    { name: 'all-minilm', dim: 384, desc: 'Leve, rápido e muito popular' },
    { name: 'bge-small', dim: 384, desc: 'Otimizado para busca e recuperação' },
    { name: 'bge-large', dim: 1024, desc: 'Alta capacidade semântica' },
  ],
  ONNX: [
    { name: 'bge-small', dim: 384, desc: 'Inferência local ultrarrápida via CPU/GPU' },
    { name: 'all-MiniLM-L6-v2', dim: 384, desc: 'Modelo compacto in-process' },
    { name: 'bge-reranker-small', dim: 384, desc: 'Focado em reranking e cross-encoder' },
  ],
}

export function RAGPage() {
  const {
    loading,
    ingesting,
    error,
    stats,
    models,
    activeModel,
    jobs,
    rerankingSettings,
    refresh,
    ingestDocument,
    ingestBatch,
    saveModelConfig,
    activateModel,
    deleteModel,
    startMigrationJob,
    updateReranking,
  } = useRAG()

  const [activeTab, setActiveTab] = useState<'rooms' | 'ingestion' | 'config' | 'migration'>('rooms')
  const [successMsg, setSuccessMsg] = useState<string | null>(null)

  // Ingestion State
  const [selectedFiles, setSelectedFiles] = useState<FileList | null>(null)
  const [ingestionSource, setIngestionSource] = useState<string>('corporate_wiki')
  const [ingestResults, setIngestResults] = useState<{ type: 'single'; data: IngestDocumentResponse } | { type: 'batch'; data: { total: number; succeeded: number; failed: number } } | null>(null)


  // Config State
  const [editingRerank, setEditingRerank] = useState<boolean>(false)
  const [neuralWeight, setNeuralWeight] = useState<number>(rerankingSettings?.neuralScoreWeight ?? 0.7)
  const [llmWeight, setLlmWeight] = useState<number>(rerankingSettings?.llmScoreWeight ?? 0.3)
  const [onnxModelPath, setOnnxModelPath] = useState<string>(rerankingSettings?.localOnnxModelPath ?? './models/bge-reranker-small.onnx')
  const [maxOutputTokens, setMaxOutputTokens] = useState<number>(rerankingSettings?.maxOutputTokens ?? 1024)

  // Migration State
  const [showNewModelForm, setShowNewModelForm] = useState(false)
  const [newModelName, setNewModelName] = useState('text-embedding-3-small')
  const [newModelProvider, setNewModelProvider] = useState('OpenAI')
  const [newModelDim, setNewModelDim] = useState(1536)
  const [newModelBaseUrl, setNewModelBaseUrl] = useState('')
  const [newModelApiKey, setNewModelApiKey] = useState('')

  const handleProviderChange = (provider: string) => {
    setNewModelProvider(provider)
    const presets = PRESET_MODELS[provider]
    if (presets && presets.length > 0) {
      setNewModelName(presets[0].name)
      setNewModelDim(presets[0].dim)
    }
  }

  const handleSelectPreset = (name: string, dim: number) => {
    setNewModelName(name)
    setNewModelDim(dim)
  }

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    if (e.target.files && e.target.files.length > 0) {
      setSelectedFiles(e.target.files)
    }
  }

  const handleIngestSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!selectedFiles || selectedFiles.length === 0) return

    setSuccessMsg(null)
    setIngestResults(null)

    try {
      if (selectedFiles.length === 1) {
        const res = await ingestDocument(selectedFiles[0], ingestionSource)
        setIngestResults({ type: 'single', data: res })
        setSuccessMsg(`Documento '${res.fileName}' ingerido com sucesso! (${res.chunksCreated} chunks criados)`)
      } else {
        const res = await ingestBatch(selectedFiles, ingestionSource)
        setIngestResults({ type: 'batch', data: res })
        setSuccessMsg(`Lote processado: ${res.succeeded} documentos ingeridos com sucesso!`)
      }
      setSelectedFiles(null)
    } catch (err) {
      // Erro tratado no hook
    }
  }

  const handleSaveRerankConfig = async () => {
    if (!rerankingSettings) return
    try {
      await updateReranking({
        ...rerankingSettings,
        neuralScoreWeight: neuralWeight,
        llmScoreWeight: llmWeight,
        localOnnxModelPath: onnxModelPath,
        maxOutputTokens: maxOutputTokens,
      })
      setSuccessMsg('Configurações de Reranking & ONNX salvas com sucesso!')
      setEditingRerank(false)
    } catch (err) {
      // Erro tratado no hook
    }
  }

  const handleCreateModel = async (e: React.FormEvent) => {
    e.preventDefault()
    try {
      await saveModelConfig({
        name: newModelName,
        provider: newModelProvider,
        modelName: newModelName.toLowerCase().replace(/\s+/g, '-'),
        dimensions: Number(newModelDim),
        baseUrl: newModelBaseUrl || undefined,
        apiKey: newModelApiKey || undefined,
        isActive: false,
      })
      setSuccessMsg(`Modelo '${newModelName}' criado com sucesso!`)
      setShowNewModelForm(false)
      setNewModelName('')
      setNewModelBaseUrl('')
      setNewModelApiKey('')
    } catch (err) {
      // Erro tratado no hook
    }
  }

  const handleStartMigration = async (targetModelId: string) => {
    if (!activeModel) return
    try {
      await startMigrationJob({
        sourceModelId: activeModel.id,
        targetModelId: targetModelId,
        batchSize: 100,
        maxConcurrency: 4,
      })
      setSuccessMsg('Job de Migração de Chunks Vetoriais iniciado com sucesso!')
    } catch (err) {
      // Erro tratado no hook
    }
  }

  const totalIndexedChunks = stats?.totalChunks ?? 0
  const searchCount24h = stats?.searchCount24h ?? 0

  return (
    <div className="h-full overflow-y-auto bg-zinc-950 text-zinc-100">
      <div className="max-w-7xl mx-auto px-6 py-8 space-y-8">

        {/* Header Section */}
        <div className="flex flex-col md:flex-row md:items-center justify-between gap-4 border-b border-zinc-800/80 pb-6">
          <div>
            <div className="flex items-center gap-3">
              <h1 className="text-2xl font-bold tracking-tight text-white">RAG & ONNX Knowledge Engine</h1>
              <Badge variant="success" className="bg-teal-500/10 text-teal-400 border border-teal-500/20 px-2 py-0.5">
                MAF 1.5 Vector Store
              </Badge>
            </div>
            <p className="text-sm text-zinc-400 mt-1">
              Ingestão de documentos corporativos, busca semântica híbrida no PostgreSQL pgvector e reranking neural in-process via ONNX Runtime.
            </p>
          </div>
          <button
            onClick={refresh}
            disabled={loading}
            className="flex items-center gap-2 px-4 py-2 bg-zinc-900 border border-zinc-800 hover:border-zinc-700 hover:bg-zinc-800/80 text-zinc-300 rounded-lg text-sm transition-all shadow-sm"
          >
            <RefreshCw className={`w-4 h-4 ${loading ? 'animate-spin text-teal-400' : ''}`} />
            Sincronizar Engine
          </button>
        </div>

        {/* Global Feedback */}
        {error && (
          <div className="flex items-center gap-3 bg-red-500/10 border border-red-500/30 text-red-400 px-4 py-3 rounded-xl text-sm">
            <AlertCircle className="w-5 h-5 flex-shrink-0" />
            <p>{error}</p>
          </div>
        )}

        {successMsg && (
          <div className="flex items-center gap-3 bg-teal-500/10 border border-teal-500/30 text-teal-300 px-4 py-3 rounded-xl text-sm transition-all">
            <Check className="w-5 h-5 text-teal-400 flex-shrink-0" />
            <p>{successMsg}</p>
          </div>
        )}

        {/* Metrics Grid */}
        <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
          <MetricCard
            icon={Database}
            title="Banco Vetorial"
            value="PostgreSQL"
            badge="pgvector"
            subtitle="Distância do Cosseno SQL"
          />
          <MetricCard
            icon={Cpu}
            title="Reranker Local"
            value="ONNX Runtime"
            badge="bge-small"
            subtitle="Inferência in-process ~12ms"
          />
          <MetricCard
            icon={FileText}
            title="Chunks Indexados"
            value={loading ? '...' : totalIndexedChunks.toLocaleString()}
            subtitle="Tabela vector_documents"
          />
          <MetricCard
            icon={Search}
            title="Buscas Híbridas"
            value={loading ? '...' : searchCount24h.toLocaleString()}
            subtitle="Últimas 24 horas"
          />
        </div>

        {/* Navigation Tabs */}
        <div className="flex border-b border-zinc-800 gap-2">
          <TabButton
            active={activeTab === 'rooms'}
            onClick={() => setActiveTab('rooms')}
            icon={Box}
            label="1. Salas de Conhecimento"
          />
          <TabButton
            active={activeTab === 'ingestion'}
            onClick={() => setActiveTab('ingestion')}
            icon={Upload}
            label="2. Ingestão Direta"
          />
          <TabButton
            active={activeTab === 'config'}
            onClick={() => setActiveTab('config')}
            icon={Settings2}
            label="3. Configuração da Engine"
          />
          <TabButton
            active={activeTab === 'migration'}
            onClick={() => setActiveTab('migration')}
            icon={RefreshCw}
            label="4. Migração e ONNX"
          />
        </div>

        {activeTab === 'rooms' && <KnowledgeRooms />}

        {/* TAB 1: INGESTÃO DE CHUNKS E DOCUMENTOS */}
        {activeTab === 'ingestion' && (
          <div className="grid grid-cols-1 lg:grid-cols-3 gap-8">
            <div className="lg:col-span-2 space-y-6">
              <div className="bg-zinc-900/60 border border-zinc-800/80 backdrop-blur-md rounded-2xl p-6 shadow-xl">
                <h2 className="text-lg font-semibold text-white flex items-center gap-2 mb-4">
                  <Upload className="w-5 h-5 text-teal-400" />
                  Ingestão e Processamento de Documentos
                </h2>
                <form onSubmit={handleIngestSubmit} className="space-y-6">
                  <div>
                    <label className="block text-sm font-medium text-zinc-300 mb-2">
                      Arquivos para Ingestão (.md, .pdf, .docx, .txt)
                    </label>
                    <div className="relative flex flex-col items-center justify-center w-full h-40 border-2 border-zinc-800 border-dashed hover:border-teal-500/50 rounded-xl bg-zinc-950/50 transition-all group">
                      <input
                        type="file"
                        multiple
                        onChange={handleFileChange}
                        className="absolute inset-0 w-full h-full opacity-0 cursor-pointer"
                        accept=".md,.txt,.pdf,.docx,.html"
                      />
                      <Upload className="w-8 h-8 text-zinc-600 group-hover:text-teal-400 transition-colors mb-2" />
                      <p className="text-sm font-medium text-zinc-300">
                        {selectedFiles && selectedFiles.length > 0 ? (
                          <span className="text-teal-400 font-bold">{selectedFiles.length} arquivo(s) selecionado(s)</span>
                        ) : (
                          "Arraste arquivos aqui ou clique para selecionar"
                        )}
                      </p>
                      <p className="text-xs text-zinc-600 mt-1">Suporta parsing de texto, tabelas e estruturação markdown</p>
                    </div>
                  </div>

                  <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                    <div>
                      <label className="block text-xs font-medium text-zinc-400 mb-1">Origem / Context Source</label>
                      <select
                        value={ingestionSource}
                        onChange={e => setIngestionSource(e.target.value)}
                        className="w-full bg-zinc-950 border border-zinc-800 rounded-xl px-4 py-2.5 text-sm text-zinc-200 focus:border-teal-500 focus:outline-none focus:ring-1 focus:ring-teal-500"
                      >
                        <option value="corporate_wiki">corporate_wiki (Base Geral)</option>
                        <option value="hr_policies">hr_policies (Políticas de RH)</option>
                        <option value="tech_architecture">tech_architecture (Arquitetura e API)</option>
                        <option value="financial_reports">financial_reports (Relatórios Financeiros)</option>
                      </select>
                    </div>

                    <div>
                      <label className="block text-xs font-medium text-zinc-400 mb-1">Estratégia de Chunking</label>
                      <div className="bg-zinc-950 border border-zinc-800 rounded-xl px-4 py-2.5 text-sm text-zinc-400 flex items-center justify-between">
                        <span>Semantic Overlap (500 tokens)</span>
                        <Badge variant="default" className="text-xs">Padrão</Badge>
                      </div>
                    </div>
                  </div>

                  <button
                    type="submit"
                    disabled={ingesting || !selectedFiles || selectedFiles.length === 0}
                    className="w-full flex items-center justify-center gap-2 px-6 py-3 bg-gradient-to-r from-teal-600 to-cyan-600 hover:from-teal-500 hover:to-cyan-500 text-white font-medium rounded-xl text-sm transition-all shadow-md shadow-teal-500/10 disabled:opacity-50 disabled:cursor-not-allowed"
                  >
                    {ingesting ? (
                      <>
                        <RefreshCw className="w-5 h-5 animate-spin" />
                        Processando Chunks e Gerando Embeddings...
                      </>
                    ) : (
                      <>
                        <Play className="w-5 h-5" />
                        Iniciar Ingestão no Vector Store
                      </>
                    )}
                  </button>
                </form>
              </div>

              {/* Ingest Results */}
              {ingestResults && (
                <div className="bg-zinc-900/60 border border-teal-500/30 backdrop-blur-md rounded-2xl p-6 space-y-4">
                  <h3 className="text-md font-semibold text-teal-300 flex items-center gap-2">
                    <Check className="w-5 h-5" />
                    Relatório de Ingestão Concluída
                  </h3>
                  {ingestResults.type === 'single' ? (
                    <div className="grid grid-cols-2 md:grid-cols-4 gap-4 bg-zinc-950 p-4 rounded-xl border border-zinc-800/80">
                      <div>
                        <p className="text-xs text-zinc-500">Documento</p>
                        <p className="text-sm font-semibold text-zinc-200 truncate">{ingestResults.data.fileName}</p>
                      </div>
                      <div>
                        <p className="text-xs text-zinc-500">Chunks Criados</p>
                        <p className="text-sm font-semibold text-teal-400">{ingestResults.data.chunksCreated}</p>
                      </div>
                      <div>
                        <p className="text-xs text-zinc-500">Tokens Processados</p>
                        <p className="text-sm font-semibold text-cyan-400">{ingestResults.data.tokensProcessed}</p>
                      </div>
                      <div>
                        <p className="text-xs text-zinc-500">Tempo Total</p>
                        <p className="text-sm font-semibold text-zinc-300">{ingestResults.data.durationMs?.toFixed(1)} ms</p>
                      </div>
                    </div>
                  ) : (
                    <div className="space-y-3">
                      <div className="flex items-center justify-between text-sm text-zinc-300 bg-zinc-950 p-3 rounded-xl border border-zinc-800">
                        <span>Total de Arquivos: {ingestResults.data.total}</span>
                        <span className="text-teal-400">Sucesso: {ingestResults.data.succeeded}</span>
                        <span className="text-red-400">Falhas: {ingestResults.data.failed}</span>
                      </div>
                    </div>
                  )}
                </div>
              )}
            </div>

            {/* Ingestion Help / Sidebar */}
            <div className="space-y-6">
              <div className="bg-zinc-900/40 border border-zinc-800/80 rounded-2xl p-6 space-y-4">
                <h3 className="text-sm font-semibold text-zinc-300 uppercase tracking-wider">Pipeline de Ingestão MAF</h3>
                <p className="text-xs text-zinc-400 leading-relaxed">
                  Quando um arquivo é ingerido, o pipeline executa 4 fases rigorosas para garantir a qualidade do RAG:
                </p>
                <ul className="space-y-3 text-xs text-zinc-300">
                  <li className="flex items-start gap-2.5">
                    <span className="w-5 h-5 rounded-full bg-teal-500/10 border border-teal-500/30 flex items-center justify-center text-teal-400 font-bold flex-shrink-0 mt-0.5">1</span>
                    <div>
                      <strong className="text-white block">Parsing & Extrato Textual</strong>
                      Identificação de cabeçalhos, parágrafos e limpeza de caracteres de controle.
                    </div>
                  </li>
                  <li className="flex items-start gap-2.5">
                    <span className="w-5 h-5 rounded-full bg-cyan-500/10 border border-cyan-500/30 flex items-center justify-center text-cyan-400 font-bold flex-shrink-0 mt-0.5">2</span>
                    <div>
                      <strong className="text-white block">Semantic Chunking</strong>
                      Divisão inteligente do texto preservando a integridade das frases e com sobreposição de contexto.
                    </div>
                  </li>
                  <li className="flex items-start gap-2.5">
                    <span className="w-5 h-5 rounded-full bg-blue-500/10 border border-blue-500/30 flex items-center justify-center text-blue-400 font-bold flex-shrink-0 mt-0.5">3</span>
                    <div>
                      <strong className="text-white block">Geração Vetorial</strong>
                      Chamada in-process ou externa para gerar o vetor flutuante no espaço dimensional configurado.
                    </div>
                  </li>
                  <li className="flex items-start gap-2.5">
                    <span className="w-5 h-5 rounded-full bg-emerald-500/10 border border-emerald-500/30 flex items-center justify-center text-emerald-400 font-bold flex-shrink-0 mt-0.5">4</span>
                    <div>
                      <strong className="text-white block">Persistência pgvector</strong>
                      Armazenamento na tabela <code className="text-teal-300">vector_documents</code> com indexação para busca rápida por cosseno.
                    </div>
                  </li>
                </ul>
              </div>
            </div>
          </div>
        )}

        {/* TAB 2: CONFIGURAÇÃO & ONNX RUNTIME */}
        {activeTab === 'config' && (
          <div className="grid grid-cols-1 lg:grid-cols-3 gap-8">
            <div className="lg:col-span-2 space-y-6">
              <div className="bg-zinc-900/60 border border-zinc-800/80 backdrop-blur-md rounded-2xl p-6 shadow-xl space-y-6">
                <div className="flex items-center justify-between">
                  <h2 className="text-lg font-semibold text-white flex items-center gap-2">
                    <Cpu className="w-5 h-5 text-cyan-400" />
                    Configurações de Reranking & ONNX Runtime Local
                  </h2>
                  <button
                    onClick={() => setEditingRerank(!editingRerank)}
                    className="px-3 py-1.5 bg-zinc-800 hover:bg-zinc-700 text-zinc-200 text-xs font-medium rounded-lg transition-colors"
                  >
                    {editingRerank ? 'Cancelar Edição' : 'Editar Parâmetros'}
                  </button>
                </div>

                <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                  <div className="space-y-2">
                    <label className="text-xs font-medium text-zinc-400">Peso do Score Neural (ONNX Cross-Encoder)</label>
                    <div className="flex items-center gap-4">
                      <input
                        type="range"
                        min="0"
                        max="1"
                        step="0.05"
                        value={neuralWeight}
                        disabled={!editingRerank}
                        onChange={e => setNeuralWeight(parseFloat(e.target.value))}
                        className="w-full accent-teal-500 bg-zinc-950 h-2 rounded-lg cursor-pointer disabled:opacity-50"
                      />
                      <span className="text-sm font-bold text-teal-400 min-w-12 text-right">
                        {(neuralWeight * 100).toFixed(0)}%
                      </span>
                    </div>
                    <p className="text-xs text-zinc-600">Relevância dada ao reranker local em relação ao score bruto do vetor.</p>
                  </div>

                  <div className="space-y-2">
                    <label className="text-xs font-medium text-zinc-400">Peso do Score LLM / Semântico</label>
                    <div className="flex items-center gap-4">
                      <input
                        type="range"
                        min="0"
                        max="1"
                        step="0.05"
                        value={llmWeight}
                        disabled={!editingRerank}
                        onChange={e => setLlmWeight(parseFloat(e.target.value))}
                        className="w-full accent-cyan-500 bg-zinc-950 h-2 rounded-lg cursor-pointer disabled:opacity-50"
                      />
                      <span className="text-sm font-bold text-cyan-400 min-w-12 text-right">
                        {(llmWeight * 100).toFixed(0)}%
                      </span>
                    </div>
                    <p className="text-xs text-zinc-600">Relevância do ajuste fino contextual injetado.</p>
                  </div>

                  <div className="space-y-2 md:col-span-2">
                    <label className="text-xs font-medium text-zinc-400">Caminho do Modelo ONNX in-process (.onnx)</label>
                    <input
                      type="text"
                      value={onnxModelPath}
                      disabled={!editingRerank}
                      onChange={e => setOnnxModelPath(e.target.value)}
                      className="w-full bg-zinc-950 border border-zinc-800 rounded-xl px-4 py-2 text-sm text-zinc-200 focus:border-cyan-500 focus:outline-none disabled:opacity-60"
                    />
                    <p className="text-xs text-zinc-600">Modelo carregado na memória RAM do servidor para latência ultrabaixa (~12ms).</p>
                  </div>

                  <div className="space-y-2">
                    <label className="text-xs font-medium text-zinc-400">Max Output Tokens (RAG Context Size)</label>
                    <select
                      value={maxOutputTokens}
                      disabled={!editingRerank}
                      onChange={e => setMaxOutputTokens(Number(e.target.value))}
                      className="w-full bg-zinc-950 border border-zinc-800 rounded-xl px-4 py-2 text-sm text-zinc-200 focus:border-cyan-500 focus:outline-none disabled:opacity-60"
                    >
                      <option value={512}>512 tokens</option>
                      <option value={1024}>1024 tokens</option>
                      <option value={2048}>2048 tokens</option>
                      <option value={4096}>4096 tokens</option>
                    </select>
                  </div>
                </div>

                {editingRerank && (
                  <div className="flex justify-end pt-4 border-t border-zinc-800">
                    <button
                      onClick={handleSaveRerankConfig}
                      className="flex items-center gap-2 px-6 py-2.5 bg-gradient-to-r from-cyan-600 to-blue-600 hover:from-cyan-500 hover:to-blue-500 text-white font-medium rounded-xl text-sm transition-all shadow-md shadow-cyan-500/10"
                    >
                      <Check className="w-4 h-4" />
                      Salvar Parâmetros no Servidor
                    </button>
                  </div>
                )}
              </div>
            </div>

            {/* Sidebar Status da Memória */}
            <div className="space-y-6">
              <div className="bg-zinc-900/40 border border-zinc-800/80 rounded-2xl p-6 space-y-4">
                <h3 className="text-sm font-semibold text-zinc-300 uppercase tracking-wider">Status do ONNX Runtime</h3>
                <div className="space-y-3">
                  <div className="flex items-center justify-between py-2 border-b border-zinc-800">
                    <span className="text-xs text-zinc-400">Módulo Native ORT</span>
                    <Badge variant={stats?.onnxStatus?.loaded ? 'success' : 'danger'}>
                      {stats?.onnxStatus?.loaded ? 'Carregado' : 'Não Carregado'}
                    </Badge>
                  </div>
                  <div className="flex items-center justify-between py-2 border-b border-zinc-800">
                    <span className="text-xs text-zinc-400">Modelo Reranker</span>
                    <span className="text-xs font-semibold text-cyan-300">{stats?.onnxStatus?.modelName ?? 'bge-reranker-small'}</span>
                  </div>
                  <div className="flex items-center justify-between py-2 border-b border-zinc-800">
                    <span className="text-xs text-zinc-400">Aceleração de Hardware</span>
                    <Badge variant="default" className="bg-blue-500/10 text-blue-400 border border-blue-500/20">
                      {stats?.onnxStatus?.hardware ?? 'CPU / AVX2'}
                    </Badge>
                  </div>
                  <div className="flex items-center justify-between py-2">
                    <span className="text-xs text-zinc-400">Latência Média</span>
                    <span className="text-xs font-semibold text-teal-400">{stats?.onnxStatus?.avgLatencyMs ?? 12.4} ms</span>
                  </div>
                </div>
              </div>
            </div>
          </div>
        )}

        {/* TAB 3: MODELOS & MIGRAÇÃO VETORIAL */}
        {activeTab === 'migration' && (
          <div className="space-y-6">
            <div className="flex items-center justify-between">
              <div>
                <h2 className="text-lg font-semibold text-white">Modelos de Embedding Configurados</h2>
                <p className="text-xs text-zinc-400">Gerencie modelos ativos e inicie migrações assíncronas entre espaços vetoriais.</p>
              </div>
              <button
                onClick={() => setShowNewModelForm(!showNewModelForm)}
                className="flex items-center gap-2 px-4 py-2 bg-teal-600 hover:bg-teal-500 text-white rounded-xl text-sm font-medium transition-all shadow-md shadow-teal-500/10"
              >
                <Plus className="w-4 h-4" />
                Adicionar Modelo
              </button>
            </div>

            {/* Form de Criação de Modelo */}
            {showNewModelForm && (
              <form onSubmit={handleCreateModel} className="bg-zinc-900/80 border border-teal-500/30 backdrop-blur-md rounded-2xl p-6 space-y-6 shadow-2xl">
                <div className="flex items-center justify-between border-b border-zinc-800 pb-3">
                  <h3 className="text-sm font-semibold text-teal-300">Cadastrar Novo Modelo de Embedding</h3>
                  <span className="text-xs text-zinc-500">Selecione um modelo da lista ou digite um nome customizado</span>
                </div>

                <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
                  <div>
                    <label className="block text-xs font-medium text-zinc-400 mb-1">Provedor</label>
                    <select
                      value={newModelProvider}
                      onChange={e => handleProviderChange(e.target.value)}
                      className="w-full bg-zinc-950 border border-zinc-800 rounded-xl px-4 py-2 text-sm text-zinc-200 focus:border-teal-500 focus:outline-none focus:ring-1 focus:ring-teal-500"
                    >
                      <option value="OpenAI">OpenAI</option>
                      <option value="Ollama">Ollama (Local)</option>
                      <option value="ONNX">ONNX Runtime (Local)</option>
                    </select>
                  </div>
                  <div className="md:col-span-2">
                    <label className="block text-xs font-medium text-zinc-400 mb-1">Nome do Modelo (Busca / Digitação Livre)</label>
                    <input
                      type="text"
                      required
                      placeholder="ex: text-embedding-3-small"
                      value={newModelName}
                      onChange={e => setNewModelName(e.target.value)}
                      className="w-full bg-zinc-950 border border-zinc-800 rounded-xl px-4 py-2 text-sm text-zinc-200 focus:border-teal-500 focus:outline-none focus:ring-1 focus:ring-teal-500 font-mono"
                    />
                  </div>
                  <div>
                    <label className="block text-xs font-medium text-zinc-400 mb-1">Dimensões do Vetor</label>
                    <select
                      value={newModelDim}
                      onChange={e => setNewModelDim(Number(e.target.value))}
                      className="w-full bg-zinc-950 border border-zinc-800 rounded-xl px-4 py-2 text-sm text-zinc-200 focus:border-teal-500 focus:outline-none focus:ring-1 focus:ring-teal-500"
                    >
                      <option value={384}>384 (all-MiniLM / bge-small)</option>
                      <option value={768}>768 (nomic-embed-text)</option>
                      <option value={1024}>1024 (bge-large / mxbai)</option>
                      <option value={1536}>1536 (text-embedding-3)</option>
                      <option value={3072}>3072 (text-embedding-3-large)</option>
                    </select>
                  </div>
                </div>

                {/* Lista de Modelos Disponíveis / Sugestões Rápidas */}
                <div className="space-y-3 bg-zinc-950/50 p-4 rounded-xl border border-zinc-800/80">
                  <label className="block text-xs font-semibold uppercase tracking-wider text-zinc-400">
                    Modelos Disponíveis para {newModelProvider} (Clique para selecionar)
                  </label>
                  <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 gap-2.5">
                    {PRESET_MODELS[newModelProvider]?.map(preset => {
                      const isSelected = newModelName === preset.name
                      return (
                        <button
                          key={preset.name}
                          type="button"
                          onClick={() => handleSelectPreset(preset.name, preset.dim)}
                          className={`flex flex-col items-start p-3 rounded-xl border text-left transition-all ${
                            isSelected
                              ? 'bg-teal-500/10 border-teal-500/50 text-white shadow-md shadow-teal-500/10 scale-[1.01]'
                              : 'bg-zinc-900 border-zinc-800 hover:border-zinc-700 text-zinc-300 hover:text-white'
                          }`}
                        >
                          <div className="flex items-center justify-between w-full mb-1">
                            <span className="text-xs font-bold font-mono truncate">{preset.name}</span>
                            <Badge variant={isSelected ? 'success' : 'default'} className="text-[10px] px-1.5 py-0.5">
                              {preset.dim}d
                            </Badge>
                          </div>
                          <span className="text-[10px] text-zinc-500 line-clamp-1">{preset.desc}</span>
                        </button>
                      )
                    })}
                  </div>
                </div>

                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  <div>
                    <label className="block text-xs font-medium text-zinc-400 mb-1">Base URL (Opcional - Custom Endpoint)</label>
                    <input
                      type="text"
                      placeholder={newModelProvider === 'Ollama' ? 'http://localhost:11434' : 'https://api.openai.com/v1'}
                      value={newModelBaseUrl}
                      onChange={e => setNewModelBaseUrl(e.target.value)}
                      className="w-full bg-zinc-950 border border-zinc-800 rounded-xl px-4 py-2 text-sm text-zinc-200 focus:border-teal-500 focus:outline-none focus:ring-1 focus:ring-teal-500"
                    />
                  </div>
                  <div>
                    <label className="block text-xs font-medium text-zinc-400 mb-1">API Key (Opcional)</label>
                    <input
                      type="password"
                      placeholder="sk-..."
                      value={newModelApiKey}
                      onChange={e => setNewModelApiKey(e.target.value)}
                      className="w-full bg-zinc-950 border border-zinc-800 rounded-xl px-4 py-2 text-sm text-zinc-200 focus:border-teal-500 focus:outline-none focus:ring-1 focus:ring-teal-500"
                    />
                  </div>
                </div>

                <div className="flex justify-end gap-2 pt-2 border-t border-zinc-800/80">
                  <button
                    type="button"
                    onClick={() => setShowNewModelForm(false)}
                    className="px-4 py-2 bg-zinc-800 hover:bg-zinc-700 text-zinc-300 rounded-xl text-sm transition-colors"
                  >
                    Cancelar
                  </button>
                  <button
                    type="submit"
                    className="px-6 py-2 bg-gradient-to-r from-teal-600 to-cyan-600 hover:from-teal-500 hover:to-cyan-500 text-white rounded-xl text-sm font-medium shadow-md shadow-teal-500/10 transition-all"
                  >
                    Salvar Modelo
                  </button>
                </div>
              </form>
            )}

            {/* Tabela de Modelos */}
            <div className="bg-zinc-900/60 border border-zinc-800/80 backdrop-blur-md rounded-2xl overflow-hidden shadow-xl">
              <table className="w-full text-left border-collapse">
                <thead>
                  <tr className="border-b border-zinc-800 bg-zinc-900/40 text-xs font-semibold text-zinc-400 uppercase tracking-wider">
                    <th className="py-3 px-6">Modelo & Provedor</th>
                    <th className="py-3 px-6">Dimensões</th>
                    <th className="py-3 px-6">Status</th>
                    <th className="py-3 px-6 text-right">Ações de Migração</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-zinc-800/60 text-sm">
                  {models.map(m => {
                    const isActive = activeModel?.id === m.id
                    return (
                      <tr key={m.id} className="hover:bg-zinc-800/30 transition-colors">
                        <td className="py-4 px-6">
                          <div className="font-semibold text-white flex items-center gap-2">
                            {m.name}
                            {m.provider === 'ONNX' && <Badge variant="success">Local</Badge>}
                          </div>
                          <div className="text-xs text-zinc-500">{m.provider} — {m.modelName}</div>
                        </td>
                        <td className="py-4 px-6 text-zinc-300">
                          <code className="text-xs bg-zinc-950 px-2 py-1 rounded border border-zinc-800 text-teal-400">
                            {m.dimensions}d
                          </code>
                        </td>
                        <td className="py-4 px-6">
                          {isActive ? (
                            <Badge variant="success" className="bg-teal-500/10 text-teal-400 border border-teal-500/20">
                              Ativo Principal
                            </Badge>
                          ) : (
                            <Badge variant="default" className="text-zinc-500">
                              Inativo
                            </Badge>
                          )}
                        </td>
                        <td className="py-4 px-6 text-right space-x-2">
                          {!isActive && (
                            <>
                              <button
                                onClick={() => activateModel(m.id)}
                                className="px-3 py-1.5 bg-zinc-800 hover:bg-zinc-700 text-zinc-200 text-xs rounded-lg transition-colors"
                              >
                                Ativar
                              </button>
                              <button
                                onClick={() => handleStartMigration(m.id)}
                                className="px-3 py-1.5 bg-cyan-600 hover:bg-cyan-500 text-white text-xs font-medium rounded-lg transition-colors"
                              >
                                Migrar Base
                              </button>
                              <button
                                onClick={() => deleteModel(m.id)}
                                className="p-1.5 bg-red-500/10 hover:bg-red-500/20 text-red-400 rounded-lg transition-colors"
                              >
                                <Trash2 className="w-4 h-4" />
                              </button>
                            </>
                          )}
                        </td>
                      </tr>
                    )
                  })}

                  {models.length === 0 && (
                    <tr>
                      <td colSpan={4} className="py-8 text-center text-zinc-500 text-sm">
                        Nenhum modelo de embedding configurado. Utilize os botões acima para cadastrar.
                      </td>
                    </tr>
                  )}
                </tbody>
              </table>
            </div>

            {/* Jobs de Migração Ativos */}
            {jobs.length > 0 && (
              <div className="space-y-4 pt-6 border-t border-zinc-800">
                <h3 className="text-md font-semibold text-white">Tarefas de Migração em Andamento</h3>
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  {jobs.map(job => (
                    <div key={job.id} className="bg-zinc-900/60 border border-zinc-800 rounded-2xl p-5 space-y-3">
                      <div className="flex items-center justify-between">
                        <span className="text-xs font-mono text-zinc-500">Job #{job.id.substring(0, 8)}</span>
                        <Badge variant={job.status === 'Processing' ? 'warning' : 'success'}>
                          {job.status}
                        </Badge>
                      </div>
                      <div className="space-y-1">
                        <div className="flex justify-between text-xs text-zinc-300">
                          <span>Progresso da Migração</span>
                          <span className="font-bold text-teal-400">
                            {job.totalDocuments > 0 ? ((job.processedDocuments / job.totalDocuments) * 100).toFixed(0) : 0}%
                          </span>
                        </div>
                        <div className="w-full bg-zinc-950 h-2 rounded-full overflow-hidden border border-zinc-800">
                          <div
                            className="bg-gradient-to-r from-teal-500 to-cyan-500 h-full transition-all duration-500"
                            style={{ width: `${job.totalDocuments > 0 ? (job.processedDocuments / job.totalDocuments) * 100 : 0}%` }}
                          />
                        </div>
                      </div>
                      <div className="flex justify-between text-xs text-zinc-500">
                        <span>Processados: {job.processedDocuments} / {job.totalDocuments}</span>
                        <span>Falhas: {job.failedDocuments}</span>
                      </div>
                    </div>
                  ))}
                </div>
              </div>
            )}

          </div>
        )}

      </div>
    </div>
  )
}

function MetricCard({
  icon: Icon,
  title,
  value,
  subtitle,
  badge,
}: {
  icon: React.ElementType
  title: string
  value: string
  subtitle: string
  badge?: string
}) {
  return (
    <div className="bg-zinc-900/60 border border-zinc-800/80 backdrop-blur-md rounded-2xl p-5 space-y-2 shadow-lg hover:border-zinc-700 transition-all">
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-2">
          <Icon className="w-4 h-4 text-teal-400" />
          <span className="text-xs font-medium text-zinc-400">{title}</span>
        </div>
        {badge && (
          <Badge variant="default" className="text-[10px] bg-zinc-800 text-zinc-300 border border-zinc-700">
            {badge}
          </Badge>
        )}
      </div>
      <p className="text-2xl font-bold tracking-tight text-white">{value}</p>
      <p className="text-xs text-zinc-500">{subtitle}</p>
    </div>
  )
}

function TabButton({
  active,
  onClick,
  icon: Icon,
  label,
}: {
  active: boolean
  onClick: () => void
  icon: React.ElementType
  label: string
}) {
  return (
    <button
      onClick={onClick}
      className={`flex items-center gap-2 px-5 py-3 border-b-2 text-sm font-medium transition-all ${active
          ? 'border-teal-500 text-teal-400 bg-teal-500/5'
          : 'border-transparent text-zinc-400 hover:text-zinc-200 hover:bg-zinc-900/40'
        }`}
    >
      <Icon className="w-4 h-4" />
      {label}
    </button>
  )
}
