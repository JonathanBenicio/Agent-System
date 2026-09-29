import { Cpu, KeyRound } from 'lucide-react'
import { useLLMConfig } from '@/hooks/chat/useLLMConfig'
import { ProviderApiKeysPanel } from './ProviderApiKeysPanel'

export function ProvidersPage() {
  const { providers, selectedProvider, selectedModel, canManageTenant,
    setSelectedProvider, setSelectedModel } = useLLMConfig()
  const active = providers.find(item => item.name === selectedProvider) ?? providers[0]
  const models = Array.from(new Set([active?.defaultModel, ...(active?.models ?? [])].filter(Boolean)))

  return (
    <div className="h-full overflow-y-auto bg-zinc-950 text-zinc-100">
      <div className="mx-auto max-w-4xl space-y-6 p-6">
        <div>
          <h1 className="flex items-center gap-2 text-xl font-semibold"><Cpu className="h-5 w-5 text-teal-400" /> IA do chat</h1>
          <p className="mt-1 text-sm text-zinc-400">Escolha o provedor e o modelo das suas próximas conversas neste tenant.</p>
        </div>

        <section className="rounded-xl border border-zinc-800 bg-zinc-900/50 p-5">
          <h2 className="text-sm font-semibold">Sua preferência</h2>
          <div className="mt-4 grid gap-4 sm:grid-cols-2">
            <label className="text-xs text-zinc-400">Provedor
              <select value={active?.name ?? ''} onChange={event => setSelectedProvider(event.target.value)}
                disabled={!active} className="mt-1 w-full rounded-lg border border-zinc-700 bg-zinc-950 p-2 text-zinc-100">
                {providers.map(item => <option key={item.name} value={item.name}>{item.name}</option>)}
              </select>
            </label>
            <label className="text-xs text-zinc-400">Modelo
              <select value={selectedModel} onChange={event => setSelectedModel(event.target.value)}
                disabled={!active} className="mt-1 w-full rounded-lg border border-zinc-700 bg-zinc-950 p-2 text-zinc-100">
                {models.map(model => <option key={model} value={model}>{model}</option>)}
              </select>
            </label>
          </div>
          {!active && <p className="mt-3 text-sm text-amber-400">Nenhum provedor está disponível para este tenant.</p>}
        </section>

        <section className="rounded-xl border border-zinc-800 bg-zinc-900/50 p-5">
          <h2 className="flex items-center gap-2 text-sm font-semibold"><KeyRound className="h-4 w-4 text-teal-400" /> Chaves do tenant</h2>
          <p className="mt-1 text-xs text-zinc-400">Owner e Admin podem gerir chaves BYOK; o segredo não reaparece depois de salvo.</p>
          {canManageTenant ? (
            providers.map(item => <ProviderApiKeysPanel key={item.name} providerName={item.name} />)
          ) : (
            <p className="mt-4 text-xs text-zinc-500">Você pode usar os modelos disponíveis, mas não pode alterar as chaves do tenant.</p>
          )}
        </section>
      </div>
    </div>
  )
}
