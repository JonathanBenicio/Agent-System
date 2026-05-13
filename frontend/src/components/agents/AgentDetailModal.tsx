import { useState, useEffect } from 'react'
import { X, Bot, History, RotateCcw, Clock, User, Eye, EyeOff, Code } from 'lucide-react'
import { Badge } from '@/components/shared/Badge'
import { TierLabels, AutonomyLabels, AutonomyColors } from '@/types/api'
import type { AgentVersion, AgentInfo } from '@/types/api'
import { agentApi } from '@/lib/api'
import { useToast } from '@/components/shared/Toast'
import { cn } from '@/lib/utils'

interface Props {
  agent: AgentInfo
  onClose: () => void
  onRefresh?: () => void
}

interface DiffLine {
  type: 'added' | 'removed' | 'unchanged'
  text: string
}

// Converte dinamicamente uma versão estruturada de agente de volta em YAML de especificação MAF 1.5.0
function serializeVersionToYaml(ver: AgentVersion): string {
  if (!ver) return ''
  const lines: string[] = []
  lines.push(`# Microsoft Agent Framework (MAF) - Agent Specification (v${ver.versionNumber})`)
  lines.push(`name: "${ver.agentName || ''}"`)
  lines.push(`description: "${ver.description || ''}"`)
  
  // O autonomyLevel costuma estar nos parameters, ou mapeado na versão
  const autonomy = typeof ver.parameters?.autonomyLevel === 'number' 
    ? ver.parameters.autonomyLevel 
    : 2 // default Supervised
  lines.push(`autonomyLevel: ${autonomy}`)
  
  if (ver.tools && ver.tools.length > 0) {
    lines.push('allowedTools:')
    ver.tools.forEach(tool => {
      lines.push(`  - "${tool}"`)
    })
  } else {
    lines.push('allowedTools: []')
  }

  if (ver.systemPrompt) {
    lines.push('systemPrompt: |')
    const promptLines = ver.systemPrompt.split('\n')
    promptLines.forEach(line => {
      lines.push(`  ${line}`)
    })
  } else {
    lines.push('systemPrompt: ""')
  }

  return lines.join('\n')
}

export function AgentDetailModal({ agent, onClose, onRefresh }: Props) {
  const [activeTab, setActiveTab] = useState<'details' | 'history'>('details')
  const { addToast } = useToast()

  // --- Estado do Histórico de Versões ---
  const [versions, setVersions] = useState<AgentVersion[]>([])
  const [loadingHistory, setLoadingHistory] = useState(false)
  const [selectedDiffVersion, setSelectedDiffVersion] = useState<AgentVersion | null>(null)
  const [rollingBackId, setRollingBackId] = useState<string | null>(null)

  // Carrega o histórico de versões quando a aba de histórico é ativada
  useEffect(() => {
    if (activeTab === 'history') {
      setLoadingHistory(true)
      agentApi.getHistory(agent.name)
        .then(data => {
          // Ordena decrescente pelo número da versão para que as mais novas apareçam no topo
          const sorted = [...data].sort((a, b) => b.versionNumber - a.versionNumber)
          setVersions(sorted)
        })
        .catch(err => {
          console.error('Erro ao carregar histórico:', err)
          addToast('Erro ao carregar histórico de versões', 'error')
        })
        .finally(() => {
          setLoadingHistory(false)
        })
    }
  }, [activeTab, agent.name])

  // Algoritmo de diff inline simples e extremamente rápido para visualização estruturada de YAML
  const computeYamlDiff = (oldYaml: string, newYaml: string): DiffLine[] => {
    const oldLines = (oldYaml || '').split('\n')
    const newLines = (newYaml || '').split('\n')
    const diff: DiffLine[] = []
    
    let o = 0
    let n = 0
    
    while (o < oldLines.length || n < newLines.length) {
      if (o < oldLines.length && n < newLines.length) {
        if (oldLines[o] === newLines[n]) {
          diff.push({ type: 'unchanged', text: oldLines[o] })
          o++
          n++
        } else {
          // Look-ahead simples para casar linhas adicionadas ou removidas
          let foundMatch = false
          for (let i = 1; i <= 5; i++) {
            if (o + i < oldLines.length && oldLines[o + i] === newLines[n]) {
              for (let j = 0; j < i; j++) {
                diff.push({ type: 'removed', text: oldLines[o + j] })
              }
              o += i
              foundMatch = true
              break
            }
            if (n + i < newLines.length && oldLines[o] === newLines[n + i]) {
              for (let j = 0; j < i; j++) {
                diff.push({ type: 'added', text: newLines[n + j] })
              }
              n += i
              foundMatch = true
              break
            }
          }
          
          if (!foundMatch) {
            diff.push({ type: 'removed', text: oldLines[o] })
            diff.push({ type: 'added', text: newLines[n] })
            o++
            n++
          }
        }
      } else if (o < oldLines.length) {
        diff.push({ type: 'removed', text: oldLines[o] })
        o++
      } else if (n < newLines.length) {
        diff.push({ type: 'added', text: newLines[n] })
        n++
      }
    }
    
    return diff
  }

  // Executar o Rollback de Versão Histórica
  const handleRollback = async (version: AgentVersion) => {
    if (rollingBackId) return
    
    const confirmMsg = `Confirmar Rollback para a Versão v${version.versionNumber}? O agente será restaurado para estas configurações.`
    if (!window.confirm(confirmMsg)) return

    setRollingBackId(version.id)
    try {
      await agentApi.rollback(agent.name, version.id)
      addToast(`Agente restaurado com sucesso para a Versão v${version.versionNumber}`, 'success')
      
      // Fecha modal de diff ativo
      setSelectedDiffVersion(null)
      
      // Recarrega o histórico
      if (onRefresh) {
        onRefresh()
      }
      
      // Força a recarga do histórico de versões fechando e reabrindo a aba
      setActiveTab('details')
      setTimeout(() => setActiveTab('history'), 100)
    } catch (err) {
      console.error('Erro no rollback:', err)
      addToast('Erro ao restaurar versão do agente', 'error')
    } finally {
      setRollingBackId(null)
    }
  }

  // Encontra a versão ativa atual na lista para comparar
  const activeVersion = versions.find(v => v.status === 1) || versions[0]

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center">
      <div className="absolute inset-0 bg-black/75 backdrop-blur-sm" onClick={onClose} />
      <div className="relative bg-zinc-950 border border-zinc-850 rounded-xl w-full max-w-2xl max-h-[92vh] overflow-y-auto shadow-2xl flex flex-col">
        
        {/* Cabeçalho do Detalhe com Tabs */}
        <div className="sticky top-0 bg-zinc-950 px-6 pt-5 pb-3 border-b border-zinc-900 flex flex-col gap-4 z-10">
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-3">
              <div className={cn('flex items-center justify-center w-10 h-10 rounded-xl border', 
                agent.tier === 0 ? 'bg-zinc-900 border-zinc-700 text-zinc-300' :
                agent.tier === 1 ? 'bg-teal-500/10 border-teal-500/20 text-teal-400' :
                agent.tier === 2 ? 'bg-sky-500/10 border-sky-500/20 text-sky-400' :
                'bg-emerald-500/10 border-emerald-500/20 text-emerald-400'
              )}>
                <Bot className="w-5 h-5" />
              </div>
              <div>
                <h2 className="text-base font-semibold text-zinc-100 flex items-center gap-2">
                  {agent.name}
                  <span className="text-[10px] px-1.5 py-0.5 rounded-md bg-zinc-900 border border-zinc-800 text-zinc-400 font-normal">
                    {agent.domain}
                  </span>
                </h2>
                <p className="text-xs text-zinc-500">Visualização de Configurações e Logs de Modificação</p>
              </div>
            </div>
            <button onClick={onClose} className="text-zinc-500 hover:text-zinc-300 transition-colors">
              <X className="w-5 h-5" />
            </button>
          </div>

          {/* Abas */}
          <div className="flex bg-zinc-900/50 p-1 rounded-lg border border-zinc-900 self-start">
            <button
              onClick={() => setActiveTab('details')}
              className={cn(
                'flex items-center gap-2 px-4 py-1.5 text-xs font-medium rounded-md transition-all duration-200',
                activeTab === 'details'
                  ? 'bg-zinc-800 text-teal-400 border border-zinc-700/50 shadow-sm'
                  : 'text-zinc-400 hover:text-zinc-200'
              )}
            >
              <Bot className="w-3.5 h-3.5" />
              Especificações Ativas
            </button>
            <button
              onClick={() => setActiveTab('history')}
              className={cn(
                'flex items-center gap-2 px-4 py-1.5 text-xs font-medium rounded-md transition-all duration-200',
                activeTab === 'history'
                  ? 'bg-zinc-800 text-teal-400 border border-zinc-700/50 shadow-sm'
                  : 'text-zinc-400 hover:text-zinc-200'
              )}
            >
              <History className="w-3.5 h-3.5" />
              Histórico & Versionamento
            </button>
          </div>
        </div>

        {/* Corpo do Modal de acordo com a aba */}
        <div className="p-6 flex-1 overflow-y-auto">
          {activeTab === 'details' ? (
            /* =========================================================================
               ABA DETALHES OPERACIONAIS (ATIVO)
               ========================================================================= */
            <div className="space-y-6 animate-fadeIn">
              <div className="flex flex-wrap gap-2">
                <Badge variant="teal">{TierLabels[agent.tier]}</Badge>
                <Badge variant={agent.isActive ? 'success' : 'default'}>
                  {agent.isActive ? 'Operational' : 'Maintenance'}
                </Badge>
                <span className={cn(
                  'px-2 py-0.5 text-xs font-semibold rounded-md border shadow-sm',
                  AutonomyColors[agent.autonomyLevel ?? 2]
                )}>
                  {AutonomyLabels[agent.autonomyLevel ?? 2]}
                </span>
              </div>

              <Section title="Descrição Operacional">
                <p className="text-sm text-zinc-300 leading-relaxed bg-zinc-900/20 border border-zinc-900 rounded-xl p-4">{agent.description}</p>
              </Section>

              {agent.capabilities && agent.capabilities.length > 0 && (
                <Section title="Capabilities Autorizadas">
                  <div className="flex flex-wrap gap-1.5">
                    {agent.capabilities.map(c => (
                      <span key={c} className="px-2.5 py-1 text-xs bg-zinc-900 border border-zinc-850 rounded-lg text-zinc-300 font-medium">
                        {c}
                      </span>
                    ))}
                  </div>
                </Section>
              )}

              {agent.systemPrompt && (
                <Section title="System Prompt Ativo">
                  <pre className="text-xs text-zinc-300 font-mono bg-zinc-950 border border-zinc-900 rounded-xl p-4 overflow-x-auto whitespace-pre-wrap max-h-64 leading-relaxed">
                    {agent.systemPrompt}
                  </pre>
                </Section>
              )}

              <Section title="Métricas Operacionais">
                <div className="grid grid-cols-2 sm:grid-cols-4 gap-3 text-sm">
                  <ConfigItem label="Máxima Concorrência" value={`${agent.maxConcurrency} canais`} />
                  <ConfigItem label="Timeout Limite" value={`${agent.timeoutSeconds}s`} />
                  <ConfigItem label="Ferramentas Úteis" value={`${agent.toolNames?.length ?? 0} ativas`} />
                  <ConfigItem label="Habilidades Integradas" value={`${agent.skillNames?.length ?? 0} skills`} />
                </div>
              </Section>

              {agent.toolNames && agent.toolNames.length > 0 && (
                <Section title="Ferramentas Conectadas (Allowed Tools)">
                  <div className="flex flex-wrap gap-1.5">
                    {agent.toolNames.map(t => (
                      <span key={t} className="px-2.5 py-1 text-xs bg-teal-500/5 border border-teal-500/10 rounded-lg text-teal-400 font-medium">
                        {t}
                      </span>
                    ))}
                  </div>
                </Section>
              )}
            </div>
          ) : (
            /* =========================================================================
               ABA HISTÓRICO E VERSIONAMENTO
               ========================================================================= */
            <div className="space-y-6 animate-fadeIn">
              
              {/* Carregamento */}
              {loadingHistory ? (
                <div className="flex flex-col items-center justify-center py-16 space-y-3">
                  <div className="w-8 h-8 rounded-full border-2 border-teal-500/20 border-t-teal-500 animate-spin" />
                  <p className="text-xs text-zinc-500 font-medium">Lendo histórico de alterações do banco de dados...</p>
                </div>
              ) : versions.length === 0 ? (
                <div className="text-center py-16 text-zinc-500 text-xs italic">
                  Nenhuma versão histórica encontrada para este agente no banco.
                </div>
              ) : (
                <div className="space-y-6">
                  
                  {/* Timeline Vertical */}
                  <div className="relative pl-6 border-l border-zinc-850 space-y-6">
                    {versions.map((ver) => {
                      const isCurrent = ver.status === 1
                      const isOpenDiff = selectedDiffVersion?.id === ver.id
                      const dateObj = new Date(ver.createdAt)
                      const formattedDate = dateObj.toLocaleDateString('pt-BR', {
                        day: '2-digit',
                        month: 'short',
                        year: 'numeric',
                        hour: '2-digit',
                        minute: '2-digit'
                      })

                      return (
                        <div key={ver.id} className="relative">
                          
                          {/* Indicador na Timeline */}
                          <div className={cn(
                            'absolute -left-[31px] top-1.5 w-4.5 h-4.5 rounded-full border-2 flex items-center justify-center transition-all duration-300',
                            isCurrent 
                              ? 'bg-zinc-950 border-emerald-500 shadow-lg shadow-emerald-500/20 scale-110' 
                              : 'bg-zinc-900 border-zinc-800'
                          )}>
                            <div className={cn(
                              'w-2 h-2 rounded-full',
                              isCurrent ? 'bg-emerald-500' : 'bg-zinc-650'
                            )} />
                          </div>

                          {/* Bloco de Informações da Versão */}
                          <div className={cn(
                            'p-4 border rounded-xl transition-all duration-200',
                            isCurrent 
                              ? 'bg-emerald-950/10 border-emerald-500/20' 
                              : 'bg-zinc-900/30 border-zinc-900/60 hover:bg-zinc-900/50 hover:border-zinc-800'
                          )}>
                            
                            <div className="flex flex-wrap items-center justify-between gap-3 mb-2">
                              <div className="flex items-center gap-2">
                                <span className={cn(
                                  'text-xs font-bold px-2 py-0.5 rounded',
                                  isCurrent ? 'bg-emerald-500/20 text-emerald-400' : 'bg-zinc-800 text-zinc-400'
                                )}>
                                  v{ver.versionNumber}
                                </span>
                                {isCurrent && (
                                  <span className="text-[9px] font-bold px-1.5 py-0.5 bg-emerald-500/10 text-emerald-400 border border-emerald-500/20 rounded uppercase">
                                    Ativo
                                  </span>
                                )}
                              </div>

                              <div className="flex items-center gap-3 text-xs text-zinc-500">
                                <span className="flex items-center gap-1">
                                  <Clock className="w-3 h-3" />
                                  {formattedDate}
                                </span>
                                <span className="flex items-center gap-1">
                                  <User className="w-3 h-3" />
                                  {ver.createdBy || 'Sistema'}
                                </span>
                              </div>
                            </div>

                            {/* Changelog */}
                            <p className="text-xs text-zinc-400 mb-4 font-medium pl-1">
                              {ver.changeLog || 'Alteração realizada via painel administrativo.'}
                            </p>

                            {/* Ações da Versão */}
                            <div className="flex items-center gap-2">
                              {!isCurrent && (
                                <button
                                  type="button"
                                  onClick={() => handleRollback(ver)}
                                  disabled={!!rollingBackId}
                                  className="flex items-center gap-1.5 px-3 py-1.5 bg-zinc-800 hover:bg-zinc-750 text-zinc-300 text-xs font-semibold rounded-lg border border-zinc-700 hover:text-white transition-all duration-200"
                                >
                                  <RotateCcw className="w-3 h-3 text-teal-400" />
                                  {rollingBackId === ver.id ? 'Restaurando...' : 'Restaurar Versão'}
                                </button>
                              )}

                              <button
                                type="button"
                                onClick={() => setSelectedDiffVersion(isOpenDiff ? null : ver)}
                                className="flex items-center gap-1.5 px-3 py-1.5 bg-zinc-900 hover:bg-zinc-800 text-zinc-400 hover:text-zinc-200 text-xs font-semibold rounded-lg border border-zinc-850 transition-all duration-200"
                              >
                                {isOpenDiff ? <EyeOff className="w-3 h-3" /> : <Eye className="w-3 h-3 text-teal-400" />}
                                {isOpenDiff ? 'Ocultar Diferenças' : 'Visualizar Diferenças'}
                              </button>
                            </div>

                            {/* Painel de Exibição de Diff Inline */}
                            {isOpenDiff && (
                              <div className="mt-4 border border-zinc-800 rounded-xl bg-zinc-950 overflow-hidden animate-slideDown">
                                <div className="px-4 py-2.5 bg-zinc-900 border-b border-zinc-850 flex items-center justify-between text-[10px] text-zinc-500 font-mono">
                                  <span className="flex items-center gap-1">
                                    <Code className="w-3.5 h-3.5 text-teal-400" />
                                    YAML COMPARATOR (v{ver.versionNumber} ↔ Atual)
                                  </span>
                                  <span className="flex items-center gap-3">
                                    <span className="text-red-400 flex items-center gap-1">
                                      <span className="w-2 h-2 bg-red-500/20 border border-red-500 rounded-sm inline-block" /> Removido
                                    </span>
                                    <span className="text-emerald-400 flex items-center gap-1">
                                      <span className="w-2 h-2 bg-emerald-500/20 border border-emerald-500 rounded-sm inline-block" /> Adicionado
                                    </span>
                                  </span>
                                </div>
                                <div className="max-h-72 overflow-y-auto p-4 font-mono text-[10px] leading-relaxed select-text space-y-0.5">
                                  {computeYamlDiff(serializeVersionToYaml(ver), serializeVersionToYaml(activeVersion)).map((line, lIdx) => (
                                    <div 
                                      key={lIdx} 
                                      className={cn(
                                        'px-2 py-0.5 rounded font-mono',
                                        line.type === 'removed' ? 'bg-red-950/20 text-red-300 border-l-2 border-red-500/70' :
                                        line.type === 'added' ? 'bg-emerald-950/20 text-emerald-300 border-l-2 border-emerald-500/70' :
                                        'text-zinc-500'
                                      )}
                                    >
                                      <span className="opacity-40 select-none mr-2">
                                        {line.type === 'removed' ? '-' : line.type === 'added' ? '+' : ' '}
                                      </span>
                                      {line.text}
                                    </div>
                                  ))}
                                </div>
                              </div>
                            )}

                          </div>
                        </div>
                      )
                    })}
                  </div>

                </div>
              )}

            </div>
          )}
        </div>
      </div>
    </div>
  )
}

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <div className="space-y-2">
      <h3 className="text-xs font-semibold text-zinc-500 uppercase tracking-wider pl-1">{title}</h3>
      {children}
    </div>
  )
}

function ConfigItem({ label, value }: { label: string; value: string | number }) {
  return (
    <div className="bg-zinc-900/50 border border-zinc-900 rounded-xl px-4 py-3 text-zinc-300 space-y-1">
      <p className="text-[10px] font-semibold text-zinc-500 uppercase tracking-wider">{label}</p>
      <p className="text-zinc-200 font-bold text-sm tracking-wide">{value}</p>
    </div>
  )
}
