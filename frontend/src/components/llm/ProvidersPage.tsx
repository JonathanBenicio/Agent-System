import { useEffect, useMemo, useState, type ChangeEvent } from 'react'
import { BrainCircuit, Cpu, TestTube, RefreshCw, Check, X, Star, WandSparkles } from 'lucide-react'
import { useLLMProviders } from '@/hooks/useLLMProviders'
import { PageLoading, PageError } from '@/components/shared/Loading'
import { Badge } from '@/components/shared/Badge'
import { useToast } from '@/components/shared/Toast'
import { cn } from '@/lib/utils'
import type { LLMProviderInfo } from '@/types/api'

export function ProvidersPage() {
  const {
    providers,
    defaultProvider,
    defaultModel,
    loading,
    error,
    refresh,
    updateProvider,
    updateDefaultSelection,
    testProvider,
    discoverModels,
  } = useLLMProviders()
  const { addToast } = useToast()
  const [testing, setTesting] = useState<string | null>(null)
  const [discovering, setDiscovering] = useState<string | null>(null)
  const [editing, setEditing] = useState<string | null>(null)
  const [editForm, setEditForm] = useState({ apiKey: '', defaultModel: '', isEnabled: false, priority: 0 })
  const [defaultSelection, setDefaultSelection] = useState({ providerName: '', model: '' })

  useEffect(() => {
    setDefaultSelection({ providerName: defaultProvider, model: defaultModel })
  }, [defaultModel, defaultProvider])

  const selectedDefaultProvider = useMemo(
    () => providers.find((provider: LLMProviderInfo) => provider.name === defaultSelection.providerName) ?? providers[0],
    [defaultSelection.providerName, providers],
  )

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

  const handleDiscoverModels = async (p: LLMProviderInfo) => {
    if (!editForm.apiKey) {
      addToast('Insira a nova API Key para buscar modelos', 'info')
      return
    }
    setDiscovering(p.name)
    try {
      const res = await discoverModels(p.name, editForm.apiKey)
      if (res.success && res.discoveredModels.length > 0) {
        addToast(`${res.discoveredModels.length} modelos localizados!`, 'success')
        await updateProvider(p.name, {
          apiKey: editForm.apiKey,
          discoveredModels: res.discoveredModels,
        })
        window.dispatchEvent(new Event('agentic:llm-config-updated'))
      } else {
        addToast(res.errorMessage ?? 'Nenhum modelo novo encontrado', 'info')
      }
    } catch (err) {
      addToast(err instanceof Error ? err.message : 'Erro ao descobrir modelos', 'error')
    } finally {
      setDiscovering(null)
    }
  }

  const handleEdit = (p: LLMProviderInfo) => {
    setEditing(p.name)
    setEditForm({ apiKey: '', defaultModel: p.defaultModel, isEnabled: p.isEnabled, priority: p.priority })
  }

  const handleSave = async (name: string) => {
    try {
      await updateProvider(name, {
        apiKey: editForm.apiKey || undefined,
        defaultModel: editForm.defaultModel || undefined,
        enabled: editForm.isEnabled,
        priority: editForm.priority,
      })
      window.dispatchEvent(new Event('agentic:llm-config-updated'))
      addToast('Provider atualizado', 'success')
      setEditing(null)
    } catch {
      addToast('Erro ao atualizar provider', 'error')
    }
  }

  const handleSaveDefaultSelection = async () => {
    if (!defaultSelection.providerName) {
      addToast('Selecione um provider default antes de salvar', 'info')
      return
    }

    try {
      await updateDefaultSelection(defaultSelection)
      window.dispatchEvent(new Event('agentic:llm-config-updated'))
      addToast('IA default do chat atualizada', 'success')
    } catch {
      addToast('Erro ao atualizar IA default do chat', 'error')
    }
  }

  return (
    <div className="h-full overflow-y-auto">
      <div className="max-w-5xl mx-auto px-6 py-6 space-y-6">
        <div className="flex items-center justify-between">
          <div>
            <h1 className="text-xl font-semibold text-zinc-100">Configuração de IAs ({providers.length})</h1>
            <p className="mt-1 text-sm text-zinc-400">
              Gerencie quais IAs ficam disponíveis e qual combinação provider + modelo abre pré-selecionada no chat.
            </p>
          </div>
          <button
            onClick={refresh}
            className="flex items-center gap-2 px-3 py-2 text-sm rounded-lg border border-zinc-700 text-zinc-300 hover:bg-zinc-800"
          >
            <RefreshCw className="w-4 h-4" />
            Atualizar
          </button>
        </div>

        <div className="rounded-2xl border border-zinc-800 bg-[radial-gradient(circle_at_top_left,_rgba(59,130,246,0.18),_transparent_30%),radial-gradient(circle_at_bottom_right,_rgba(168,85,247,0.16),_transparent_34%),#09090b] p-5">
          <div className="flex flex-col gap-4 lg:flex-row lg:items-end lg:justify-between">
            <div className="max-w-2xl">
              <div className="flex items-center gap-2">
                <div className="flex h-10 w-10 items-center justify-center rounded-2xl border border-sky-900/70 bg-sky-950/60 text-sky-300">
                  <BrainCircuit className="h-5 w-5" />
                </div>
                <div>
                  <h2 className="text-sm font-semibold text-zinc-100">IA padrão do chat</h2>
                  <p className="text-xs text-zinc-400">Define a seleção inicial exibida para qualquer conversa nova.</p>
                </div>
              </div>
              <div className="mt-4 grid gap-3 md:grid-cols-2">
                <label className="block">
                  <span className="text-xs text-zinc-400">Provider default</span>
                  <select
                    value={defaultSelection.providerName}
                    onChange={(event: ChangeEvent<HTMLSelectElement>) => {
                      const provider = providers.find((item: LLMProviderInfo) => item.name === event.target.value)
                      setDefaultSelection({
                        providerName: event.target.value,
                        model: provider?.defaultModel ?? provider?.models[0] ?? '',
                      })
                    }}
                    className="input mt-1"
                  >
                    {providers.filter((provider: LLMProviderInfo) => provider.isEnabled).map((provider: LLMProviderInfo) => (
                      <option key={provider.name} value={provider.name}>{provider.name}</option>
                    ))}
                  </select>
                </label>
                <label className="block">
                  <span className="text-xs text-zinc-400">Modelo default</span>
                  <select
                    value={defaultSelection.model}
                    onChange={(event: ChangeEvent<HTMLSelectElement>) => setDefaultSelection(prev => ({ ...prev, model: event.target.value }))}
                    className="input mt-1"
                  >
                    {(selectedDefaultProvider?.models ?? []).map((model: string) => (
                      <option key={model} value={model}>{model}</option>
                    ))}
                  </select>
                </label>
              </div>
            </div>

            <div className="flex flex-wrap items-center gap-2">
              {defaultProvider ? <Badge variant="success">Provider atual: {defaultProvider}</Badge> : null}
              {defaultModel ? <Badge variant="teal">Modelo atual: {defaultModel}</Badge> : null}
              <button
                onClick={handleSaveDefaultSelection}
                disabled={!defaultSelection.providerName}
                className="inline-flex items-center gap-2 rounded-xl bg-sky-600 px-4 py-2 text-sm font-medium text-white transition hover:bg-sky-500"
              >
                <WandSparkles className="h-4 w-4" />
                Salvar IA padrão
              </button>
            </div>
          </div>
        </div>

        <div className="space-y-4">
          {providers.map(p => (
            <div key={p.name} className="bg-zinc-900 border border-zinc-800 rounded-xl p-5 hover:border-zinc-700 transition-colors">
              <div className="flex items-start justify-between">
                <div className="flex items-start gap-3">
                  <div className={cn(
                    'flex items-center justify-center w-10 h-10 rounded-lg',
                    p.isEnabled ? 'bg-teal-900/50' : 'bg-zinc-800'
                  )}>
                    <Cpu className={cn('w-5 h-5', p.isEnabled ? 'text-teal-400' : 'text-zinc-500')} />
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
                  <div className="grid gap-4 md:grid-cols-[1fr_auto] items-end">
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
                    <button
                      type="button"
                      onClick={() => handleDiscoverModels(p)}
                      disabled={discovering === p.name || !editForm.apiKey}
                      className="inline-flex items-center gap-2 h-10 px-4 rounded-xl bg-sky-600/20 border border-sky-500/30 text-sky-300 hover:bg-sky-600/30 transition text-xs font-medium disabled:opacity-50"
                    >
                      <WandSparkles className="w-3.5 h-3.5" />
                      {discovering === p.name ? 'Buscando...' : 'Buscar Modelos'}
                    </button>
                  </div>
                  <div className="grid gap-4 md:grid-cols-[minmax(0,1fr)_minmax(0,140px)_auto]">
                    <label className="block flex-1">
                      <span className="text-xs text-zinc-400">Modelo padrão do provider</span>
                      <select
                        value={editForm.defaultModel}
                        onChange={e => setEditForm(prev => ({ ...prev, defaultModel: e.target.value }))}
                        className="input mt-1"
                      >
                        {p.models.map(model => (
                          <option key={model} value={model}>{model}</option>
                        ))}
                      </select>
                    </label>
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
                        className="rounded border-zinc-700 bg-zinc-800 text-teal-600"
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
                      className="flex items-center gap-1 px-3 py-1.5 text-xs rounded-lg bg-teal-600 text-white hover:bg-teal-500"
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
