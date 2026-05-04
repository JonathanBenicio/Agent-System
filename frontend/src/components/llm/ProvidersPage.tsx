import { useState } from 'react'
import { Cpu, TestTube, RefreshCw, Check, X, Star } from 'lucide-react'
import { useLLMProviders } from '@/hooks/useLLMProviders'
import { PageLoading, PageError } from '@/components/shared/Loading'
import { Badge } from '@/components/shared/Badge'
import { useToast } from '@/components/shared/Toast'
import { cn } from '@/lib/utils'
import type { LLMProviderInfo } from '@/types/api'

export function ProvidersPage() {
  const { providers, loading, error, refresh, updateProvider, testProvider } = useLLMProviders()
  const { addToast } = useToast()
  const [testing, setTesting] = useState<string | null>(null)
  const [editing, setEditing] = useState<string | null>(null)
  const [editForm, setEditForm] = useState({ apiKey: '', isEnabled: false, priority: 0 })

  if (loading) return <PageLoading />
  if (error) return <PageError message={error} onRetry={refresh} />

  const handleTest = async (name: string) => {
    setTesting(name)
    try {
      const result = await testProvider(name)
      addToast(
        result.available ? `${name}: disponível ✓` : `${name}: indisponível`,
        result.available ? 'success' : 'error'
      )
    } catch {
      addToast(`Erro ao testar ${name}`, 'error')
    }
    setTesting(null)
  }

  const handleEdit = (p: LLMProviderInfo) => {
    setEditing(p.name)
    setEditForm({ apiKey: '', isEnabled: p.isEnabled, priority: p.priority })
  }

  const handleSave = async (name: string) => {
    try {
      await updateProvider(name, {
        apiKey: editForm.apiKey || undefined,
        isEnabled: editForm.isEnabled,
        priority: editForm.priority,
      })
      addToast('Provider atualizado', 'success')
      setEditing(null)
    } catch {
      addToast('Erro ao atualizar provider', 'error')
    }
  }

  return (
    <div className="h-full overflow-y-auto">
      <div className="max-w-5xl mx-auto px-6 py-6 space-y-6">
        <div className="flex items-center justify-between">
          <h1 className="text-xl font-semibold text-zinc-100">LLM Providers ({providers.length})</h1>
          <button
            onClick={refresh}
            className="flex items-center gap-2 px-3 py-2 text-sm rounded-lg border border-zinc-700 text-zinc-300 hover:bg-zinc-800"
          >
            <RefreshCw className="w-4 h-4" />
            Atualizar
          </button>
        </div>

        <div className="space-y-4">
          {providers.map(p => (
            <div key={p.name} className="bg-zinc-900 border border-zinc-800 rounded-xl p-5 hover:border-zinc-700 transition-colors">
              <div className="flex items-start justify-between">
                <div className="flex items-start gap-3">
                  <div className={cn(
                    'flex items-center justify-center w-10 h-10 rounded-lg',
                    p.isEnabled ? 'bg-violet-900/50' : 'bg-zinc-800'
                  )}>
                    <Cpu className={cn('w-5 h-5', p.isEnabled ? 'text-violet-400' : 'text-zinc-500')} />
                  </div>
                  <div>
                    <div className="flex items-center gap-2">
                      <h3 className="text-sm font-semibold text-zinc-100">{p.name}</h3>
                      {p.isDefault && (
                        <Badge variant="warning">
                          <Star className="w-3 h-3 mr-1" />
                          Default
                        </Badge>
                      )}
                    </div>
                    <p className="text-xs text-zinc-400 mt-0.5">
                      {p.models?.length ?? 0} modelos disponíveis
                    </p>
                    <div className="flex gap-2 mt-2">
                      <Badge variant={p.isEnabled ? 'success' : 'default'}>
                        {p.isEnabled ? 'Ativado' : 'Desativado'}
                      </Badge>
                      <Badge variant={p.isAvailable ? 'success' : 'danger'}>
                        {p.isAvailable ? 'Disponível' : 'Indisponível'}
                      </Badge>
                      <Badge>Prioridade: {p.priority}</Badge>
                    </div>
                  </div>
                </div>
                <div className="flex gap-2">
                  <button
                    onClick={() => handleTest(p.name)}
                    disabled={testing === p.name}
                    className="flex items-center gap-1.5 px-3 py-1.5 text-xs rounded-lg border border-zinc-700 text-zinc-300 hover:bg-zinc-800 disabled:opacity-50"
                  >
                    <TestTube className="w-3.5 h-3.5" />
                    {testing === p.name ? 'Testando...' : 'Testar'}
                  </button>
                  <button
                    onClick={() => editing === p.name ? setEditing(null) : handleEdit(p)}
                    className="px-3 py-1.5 text-xs rounded-lg border border-zinc-700 text-zinc-300 hover:bg-zinc-800"
                  >
                    {editing === p.name ? 'Cancelar' : 'Editar'}
                  </button>
                </div>
              </div>

              {/* Models */}
              {p.models && p.models.length > 0 && (
                <div className="mt-3 pt-3 border-t border-zinc-800">
                  <p className="text-xs text-zinc-500 mb-1.5">Modelos</p>
                  <div className="flex flex-wrap gap-1">
                    {p.models.map(m => <Badge key={m}>{m}</Badge>)}
                  </div>
                </div>
              )}

              {/* Edit form */}
              {editing === p.name && (
                <div className="mt-4 pt-4 border-t border-zinc-800 space-y-3">
                  <label className="block">
                    <span className="text-xs text-zinc-400">API Key (deixe vazio para manter)</span>
                    <input
                      type="password"
                      value={editForm.apiKey}
                      onChange={e => setEditForm(prev => ({ ...prev, apiKey: e.target.value }))}
                      className="input mt-1"
                      placeholder="••••••••"
                    />
                  </label>
                  <div className="flex gap-4">
                    <label className="block flex-1">
                      <span className="text-xs text-zinc-400">Prioridade</span>
                      <input
                        type="number"
                        min={0}
                        max={100}
                        value={editForm.priority}
                        onChange={e => setEditForm(prev => ({ ...prev, priority: Number(e.target.value) }))}
                        className="input mt-1"
                      />
                    </label>
                    <label className="flex items-center gap-2 cursor-pointer pt-4">
                      <input
                        type="checkbox"
                        checked={editForm.isEnabled}
                        onChange={e => setEditForm(prev => ({ ...prev, isEnabled: e.target.checked }))}
                        className="rounded border-zinc-700 bg-zinc-800 text-violet-600"
                      />
                      <span className="text-sm text-zinc-300">Ativado</span>
                    </label>
                  </div>
                  <div className="flex justify-end gap-2 pt-2">
                    <button
                      onClick={() => setEditing(null)}
                      className="flex items-center gap-1 px-3 py-1.5 text-xs rounded-lg border border-zinc-700 text-zinc-300 hover:bg-zinc-800"
                    >
                      <X className="w-3 h-3" />
                      Cancelar
                    </button>
                    <button
                      onClick={() => handleSave(p.name)}
                      className="flex items-center gap-1 px-3 py-1.5 text-xs rounded-lg bg-violet-600 text-white hover:bg-violet-500"
                    >
                      <Check className="w-3 h-3" />
                      Salvar
                    </button>
                  </div>
                </div>
              )}
            </div>
          ))}
        </div>
      </div>
    </div>
  )
}
