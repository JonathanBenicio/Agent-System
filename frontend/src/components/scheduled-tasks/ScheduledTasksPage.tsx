import { useState, useEffect, useCallback } from 'react'
import {
  Clock,
  Play,
  Pause,
  Trash2,
  RefreshCw,
  Plus,
  Zap,
  Send,
  CheckCircle2,
  XCircle,
  AlertCircle,
  Activity,
} from 'lucide-react'
import { scheduledTasksApi } from '@/lib/api'
import { PageLoading, PageError } from '@/components/shared/Loading'
import { Badge } from '@/components/shared/Badge'
import { cn } from '@/lib/utils'
import type {
  ScheduledTask,
  TriggerRule,
  ChannelInfo,
  ScheduledTasksHealthReport,
  ScheduledTaskStatus,
  TriggerSourceType,
  ConditionType,
} from '@/types/api'

type Tab = 'tasks' | 'rules' | 'channels' | 'health'

export function ScheduledTasksPage() {
  const [tab, setTab] = useState<Tab>('tasks')

  return (
    <div className="h-full overflow-y-auto">
      <div className="max-w-7xl mx-auto px-6 py-6 space-y-6">
        <div className="flex items-center gap-3">
          <Clock className="w-6 h-6 text-blue-400" />
          <h1 className="text-xl font-semibold text-zinc-100">Scheduled Tasks & Triggers</h1>
        </div>

        {/* Tabs */}
        <div className="flex gap-1 p-1 bg-zinc-900 rounded-lg border border-zinc-800 w-fit">
          {(['tasks', 'rules', 'channels', 'health'] as Tab[]).map(t => (
            <button
              key={t}
              onClick={() => setTab(t)}
              className={cn(
                'px-4 py-2 text-sm rounded-md capitalize transition-colors',
                tab === t
                  ? 'bg-zinc-700 text-zinc-100 font-medium'
                  : 'text-zinc-400 hover:text-zinc-200'
              )}
            >
              {t === 'tasks' ? 'Tasks' : t === 'rules' ? 'Trigger Rules' : t === 'channels' ? 'Channels' : 'Health'}
            </button>
          ))}
        </div>

        {tab === 'tasks' && <TasksTab />}
        {tab === 'rules' && <RulesTab />}
        {tab === 'channels' && <ChannelsTab />}
        {tab === 'health' && <HealthTab />}
      </div>
    </div>
  )
}

// ═══════════════════════════════════════════════════════════
// Tasks Tab
// ═══════════════════════════════════════════════════════════

function TasksTab() {
  const [tasks, setTasks] = useState<ScheduledTask[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [showCreate, setShowCreate] = useState(false)

  const refresh = useCallback(async () => {
    try {
      setError(null)
      setLoading(true)
      const data = await scheduledTasksApi.listTasks()
      setTasks(data)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erro ao carregar tasks')
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => { refresh() }, [refresh])

  const handlePause = async (id: string) => {
    await scheduledTasksApi.pauseTask(id)
    refresh()
  }

  const handleResume = async (id: string) => {
    await scheduledTasksApi.resumeTask(id)
    refresh()
  }

  const handleExecute = async (id: string) => {
    await scheduledTasksApi.executeTask(id)
    refresh()
  }

  const handleDelete = async (id: string) => {
    if (!confirm('Remover esta task?')) return
    await scheduledTasksApi.deleteTask(id)
    refresh()
  }

  if (loading) return <PageLoading />
  if (error) return <PageError message={error} onRetry={refresh} />

  return (
    <div className="space-y-4">
      <div className="flex justify-between items-center">
        <p className="text-sm text-zinc-400">{tasks.length} task(s) registrada(s)</p>
        <div className="flex gap-2">
          <button onClick={() => setShowCreate(!showCreate)} className="flex items-center gap-2 px-3 py-2 text-sm rounded-lg bg-blue-600 text-white hover:bg-blue-700">
            <Plus className="w-4 h-4" />
            Nova Task
          </button>
          <button onClick={refresh} className="flex items-center gap-2 px-3 py-2 text-sm rounded-lg border border-zinc-700 text-zinc-300 hover:bg-zinc-800">
            <RefreshCw className="w-4 h-4" />
          </button>
        </div>
      </div>

      {showCreate && <CreateTaskForm onCreated={() => { setShowCreate(false); refresh() }} />}

      <div className="space-y-3">
        {tasks.map(task => (
          <div key={task.id} className="bg-zinc-900 border border-zinc-800 rounded-xl p-4">
            <div className="flex items-center justify-between">
              <div className="flex items-center gap-3">
                <StatusIcon status={task.status} />
                <div>
                  <h3 className="text-sm font-semibold text-zinc-100">{task.name}</h3>
                  <p className="text-xs text-zinc-500">
                    {task.schedule} • Execuções: {task.totalExecutions} ({task.failedExecutions} falhas)
                  </p>
                </div>
              </div>
              <div className="flex items-center gap-2">
                <Badge variant={statusVariant(task.status)}>{task.status}</Badge>
                {task.status === 'Active' && (
                  <button onClick={() => handlePause(task.id)} className="p-1.5 rounded hover:bg-zinc-800 text-zinc-400" title="Pausar">
                    <Pause className="w-4 h-4" />
                  </button>
                )}
                {task.status === 'Paused' && (
                  <button onClick={() => handleResume(task.id)} className="p-1.5 rounded hover:bg-zinc-800 text-zinc-400" title="Retomar">
                    <Play className="w-4 h-4" />
                  </button>
                )}
                <button onClick={() => handleExecute(task.id)} className="p-1.5 rounded hover:bg-zinc-800 text-blue-400" title="Executar agora">
                  <Zap className="w-4 h-4" />
                </button>
                <button onClick={() => handleDelete(task.id)} className="p-1.5 rounded hover:bg-zinc-800 text-red-400" title="Remover">
                  <Trash2 className="w-4 h-4" />
                </button>
              </div>
            </div>
            {task.nextRunAt && (
              <p className="text-xs text-zinc-500 mt-2">Próxima execução: {new Date(task.nextRunAt).toLocaleString()}</p>
            )}
          </div>
        ))}
        {tasks.length === 0 && (
          <div className="text-center py-12 text-zinc-500">Nenhuma task configurada. Clique em "Nova Task" para começar.</div>
        )}
      </div>
    </div>
  )
}

// ═══════════════════════════════════════════════════════════
// Create Task Form
// ═══════════════════════════════════════════════════════════

function CreateTaskForm({ onCreated }: { onCreated: () => void }) {
  const [name, setName] = useState('')
  const [mode, setMode] = useState<'cron' | 'interval'>('cron')
  const [cron, setCron] = useState('*/5 * * * *')
  const [interval, setInterval] = useState(60)
  const [submitting, setSubmitting] = useState(false)

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setSubmitting(true)
    try {
      await scheduledTasksApi.createTask({
        name,
        cronExpression: mode === 'cron' ? cron : undefined,
        intervalSeconds: mode === 'interval' ? interval : undefined,
      })
      onCreated()
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <form onSubmit={handleSubmit} className="bg-zinc-900 border border-zinc-800 rounded-xl p-4 space-y-4">
      <h3 className="text-sm font-semibold text-zinc-100">Criar Task</h3>
      <div className="grid grid-cols-2 gap-4">
        <div>
          <label className="block text-xs text-zinc-400 mb-1">Nome</label>
          <input
            value={name}
            onChange={e => setName(e.target.value)}
            required
            className="w-full px-3 py-2 text-sm bg-zinc-800 border border-zinc-700 rounded-lg text-zinc-100"
            placeholder="health-check-api"
          />
        </div>
        <div>
          <label className="block text-xs text-zinc-400 mb-1">Modo</label>
          <select
            value={mode}
            onChange={e => setMode(e.target.value as 'cron' | 'interval')}
            className="w-full px-3 py-2 text-sm bg-zinc-800 border border-zinc-700 rounded-lg text-zinc-100"
          >
            <option value="cron">CRON Expression</option>
            <option value="interval">Intervalo (segundos)</option>
          </select>
        </div>
      </div>
      {mode === 'cron' ? (
        <div>
          <label className="block text-xs text-zinc-400 mb-1">CRON</label>
          <input
            value={cron}
            onChange={e => setCron(e.target.value)}
            className="w-full px-3 py-2 text-sm bg-zinc-800 border border-zinc-700 rounded-lg text-zinc-100 font-mono"
            placeholder="*/5 * * * *"
          />
        </div>
      ) : (
        <div>
          <label className="block text-xs text-zinc-400 mb-1">Intervalo (s)</label>
          <input
            type="number"
            min={1}
            value={interval}
            onChange={e => setInterval(Number(e.target.value))}
            className="w-full px-3 py-2 text-sm bg-zinc-800 border border-zinc-700 rounded-lg text-zinc-100"
          />
        </div>
      )}
      <div className="flex justify-end gap-2">
        <button type="submit" disabled={submitting || !name} className="px-4 py-2 text-sm bg-blue-600 text-white rounded-lg hover:bg-blue-700 disabled:opacity-50">
          {submitting ? 'Criando...' : 'Criar Task'}
        </button>
      </div>
    </form>
  )
}

// ═══════════════════════════════════════════════════════════
// Rules Tab
// ═══════════════════════════════════════════════════════════

function RulesTab() {
  const [rules, setRules] = useState<TriggerRule[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [showCreate, setShowCreate] = useState(false)

  const refresh = useCallback(async () => {
    try {
      setError(null)
      setLoading(true)
      const data = await scheduledTasksApi.listRules()
      setRules(data)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erro ao carregar rules')
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => { refresh() }, [refresh])

  const handleToggle = async (rule: TriggerRule) => {
    if (rule.enabled) {
      await scheduledTasksApi.disableRule(rule.id)
    } else {
      await scheduledTasksApi.enableRule(rule.id)
    }
    refresh()
  }

  const handleDelete = async (id: string) => {
    if (!confirm('Remover esta rule?')) return
    await scheduledTasksApi.deleteRule(id)
    refresh()
  }

  const handleEvaluate = async (id: string) => {
    await scheduledTasksApi.evaluateRule(id)
    refresh()
  }

  if (loading) return <PageLoading />
  if (error) return <PageError message={error} onRetry={refresh} />

  return (
    <div className="space-y-4">
      <div className="flex justify-between items-center">
        <p className="text-sm text-zinc-400">{rules.length} trigger rule(s)</p>
        <div className="flex gap-2">
          <button onClick={() => setShowCreate(!showCreate)} className="flex items-center gap-2 px-3 py-2 text-sm rounded-lg bg-blue-600 text-white hover:bg-blue-700">
            <Plus className="w-4 h-4" />
            Nova Rule
          </button>
          <button onClick={refresh} className="flex items-center gap-2 px-3 py-2 text-sm rounded-lg border border-zinc-700 text-zinc-300 hover:bg-zinc-800">
            <RefreshCw className="w-4 h-4" />
          </button>
        </div>
      </div>

      {showCreate && <CreateRuleForm onCreated={() => { setShowCreate(false); refresh() }} />}

      <div className="space-y-3">
        {rules.map(rule => (
          <div key={rule.id} className="bg-zinc-900 border border-zinc-800 rounded-xl p-4">
            <div className="flex items-center justify-between">
              <div>
                <div className="flex items-center gap-2">
                  <Zap className={cn('w-4 h-4', rule.enabled ? 'text-emerald-400' : 'text-zinc-600')} />
                  <h3 className="text-sm font-semibold text-zinc-100">{rule.name}</h3>
                  <Badge variant={rule.enabled ? 'success' : 'warning'}>{rule.enabled ? 'Ativa' : 'Desativada'}</Badge>
                </div>
                {rule.description && <p className="text-xs text-zinc-500 mt-1">{rule.description}</p>}
                <p className="text-xs text-zinc-500 mt-1">
                  Fonte: {rule.source.type} → {rule.source.endpoint} | Condição: {rule.condition.type} ({rule.condition.expression})
                </p>
                <p className="text-xs text-zinc-500">
                  Execuções: {rule.executionCount} {rule.lastTriggeredAt && `• Último: ${new Date(rule.lastTriggeredAt).toLocaleString()}`}
                </p>
              </div>
              <div className="flex items-center gap-2">
                <button onClick={() => handleToggle(rule)} className="p-1.5 rounded hover:bg-zinc-800 text-zinc-400" title={rule.enabled ? 'Desativar' : 'Ativar'}>
                  {rule.enabled ? <Pause className="w-4 h-4" /> : <Play className="w-4 h-4" />}
                </button>
                <button onClick={() => handleEvaluate(rule.id)} className="p-1.5 rounded hover:bg-zinc-800 text-blue-400" title="Avaliar agora">
                  <Zap className="w-4 h-4" />
                </button>
                <button onClick={() => handleDelete(rule.id)} className="p-1.5 rounded hover:bg-zinc-800 text-red-400" title="Remover">
                  <Trash2 className="w-4 h-4" />
                </button>
              </div>
            </div>
          </div>
        ))}
        {rules.length === 0 && (
          <div className="text-center py-12 text-zinc-500">Nenhuma trigger rule configurada.</div>
        )}
      </div>
    </div>
  )
}

// ═══════════════════════════════════════════════════════════
// Create Rule Form
// ═══════════════════════════════════════════════════════════

function CreateRuleForm({ onCreated }: { onCreated: () => void }) {
  const [name, setName] = useState('')
  const [description, setDescription] = useState('')
  const [sourceType, setSourceType] = useState<TriggerSourceType>('Http' as TriggerSourceType)
  const [endpoint, setEndpoint] = useState('')
  const [conditionType, setConditionType] = useState<ConditionType>('JsonPath' as ConditionType)
  const [expression, setExpression] = useState('')
  const [expectedValue, setExpectedValue] = useState('')
  const [schedule, setSchedule] = useState('')
  const [submitting, setSubmitting] = useState(false)

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setSubmitting(true)
    try {
      await scheduledTasksApi.createRule({
        id: '',
        name,
        description: description || undefined,
        schedule: schedule || undefined,
        source: { type: sourceType, endpoint },
        condition: { type: conditionType, expression, expectedValue: expectedValue || undefined },
        enabled: true,
        createdAt: new Date().toISOString(),
        executionCount: 0,
      })
      onCreated()
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <form onSubmit={handleSubmit} className="bg-zinc-900 border border-zinc-800 rounded-xl p-4 space-y-4">
      <h3 className="text-sm font-semibold text-zinc-100">Criar Trigger Rule</h3>
      <div className="grid grid-cols-2 gap-4">
        <div>
          <label className="block text-xs text-zinc-400 mb-1">Nome</label>
          <input value={name} onChange={e => setName(e.target.value)} required className="w-full px-3 py-2 text-sm bg-zinc-800 border border-zinc-700 rounded-lg text-zinc-100" placeholder="api-health-monitor" />
        </div>
        <div>
          <label className="block text-xs text-zinc-400 mb-1">Schedule (CRON, opcional)</label>
          <input value={schedule} onChange={e => setSchedule(e.target.value)} className="w-full px-3 py-2 text-sm bg-zinc-800 border border-zinc-700 rounded-lg text-zinc-100 font-mono" placeholder="*/10 * * * *" />
        </div>
      </div>
      <div>
        <label className="block text-xs text-zinc-400 mb-1">Descrição</label>
        <input value={description} onChange={e => setDescription(e.target.value)} className="w-full px-3 py-2 text-sm bg-zinc-800 border border-zinc-700 rounded-lg text-zinc-100" placeholder="Monitora status da API de pagamento" />
      </div>
      <div className="grid grid-cols-2 gap-4">
        <div>
          <label className="block text-xs text-zinc-400 mb-1">Tipo de Fonte</label>
          <select value={sourceType} onChange={e => setSourceType(e.target.value as TriggerSourceType)} className="w-full px-3 py-2 text-sm bg-zinc-800 border border-zinc-700 rounded-lg text-zinc-100">
            <option value="Http">HTTP</option>
            <option value="Queue">Queue</option>
            <option value="FileSystem">File System</option>
            <option value="Database">Database</option>
          </select>
        </div>
        <div>
          <label className="block text-xs text-zinc-400 mb-1">Endpoint / URL</label>
          <input value={endpoint} onChange={e => setEndpoint(e.target.value)} required className="w-full px-3 py-2 text-sm bg-zinc-800 border border-zinc-700 rounded-lg text-zinc-100" placeholder="https://api.example.com/health" />
        </div>
      </div>
      <div className="grid grid-cols-3 gap-4">
        <div>
          <label className="block text-xs text-zinc-400 mb-1">Tipo de Condição</label>
          <select value={conditionType} onChange={e => setConditionType(e.target.value as ConditionType)} className="w-full px-3 py-2 text-sm bg-zinc-800 border border-zinc-700 rounded-lg text-zinc-100">
            <option value="JsonPath">JSONPath</option>
            <option value="Threshold">Threshold</option>
            <option value="Regex">Regex</option>
            <option value="Contains">Contains</option>
          </select>
        </div>
        <div>
          <label className="block text-xs text-zinc-400 mb-1">Expressão</label>
          <input value={expression} onChange={e => setExpression(e.target.value)} required className="w-full px-3 py-2 text-sm bg-zinc-800 border border-zinc-700 rounded-lg text-zinc-100 font-mono" placeholder="$.status" />
        </div>
        <div>
          <label className="block text-xs text-zinc-400 mb-1">Valor Esperado</label>
          <input value={expectedValue} onChange={e => setExpectedValue(e.target.value)} className="w-full px-3 py-2 text-sm bg-zinc-800 border border-zinc-700 rounded-lg text-zinc-100" placeholder="healthy" />
        </div>
      </div>
      <div className="flex justify-end">
        <button type="submit" disabled={submitting || !name || !endpoint || !expression} className="px-4 py-2 text-sm bg-blue-600 text-white rounded-lg hover:bg-blue-700 disabled:opacity-50">
          {submitting ? 'Criando...' : 'Criar Rule'}
        </button>
      </div>
    </form>
  )
}

// ═══════════════════════════════════════════════════════════
// Channels Tab
// ═══════════════════════════════════════════════════════════

function ChannelsTab() {
  const [channels, setChannels] = useState<ChannelInfo[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [testChannel, setTestChannel] = useState<string | null>(null)
  const [testConfig, setTestConfig] = useState<Record<string, string>>({})
  const [testResult, setTestResult] = useState<string | null>(null)

  const refresh = useCallback(async () => {
    try {
      setError(null)
      setLoading(true)
      const data = await scheduledTasksApi.listChannels()
      setChannels(data)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erro ao carregar channels')
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => { refresh() }, [refresh])

  const handleTest = async (name: string) => {
    try {
      const result = await scheduledTasksApi.testChannel(name, testConfig)
      setTestResult(`Status: ${result.status} | Attempts: ${result.attempts}${result.errorMessage ? ` | Error: ${result.errorMessage}` : ''}`)
    } catch (err) {
      setTestResult(err instanceof Error ? err.message : 'Erro no teste')
    }
  }

  if (loading) return <PageLoading />
  if (error) return <PageError message={error} onRetry={refresh} />

  const channelConfigs: Record<string, string[]> = {
    webhook: ['webhookUrl'],
    email: ['smtpHost', 'smtpPort', 'smtpUser', 'smtpPassword', 'fromAddress', 'toAddress', 'useSsl'],
    push: ['fcmServerKey', 'deviceToken', 'title'],
  }

  return (
    <div className="space-y-4">
      <div className="flex justify-between items-center">
        <p className="text-sm text-zinc-400">{channels.length} canal(is) de entrega disponível(is)</p>
        <button onClick={refresh} className="flex items-center gap-2 px-3 py-2 text-sm rounded-lg border border-zinc-700 text-zinc-300 hover:bg-zinc-800">
          <RefreshCw className="w-4 h-4" />
        </button>
      </div>

      <div className="space-y-3">
        {channels.map(ch => (
          <div key={ch.name} className="bg-zinc-900 border border-zinc-800 rounded-xl p-4 space-y-3">
            <div className="flex items-center justify-between">
              <div className="flex items-center gap-3">
                <Send className={cn('w-5 h-5', ch.healthy ? 'text-emerald-400' : 'text-red-400')} />
                <div>
                  <h3 className="text-sm font-semibold text-zinc-100 capitalize">{ch.name}</h3>
                  <p className="text-xs text-zinc-500">{ch.healthy ? 'Conectado' : 'Indisponível'}</p>
                </div>
              </div>
              <div className="flex items-center gap-2">
                <Badge variant={ch.healthy ? 'success' : 'danger'}>{ch.healthy ? 'Healthy' : 'Unhealthy'}</Badge>
                <button
                  onClick={() => setTestChannel(testChannel === ch.name ? null : ch.name)}
                  className="px-3 py-1.5 text-xs rounded-lg border border-zinc-700 text-zinc-300 hover:bg-zinc-800"
                >
                  Testar
                </button>
              </div>
            </div>

            {testChannel === ch.name && (
              <div className="border-t border-zinc-800 pt-3 space-y-3">
                <p className="text-xs text-zinc-400">Configuração de teste para canal <span className="font-mono">{ch.name}</span>:</p>
                <div className="grid grid-cols-2 gap-2">
                  {(channelConfigs[ch.name] || []).map(key => (
                    <div key={key}>
                      <label className="block text-xs text-zinc-500 mb-0.5">{key}</label>
                      <input
                        type={key.toLowerCase().includes('password') || key.toLowerCase().includes('key') ? 'password' : 'text'}
                        value={testConfig[key] || ''}
                        onChange={e => setTestConfig(prev => ({ ...prev, [key]: e.target.value }))}
                        className="w-full px-2 py-1.5 text-xs bg-zinc-800 border border-zinc-700 rounded text-zinc-100 font-mono"
                        placeholder={key}
                      />
                    </div>
                  ))}
                </div>
                <div className="flex items-center gap-3">
                  <button
                    onClick={() => handleTest(ch.name)}
                    className="px-3 py-1.5 text-xs bg-blue-600 text-white rounded hover:bg-blue-700"
                  >
                    Enviar Teste
                  </button>
                  {testResult && <p className="text-xs text-zinc-400">{testResult}</p>}
                </div>
              </div>
            )}
          </div>
        ))}
        {channels.length === 0 && (
          <div className="text-center py-12 text-zinc-500">Nenhum canal de entrega registrado.</div>
        )}
      </div>
    </div>
  )
}

// ═══════════════════════════════════════════════════════════
// Health Tab
// ═══════════════════════════════════════════════════════════

function HealthTab() {
  const [health, setHealth] = useState<ScheduledTasksHealthReport | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  const refresh = useCallback(async () => {
    try {
      setError(null)
      setLoading(true)
      const data = await scheduledTasksApi.health()
      setHealth(data)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erro ao carregar health')
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => { refresh() }, [refresh])

  if (loading) return <PageLoading />
  if (error || !health) return <PageError message={error ?? 'Sem dados'} onRetry={refresh} />

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-3">
          <Activity className="w-5 h-5 text-blue-400" />
          <h2 className="text-lg font-medium text-zinc-100">Health Report</h2>
          <Badge variant={health.overallHealthy ? 'success' : 'danger'}>
            {health.overallHealthy ? 'Saudável' : 'Degradado'}
          </Badge>
        </div>
        <button onClick={refresh} className="flex items-center gap-2 px-3 py-2 text-sm rounded-lg border border-zinc-700 text-zinc-300 hover:bg-zinc-800">
          <RefreshCw className="w-4 h-4" />
        </button>
      </div>

      <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
        <StatCard label="Total Tasks" value={health.totalTasks} />
        <StatCard label="Ativas" value={health.activeTasks} color="text-emerald-400" />
        <StatCard label="Pausadas" value={health.pausedTasks} color="text-amber-400" />
        <StatCard label="Falhas" value={health.failedTasks} color={health.failedTasks > 0 ? 'text-red-400' : 'text-zinc-400'} />
      </div>

      <div className="grid grid-cols-2 gap-4">
        <StatCard label="Total Rules" value={health.totalRules} />
        <StatCard label="Rules Ativas" value={health.enabledRules} color="text-emerald-400" />
      </div>

      <div>
        <h3 className="text-sm font-medium text-zinc-300 mb-3">Canais de Entrega</h3>
        <div className="grid grid-cols-3 gap-3">
          {health.channels.map(ch => (
            <div key={ch.name} className={cn('p-3 rounded-lg border', ch.healthy ? 'border-emerald-900/50 bg-emerald-950/20' : 'border-red-900/50 bg-red-950/20')}>
              <div className="flex items-center gap-2">
                {ch.healthy ? <CheckCircle2 className="w-4 h-4 text-emerald-400" /> : <XCircle className="w-4 h-4 text-red-400" />}
                <span className="text-sm text-zinc-100 capitalize">{ch.name}</span>
              </div>
            </div>
          ))}
        </div>
      </div>
    </div>
  )
}

// ═══════════════════════════════════════════════════════════
// Shared components
// ═══════════════════════════════════════════════════════════

function StatCard({ label, value, color }: { label: string; value: number; color?: string }) {
  return (
    <div className="bg-zinc-900 border border-zinc-800 rounded-xl p-4">
      <p className="text-xs text-zinc-500">{label}</p>
      <p className={cn('text-2xl font-bold mt-1', color ?? 'text-zinc-100')}>{value}</p>
    </div>
  )
}

function StatusIcon({ status }: { status: ScheduledTaskStatus | string }) {
  switch (status) {
    case 'Active':
      return <CheckCircle2 className="w-5 h-5 text-emerald-400" />
    case 'Paused':
      return <Pause className="w-5 h-5 text-amber-400" />
    case 'Failed':
      return <XCircle className="w-5 h-5 text-red-400" />
    default:
      return <AlertCircle className="w-5 h-5 text-zinc-400" />
  }
}

function statusVariant(status: ScheduledTaskStatus | string): 'success' | 'warning' | 'danger' | 'default' {
  switch (status) {
    case 'Active': return 'success'
    case 'Paused': return 'warning'
    case 'Failed': return 'danger'
    default: return 'default'
  }
}
