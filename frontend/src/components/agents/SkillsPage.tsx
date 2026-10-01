import { useState, useCallback } from 'react'
import { 
  Sparkles, 
  Search, 
  Trash2, 
  Plus, 
  Download, 
  UploadCloud, 
  X, 
  ChevronRight, 
  Bot, 
  Send, 
  Info,
  CheckCircle,
  HelpCircle,
  FileText
} from 'lucide-react'
import { useSkills } from '@/hooks/useSkills'
import { PageLoading, PageError } from '@/components/shared/Loading'
import { Badge } from '@/components/shared/Badge'
import { ConfirmModal } from '@/components/shared/ConfirmModal'
import { useToast } from '@/components/shared/Toast'
import { skillApi } from '@/lib/api'

type BrainstormSuggestion = {
  suggestedId: string
  suggestedName: string
  systemPromptFragment: string
  fewShotExamples?: string
}

// Componente simples para renderizar Markdown no Preview de forma legível
function SimpleMarkdownPreview({ text }: { text: string }) {
  if (!text) return <em className="text-zinc-500 text-xs">Escreva algo no prompt para ver a pré-visualização...</em>

  // Transforma quebras de linha e tópicos em HTML amigável para exibição limpa
  const formatted = text
    .replace(/^### (.*$)/gim, '<h5 class="text-sm font-semibold text-teal-400 mt-3 mb-1">$1</h5>')
    .replace(/^## (.*$)/gim, '<h4 class="text-sm font-bold text-teal-400 mt-4 mb-2">$1</h4>')
    .replace(/^# (.*$)/gim, '<h3 class="text-base font-bold text-zinc-100 mt-4 mb-2 border-b border-zinc-800 pb-1">$1</h3>')
    .replace(/^[-]\s(.*$)/gim, '<li class="list-disc ml-4 text-xs text-zinc-300">$1</li>')
    .replace(/\*\*(.*?)\*\*/g, '<strong>$1</strong>')
    .replace(/`(.*?)`/g, '<code class="bg-zinc-800 px-1 py-0.5 rounded text-teal-300 font-mono text-[10px]">$1</code>')
    .split('\n').join('<br />')

  return <div className="text-xs text-zinc-300 leading-relaxed" dangerouslySetInnerHTML={{ __html: formatted }} />
}

export function SkillsPage() {
  const { skills, loading, error, refresh, deleteSkill, setSkillEnabled, createSkill, updateSkill, uploadSkill } = useSkills()
  const { addToast } = useToast()
  
  // State de Controle de Telas
  const [search, setSearch] = useState('')
  const [deleteTarget, setDeleteTarget] = useState<string | null>(null)
  
  // State de Modal (Criar / Editar)
  const [modalOpen, setModalOpen] = useState(false)
  const [modalMode, setModalMode] = useState<'create' | 'edit'>('create')
  const [activeTab, setActiveTab] = useState<'form' | 'upload' | 'brainstorm'>('form')
  
  // Form Fields State
  const [editingId, setEditingId] = useState('')
  const [skillId, setSkillId] = useState('')
  const [skillName, setSkillName] = useState('')
  const [skillDomain, setSkillDomain] = useState('general')
  const [skillType, setSkillType] = useState('Instruction')
  const [promptText, setPromptText] = useState('')
  const [examplesText, setExamplesText] = useState('')
  const [metadataCategory, setMetadataCategory] = useState('general')
  
  // File Upload State
  const [dragActive, setDragActive] = useState(false)
  const [selectedFile, setSelectedFile] = useState<File | null>(null)
  
  // Brainstorm Chat State
  const [brainstormDesc, setBrainstormDesc] = useState('')
  const [brainstormLogs, setBrainstormLogs] = useState<Array<{ sender: 'user' | 'ai'; text: string; data?: BrainstormSuggestion }>>([])
  const [isBrainstorming, setIsBrainstorming] = useState(false)

  // Reset de Formulário
  const resetForm = () => {
    setSkillId('')
    setSkillName('')
    setSkillDomain('general')
    setSkillType('Instruction')
    setPromptText('')
    setExamplesText('')
    setMetadataCategory('general')
    setSelectedFile(null)
    setEditingId('')
  }

  const handleOpenCreate = () => {
    resetForm()
    setModalMode('create')
    setActiveTab('form')
    setModalOpen(true)
  }

  const handleOpenEdit = async (id: string) => {
    try {
      const detail = await skillApi.get(id)
      setEditingId(detail.id)
      setSkillId(detail.id)
      setSkillName(detail.name)
      setSkillDomain(detail.domain || 'general')
      setSkillType(detail.type)
      setPromptText(detail.systemPrompt || '')
      setExamplesText(detail.examples || '')
      setMetadataCategory(detail.metadata?.category || 'general')
      
      setModalMode('edit')
      setActiveTab('form')
      setModalOpen(true)
    } catch {
      addToast('Erro ao carregar detalhes da skill', 'error')
    }
  }

  // Ações de Drag and Drop
  const handleDrag = useCallback((e: React.DragEvent) => {
    e.preventDefault()
    e.stopPropagation()
    if (e.type === "dragenter" || e.type === "dragover") {
      setDragActive(true)
    } else if (e.type === "dragleave") {
      setDragActive(false)
    }
  }, [])

  const handleDrop = useCallback((e: React.DragEvent) => {
    e.preventDefault()
    e.stopPropagation()
    setDragActive(false)
    if (e.dataTransfer.files && e.dataTransfer.files[0]) {
      const file = e.dataTransfer.files[0]
      if (file.name.endsWith('.md')) {
        setSelectedFile(file)
        addToast(`Arquivo ${file.name} carregado com sucesso`, 'success')
      } else {
        addToast('Apenas arquivos Markdown (.md) são permitidos', 'error')
      }
    }
  }, [addToast])

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    if (e.target.files && e.target.files[0]) {
      const file = e.target.files[0]
      if (file.name.endsWith('.md')) {
        setSelectedFile(file)
        addToast(`Arquivo ${file.name} carregado com sucesso`, 'success')
      } else {
        addToast('Apenas arquivos Markdown (.md) são permitidos', 'error')
      }
    }
  }

  // Execução de Criação/Edição
  const handleSubmitForm = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!skillId || !skillName) {
      addToast('ID e Nome são obrigatórios.', 'error')
      return
    }

    try {
      if (modalMode === 'create') {
        await createSkill({
          id: skillId.toLowerCase().trim(),
          name: skillName.trim(),
          domain: skillDomain,
          type: skillType,
          systemPromptFragment: promptText,
          fewShotExamples: examplesText,
          metadata: { category: metadataCategory, author: 'user' }
        })
        addToast('Habilidade cadastrada com sucesso no PostgreSQL', 'success')
      } else {
        await updateSkill(editingId, {
          name: skillName.trim(),
          domain: skillDomain,
          systemPromptFragment: promptText,
          fewShotExamples: examplesText,
          metadata: { category: metadataCategory }
        })
        addToast('Habilidade atualizada com sucesso', 'success')
      }
      setModalOpen(false)
      resetForm()
    } catch (err) {
      addToast(err instanceof Error ? err.message : 'Erro ao salvar habilidade', 'error')
    }
  }

  // Execução de Importação por Upload
  const handleUploadSubmit = async () => {
    if (!selectedFile) return
    try {
      await uploadSkill(selectedFile)
      addToast('Skill importada com sucesso no PostgreSQL', 'success')
      setModalOpen(false)
      resetForm()
    } catch (err) {
      addToast(err instanceof Error ? err.message : 'Erro ao realizar upload do arquivo', 'error')
    }
  }

  // Execução de Deletar
  const handleDelete = async () => {
    if (!deleteTarget) return
    try {
      await deleteSkill(deleteTarget)
      addToast('Habilidade removida do PostgreSQL com sucesso', 'success')
    } catch {
      addToast('Erro ao remover habilidade', 'error')
    }
    setDeleteTarget(null)
  }

  // Execução de Brainstorming AI
  const handleBrainstormSubmit = async () => {
    if (!brainstormDesc.trim()) return
    const userMsg = brainstormDesc
    setBrainstormLogs(prev => [...prev, { sender: 'user', text: userMsg }])
    setBrainstormDesc('')
    setIsBrainstorming(true)

    try {
      const res = await skillApi.brainstorm(userMsg)
      setBrainstormLogs(prev => [...prev, { 
        sender: 'ai', 
        text: `Com base em sua descrição, elaborei as seguintes diretrizes para o seu agente corporativo. Veja se te atende:`,
        data: res
      }])
    } catch {
      addToast('Falha na comunicação de brainstorming', 'error')
    } finally {
      setIsBrainstorming(false)
    }
  }

  const applyBrainstormResult = (data: BrainstormSuggestion) => {
    setSkillId(data.suggestedId || 'generated-skill')
    setSkillName(data.suggestedName || 'Habilidade Gerada')
    setPromptText(data.systemPromptFragment || '')
    setExamplesText(data.fewShotExamples || '')
    setActiveTab('form')
    addToast('Habilidade carregada no formulário de edição!', 'success')
  }

  // Filtragem da Lista Principal
  const filtered = skills.filter(
    s => !search || s.name.toLowerCase().includes(search.toLowerCase()) ||
      s.domain?.toLowerCase().includes(search.toLowerCase()) ||
      s.id.toLowerCase().includes(search.toLowerCase())
  )

  if (loading) return <PageLoading />
  if (error) return <PageError message={error} onRetry={refresh} />

  return (
    <div className="h-full overflow-y-auto bg-zinc-950 text-zinc-100">
      <div className="max-w-7xl mx-auto px-6 py-8 space-y-6">
        
        {/* Top Header */}
        <div className="flex flex-col md:flex-row md:items-center justify-between gap-4 border-b border-zinc-800 pb-5">
          <div>
            <h1 className="text-xl font-bold text-zinc-100 tracking-tight flex items-center gap-2">
              <Sparkles className="w-5 h-5 text-teal-400" />
              Catálogo de Habilidades (Skills)
            </h1>
            <p className="text-xs text-zinc-500 mt-1">
              Gerencie instruções de prompt dinâmicas e semeadas em banco de dados isolado por Tenant.
            </p>
          </div>

          <div className="flex items-center gap-3">
            <a 
              href="/api/agent/skills/template"
              download
              className="px-3.5 py-2 text-xs bg-zinc-900 border border-zinc-800 hover:border-zinc-700 text-zinc-300 rounded-lg flex items-center gap-2 transition-all font-medium"
            >
              <Download className="w-3.5 h-3.5" /> Template
            </a>
            
            {skills.some(skill => skill.canManage) && <button
              onClick={handleOpenCreate}
              className="px-4 py-2 text-xs bg-teal-600 hover:bg-teal-500 text-zinc-100 font-semibold rounded-lg flex items-center gap-1.5 shadow-md shadow-teal-950/40 transition-all"
            >
              <Plus className="w-4 h-4" /> Nova Skill
            </button>}
          </div>
        </div>

        {/* Search Filter bar */}
        <div className="relative max-w-md">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-zinc-500" />
          <input
            type="text"
            placeholder="Buscar por ID, Nome ou Domínio..."
            value={search}
            onChange={e => setSearch(e.target.value)}
            className="w-full pl-10 pr-4 py-2 bg-zinc-900/60 border border-zinc-800 rounded-lg text-sm text-zinc-200 placeholder-zinc-500 focus:outline-none focus:border-teal-500 focus:bg-zinc-900 transition-all"
          />
        </div>

        {/* Dashboard Grid */}
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-5">
          {filtered.map(skill => (
            <div 
              key={skill.id} 
              data-skill-id={skill.id}
              className="bg-zinc-900/40 border border-zinc-850 rounded-xl p-5 hover:border-zinc-700 hover:bg-zinc-900/60 transition-all flex flex-col justify-between"
            >
              <div>
                <div className="flex items-start justify-between mb-3">
                  <div className="flex items-center gap-2">
                    <Sparkles className="w-4 h-4 text-teal-400" />
                    <h3 className="text-sm font-semibold text-zinc-100 truncate max-w-[150px]">{skill.name}</h3>
                  </div>

                  <span className={`text-[10px] px-2 py-0.5 rounded-full font-medium border ${
                    skill.isSystem 
                      ? 'bg-zinc-800/40 text-zinc-400 border-zinc-750' 
                      : 'bg-teal-950/40 text-teal-300 border-teal-900/50'
                  }`}>
                    {skill.isSystem ? 'Sistema' : 'Customizada'}
                  </span>
                  <span className={`text-[10px] ${skill.isEnabled === false ? 'text-zinc-500' : 'text-teal-400'}`}>
                    {skill.isEnabled === false ? 'Desativada' : 'Ativa'}
                  </span>
                </div>

                <p className="text-[10px] font-mono text-zinc-500 mb-2 truncate">ID: {skill.id}</p>
                <p className="text-xs text-zinc-400 mb-4 line-clamp-3">
                  {skill.description || 'Habilidade customizada de prompt injetada dinamicamente.'}
                </p>
              </div>

              <div className="flex items-center justify-between pt-3 border-t border-zinc-850 mt-2">
                <div className="flex gap-2">
                  <Badge>{skill.domain || 'general'}</Badge>
                  <Badge variant="teal">{skill.type}</Badge>
                </div>

                <div className="flex items-center gap-1.5">
                  {skill.canManage && <button
                    onClick={async () => {
                      try {
                        await setSkillEnabled(skill.id, skill.isEnabled === false)
                        addToast(skill.isEnabled === false ? 'Skill ativada' : 'Skill desativada', 'success')
                      } catch {
                        addToast('Não foi possível alterar a skill', 'error')
                      }
                    }}
                    className="rounded-lg border border-zinc-700 px-2 py-1 text-[10px] text-zinc-300 hover:bg-zinc-800"
                  >{skill.isEnabled === false ? 'Ativar' : 'Desativar'}</button>}
                  {!skill.isSystem && skill.canManage ? (
                    <>
                      <button
                        onClick={() => handleOpenEdit(skill.id)}
                        className="p-1.5 rounded-lg text-zinc-400 hover:bg-zinc-800 hover:text-zinc-100 transition-colors"
                        title="Editar Habilidade"
                      >
                        <FileText className="w-3.5 h-3.5" />
                      </button>
                      <button
                        onClick={() => setDeleteTarget(skill.id)}
                        className="p-1.5 rounded-lg text-zinc-500 hover:bg-zinc-850 hover:text-red-400 transition-colors"
                        title="Excluir Habilidade"
                      >
                        <Trash2 className="w-3.5 h-3.5" />
                      </button>
                    </>
                  ) : (
                    <span className="text-[9px] text-zinc-600 font-mono tracking-wider uppercase bg-zinc-950 px-2 py-0.5 rounded border border-zinc-850">
                      Somente leitura
                    </span>
                  )}
                </div>
              </div>
            </div>
          ))}

          {filtered.length === 0 && (
            <div className="col-span-full text-center py-16 text-zinc-500 text-sm border border-dashed border-zinc-850 rounded-xl bg-zinc-950/20">
              <HelpCircle className="w-8 h-8 text-zinc-600 mx-auto mb-3" />
              Nenhuma habilidade cadastrada ou encontrada para a busca.
            </div>
          )}
        </div>
      </div>

      {/* CONFIRM DELETE MODAL */}
      <ConfirmModal
        open={!!deleteTarget}
        title="Excluir Habilidade"
        message="Tem certeza absoluta que deseja excluir esta habilidade do banco PostgreSQL? Isso afetará dinamicamente os agentes ativos."
        variant="danger"
        confirmLabel="Excluir"
        onConfirm={handleDelete}
        onCancel={() => setDeleteTarget(null)}
      />

      {/* MAIN CREATE / EDIT MULTI-TAB MODAL */}
      {modalOpen && (
        <div className="fixed inset-0 z-50 bg-black/75 backdrop-blur-sm flex items-center justify-center p-4">
          <div className="bg-zinc-900 border border-zinc-800 rounded-xl w-full max-w-5xl h-[650px] flex flex-col shadow-2xl overflow-hidden">
            
            {/* Modal Top Header */}
            <div className="px-6 py-4 border-b border-zinc-800 flex items-center justify-between">
              <div>
                <h2 className="text-md font-bold text-zinc-100 flex items-center gap-1.5">
                  <Sparkles className="w-4 h-4 text-teal-400" />
                  {modalMode === 'create' ? 'Cadastrar Habilidade' : 'Editar Habilidade'}
                </h2>
                <p className="text-[10px] text-zinc-500 mt-0.5">Configure prompt, escopo e comportamento da habilidade no Postgres.</p>
              </div>
              <button 
                onClick={() => setModalOpen(false)}
                className="p-1 rounded-lg text-zinc-500 hover:bg-zinc-800 hover:text-zinc-200"
              >
                <X className="w-4 h-4" />
              </button>
            </div>

            {/* Modal Tabs Selection */}
            <div className="px-6 bg-zinc-950 border-b border-zinc-800 flex items-center justify-between">
              <div className="flex gap-4">
                <button
                  onClick={() => setActiveTab('form')}
                  className={`py-3 text-xs font-semibold border-b-2 transition-all ${
                    activeTab === 'form' 
                      ? 'border-teal-500 text-teal-400' 
                      : 'border-transparent text-zinc-500 hover:text-zinc-300'
                  }`}
                >
                  Formulário de Cadastro
                </button>
                {modalMode === 'create' && (
                  <>
                    <button
                      onClick={() => setActiveTab('upload')}
                      className={`py-3 text-xs font-semibold border-b-2 transition-all ${
                        activeTab === 'upload' 
                          ? 'border-teal-500 text-teal-400' 
                          : 'border-transparent text-zinc-500 hover:text-zinc-300'
                      }`}
                    >
                      Importar Markdown (.md)
                    </button>
                    <button
                      onClick={() => setActiveTab('brainstorm')}
                      className={`py-3 text-xs font-semibold border-b-2 transition-all ${
                        activeTab === 'brainstorm' 
                          ? 'border-teal-500 text-teal-400' 
                          : 'border-transparent text-zinc-500 hover:text-zinc-300'
                      }`}
                    >
                      Brainstorming assistido por IA
                    </button>
                  </>
                )}
              </div>
              
              <div className="text-[10px] text-zinc-500 italic flex items-center gap-1.5">
                <Info className="w-3.5 h-3.5 text-zinc-600" />
                Gravado estritamente em Postgres
              </div>
            </div>

            {/* Modal Inner Content */}
            <div className="flex-1 overflow-hidden flex">
              
              {/* TAB 1: FORM INTERACTIVE SCREEN */}
              {activeTab === 'form' && (
                <form onSubmit={handleSubmitForm} className="flex-1 flex overflow-hidden">
                  
                  {/* Left Side: Fields */}
                  <div className="w-1/2 p-6 overflow-y-auto space-y-4 border-r border-zinc-850">
                    <div className="grid grid-cols-2 gap-4">
                      <div>
                        <label className="text-[11px] font-semibold text-zinc-400 block mb-1">Identificador da Skill (ID)</label>
                        <input
                          type="text"
                          disabled={modalMode === 'edit'}
                          value={skillId}
                          onChange={e => setSkillId(e.target.value.toLowerCase().replace(/[^a-z0-9-_]/g, ''))}
                          placeholder="ex: code-quality"
                          className="w-full bg-zinc-950 border border-zinc-800 rounded px-3 py-1.5 text-xs text-zinc-200 placeholder-zinc-600 focus:outline-none focus:border-teal-500 disabled:opacity-50 disabled:cursor-not-allowed"
                          required
                        />
                      </div>
                      <div>
                        <label className="text-[11px] font-semibold text-zinc-400 block mb-1">Nome de Exibição</label>
                        <input
                          type="text"
                          value={skillName}
                          onChange={e => setSkillName(e.target.value)}
                          placeholder="ex: Verificador de Clean Code"
                          className="w-full bg-zinc-950 border border-zinc-800 rounded px-3 py-1.5 text-xs text-zinc-200 placeholder-zinc-600 focus:outline-none focus:border-teal-500"
                          required
                        />
                      </div>
                    </div>

                    <div className="grid grid-cols-3 gap-4">
                      <div>
                        <label className="text-[11px] font-semibold text-zinc-400 block mb-1">Domínio</label>
                        <select
                          value={skillDomain}
                          onChange={e => setSkillDomain(e.target.value)}
                          className="w-full bg-zinc-950 border border-zinc-800 rounded px-3 py-1.5 text-xs text-zinc-200 focus:outline-none focus:border-teal-500"
                        >
                          <option value="general">Geral (General)</option>
                          <option value="work">Trabalho (Work)</option>
                          <option value="personal">Pessoal (Personal)</option>
                        </select>
                      </div>
                      <div>
                        <label className="text-[11px] font-semibold text-zinc-400 block mb-1">Tipo da Skill</label>
                        <select
                          value={skillType}
                          onChange={e => setSkillType(e.target.value)}
                          className="w-full bg-zinc-950 border border-zinc-800 rounded px-3 py-1.5 text-xs text-zinc-200 focus:outline-none focus:border-teal-500"
                        >
                          <option value="Instruction">Instrução (Prompt)</option>
                          <option value="Knowledge">Conhecimento (RAG)</option>
                          <option value="Template">Template</option>
                        </select>
                      </div>
                      <div>
                        <label className="text-[11px] font-semibold text-zinc-400 block mb-1">Categoria (Metadado)</label>
                        <select
                          value={metadataCategory}
                          onChange={e => setMetadataCategory(e.target.value)}
                          className="w-full bg-zinc-950 border border-zinc-800 rounded px-3 py-1.5 text-xs text-zinc-200 focus:outline-none focus:border-teal-500"
                        >
                          <option value="general">Geral</option>
                          <option value="development">Desenvolvimento</option>
                          <option value="writing">Escrita & Tom</option>
                          <option value="analytics">Análise de Dados</option>
                          <option value="management">Negócios</option>
                        </select>
                      </div>
                    </div>

                    <div>
                      <label className="text-[11px] font-semibold text-zinc-400 block mb-1">Prompt de Instruções (Fragmento de Sistema)</label>
                      <textarea
                        value={promptText}
                        onChange={e => setPromptText(e.target.value)}
                        placeholder="Escreva as diretrizes Markdown de sistema que definem a habilidade..."
                        className="w-full h-36 bg-zinc-950 border border-zinc-800 rounded px-3 py-2 text-xs text-zinc-250 placeholder-zinc-700 font-mono resize-none focus:outline-none focus:border-teal-500"
                        required
                      />
                    </div>

                    <div>
                      <label className="text-[11px] font-semibold text-zinc-400 block mb-1">Exemplos Few-Shot (Opcional)</label>
                      <textarea
                        value={examplesText}
                        onChange={e => setExamplesText(e.target.value)}
                        placeholder="Insira exemplos no formato 'User: ... \nAgent: ...' para treinar a LLM..."
                        className="w-full h-20 bg-zinc-950 border border-zinc-800 rounded px-3 py-2 text-xs text-zinc-250 placeholder-zinc-700 font-mono resize-none focus:outline-none focus:border-teal-500"
                      />
                    </div>

                    <div className="flex justify-end gap-2 pt-2">
                      <button
                        type="button"
                        onClick={() => setModalOpen(false)}
                        className="px-4 py-2 bg-zinc-800 hover:bg-zinc-750 text-zinc-300 font-semibold rounded text-xs transition-colors"
                      >
                        Cancelar
                      </button>
                      <button
                        type="submit"
                        className="px-5 py-2 bg-teal-600 hover:bg-teal-500 text-zinc-100 font-semibold rounded text-xs transition-colors shadow-md"
                      >
                        Salvar
                      </button>
                    </div>
                  </div>

                  {/* Right Side: Live Preview Panel */}
                  <div className="w-1/2 p-6 bg-zinc-950/40 overflow-y-auto flex flex-col justify-between">
                    <div>
                      <h4 className="text-[10px] font-bold text-zinc-500 uppercase tracking-widest mb-3 flex items-center gap-1.5 border-b border-zinc-850 pb-1.5">
                        <CheckCircle className="w-3.5 h-3.5 text-teal-500" />
                        Visualização em Tempo Real (Live Preview)
                      </h4>
                      
                      <div className="bg-zinc-950/80 border border-zinc-850 rounded-lg p-4 min-h-[350px]">
                        <SimpleMarkdownPreview text={promptText} />
                      </div>
                    </div>

                    <div className="text-[10px] text-zinc-500 bg-zinc-900/20 border border-zinc-850 p-2.5 rounded flex items-center gap-2 mt-4">
                      <Info className="w-4 h-4 text-teal-600" />
                      As instruções em Markdown acima serão injetadas dinamicamente no prompt do sistema do agente em cada turn da LLM.
                    </div>
                  </div>

                </form>
              )}

              {/* TAB 2: FILE UPLOAD ZONE */}
              {activeTab === 'upload' && (
                <div className="flex-1 p-8 flex flex-col items-center justify-center space-y-6">
                  <div 
                    onDragEnter={handleDrag}
                    onDragOver={handleDrag}
                    onDragLeave={handleDrag}
                    onDrop={handleDrop}
                    className={`w-full max-w-lg p-10 border-2 border-dashed rounded-xl flex flex-col items-center justify-center transition-all ${
                      dragActive 
                        ? 'border-teal-500 bg-teal-950/20' 
                        : selectedFile 
                          ? 'border-emerald-500 bg-emerald-950/10' 
                          : 'border-zinc-800 bg-zinc-950/20 hover:border-zinc-700'
                    }`}
                  >
                    <UploadCloud className={`w-12 h-12 mb-3 ${selectedFile ? 'text-emerald-400' : 'text-zinc-500'}`} />
                    
                    {selectedFile ? (
                      <div className="text-center">
                        <p className="text-sm font-semibold text-emerald-400">{selectedFile.name}</p>
                        <p className="text-xs text-zinc-500 mt-1">{(selectedFile.size / 1024).toFixed(2)} KB</p>
                      </div>
                    ) : (
                      <div className="text-center">
                        <p className="text-sm font-semibold text-zinc-300">Arraste seu arquivo Markdown (.md) aqui</p>
                        <p className="text-xs text-zinc-500 mt-1.5">Apenas arquivos contendo Frontmatter YAML demarcado por '---'</p>
                      </div>
                    )}

                    <input
                      type="file"
                      id="skill-file-input"
                      onChange={handleFileChange}
                      accept=".md"
                      className="hidden"
                    />
                    
                    <button
                      onClick={() => document.getElementById('skill-file-input')?.click()}
                      className="mt-6 px-4 py-2 bg-zinc-800 hover:bg-zinc-700 text-zinc-200 rounded font-semibold text-xs transition-colors"
                    >
                      Selecionar Arquivo
                    </button>
                  </div>

                  <div className="flex items-center gap-4">
                    <a
                      href="/api/agent/skills/template"
                      download
                      className="px-4 py-2 bg-zinc-900 border border-zinc-800 hover:border-zinc-750 text-zinc-300 font-semibold rounded text-xs flex items-center gap-2 transition-all"
                    >
                      <Download className="w-3.5 h-3.5" /> Baixar Template Markdown
                    </a>
                    
                    <button
                      disabled={!selectedFile}
                      onClick={handleUploadSubmit}
                      className="px-5 py-2 bg-teal-600 hover:bg-teal-500 disabled:opacity-50 disabled:cursor-not-allowed text-zinc-100 font-semibold rounded text-xs shadow-md transition-colors"
                    >
                      Importar para PostgreSQL
                    </button>
                  </div>
                </div>
              )}

              {/* TAB 3: AI BRAINSTORMING CANVAS */}
              {activeTab === 'brainstorm' && (
                <div className="flex-1 flex overflow-hidden">
                  
                  {/* Left Side: Idea and Chat */}
                  <div className="w-1/2 p-6 border-r border-zinc-850 flex flex-col justify-between">
                    <div className="space-y-4">
                      <div>
                        <h3 className="text-xs font-semibold text-zinc-400 flex items-center gap-1.5">
                          <Bot className="w-4 h-4 text-teal-400" />
                          Descreva o objetivo da Habilidade
                        </h3>
                        <p className="text-[10px] text-zinc-500 mt-0.5">Diga qual o escopo (ex: 'revisar código C# contra memory leak'). O assistente modelará o prompt.</p>
                      </div>

                      <div className="flex gap-2">
                        <input
                          type="text"
                          value={brainstormDesc}
                          onChange={e => setBrainstormDesc(e.target.value)}
                          onKeyDown={e => e.key === 'Enter' && handleBrainstormSubmit()}
                          placeholder="ex: ajudar o usuário a resumir e estruturar atas de reuniões..."
                          className="flex-1 bg-zinc-950 border border-zinc-850 rounded px-3 py-2 text-xs text-zinc-200 placeholder-zinc-600 focus:outline-none focus:border-teal-500"
                        />
                        <button
                          onClick={handleBrainstormSubmit}
                          disabled={isBrainstorming || !brainstormDesc.trim()}
                          className="px-3 bg-teal-600 hover:bg-teal-500 disabled:opacity-50 rounded text-zinc-100 transition-colors"
                        >
                          {isBrainstorming ? 'Gerando...' : <Send className="w-3.5 h-3.5" />}
                        </button>
                      </div>
                    </div>

                    {/* Chat Logs Window */}
                    <div className="flex-1 bg-zinc-950/60 border border-zinc-850 rounded-lg p-4 my-4 overflow-y-auto space-y-3 min-h-[220px]">
                      {brainstormLogs.length === 0 && (
                        <div className="text-center text-zinc-600 text-xs py-8">
                          Nenhuma interação iniciada. Descreva acima sua ideia!
                        </div>
                      )}
                      
                      {brainstormLogs.map((log, idx) => (
                        <div key={idx} className={`flex flex-col ${log.sender === 'user' ? 'items-end' : 'items-start'}`}>
                          <div className={`text-xs p-2.5 rounded-lg max-w-[85%] ${
                            log.sender === 'user' 
                              ? 'bg-teal-950 text-teal-300 rounded-tr-none' 
                              : 'bg-zinc-800 text-zinc-300 rounded-tl-none'
                          }`}>
                            {log.text}
                          </div>
                          
                          {log.data && (
                            <button
                              onClick={() => { if (log.data) applyBrainstormResult(log.data) }}
                              className="mt-2 px-3 py-1 bg-teal-600 hover:bg-teal-500 text-zinc-100 rounded text-[10px] font-semibold flex items-center gap-1 transition-all"
                            >
                              Aplicar e Editar no Formulário <ChevronRight className="w-3 h-3" />
                            </button>
                          )}
                        </div>
                      ))}
                    </div>
                  </div>

                  {/* Right Side: Quick Guide / Help */}
                  <div className="w-1/2 p-6 bg-zinc-950/40 overflow-y-auto space-y-4">
                    <h4 className="text-[10px] font-bold text-zinc-500 uppercase tracking-widest border-b border-zinc-850 pb-1.5 flex items-center gap-1.5">
                      <HelpCircle className="w-3.5 h-3.5 text-teal-400" />
                      Como funciona o Assistant?
                    </h4>
                    
                    <div className="text-xs text-zinc-400 space-y-3 leading-relaxed">
                      <p>
                        O assistente conversacional **Skill Forge** está conectado a modelos de inteligência artificial de prompt engineering avançados. 
                      </p>
                      <p>
                        1. **Descreva seu objetivo:** Explique de forma resumida o que você quer que o agente execute.
                      </p>
                      <p>
                        2. **Calibração do Prompt:** A IA criará as diretrizes detalhadas seguindo as melhores práticas do MAF 1.6.1.
                      </p>
                      <p>
                        3. **Clique em Aplicar:** O prompt gerado alimentará o formulário do cadastro, onde você poderá conferir e fazer ajustes finais antes de salvar no banco PostgreSQL.
                      </p>
                    </div>
                  </div>

                </div>
              )}

            </div>
          </div>
        </div>
      )}

    </div>
  )
}
