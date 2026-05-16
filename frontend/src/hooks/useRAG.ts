import { useState, useEffect, useCallback } from 'react'
import { ragApi, embeddingMigrationApi, settingsApi } from '@/lib/api'
import type {
  RagStats,
  EmbeddingModelConfig,
  StartMigrationRequest,
  MigrationJob,
  MemorySettings,
  RerankingSettings,
} from '@/types/api'

export function useRAG() {
  const [loading, setLoading] = useState(true)
  const [ingesting, setIngesting] = useState(false)
  const [error, setError] = useState<string | null>(null)
  
  const [stats, setStats] = useState<RagStats | null>(null)
  const [models, setModels] = useState<EmbeddingModelConfig[]>([])
  const [activeModel, setActiveModel] = useState<EmbeddingModelConfig | null>(null)
  const [jobs, setJobs] = useState<MigrationJob[]>([])
  
  const [memorySettings, setMemorySettings] = useState<MemorySettings | null>(null)
  const [rerankingSettings, setRerankingSettings] = useState<RerankingSettings | null>(null)

  const refresh = useCallback(async () => {
    try {
      setLoading(true)
      setError(null)
      
      const [statsData, modelsData, activeModelData, jobsData, memData, rerankData] = await Promise.all([
        ragApi.stats().catch(() => null),
        embeddingMigrationApi.models().catch(() => []),
        embeddingMigrationApi.activeModel().catch(() => null),
        embeddingMigrationApi.jobs().catch(() => []),
        settingsApi.getMemory().catch(() => null),
        settingsApi.getReranking().catch(() => null),
      ])

      setStats(statsData)
      setModels(modelsData)
      setActiveModel(activeModelData)
      setJobs(jobsData)
      setMemorySettings(memData)
      setRerankingSettings(rerankData)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erro ao carregar dados do RAG')
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    const timer = setTimeout(() => {
      void refresh()
    }, 0)
    return () => clearTimeout(timer)
  }, [refresh])

  const ingestDocument = useCallback(async (file: File, source?: string) => {
    try {
      setIngesting(true)
      setError(null)
      const result = await ragApi.ingest(file, source)
      return result
    } catch (err) {
      const msg = err instanceof Error ? err.message : 'Falha na ingestão do documento'
      setError(msg)
      throw new Error(msg, { cause: err })
    } finally {
      setIngesting(false)
    }
  }, [])

  const ingestBatch = useCallback(async (files: FileList | File[], source?: string) => {
    try {
      setIngesting(true)
      setError(null)
      const result = await ragApi.ingestBatch(files, source)
      return result
    } catch (err) {
      const msg = err instanceof Error ? err.message : 'Falha na ingestão em lote'
      setError(msg)
      throw new Error(msg, { cause: err })
    } finally {
      setIngesting(false)
    }
  }, [])

  const saveModelConfig = useCallback(async (config: Partial<EmbeddingModelConfig>) => {
    try {
      setError(null)
      const saved = await embeddingMigrationApi.saveModel(config)
      await refresh()
      return saved
    } catch (err) {
      const msg = err instanceof Error ? err.message : 'Erro ao salvar configuração do modelo'
      setError(msg)
      throw new Error(msg, { cause: err })
    }
  }, [refresh])

  const activateModel = useCallback(async (id: string) => {
    try {
      setError(null)
      await embeddingMigrationApi.activateModel(id)
      await refresh()
    } catch (err) {
      const msg = err instanceof Error ? err.message : 'Erro ao ativar modelo'
      setError(msg)
      throw new Error(msg, { cause: err })
    }
  }, [refresh])

  const deleteModel = useCallback(async (id: string) => {
    try {
      setError(null)
      await embeddingMigrationApi.deleteModel(id)
      await refresh()
    } catch (err) {
      const msg = err instanceof Error ? err.message : 'Erro ao remover modelo'
      setError(msg)
      throw new Error(msg, { cause: err })
    }
  }, [refresh])

  const startMigrationJob = useCallback(async (req: StartMigrationRequest) => {
    try {
      setError(null)
      const job = await embeddingMigrationApi.startJob(req)
      await refresh()
      return job
    } catch (err) {
      const msg = err instanceof Error ? err.message : 'Erro ao iniciar migração de embeddings'
      setError(msg)
      throw new Error(msg, { cause: err })
    }
  }, [refresh])

  const updateMemory = useCallback(async (settings: MemorySettings) => {
    try {
      setError(null)
      const updated = await settingsApi.updateMemory(settings)
      setMemorySettings(updated)
      return updated
    } catch (err) {
      const msg = err instanceof Error ? err.message : 'Erro ao atualizar configurações de memória'
      setError(msg)
      throw new Error(msg, { cause: err })
    }
  }, [])

  const updateReranking = useCallback(async (settings: RerankingSettings) => {
    try {
      setError(null)
      const updated = await settingsApi.updateReranking(settings)
      setRerankingSettings(updated)
      return updated
    } catch (err) {
      const msg = err instanceof Error ? err.message : 'Erro ao atualizar configurações de reranking'
      setError(msg)
      throw new Error(msg, { cause: err })
    }
  }, [])

  return {
    loading,
    ingesting,
    error,
    stats,
    models,
    activeModel,
    jobs,
    memorySettings,
    rerankingSettings,
    refresh,
    ingestDocument,
    ingestBatch,
    saveModelConfig,
    activateModel,
    deleteModel,
    startMigrationJob,
    updateMemory,
    updateReranking,
  }
}
