import { useState } from 'react'
import { Key, Plus, Trash2, Check, WandSparkles, TestTube, Star } from 'lucide-react'
import { useLLMProviderApiKeys } from '@/hooks/useLLMProviderApiKeys'
import { Badge } from '@/components/shared/Badge'
import { useToast } from '@/components/shared/Toast'
import { cn } from '@/lib/utils'

interface ProviderApiKeysPanelProps {
  providerName: string
}

export function ProviderApiKeysPanel({ providerName }: ProviderApiKeysPanelProps) {
  const {
    keys,
    loading,
    error,
    registerKey,
    updateKey,
    deleteKey,
    setDefaultKey,
    testKey,
    discoverModelsForKey,
  } = useLLMProviderApiKeys(providerName)
  
  const { addToast } = useToast()
  
  const [isAdding, setIsAdding] = useState(false)
  const [addForm, setAddForm] = useState({ name: '', apiKey: '', isDefault: false })
  
  const [testing, setTesting] = useState<string | null>(null)
  const [discovering, setDiscovering] = useState<string | null>(null)

  const handleAddKey = async () => {
    if (!addForm.name || !addForm.apiKey) {
      addToast('Nome e API Key são obrigatórios', 'error')
      return
    }
    
    try {
      await registerKey(addForm)
      addToast('Chave registrada com sucesso', 'success')
      setAddForm({ name: '', apiKey: '', isDefault: false })
      setIsAdding(false)
    } catch (err) {
      addToast(err instanceof Error ? err.message : 'Erro ao registrar chave', 'error')
    }
  }

  const handleDeleteKey = async (id: string) => {
    if (!confirm('Tem certeza que deseja deletar esta chave?')) return
    try {
      await deleteKey(id)
      addToast('Chave deletada com sucesso', 'success')
    } catch (err) {
      addToast(err instanceof Error ? err.message : 'Erro ao deletar chave', 'error')
    }
  }

  const handleToggleEnable = async (id: string, isEnabled: boolean) => {
    try {
      await updateKey(id, { isEnabled: !isEnabled })
      addToast(isEnabled ? 'Chave desativada' : 'Chave ativada', 'success')
    } catch (err) {
      addToast(err instanceof Error ? err.message : 'Erro ao atualizar chave', 'error')
    }
  }

  const handleSetDefault = async (id: string) => {
    try {
      await setDefaultKey(id)
      addToast('Chave definida como padrão', 'success')
    } catch (err) {
      addToast(err instanceof Error ? err.message : 'Erro ao definir como padrão', 'error')
    }
  }

  const handleTestKey = async (id: string) => {
    setTesting(id)
    try {
      const res = await testKey(id)
      addToast(res.success ? 'Teste bem sucedido ✓' : 'Falha no teste', res.success ? 'success' : 'error')
    } catch (err) {
      addToast(err instanceof Error ? err.message : 'Erro ao testar chave', 'error')
    } finally {
      setTesting(null)
    }
  }

  const handleDiscoverModels = async (id: string) => {
    setDiscovering(id)
    try {
      const res = await discoverModelsForKey(id)
      if (res.success) {
        addToast(`${res.discoveredModels.length} modelos localizados para esta chave!`, 'success')
      } else {
        addToast(res.errorMessage ?? 'Nenhum modelo novo encontrado', 'info')
      }
    } catch (err) {
      addToast(err instanceof Error ? err.message : 'Erro ao descobrir modelos', 'error')
    } finally {
      setDiscovering(null)
    }
  }

  if (loading && keys.length === 0) {
    return <div className="text-sm text-zinc-500 py-4">Carregando chaves...</div>
  }

  return (
    <div className="mt-4 pt-4 border-t border-zinc-800">
      <div className="flex items-center justify-between mb-4">
        <h4 className="text-sm font-semibold text-zinc-300 flex items-center gap-2">
          <Key className="w-4 h-4 text-emerald-500" />
          Chaves de API Isoladas
        </h4>
        {!isAdding && (
          <button
            onClick={() => setIsAdding(true)}
            className="flex items-center gap-1.5 px-3 py-1.5 text-xs rounded-lg bg-emerald-600/20 text-emerald-400 hover:bg-emerald-600/30 transition-colors"
          >
            <Plus className="w-3.5 h-3.5" />
            Nova Chave
          </button>
        )}
      </div>

      {error && <div className="text-sm text-red-400 mb-4 bg-red-500/10 p-3 rounded-lg">{error}</div>}

      {isAdding && (
        <div className="bg-zinc-950/50 p-4 rounded-xl border border-zinc-800 mb-4 space-y-3">
          <h5 className="text-xs font-semibold text-zinc-400">Registrar nova credencial para {providerName}</h5>
          <div className="grid gap-4 md:grid-cols-2">
            <label className="block">
              <span className="text-xs text-zinc-400">Nome identificador (ex: Empresa A)</span>
              <input
                type="text"
                value={addForm.name}
                onChange={e => setAddForm(prev => ({ ...prev, name: e.target.value }))}
                className="input mt-1 w-full"
                placeholder="Meu time, Cliente X..."
              />
            </label>
            <label className="block">
              <span className="text-xs text-zinc-400">API Key</span>
              <input
                type="password"
                value={addForm.apiKey}
                onChange={e => setAddForm(prev => ({ ...prev, apiKey: e.target.value }))}
                className="input mt-1 w-full"
                placeholder="sk-..."
              />
            </label>
          </div>
          <div className="flex items-center justify-between pt-2 border-t border-zinc-800/50">
            <label className="flex items-center gap-2 cursor-pointer">
              <input
                type="checkbox"
                checked={addForm.isDefault}
                onChange={e => setAddForm(prev => ({ ...prev, isDefault: e.target.checked }))}
                className="rounded border-zinc-700 bg-zinc-800 text-emerald-600"
              />
              <span className="text-xs text-zinc-300">Definir como padrão (fallback)</span>
            </label>
            <div className="flex gap-2">
              <button
                onClick={() => setIsAdding(false)}
                className="px-3 py-1.5 text-xs rounded-lg border border-zinc-700 text-zinc-300 hover:bg-zinc-800"
              >
                Cancelar
              </button>
              <button
                onClick={handleAddKey}
                className="flex items-center gap-1 px-3 py-1.5 text-xs rounded-lg bg-emerald-600 text-white hover:bg-emerald-500"
              >
                <Check className="w-3.5 h-3.5" />
                Registrar
              </button>
            </div>
          </div>
        </div>
      )}

      {keys.length === 0 && !isAdding ? (
        <div className="text-sm text-zinc-500 italic bg-zinc-900/50 p-4 rounded-xl border border-zinc-800/50 text-center">
          Nenhuma chave customizada configurada para este provider. Ele usará as configurações legadas se existirem.
        </div>
      ) : (
        <div className="space-y-3">
          {keys.map(k => (
            <div key={k.id} className={cn(
              "flex flex-col gap-3 p-4 rounded-xl border transition-all",
              k.isEnabled ? "bg-zinc-950/40 border-zinc-800" : "bg-zinc-900/30 border-zinc-800/50 opacity-70"
            )}>
              <div className="flex items-start justify-between">
                <div>
                  <div className="flex items-center gap-2">
                    <h5 className="text-sm font-medium text-zinc-200">{k.name}</h5>
                    <span className="text-[10px] font-mono text-zinc-500 bg-zinc-900 px-1.5 py-0.5 rounded border border-zinc-800">
                      ...{k.lastFour}
                    </span>
                    {k.isDefault && (
                      <Badge variant="warning">
                        <Star className="w-3 h-3 mr-1" /> Padrão
                      </Badge>
                    )}
                  </div>
                  <div className="flex items-center gap-2 mt-2">
                    <label className="flex items-center gap-2 cursor-pointer">
                      <input
                        type="checkbox"
                        checked={k.isEnabled}
                        onChange={() => handleToggleEnable(k.id, k.isEnabled)}
                        className="rounded border-zinc-700 bg-zinc-800 text-emerald-600"
                      />
                      <span className="text-xs text-zinc-400">{k.isEnabled ? 'Ativa' : 'Inativa'}</span>
                    </label>
                  </div>
                </div>
                
                <div className="flex gap-2">
                  <button
                    onClick={() => handleTestKey(k.id)}
                    disabled={testing === k.id}
                    className="flex items-center gap-1.5 px-2.5 py-1 text-[11px] rounded border border-zinc-700 text-zinc-300 hover:bg-zinc-800 disabled:opacity-50"
                  >
                    <TestTube className="w-3.5 h-3.5" />
                    {testing === k.id ? 'Testando...' : 'Testar'}
                  </button>
                  <button
                    onClick={() => handleDiscoverModels(k.id)}
                    disabled={discovering === k.id}
                    className="flex items-center gap-1.5 px-2.5 py-1 text-[11px] rounded bg-sky-600/20 text-sky-400 hover:bg-sky-600/30 transition-colors disabled:opacity-50"
                  >
                    <WandSparkles className="w-3.5 h-3.5" />
                    {discovering === k.id ? 'Buscando...' : 'Modelos'}
                  </button>
                  <div className="w-px h-6 bg-zinc-800 mx-1"></div>
                  {!k.isDefault && k.isEnabled && (
                    <button
                      onClick={() => handleSetDefault(k.id)}
                      className="px-2.5 py-1 text-[11px] rounded text-zinc-400 hover:text-amber-400 hover:bg-zinc-800 transition-colors"
                      title="Tornar Padrão"
                    >
                      <Star className="w-3.5 h-3.5" />
                    </button>
                  )}
                  <button
                    onClick={() => handleDeleteKey(k.id)}
                    className="px-2.5 py-1 text-[11px] rounded text-zinc-500 hover:text-red-400 hover:bg-red-500/10 transition-colors"
                    title="Excluir"
                  >
                    <Trash2 className="w-3.5 h-3.5" />
                  </button>
                </div>
              </div>
              
              {k.models && k.models.length > 0 && (
                <div className="pt-2 border-t border-zinc-800/50">
                  <p className="text-[10px] text-zinc-500 mb-1.5 uppercase tracking-wider">Modelos Disponíveis ({k.models.length})</p>
                  <div className="flex flex-wrap gap-1">
                    {k.models.map(m => (
                      <span key={m} className="text-[10px] bg-zinc-800 text-zinc-300 px-1.5 py-0.5 rounded">
                        {m}
                      </span>
                    ))}
                  </div>
                </div>
              )}
            </div>
          ))}
        </div>
      )}
    </div>
  )
}
