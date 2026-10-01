import { useState } from 'react'
import { X } from 'lucide-react'
import type { LoadPluginRequest } from '@/types/api'

interface Props {
  initialValues?: LoadPluginRequest
  onLoad: (req: LoadPluginRequest) => Promise<void>
  onClose: () => void
}

export function PluginLoadModal({ initialValues, onLoad, onClose }: Props) {
  const [form, setForm] = useState<LoadPluginRequest>(initialValues || {
    name: '',
    command: '',
    arguments: [],
    transportType: 'stdio',
  })
  const [argsInput, setArgsInput] = useState(initialValues?.arguments?.join('\n') || '')
  const [envInput, setEnvInput] = useState(() => Object.entries(initialValues?.environmentVariables ?? {}).map(([k, v]) => `${k}=${v}`).join('\n'))
  const [headersInput, setHeadersInput] = useState(() => Object.entries(initialValues?.headers ?? {}).map(([k, v]) => `${k}=${v}`).join('\n'))
  const [loading, setLoading] = useState(false)

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setLoading(true)
    try {
      const parseKeyValue = (input: string) => {
        const result: Record<string, string> = {}
        if (input) {
          input.split('\n').forEach(line => {
            const [key, ...rest] = line.split('=')
            if (key && rest.length > 0) {
              result[key.trim()] = rest.join('=').trim()
            }
          })
        }
        return result
      }

      await onLoad({
        ...form,
        arguments: argsInput ? argsInput.split('\n').map(a => a.trim()).filter(Boolean) : [],
        environmentVariables: parseKeyValue(envInput),
        headers: parseKeyValue(headersInput),
      })
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center">
      <div className="absolute inset-0 bg-black/60" onClick={onClose} />
      <div className="relative bg-zinc-900 border border-zinc-700 rounded-xl w-full max-w-md shadow-2xl overflow-hidden flex flex-col max-h-[90vh]">
        <div className="px-6 py-4 border-b border-zinc-800 flex items-center justify-between">
          <h2 className="text-lg font-semibold text-zinc-100">
            {initialValues ? 'Editar Plugin MCP' : 'Carregar Plugin MCP'}
          </h2>
          <button onClick={onClose} className="text-zinc-500 hover:text-zinc-300">
            <X className="w-5 h-5" />
          </button>
        </div>
        <form onSubmit={handleSubmit} className="p-6 space-y-4 overflow-y-auto">
          <label className="block">
            <span className="text-xs font-medium text-zinc-400">Nome *</span>
            <input
              type="text"
              value={form.name}
              onChange={e => setForm(prev => ({ ...prev, name: e.target.value }))}
              className="input mt-1"
              placeholder="ex: Google Stitch"
              required
            />
          </label>

          <label className="block">
            <span className="text-xs font-medium text-zinc-400">Transport</span>
            <select
              value={form.transportType}
              onChange={e => setForm(prev => ({ ...prev, transportType: e.target.value as 'stdio' | 'sse' }))}
              className="input mt-1"
            >
              <option value="stdio">stdio (Local Process)</option>
              <option value="sse">sse (Remote HTTP)</option>
            </select>
          </label>

          {form.transportType === 'stdio' ? (
            <>
              <label className="block">
                <span className="text-xs font-medium text-zinc-400">Comando *</span>
                <input
                  type="text"
                  value={form.command}
                  onChange={e => setForm(prev => ({ ...prev, command: e.target.value }))}
                  className="input mt-1"
                  placeholder="npx"
                  required
                />
              </label>
              <label className="block">
                <span className="text-xs font-medium text-zinc-400">Argumentos (um por linha)</span>
                <textarea
                  value={argsInput}
                  onChange={e => setArgsInput(e.target.value)}
                  className="input mt-1 min-h-[80px] font-mono text-xs"
                  placeholder="-y&#10;@google/stitch-mcp"
                />
              </label>
              <label className="block">
                <span className="text-xs font-medium text-zinc-400">Variáveis de Ambiente (KEY=VALUE)</span>
                <textarea
                  value={envInput}
                  onChange={e => setEnvInput(e.target.value)}
                  className="input mt-1 min-h-[80px] font-mono text-xs"
                  placeholder="GOOGLE_API_KEY=sua_chave_aqui"
                />
              </label>
            </>
          ) : (
            <>
              <label className="block">
                <span className="text-xs font-medium text-zinc-400">URL (Endpoint) *</span>
                <input
                  type="url"
                  value={form.endpoint ?? ''}
                  onChange={e => setForm(prev => ({ ...prev, endpoint: e.target.value }))}
                  className="input mt-1"
                  placeholder="https://stitch.googleapis.com/mcp"
                  required
                />
              </label>
              <label className="block">
                <span className="text-xs font-medium text-zinc-400">Headers (KEY=VALUE)</span>
                <textarea
                  value={headersInput}
                  onChange={e => setHeadersInput(e.target.value)}
                  className="input mt-1 min-h-[80px] font-mono text-xs"
                  placeholder="X-Goog-Api-Key=sua_chave_aqui&#10;Accept=application/json"
                />
              </label>
            </>
          )}

          <div className="flex justify-end gap-3 pt-4 border-t border-zinc-800">
            <button type="button" onClick={onClose} className="px-4 py-2 text-sm rounded-lg border border-zinc-700 text-zinc-300 hover:bg-zinc-800">
              Cancelar
            </button>
            <button type="submit" disabled={loading} className="px-4 py-2 text-sm rounded-lg bg-teal-600 text-white hover:bg-teal-500 disabled:opacity-50">
              {loading ? 'Salvando...' : initialValues ? 'Salvar' : 'Carregar'}
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}
