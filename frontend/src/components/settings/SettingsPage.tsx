import { useState } from 'react'
import { Settings2, Save, Loader2 } from 'lucide-react'
import { useSettings } from '@/hooks/useSettings'
import { PageLoading, PageError } from '@/components/shared/Loading'
import { useToast } from '@/components/shared/Toast'
import type { GatewaySettings, MemorySettings } from '@/types/api'

type Tab = 'gateway' | 'memory'

export function SettingsPage() {
  const { settings, loading, error, saving, refresh, saveGateway, saveMemory } = useSettings()
  const { addToast } = useToast()
  const [tab, setTab] = useState<Tab>('gateway')
  const [gwForm, setGwForm] = useState<GatewaySettings | null>(null)
  const [memForm, setMemForm] = useState<MemorySettings | null>(null)

  if (loading) return <PageLoading />
  if (error || !settings) return <PageError message={error ?? 'Sem dados'} onRetry={refresh} />

  const gw = gwForm ?? settings.gateway
  const mem = memForm ?? settings.memory

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

  const tabs: { id: Tab; label: string }[] = [
    { id: 'gateway', label: 'Gateway' },
    { id: 'memory', label: 'Memória' },
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
                  ? 'border-violet-500 text-violet-400'
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
            <FormField label="Max Retry Attempts">
              <input
                type="number"
                min={0}
                max={10}
                value={gw.maxRetryAttempts}
                onChange={e => setGwForm({ ...gw, maxRetryAttempts: Number(e.target.value) })}
                className="input"
              />
            </FormField>
            <FormField label="Circuit Breaker Threshold">
              <input
                type="number"
                min={1}
                max={50}
                value={gw.circuitBreakerThreshold}
                onChange={e => setGwForm({ ...gw, circuitBreakerThreshold: Number(e.target.value) })}
                className="input"
              />
            </FormField>
            <FormField label="Circuit Breaker Duration (s)">
              <input
                type="number"
                min={5}
                max={300}
                value={gw.circuitBreakerDuration}
                onChange={e => setGwForm({ ...gw, circuitBreakerDuration: Number(e.target.value) })}
                className="input"
              />
            </FormField>
            <FormField label="Daily Budget ($)">
              <input
                type="number"
                min={0}
                step={0.01}
                value={gw.dailyBudget}
                onChange={e => setGwForm({ ...gw, dailyBudget: Number(e.target.value) })}
                className="input"
              />
            </FormField>
            <FormField label="Default Timeout (s)">
              <input
                type="number"
                min={5}
                max={300}
                value={gw.defaultTimeout}
                onChange={e => setGwForm({ ...gw, defaultTimeout: Number(e.target.value) })}
                className="input"
              />
            </FormField>
            <label className="flex items-center gap-2 cursor-pointer">
              <input
                type="checkbox"
                checked={gw.enableCostTracking}
                onChange={e => setGwForm({ ...gw, enableCostTracking: e.target.checked })}
                className="rounded border-zinc-700 bg-zinc-800 text-violet-600"
              />
              <span className="text-sm text-zinc-300">Habilitar Tracking de Custos</span>
            </label>
            <SaveButton onClick={handleSaveGateway} saving={saving} disabled={!gwForm} />
          </div>
        )}

        {/* Memory Tab */}
        {tab === 'memory' && mem && (
          <div className="space-y-4">
            <FormField label="Max Sessions">
              <input
                type="number"
                min={1}
                max={1000}
                value={mem.maxSessions}
                onChange={e => setMemForm({ ...mem, maxSessions: Number(e.target.value) })}
                className="input"
              />
            </FormField>
            <FormField label="Max History Per Session">
              <input
                type="number"
                min={10}
                max={10000}
                value={mem.maxHistoryPerSession}
                onChange={e => setMemForm({ ...mem, maxHistoryPerSession: Number(e.target.value) })}
                className="input"
              />
            </FormField>
            <FormField label="Session Timeout (min)">
              <input
                type="number"
                min={1}
                max={1440}
                value={mem.sessionTimeoutMinutes}
                onChange={e => setMemForm({ ...mem, sessionTimeoutMinutes: Number(e.target.value) })}
                className="input"
              />
            </FormField>
            <label className="flex items-center gap-2 cursor-pointer">
              <input
                type="checkbox"
                checked={mem.enablePersistence}
                onChange={e => setMemForm({ ...mem, enablePersistence: e.target.checked })}
                className="rounded border-zinc-700 bg-zinc-800 text-violet-600"
              />
              <span className="text-sm text-zinc-300">Habilitar Persistência</span>
            </label>
            <SaveButton onClick={handleSaveMemory} saving={saving} disabled={!memForm} />
          </div>
        )}
      </div>
    </div>
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

function SaveButton({ onClick, saving, disabled }: { onClick: () => void; saving: boolean; disabled: boolean }) {
  return (
    <div className="flex justify-end pt-4 border-t border-zinc-800">
      <button
        onClick={onClick}
        disabled={saving || disabled}
        className="flex items-center gap-2 px-4 py-2 text-sm rounded-lg bg-violet-600 text-white hover:bg-violet-500 disabled:opacity-50"
      >
        {saving ? <Loader2 className="w-4 h-4 animate-spin" /> : <Save className="w-4 h-4" />}
        {saving ? 'Salvando...' : 'Salvar'}
      </button>
    </div>
  )
}
