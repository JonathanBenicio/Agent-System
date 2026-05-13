import { useState, useEffect } from 'react'
import { Shield, FolderOpen, Database, Cpu, Settings, History, Plus, Trash2, Edit, Check, X, RefreshCw } from 'lucide-react'

interface ConfigEntry {
  id: string
  key: string
  value: string
  isSecret: boolean
  category: string
  status: string
  description: string
  provider: string
  createdAt: string
  updatedAt: string
  expiresAt: string | null
  metadata: Record<string, string>
}

interface ConfigChangeLog {
  id: string
  configKey: string
  action: string
  changedAt: string
  previousValueHash: string | null
  newValueHash: string | null
}

type ConfigCategory = 'Credentials' | 'Paths' | 'Connection' | 'Provider' | 'General'

const categoryConfig: Record<ConfigCategory, { icon: React.ElementType; label: string; color: string }> = {
  Credentials: { icon: Shield, label: 'Credenciais', color: 'text-red-400' },
  Paths: { icon: FolderOpen, label: 'Caminhos', color: 'text-blue-400' },
  Connection: { icon: Database, label: 'Conexões', color: 'text-green-400' },
  Provider: { icon: Cpu, label: 'Providers', color: 'text-cyan-400' },
  General: { icon: Settings, label: 'Geral', color: 'text-zinc-400' },
}

const API_BASE = '/api/admin/config'

export function ConfigAdvancedPage() {
  const [entries, setEntries] = useState<ConfigEntry[]>([])
  const [auditLog, setAuditLog] = useState<ConfigChangeLog[]>([])
  const [activeTab, setActiveTab] = useState<ConfigCategory | 'audit'>('Credentials')
  const [showForm, setShowForm] = useState(false)
  const [editKey, setEditKey] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)
  const [form, setForm] = useState({
    key: '', value: '', isSecret: false, category: 'General' as ConfigCategory,
    description: '', provider: '', expiresAt: ''
  })

  useEffect(() => { fetchEntries() }, [activeTab])

  async function fetchEntries() {
    setLoading(true)
    try {
      if (activeTab === 'audit') {
        const res = await fetch(`${API_BASE}/audit-log?limit=100`)
        setAuditLog(await res.json())
      } else {
        const res = await fetch(`${API_BASE}?category=${activeTab}`)
        setEntries(await res.json())
      }
    } catch { /* API not available */ }
    setLoading(false)
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    const body = { ...form, expiresAt: form.expiresAt || null }
    const url = editKey ? `${API_BASE}/${editKey}` : API_BASE
    const method = editKey ? 'PUT' : 'POST'
    await fetch(url, { method, headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) })
    resetForm()
    fetchEntries()
  }

  async function handleDelete(key: string) {
    if (!confirm(`Deletar configuração "${key}"?`)) return
    await fetch(`${API_BASE}/${key}`, { method: 'DELETE' })
    fetchEntries()
  }

  function startEdit(entry: ConfigEntry) {
    setForm({
      key: entry.key,
      value: entry.isSecret ? '' : entry.value,
      isSecret: entry.isSecret,
      category: entry.category as ConfigCategory,
      description: entry.description,
      provider: entry.provider,
      expiresAt: entry.expiresAt ?? ''
    })
    setEditKey(entry.key)
    setShowForm(true)
  }

  function resetForm() {
    setForm({ key: '', value: '', isSecret: false, category: 'General', description: '', provider: '', expiresAt: '' })
    setEditKey(null)
    setShowForm(false)
  }

  const tabs = [...Object.keys(categoryConfig) as ConfigCategory[], 'audit' as const]

  return (
    <div className="p-6 space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-zinc-100">Configurações Avançadas</h1>
          <p className="text-sm text-zinc-400 mt-1">Gerenciamento de credenciais, caminhos e conexões com encriptação AES-256</p>
        </div>
        <button onClick={() => setShowForm(true)} className="flex items-center gap-2 px-4 py-2 bg-teal-600 text-white rounded-lg hover:bg-teal-700 text-sm">
          <Plus className="w-4 h-4" /> Nova Config
        </button>
      </div>

      {/* Tabs */}
      <div className="flex gap-1 border-b border-zinc-800 pb-0">
        {tabs.map((tab) => {
          const cfg = tab === 'audit' ? { icon: History, label: 'Audit Log', color: 'text-yellow-400' } : categoryConfig[tab]
          const Icon = cfg.icon
          return (
            <button key={tab} onClick={() => setActiveTab(tab)}
              className={`flex items-center gap-2 px-4 py-2 text-sm rounded-t-lg border-b-2 transition-colors ${
                activeTab === tab ? 'border-teal-500 text-zinc-100 bg-zinc-800/50' : 'border-transparent text-zinc-400 hover:text-zinc-200'
              }`}>
              <Icon className={`w-4 h-4 ${cfg.color}`} />
              {cfg.label}
            </button>
          )
        })}
      </div>

      {/* Form */}
      {showForm && (
        <form onSubmit={handleSubmit} className="bg-zinc-900 border border-zinc-800 rounded-xl p-4 space-y-4">
          <div className="grid grid-cols-2 gap-4">
            <div>
              <label className="block text-xs text-zinc-400 mb-1">Chave</label>
              <input value={form.key} onChange={e => setForm({ ...form, key: e.target.value })} disabled={!!editKey}
                className="w-full bg-zinc-800 border border-zinc-700 rounded-lg px-3 py-2 text-sm text-zinc-100 disabled:opacity-50" required />
            </div>
            <div>
              <label className="block text-xs text-zinc-400 mb-1">Valor</label>
              <input value={form.value} onChange={e => setForm({ ...form, value: e.target.value })} type={form.isSecret ? 'password' : 'text'}
                className="w-full bg-zinc-800 border border-zinc-700 rounded-lg px-3 py-2 text-sm text-zinc-100" required />
            </div>
            <div>
              <label className="block text-xs text-zinc-400 mb-1">Categoria</label>
              <select value={form.category} onChange={e => setForm({ ...form, category: e.target.value as ConfigCategory })}
                className="w-full bg-zinc-800 border border-zinc-700 rounded-lg px-3 py-2 text-sm text-zinc-100">
                {Object.keys(categoryConfig).map(c => <option key={c} value={c}>{categoryConfig[c as ConfigCategory].label}</option>)}
              </select>
            </div>
            <div>
              <label className="block text-xs text-zinc-400 mb-1">Provider</label>
              <input value={form.provider} onChange={e => setForm({ ...form, provider: e.target.value })}
                className="w-full bg-zinc-800 border border-zinc-700 rounded-lg px-3 py-2 text-sm text-zinc-100" placeholder="ex: OpenAI, Azure" />
            </div>
            <div className="col-span-2">
              <label className="block text-xs text-zinc-400 mb-1">Descrição</label>
              <input value={form.description} onChange={e => setForm({ ...form, description: e.target.value })}
                className="w-full bg-zinc-800 border border-zinc-700 rounded-lg px-3 py-2 text-sm text-zinc-100" />
            </div>
            <div className="flex items-center gap-2">
              <input type="checkbox" checked={form.isSecret} onChange={e => setForm({ ...form, isSecret: e.target.checked })}
                className="rounded border-zinc-700" id="isSecret" />
              <label htmlFor="isSecret" className="text-sm text-zinc-300">Valor secreto (encriptado)</label>
            </div>
            <div>
              <label className="block text-xs text-zinc-400 mb-1">Expira em (opcional)</label>
              <input type="datetime-local" value={form.expiresAt} onChange={e => setForm({ ...form, expiresAt: e.target.value })}
                className="w-full bg-zinc-800 border border-zinc-700 rounded-lg px-3 py-2 text-sm text-zinc-100" />
            </div>
          </div>
          <div className="flex gap-2 justify-end">
            <button type="button" onClick={resetForm} className="px-3 py-2 text-sm text-zinc-400 hover:text-zinc-200"><X className="w-4 h-4 inline mr-1" />Cancelar</button>
            <button type="submit" className="px-4 py-2 bg-teal-600 text-white rounded-lg text-sm hover:bg-teal-700"><Check className="w-4 h-4 inline mr-1" />{editKey ? 'Atualizar' : 'Criar'}</button>
          </div>
        </form>
      )}

      {/* Content */}
      {loading ? (
        <div className="flex items-center justify-center py-12 text-zinc-400"><RefreshCw className="w-5 h-5 animate-spin mr-2" />Carregando...</div>
      ) : activeTab === 'audit' ? (
        <div className="bg-zinc-900 border border-zinc-800 rounded-xl overflow-hidden">
          <table className="w-full text-sm">
            <thead className="bg-zinc-800/50 text-zinc-400">
              <tr><th className="px-4 py-3 text-left">Data</th><th className="px-4 py-3 text-left">Chave</th><th className="px-4 py-3 text-left">Ação</th></tr>
            </thead>
            <tbody className="divide-y divide-zinc-800">
              {auditLog.map(log => (
                <tr key={log.id} className="hover:bg-zinc-800/30">
                  <td className="px-4 py-3 text-zinc-300">{new Date(log.changedAt).toLocaleString('pt-BR')}</td>
                  <td className="px-4 py-3 text-zinc-100 font-mono text-xs">{log.configKey}</td>
                  <td className="px-4 py-3"><span className={`px-2 py-0.5 rounded-full text-xs ${log.action === 'Deleted' ? 'bg-red-900/30 text-red-400' : 'bg-blue-900/30 text-blue-400'}`}>{log.action}</span></td>
                </tr>
              ))}
            </tbody>
          </table>
          {auditLog.length === 0 && <div className="text-center py-8 text-zinc-500">Nenhum registro de auditoria</div>}
        </div>
      ) : (
        <div className="space-y-2">
          {entries.map(entry => (
            <div key={entry.id} className="flex items-center justify-between bg-zinc-900 border border-zinc-800 rounded-xl px-4 py-3 hover:border-zinc-700 transition-colors">
              <div className="flex-1 min-w-0">
                <div className="flex items-center gap-2">
                  <span className="font-mono text-sm text-zinc-100">{entry.key}</span>
                  {entry.isSecret && <Shield className="w-3.5 h-3.5 text-red-400" />}
                  <span className={`text-xs px-1.5 py-0.5 rounded ${entry.status === 'Active' ? 'bg-green-900/30 text-green-400' : 'bg-zinc-800 text-zinc-500'}`}>{entry.status}</span>
                </div>
                <div className="text-xs text-zinc-500 mt-0.5">{entry.description || entry.value}</div>
              </div>
              <div className="flex gap-1">
                <button onClick={() => startEdit(entry)} className="p-2 text-zinc-400 hover:text-zinc-100 rounded-lg hover:bg-zinc-800"><Edit className="w-4 h-4" /></button>
                <button onClick={() => handleDelete(entry.key)} className="p-2 text-zinc-400 hover:text-red-400 rounded-lg hover:bg-zinc-800"><Trash2 className="w-4 h-4" /></button>
              </div>
            </div>
          ))}
          {entries.length === 0 && <div className="text-center py-12 text-zinc-500">Nenhuma configuração nesta categoria</div>}
        </div>
      )}
    </div>
  )
}
