import { useState } from 'react'
import { X } from 'lucide-react'
import type { LoadPluginRequest } from '@/types/api'

interface Props {
  onLoad: (req: LoadPluginRequest) => Promise<void>
  onClose: () => void
}

export function PluginLoadModal({ onLoad, onClose }: Props) {
  const [form, setForm] = useState<LoadPluginRequest>({
    name: '',
    command: '',
    args: [],
    transport: 'stdio',
  })
  const [argsInput, setArgsInput] = useState('')
  const [loading, setLoading] = useState(false)

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setLoading(true)
    try {
      await onLoad({
        ...form,
        args: argsInput ? argsInput.split('\n').map(a => a.trim()).filter(Boolean) : [],
      })
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center">
      <div className="absolute inset-0 bg-black/60" onClick={onClose} />
      <div className="relative bg-zinc-900 border border-zinc-700 rounded-xl w-full max-w-md shadow-2xl">
        <div className="px-6 py-4 border-b border-zinc-800 flex items-center justify-between">
          <h2 className="text-lg font-semibold text-zinc-100">Carregar Plugin MCP</h2>
          <button onClick={onClose} className="text-zinc-500 hover:text-zinc-300">
            <X className="w-5 h-5" />
          </button>
        </div>
        <form onSubmit={handleSubmit} className="p-6 space-y-4">
          <label className="block">
            <span className="text-xs font-medium text-zinc-400">Nome *</span>
            <input
              type="text"
              value={form.name}
              onChange={e => setForm(prev => ({ ...prev, name: e.target.value }))}
              className="input mt-1"
              required
            />
          </label>
          <label className="block">
            <span className="text-xs font-medium text-zinc-400">Comando *</span>
            <input
              type="text"
              value={form.command}
              onChange={e => setForm(prev => ({ ...prev, command: e.target.value }))}
              className="input mt-1"
              placeholder="npx @modelcontextprotocol/server-xyz"
              required
            />
          </label>
          <label className="block">
            <span className="text-xs font-medium text-zinc-400">Argumentos (um por linha)</span>
            <textarea
              value={argsInput}
              onChange={e => setArgsInput(e.target.value)}
              className="input mt-1 min-h-[80px] font-mono text-xs"
              placeholder="--flag&#10;value"
            />
          </label>
          <label className="block">
            <span className="text-xs font-medium text-zinc-400">Transport</span>
            <select
              value={form.transport}
              onChange={e => setForm(prev => ({ ...prev, transport: e.target.value as 'stdio' | 'sse' }))}
              className="input mt-1"
            >
              <option value="stdio">stdio</option>
              <option value="sse">sse</option>
            </select>
          </label>
          {form.transport === 'sse' && (
            <label className="block">
              <span className="text-xs font-medium text-zinc-400">URL</span>
              <input
                type="url"
                value={form.url ?? ''}
                onChange={e => setForm(prev => ({ ...prev, url: e.target.value }))}
                className="input mt-1"
                placeholder="http://localhost:3000/sse"
              />
            </label>
          )}
          <div className="flex justify-end gap-3 pt-4 border-t border-zinc-800">
            <button type="button" onClick={onClose} className="px-4 py-2 text-sm rounded-lg border border-zinc-700 text-zinc-300 hover:bg-zinc-800">
              Cancelar
            </button>
            <button type="submit" disabled={loading} className="px-4 py-2 text-sm rounded-lg bg-teal-600 text-white hover:bg-teal-500 disabled:opacity-50">
              {loading ? 'Carregando...' : 'Carregar'}
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}
