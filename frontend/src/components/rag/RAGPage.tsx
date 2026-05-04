import { Database, Search, FileText } from 'lucide-react'
import { Badge } from '@/components/shared/Badge'

export function RAGPage() {
  return (
    <div className="h-full overflow-y-auto">
      <div className="max-w-7xl mx-auto px-6 py-6 space-y-6">
        <h1 className="text-xl font-semibold text-zinc-100">RAG — Knowledge Base</h1>

        <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
          <InfoCard
            icon={Database}
            title="Documentos"
            value="—"
            subtitle="Total indexados"
          />
          <InfoCard
            icon={Search}
            title="Buscas"
            value="—"
            subtitle="Últimas 24h"
          />
          <InfoCard
            icon={FileText}
            title="Chunks"
            value="—"
            subtitle="Total processados"
          />
        </div>

        <div className="bg-zinc-900 border border-zinc-800 rounded-xl p-8 text-center">
          <Database className="w-12 h-12 mx-auto text-zinc-600 mb-4" />
          <h2 className="text-lg font-semibold text-zinc-300 mb-2">RAG em Desenvolvimento</h2>
          <p className="text-sm text-zinc-500 max-w-md mx-auto">
            O módulo de Retrieval-Augmented Generation permite indexar documentos,
            bases de conhecimento e contexto para enriquecer as respostas dos agents.
          </p>
          <div className="flex justify-center gap-2 mt-4">
            <Badge variant="warning">Em breve</Badge>
            <Badge>Embeddings</Badge>
            <Badge>Vector Search</Badge>
          </div>
        </div>

        <div className="bg-zinc-900 border border-zinc-800 rounded-xl p-5">
          <h3 className="text-sm font-semibold text-zinc-200 mb-3">Funcionalidades Planejadas</h3>
          <ul className="space-y-2 text-sm text-zinc-400">
            <Feature label="Upload de documentos" status="planned" />
            <Feature label="Chunking automático" status="planned" />
            <Feature label="Embedding com OpenAI / local" status="planned" />
            <Feature label="Vector search (pgvector)" status="planned" />
            <Feature label="Context injection nos agents" status="planned" />
            <Feature label="Métricas de retrieval quality" status="planned" />
          </ul>
        </div>
      </div>
    </div>
  )
}

function InfoCard({
  icon: Icon,
  title,
  value,
  subtitle,
}: {
  icon: React.ElementType
  title: string
  value: string
  subtitle: string
}) {
  return (
    <div className="bg-zinc-900 border border-zinc-800 rounded-xl p-4">
      <div className="flex items-center gap-2 mb-2">
        <Icon className="w-4 h-4 text-zinc-500" />
        <p className="text-xs text-zinc-500">{title}</p>
      </div>
      <p className="text-2xl font-semibold text-zinc-300">{value}</p>
      <p className="text-xs text-zinc-600">{subtitle}</p>
    </div>
  )
}

function Feature({ label, status }: { label: string; status: 'done' | 'wip' | 'planned' }) {
  const icons = {
    done: '✅',
    wip: '🔄',
    planned: '📋',
  }
  return (
    <li className="flex items-center gap-2">
      <span>{icons[status]}</span>
      <span>{label}</span>
    </li>
  )
}
