import { useState, useEffect, useRef } from 'react'
import { X, Bot, Shield, Wrench, FileCode, CheckCircle2, AlertTriangle, Search } from 'lucide-react'
import type { AgentInfo, AgentSpecification, ToolSummary, YamlValidationError } from '@/types/api'
import { TierLabels, AutonomyLevel, AutonomyLabels, AutonomyColors } from '@/types/api'
import { toolApi, agentApi } from '@/lib/api'
import { cn } from '@/lib/utils'

interface Props {
  agent: AgentInfo | null
  onSave: (spec: AgentSpecification, yaml?: string) => Promise<void>
  onClose: () => void
}

export function AgentFormModal({ agent, onSave, onClose }: Props) {
  const [activeTab, setActiveTab] = useState<'visual' | 'yaml'>('visual')
  
  // --- Estado do Formulário Visual ---
  const [form, setForm] = useState<AgentSpecification>({
    name: agent?.name ?? '',
    description: agent?.description ?? '',
    domain: agent?.domain ?? '',
    tier: agent?.tier ?? 1,
    systemPrompt: agent?.systemPrompt ?? '',
    capabilities: agent?.capabilities ?? [],
    maxConcurrency: agent?.maxConcurrency ?? 5,
    timeoutSeconds: agent?.timeoutSeconds ?? 30,
    allowedTools: agent?.toolNames ?? [],
    autonomyLevel: agent?.autonomyLevel ?? AutonomyLevel.Supervised,
  })

  const [capInput, setCapInput] = useState('')
  const [saving, setSaving] = useState(false)

  // --- Estado das Ferramentas (Tools) ---
  const [availableTools, setAvailableTools] = useState<ToolSummary[]>([])
  const [toolSearch, setToolSearch] = useState('')
  const [toolDropdownOpen, setToolDropdownOpen] = useState(false)
  const dropdownRef = useRef<HTMLDivElement>(null)

  // --- Estado do Editor YAML ---
  const [yamlText, setYamlText] = useState('')
  const [yamlValid, setYamlValid] = useState(true)
  const [yamlErrors, setYamlErrors] = useState<YamlValidationError[]>([])
  const [validatingYaml, setValidatingYaml] = useState(false)
  
  // Ref para controlar chamadas de validação concorrentes
  const validationTimeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null)

  // Carregar ferramentas disponíveis do backend
  useEffect(() => {
    toolApi.list()
      .then(setAvailableTools)
      .catch(err => console.error('Erro ao carregar ferramentas:', err))
  }, [])

  // Fechar dropdown de ferramentas ao clicar fora
  useEffect(() => {
    function handleClickOutside(event: MouseEvent) {
      if (dropdownRef.current && !dropdownRef.current.contains(event.target as Node)) {
        setToolDropdownOpen(false)
      }
    }
    document.addEventListener('mousedown', handleClickOutside)
    return () => document.removeEventListener('mousedown', handleClickOutside)
  }, [])

  // Helper para converter o estado do formulário para uma string YAML elegante
  const serializeFormToYaml = (spec: AgentSpecification): string => {
    const lines: string[] = []
    lines.push(`# Microsoft Agent Framework (MAF) - Agent Specification`)
    lines.push(`name: "${spec.name || ''}"`)
    lines.push(`description: "${spec.description || ''}"`)
    lines.push(`tier: ${spec.tier ?? 1}`)
    lines.push(`domain: "${spec.domain || ''}"`)
    lines.push(`autonomyLevel: ${spec.autonomyLevel ?? AutonomyLevel.Supervised}`)
    lines.push(`maxConcurrency: ${spec.maxConcurrency ?? 5}`)
    lines.push(`timeoutSeconds: ${spec.timeoutSeconds ?? 30}`)
    
    if (spec.capabilities && spec.capabilities.length > 0) {
      lines.push('capabilities:')
      spec.capabilities.forEach(cap => {
        lines.push(`  - "${cap}"`)
      })
    } else {
      lines.push('capabilities: []')
    }

    if (spec.allowedTools && spec.allowedTools.length > 0) {
      lines.push('allowedTools:')
      spec.allowedTools.forEach(tool => {
        lines.push(`  - "${tool}"`)
      })
    } else {
      lines.push('allowedTools: []')
    }

    if (spec.systemPrompt) {
      lines.push('systemPrompt: |')
      const promptLines = spec.systemPrompt.split('\n')
      promptLines.forEach(line => {
        lines.push(`  ${line}`)
      })
    } else {
      lines.push('systemPrompt: ""')
    }

    return lines.join('\n')
  }

  // Sincronizar abas com conversão bidirecional inteligente
  const handleTabChange = async (tab: 'visual' | 'yaml') => {
    if (tab === 'yaml') {
      // Converte o estado atual do visual para YAML
      const generatedYaml = serializeFormToYaml(form)
      setYamlText(generatedYaml)
      setYamlErrors([])
      setYamlValid(true)
      setActiveTab('yaml')
    } else {
      // Ao voltar para a aba visual, valida e extrai a especificação do YAML se ele for válido
      if (yamlValid && yamlText.trim()) {
        try {
          setValidatingYaml(true)
          const result = await agentApi.validateYaml(yamlText)
          if (result.isValid && result.specification) {
            const spec = result.specification
            setForm({
              name: spec.name || form.name,
              description: spec.description || form.description,
              domain: spec.domain || form.domain,
              tier: spec.tier ?? form.tier,
              systemPrompt: spec.instructions || spec.systemPrompt || form.systemPrompt,
              capabilities: spec.capabilities || form.capabilities,
              maxConcurrency: spec.maxConcurrency || form.maxConcurrency,
              timeoutSeconds: spec.timeoutSeconds || form.timeoutSeconds,
              allowedTools: spec.allowedTools || form.allowedTools,
              autonomyLevel: spec.autonomyLevel ?? form.autonomyLevel,
            })
            setActiveTab('visual')
          } else {
            // Se o YAML for inválido, não permite alternar e mostra aviso
            alert('Não é possível voltar ao Formulário Visual enquanto houver erros no YAML.')
          }
        } catch (err) {
          console.error('Erro na validação de transição:', err)
        } finally {
          setValidatingYaml(false)
        }
      } else if (!yamlText.trim()) {
        setActiveTab('visual')
      } else {
        alert('Por favor, corrija os erros do seu YAML declarativo antes de voltar.')
      }
    }
  }

  // Validação em Tempo Real com Debounce para a aba YAML
  const handleYamlChange = (text: string) => {
    setYamlText(text)
    setYamlValid(false) // assume inválido até a validação passar
    setValidatingYaml(true)

    if (validationTimeoutRef.current) {
      clearTimeout(validationTimeoutRef.current)
    }

    validationTimeoutRef.current = setTimeout(async () => {
      try {
        if (!text.trim()) {
          setYamlErrors([])
          setYamlValid(true)
          setValidatingYaml(false)
          return
        }
        const result = await agentApi.validateYaml(text)
        setYamlValid(result.isValid)
        setYamlErrors(result.errors || [])
      } catch (err) {
        console.error('Falha ao validar YAML declarativo:', err)
      } finally {
        setValidatingYaml(false)
      }
    }, 500)
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setSaving(true)
    try {
      if (activeTab === 'yaml') {
        // Salva diretamente usando a string YAML para gerar versão histórica no banco
        await onSave(form, yamlText)
      } else {
        // Salva passando a especificação visual tradicional
        await onSave(form)
      }
    } finally {
      setSaving(false)
    }
  }

  // Adicionar/Remover capabilities
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

  // Adicionar/Remover ferramentas permitidas (AllowedTools)
  const toggleTool = (toolName: string) => {
    setForm(prev => {
      const tools = prev.allowedTools || []
      const updated = tools.includes(toolName)
        ? tools.filter(t => t !== toolName)
        : [...tools, toolName]
      return { ...prev, allowedTools: updated }
    })
  }

  // Agrupar ferramentas disponíveis por categoria
  const groupedTools = availableTools.reduce<Record<string, ToolSummary[]>>((acc, tool) => {
    const cat = tool.category || 'Outros'
    if (!acc[cat]) acc[cat] = []
    acc[cat].push(tool)
    return acc
  }, {})

  // Filtra ferramentas baseadas no texto de busca do select
  const filteredCategories = Object.keys(groupedTools).reduce<Record<string, ToolSummary[]>>((acc, cat) => {
    const matches = groupedTools[cat].filter(t => 
      t.name.toLowerCase().includes(toolSearch.toLowerCase()) || 
      (t.description && t.description.toLowerCase().includes(toolSearch.toLowerCase()))
    )
    if (matches.length > 0) {
      acc[cat] = matches
    }
    return acc
  }, {})

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center">
      <div className="absolute inset-0 bg-black/75 backdrop-blur-sm" onClick={onClose} />
      
      <div className="relative bg-zinc-950 border border-zinc-800 rounded-xl w-full max-w-2xl max-h-[92vh] overflow-y-auto shadow-2xl flex flex-col">
        
        {/* Header com Abas Unificadas */}
        <div className="sticky top-0 bg-zinc-950 px-6 pt-5 pb-3 border-b border-zinc-900 flex flex-col gap-4 z-10">
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-2.5">
              <div className="w-8 h-8 rounded-lg bg-teal-500/10 border border-teal-500/20 flex items-center justify-center text-teal-400">
                <Bot className="w-4 h-4" />
              </div>
              <h2 className="text-base font-semibold text-zinc-100">
                {agent ? `Editar Configurações de ${agent.name}` : 'Criar Novo Agente de IA'}
              </h2>
            </div>
            <button onClick={onClose} className="text-zinc-500 hover:text-zinc-300 transition-colors">
              <X className="w-5 h-5" />
            </button>
          </div>

          {/* Abas Deslizantes (Visual vs Declarativo) */}
          <div className="flex bg-zinc-900/60 p-1 rounded-lg border border-zinc-800 self-start">
            <button
              type="button"
              onClick={() => handleTabChange('visual')}
              className={cn(
                'flex items-center gap-2 px-4 py-1.5 text-xs font-medium rounded-md transition-all duration-200',
                activeTab === 'visual'
                  ? 'bg-zinc-800 text-teal-400 border border-zinc-700/50 shadow-sm'
                  : 'text-zinc-400 hover:text-zinc-200'
              )}
            >
              <Wrench className="w-3.5 h-3.5" />
              Formulário Visual
            </button>
            <button
              type="button"
              onClick={() => handleTabChange('yaml')}
              className={cn(
                'flex items-center gap-2 px-4 py-1.5 text-xs font-medium rounded-md transition-all duration-200',
                activeTab === 'yaml'
                  ? 'bg-zinc-800 text-teal-400 border border-zinc-700/50 shadow-sm'
                  : 'text-zinc-400 hover:text-zinc-200'
              )}
            >
              <FileCode className="w-3.5 h-3.5" />
              YAML Declarativo (Avançado)
            </button>
          </div>
        </div>

        {/* Corpo do Modal */}
        <form onSubmit={handleSubmit} className="p-6 flex-1 overflow-y-auto space-y-6">
          
          {activeTab === 'visual' ? (
            /* =========================================================================
               ABA FORMULÁRIO VISUAL
               ========================================================================= */
            <div className="space-y-5 animate-fadeIn">
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <Field label="Nome do Agente" required>
                  <input
                    type="text"
                    value={form.name}
                    onChange={e => setForm(prev => ({ ...prev, name: e.target.value }))}
                    disabled={!!agent}
                    placeholder="ex: DataAnalyst"
                    className="input h-10 bg-zinc-900/50 border-zinc-800 text-sm focus:border-teal-500 text-zinc-100 rounded-lg w-full"
                    required
                  />
                </Field>
                <Field label="Domínio Operacional" required>
                  <input
                    type="text"
                    value={form.domain}
                    onChange={e => setForm(prev => ({ ...prev, domain: e.target.value }))}
                    placeholder="ex: Analytics, Finance, Dev"
                    className="input h-10 bg-zinc-900/50 border-zinc-800 text-sm focus:border-teal-500 text-zinc-100 rounded-lg w-full"
                    required
                  />
                </Field>
              </div>

              <Field label="Descrição Detalhada" required>
                <textarea
                  value={form.description}
                  onChange={e => setForm(prev => ({ ...prev, description: e.target.value }))}
                  placeholder="Explique detalhadamente o papel operacional e o objetivo deste agente."
                  className="input min-h-[70px] bg-zinc-900/50 border-zinc-800 text-sm focus:border-teal-500 text-zinc-100 rounded-lg w-full"
                  required
                />
              </Field>

              <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
                <Field label="Hierarquia (Tier)">
                  <select
                    value={form.tier}
                    onChange={e => setForm(prev => ({ ...prev, tier: Number(e.target.value) as AgentSpecification['tier'] }))}
                    className="input h-10 bg-zinc-900/50 border-zinc-800 text-sm focus:border-teal-500 text-zinc-100 rounded-lg w-full"
                  >
                    {[0, 1, 2, 3].map(t => (
                      <option key={t} value={t}>{TierLabels[t]} Agent</option>
                    ))}
                  </select>
                </Field>

                <Field label="Concorrência Máxima">
                  <input
                    type="number"
                    min={1}
                    max={50}
                    value={form.maxConcurrency}
                    onChange={e => setForm(prev => ({ ...prev, maxConcurrency: Number(e.target.value) }))}
                    className="input h-10 bg-zinc-900/50 border-zinc-800 text-sm focus:border-teal-500 text-zinc-100 rounded-lg w-full"
                  />
                </Field>

                <Field label="Timeout Limite (s)">
                  <input
                    type="number"
                    min={1}
                    max={600}
                    value={form.timeoutSeconds}
                    onChange={e => setForm(prev => ({ ...prev, timeoutSeconds: Number(e.target.value) }))}
                    className="input h-10 bg-zinc-900/50 border-zinc-800 text-sm focus:border-teal-500 text-zinc-100 rounded-lg w-full"
                  />
                </Field>
              </div>

              {/* CONTROLE DE AUTONOMIA (Rich Step Slider L0-L5) */}
              <div className="p-4 bg-zinc-900/40 border border-zinc-850 rounded-xl space-y-4">
                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-2">
                    <Shield className="w-4 h-4 text-teal-400" />
                    <span className="text-xs font-semibold text-zinc-300 uppercase tracking-wider">Políticas de Autonomia & Consentimento</span>
                  </div>
                  <span className={cn(
                    'px-2 py-0.5 text-xs font-semibold rounded-md border shadow-sm transition-all duration-300',
                    AutonomyColors[form.autonomyLevel ?? 2]
                  )}>
                    {AutonomyLabels[form.autonomyLevel ?? 2]}
                  </span>
                </div>

                {/* Slider */}
                <div className="relative pt-2 pb-1">
                  <input
                    type="range"
                    min={0}
                    max={5}
                    value={form.autonomyLevel ?? 2}
                    onChange={e => setForm(prev => ({ ...prev, autonomyLevel: Number(e.target.value) as AutonomyLevel }))}
                    className="w-full h-1.5 bg-zinc-800 rounded-lg appearance-none cursor-pointer accent-teal-500 focus:outline-none"
                  />
                  <div className="flex justify-between text-[10px] text-zinc-500 font-semibold mt-2 px-1">
                    <span>L0</span>
                    <span>L1</span>
                    <span>L2</span>
                    <span>L3</span>
                    <span>L4</span>
                    <span>L5</span>
                  </div>
                </div>

                {/* Descritivo Dinâmico da Autonomia */}
                <div className="text-xs bg-zinc-950/50 border border-zinc-900/50 rounded-lg p-3 text-zinc-400 transition-colors duration-200">
                  {form.autonomyLevel === 0 && (
                    <p>🧑‍💻 <strong>L0 - Manual:</strong> O agente trabalha estritamente de forma assistiva, formulando sugestões e ações. Nenhuma ferramenta é disparada sem aprovação humana direta.</p>
                  )}
                  {form.autonomyLevel === 1 && (
                    <p>🔍 <strong>L1 - Assistido:</strong> Executa ações de leitura em bancos ou arquivos automaticamente, mas todas as operações de escrita exigem verificação humana prévia.</p>
                  )}
                  {form.autonomyLevel === 2 && (
                    <p>🛡️ <strong>L2 - Supervisionado (Padrão):</strong> Executa de forma autônoma tarefas comuns de leitura e escrita de baixo impacto. Pede aprovação expressa para ferramentas de médio e alto risco.</p>
                  )}
                  {form.autonomyLevel === 3 && (
                    <p>⚡ <strong>L3 - Semi-Autônomo:</strong> Realiza fluxos e execuções operacionais completas. Apenas ações identificadas como críticas ou destrutivas requerem consentimento humano.</p>
                  )}
                  {form.autonomyLevel === 4 && (
                    <p>🚀 <strong>L4 - Autônomo:</strong> Autonomia operacional completa para tomar decisões complexas. Envia alertas instantâneos de conformidade em caso de ações críticas, sem bloquear o fluxo.</p>
                  )}
                  {form.autonomyLevel === 5 && (
                    <p className="text-red-400 font-medium">⚠️ <strong>L5 - Autonomia Total:</strong> Sem amarras ou necessidade de consentimento. Dispara e executa ferramentas livremente, apenas escrevendo logs. Use com cautela absoluta em produção.</p>
                  )}
                </div>
              </div>

              {/* COMBOBOX BADGE MULTI-SELECT CATEGORIZADO DE TOOLS */}
              <Field label="Ferramentas Autorizadas (Allowed Tools)">
                <div className="relative" ref={dropdownRef}>
                  
                  {/* Visualização de Badges Ativas + Campo de Trigger */}
                  <div 
                    onClick={() => setToolDropdownOpen(prev => !prev)}
                    className="min-h-[42px] p-2 bg-zinc-900/50 border border-zinc-800 rounded-lg flex flex-wrap gap-1.5 items-center cursor-pointer hover:border-zinc-700 transition-colors"
                  >
                    {form.allowedTools && form.allowedTools.length > 0 ? (
                      form.allowedTools.map(t => (
                        <span 
                          key={t} 
                          className="inline-flex items-center gap-1.5 pl-2 pr-1.5 py-0.5 text-xs bg-zinc-800/80 border border-zinc-700/50 rounded text-zinc-300 hover:bg-zinc-750 transition-colors"
                          onClick={(e) => { e.stopPropagation(); toggleTool(t); }}
                        >
                          {t}
                          <button type="button" className="text-zinc-500 hover:text-red-400 font-bold">×</button>
                        </span>
                      ))
                    ) : (
                      <span className="text-sm text-zinc-500 px-1">Selecione as ferramentas autorizadas para este agente...</span>
                    )}
                  </div>

                  {/* Dropdown com Categorias e Filtro */}
                  {toolDropdownOpen && (
                    <div className="absolute top-[105%] left-0 right-0 mt-1 bg-zinc-950 border border-zinc-800 rounded-lg shadow-2xl z-20 max-h-64 overflow-y-auto p-3 space-y-3">
                      <div className="relative">
                        <Search className="absolute left-2.5 top-1/2 -translate-y-1/2 w-3.5 h-3.5 text-zinc-500" />
                        <input
                          type="text"
                          placeholder="Filtrar ferramentas..."
                          value={toolSearch}
                          onChange={e => setToolSearch(e.target.value)}
                          className="w-full pl-8 pr-3 py-1.5 text-xs bg-zinc-900 border border-zinc-800 rounded text-zinc-200 focus:outline-none focus:border-teal-500"
                        />
                      </div>

                      <div className="space-y-2 max-h-48 overflow-y-auto">
                        {Object.keys(filteredCategories).length > 0 ? (
                          Object.keys(filteredCategories).map(cat => (
                            <div key={cat} className="space-y-1">
                              <h4 className="text-[10px] font-semibold text-zinc-500 uppercase tracking-wider pl-1">{cat}</h4>
                              <div className="grid grid-cols-1 sm:grid-cols-2 gap-1">
                                {filteredCategories[cat].map(tool => {
                                  const isSelected = form.allowedTools?.includes(tool.name) ?? false
                                  return (
                                    <button
                                      type="button"
                                      key={tool.id}
                                      onClick={() => toggleTool(tool.name)}
                                      className={cn(
                                        'flex flex-col items-start p-2 rounded text-left border text-xs transition-colors',
                                        isSelected
                                          ? 'bg-teal-500/10 border-teal-500/40 text-teal-300'
                                          : 'bg-zinc-900/30 border-transparent text-zinc-400 hover:bg-zinc-900/60'
                                      )}
                                    >
                                      <span className="font-medium text-zinc-200">{tool.name}</span>
                                      {tool.description && (
                                        <span className="text-[10px] text-zinc-500 line-clamp-1">{tool.description}</span>
                                      )}
                                    </button>
                                  )
                                })}
                              </div>
                            </div>
                          ))
                        ) : (
                          <p className="text-[11px] text-zinc-500 text-center py-2">Nenhuma ferramenta encontrada para a busca.</p>
                        )}
                      </div>
                    </div>
                  )}
                </div>
              </Field>

              {/* CAPABILITIES */}
              <Field label="Capacidades Operacionais (Capabilities)">
                <div className="flex gap-2 mb-2">
                  <input
                    type="text"
                    value={capInput}
                    onChange={e => setCapInput(e.target.value)}
                    onKeyDown={e => { if (e.key === 'Enter') { e.preventDefault(); addCapability() } }}
                    placeholder="Adicionar capacidade (ex: code-execution, file-write)"
                    className="input h-10 bg-zinc-900/50 border-zinc-800 text-sm focus:border-teal-500 text-zinc-100 rounded-lg flex-1"
                  />
                  <button 
                    type="button" 
                    onClick={addCapability} 
                    className="px-4 bg-zinc-800 border border-zinc-700 hover:bg-zinc-700 text-zinc-300 text-sm font-semibold rounded-lg hover:text-white transition-colors"
                  >
                    Adicionar
                  </button>
                </div>
                <div className="flex flex-wrap gap-1.5">
                  {form.capabilities.map(c => (
                    <span key={c} className="inline-flex items-center gap-1.5 pl-2.5 pr-1.5 py-0.5 text-xs bg-zinc-900 border border-zinc-800 rounded-md text-zinc-300">
                      {c}
                      <button type="button" onClick={() => removeCapability(c)} className="text-zinc-500 hover:text-red-400 font-bold">×</button>
                    </span>
                  ))}
                  {form.capabilities.length === 0 && (
                    <p className="text-xs text-zinc-500 italic pl-1">Sem capabilities cadastradas.</p>
                  )}
                </div>
              </Field>

              {/* SYSTEM PROMPT / INSTRUCTIONS */}
              <Field label="System Prompt / Diretrizes Operacionais (System Prompt)">
                <textarea
                  value={form.systemPrompt}
                  onChange={e => setForm(prev => ({ ...prev, systemPrompt: e.target.value }))}
                  placeholder="Defina o prompt mestre do sistema, restrições operacionais e regras comportamentais do agente..."
                  className="input min-h-[140px] font-mono text-[11px] leading-relaxed bg-zinc-900/50 border-zinc-800 text-zinc-300 focus:border-teal-500 rounded-lg w-full"
                />
              </Field>
            </div>
          ) : (
            /* =========================================================================
               ABA EDITOR YAML DECLARATIVO
               ========================================================================= */
            <div className="space-y-4 animate-fadeIn flex flex-col h-full min-h-[400px]">
              <div className="flex items-center justify-between text-xs text-zinc-400 bg-zinc-900/40 p-3 rounded-lg border border-zinc-900">
                <p>💡 <strong>Edição Declarativa:</strong> Edite diretamente as propriedades declarativas do agente. O formato aceita Block Scalars (como `|` no prompt de sistema), compatível com o MAF 1.5.0.</p>
              </div>

              {/* Editor Textarea de Alta Fidelidade */}
              <div className="relative flex-1 border border-zinc-800 rounded-lg overflow-hidden flex flex-col bg-zinc-950 font-mono text-[11px]">
                <div className="flex items-center justify-between px-4 py-2 bg-zinc-900/70 border-b border-zinc-850">
                  <span className="text-[10px] text-zinc-500 font-semibold uppercase tracking-wider flex items-center gap-1.5">
                    <FileCode className="w-3.5 h-3.5 text-teal-400" />
                    agent_specification.yaml
                  </span>
                  
                  {/* Indicador de Validação */}
                  {validatingYaml ? (
                    <span className="text-[10px] text-yellow-500 flex items-center gap-1">
                      <span className="w-1.5 h-1.5 bg-yellow-500 rounded-full animate-ping" />
                      Validando no Backend...
                    </span>
                  ) : yamlValid ? (
                    <span className="text-[10px] text-emerald-400 flex items-center gap-1">
                      <CheckCircle2 className="w-3.5 h-3.5" />
                      YAML Válido
                    </span>
                  ) : (
                    <span className="text-[10px] text-red-400 flex items-center gap-1">
                      <AlertTriangle className="w-3.5 h-3.5" />
                      Sintaxe Inválida
                    </span>
                  )}
                </div>

                <div className="flex flex-1 min-h-[250px]">
                  {/* Linha Lateral Vermelha se Houver Erro */}
                  {!yamlValid && yamlErrors.length > 0 && (
                    <div className="w-1 bg-red-600 self-stretch" title="Contém erros estruturais" />
                  )}
                  <textarea
                    value={yamlText}
                    onChange={e => handleYamlChange(e.target.value)}
                    className="flex-1 p-4 bg-zinc-950 text-zinc-300 font-mono text-[11px] leading-relaxed border-0 focus:outline-none focus:ring-0 resize-none h-[320px] overflow-y-auto"
                    placeholder="# Insira ou edite sua configuração YAML declarativa"
                    spellCheck={false}
                  />
                </div>
              </div>

              {/* Painel Flutuante/Rodapé de Erros do YAML */}
              {!yamlValid && yamlErrors.length > 0 && (
                <div className="p-4 bg-red-950/25 border border-red-900/40 rounded-xl space-y-2">
                  <h3 className="text-xs font-semibold text-red-400 flex items-center gap-1.5">
                    <AlertTriangle className="w-4 h-4" />
                    Especificação Inválida ({yamlErrors.length} erro encontrado)
                  </h3>
                  <div className="space-y-1">
                    {yamlErrors.map((err, i) => (
                      <p key={i} className="text-xs text-red-300/90 leading-relaxed font-mono">
                        🔴 Linha {err.line}, Coluna {err.column}: <span className="font-sans text-zinc-300">{err.message}</span>
                      </p>
                    ))}
                  </div>
                </div>
              )}
            </div>
          )}

          {/* Rodapé de Ações */}
          <div className="flex justify-end gap-3 pt-4 border-t border-zinc-900">
            <button 
              type="button" 
              onClick={onClose} 
              className="px-4 py-2 text-xs font-medium rounded-lg border border-zinc-800 text-zinc-400 hover:bg-zinc-900 hover:text-zinc-200 transition-all duration-200"
            >
              Cancelar
            </button>
            <button 
              type="submit" 
              disabled={saving || (activeTab === 'yaml' && !yamlValid) || validatingYaml} 
              className={cn(
                'px-4 py-2 text-xs font-semibold rounded-lg text-white transition-all duration-200 flex items-center gap-1.5 shadow-md shadow-teal-500/10',
                (activeTab === 'yaml' && !yamlValid) || validatingYaml
                  ? 'bg-zinc-800 text-zinc-500 border border-zinc-850 cursor-not-allowed shadow-none'
                  : 'bg-teal-600 hover:bg-teal-500 active:scale-[0.98]'
              )}
            >
              {saving ? 'Gravando Alterações...' : agent ? 'Gravar Alterações' : 'Criar Novo Agente'}
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}

function Field({ label, required, children }: { label: string; required?: boolean; children: React.ReactNode }) {
  return (
    <label className="block space-y-1.5">
      <span className="block text-[11px] font-semibold text-zinc-400 uppercase tracking-wider">
        {label} {required && <span className="text-red-400">*</span>}
      </span>
      {children}
    </label>
  )
}
