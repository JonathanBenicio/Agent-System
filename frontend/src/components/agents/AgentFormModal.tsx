import { useState } from 'react'
import { X } from 'lucide-react'
import type { AgentInfo, AgentSpecification } from '@/types/api'
import { TierLabels } from '@/types/api'

interface Props {
  agent: AgentInfo | null
  onSave: (spec: AgentSpecification) => Promise<void>
  onClose: () => void
}

export function AgentFormModal({ agent, onSave, onClose }: Props) {
  const [form, setForm] = useState<AgentSpecification>({
    name: agent?.name ?? '',
    description: agent?.description ?? '',
    domain: agent?.domain ?? '',
    tier: agent?.tier ?? 1,
    systemPrompt: agent?.systemPrompt ?? '',
    capabilities: agent?.capabilities ?? [],
    maxConcurrency: agent?.maxConcurrency ?? 5,
    timeoutSeconds: agent?.timeoutSeconds ?? 30,
  })
  const [saving, setSaving] = useState(false)
  const [capInput, setCapInput] = useState('')

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setSaving(true)
    try {
      await onSave(form)
    } finally {
      setSaving(false)
    }
  }

  const addCapability = () => {
    const cap = capInput.trim()
    if (cap && !form.capabilities.includes(cap)) {
      setForm(prev => ({ ...prev, capabilities: [...prev.capabilities, cap] }))
      setCapInput('')
    }
  }

  const removeCapability = (cap: string) => {
    setForm(prev => ({ ...prev, capabilities: prev.capabilities.filter(c => c !== cap) }))
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center">
      <div className="absolute inset-0 bg-black/60" onClick={onClose} />
      <div className="relative bg-zinc-900 border border-zinc-700 rounded-xl w-full max-w-lg max-h-[90vh] overflow-y-auto shadow-2xl">
        <div className="sticky top-0 bg-zinc-900 px-6 py-4 border-b border-zinc-800 flex items-center justify-between">
          <h2 className="text-lg font-semibold text-zinc-100">
            {agent ? 'Editar Agent' : 'Novo Agent'}
          </h2>
          <button onClick={onClose} className="text-zinc-500 hover:text-zinc-300">
            <X className="w-5 h-5" />
          </button>
        </div>
        <form onSubmit={handleSubmit} className="p-6 space-y-4">
          <Field label="Nome" required>
            <input
              type="text"
              value={form.name}
              onChange={e => setForm(prev => ({ ...prev, name: e.target.value }))}
              disabled={!!agent}
              className="input"
              required
            />
          </Field>
          <Field label="Descrição" required>
            <textarea
              value={form.description}
              onChange={e => setForm(prev => ({ ...prev, description: e.target.value }))}
              className="input min-h-[80px]"
              required
            />
          </Field>
          <Field label="Domínio">
            <input
              type="text"
              value={form.domain}
              onChange={e => setForm(prev => ({ ...prev, domain: e.target.value }))}
              className="input"
            />
          </Field>
          <Field label="Tier">
            <select
              value={form.tier}
              onChange={e => setForm(prev => ({ ...prev, tier: Number(e.target.value) }))}
              className="input"
            >
              {[0, 1, 2, 3].map(t => (
                <option key={t} value={t}>{TierLabels[t]}</option>
              ))}
            </select>
          </Field>
          <Field label="System Prompt">
            <textarea
              value={form.systemPrompt}
              onChange={e => setForm(prev => ({ ...prev, systemPrompt: e.target.value }))}
              className="input min-h-[120px] font-mono text-xs"
            />
          </Field>
          <Field label="Capabilities">
            <div className="flex gap-2 mb-2">
              <input
                type="text"
                value={capInput}
                onChange={e => setCapInput(e.target.value)}
                onKeyDown={e => { if (e.key === 'Enter') { e.preventDefault(); addCapability() } }}
                className="input flex-1"
                placeholder="Adicionar capability"
              />
              <button type="button" onClick={addCapability} className="px-3 py-2 text-sm bg-zinc-800 border border-zinc-700 rounded-lg text-zinc-300 hover:bg-zinc-700">
                +
              </button>
            </div>
            <div className="flex flex-wrap gap-1">
              {form.capabilities.map(c => (
                <span key={c} className="inline-flex items-center gap-1 px-2 py-0.5 text-xs bg-zinc-800 rounded border border-zinc-700 text-zinc-300">
                  {c}
                  <button type="button" onClick={() => removeCapability(c)} className="text-zinc-500 hover:text-red-400">×</button>
                </span>
              ))}
            </div>
          </Field>
          <div className="grid grid-cols-2 gap-4">
            <Field label="Max Concurrency">
              <input
                type="number"
                min={1}
                max={100}
                value={form.maxConcurrency}
                onChange={e => setForm(prev => ({ ...prev, maxConcurrency: Number(e.target.value) }))}
                className="input"
              />
            </Field>
            <Field label="Timeout (s)">
              <input
                type="number"
                min={1}
                max={300}
                value={form.timeoutSeconds}
                onChange={e => setForm(prev => ({ ...prev, timeoutSeconds: Number(e.target.value) }))}
                className="input"
              />
            </Field>
          </div>
          <div className="flex justify-end gap-3 pt-4 border-t border-zinc-800">
            <button type="button" onClick={onClose} className="px-4 py-2 text-sm rounded-lg border border-zinc-700 text-zinc-300 hover:bg-zinc-800">
              Cancelar
            </button>
            <button type="submit" disabled={saving} className="px-4 py-2 text-sm rounded-lg bg-violet-600 text-white hover:bg-violet-500 disabled:opacity-50">
              {saving ? 'Salvando...' : agent ? 'Salvar' : 'Criar'}
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}

function Field({ label, required, children }: { label: string; required?: boolean; children: React.ReactNode }) {
  return (
    <label className="block">
      <span className="block text-xs font-medium text-zinc-400 mb-1">
        {label} {required && <span className="text-red-400">*</span>}
      </span>
      {children}
    </label>
  )
}
