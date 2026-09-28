import { useState } from 'react'
import { 
  X, 
  Sliders, 
  FolderKanban, 
  Sparkles, 
  Workflow, 
  Play, 
  CheckSquare, 
  Square,
  Info 
} from 'lucide-react'
import { useChat } from '@/hooks/useChat'
import { useKnowledgeRooms } from '@/hooks/useKnowledgeRooms'
import { useAgents } from '@/hooks/useAgents'
import { useWorkflows } from '@/hooks/useWorkflows'
import { toast } from 'sonner'

interface ChatConfigSidebarProps {
  onClose: () => void
}

export function ChatConfigSidebar({ onClose }: ChatConfigSidebarProps) {
  const {
    selectedRoomId,
    setSelectedRoomId,
    selectedAgentId,
    setSelectedAgentId,
    associateToRoom,
    setAssociateToRoom,
    addLocalMessage
  } = useChat()

  const { rooms = [] } = useKnowledgeRooms()
  const { agents = [] } = useAgents()
  const { workflows = [], executeWorkflow } = useWorkflows()

  const [selectedWorkflowId, setSelectedWorkflowId] = useState<string>('')
  const [executingWorkflow, setExecutingWorkflow] = useState<boolean>(false)

  const handleRunWorkflow = async () => {
    if (!selectedWorkflowId) return
    const flow = workflows.find(w => w.id === selectedWorkflowId)
    if (!flow) return

    setExecutingWorkflow(true)
    const runToast = toast.loading(`Iniciando workflow "${flow.name}"...`)
    
    try {
      const execution = await executeWorkflow(selectedWorkflowId)
      
      toast.success(`Workflow "${flow.name}" iniciado!`, {
        id: runToast
      })

      addLocalMessage({
        id: crypto.randomUUID(),
        role: 'system',
        content: `Iniciada a execução do workflow "${flow.name}"...`,
        timestamp: new Date().toISOString(),
        workflowExecutionId: execution.id,
        workflowName: flow.name
      })

      setSelectedWorkflowId('')
    } catch (err) {
      console.error(err)
      toast.error(`Falha ao iniciar o workflow: ${err instanceof Error ? err.message : 'Erro interno'}`, {
        id: runToast
      })
    } finally {
      setExecutingWorkflow(false)
    }
  }

  return (
    <div className="absolute inset-y-0 right-0 w-80 bg-zinc-950/95 border-l border-zinc-800 shadow-2xl flex flex-col z-40 animate-in slide-in-from-right duration-300 select-none">
      {/* Background Teal Glow */}
      <div className="absolute inset-0 -z-10 bg-[radial-gradient(circle_at_50%_0%,_rgba(20,184,166,0.05),_transparent_50%)]" />

      {/* Header */}
      <div className="p-4 border-b border-zinc-800 flex items-center justify-between">
        <div className="flex items-center gap-2 text-teal-400 font-semibold">
          <Sliders className="w-4 h-4" />
          <span className="text-sm font-bold uppercase tracking-wider font-mono">Configurações</span>
        </div>
        <button 
          onClick={onClose}
          className="p-1 rounded-lg hover:bg-zinc-900 text-zinc-500 hover:text-zinc-300 transition-colors"
        >
          <X className="w-4 h-4" />
        </button>
      </div>

      {/* Scrollable Content */}
      <div className="flex-1 overflow-y-auto p-4 flex flex-col gap-6 custom-scrollbar">
        
        {/* RAG Source Section */}
        <div className="flex flex-col gap-2">
          <label className="flex items-center gap-1.5 text-[10px] font-bold uppercase tracking-widest text-zinc-400 font-mono">
            <FolderKanban className="h-3.5 w-3.5 text-teal-500" /> RAG SOURCE (ROOM)
          </label>
          <p className="text-[11px] text-zinc-500 font-mono -mt-1 leading-relaxed">
            Restringe buscas semânticas à base selecionada.
          </p>
          <select
            value={selectedRoomId}
            onChange={(event) => {
              setSelectedRoomId(event.target.value)
              if (!event.target.value) {
                setAssociateToRoom(false)
              } else {
                setAssociateToRoom(true)
              }
            }}
            className="w-full rounded-xl border border-zinc-800 bg-zinc-900/60 hover:bg-zinc-900 px-3 py-2 text-xs text-zinc-200 outline-none transition focus:border-teal-500 cursor-pointer font-mono"
          >
            <option value="">💬 Chat Livre (Sessão)</option>
            {rooms.map(room => (
              <option key={room.id} value={room.id}>📚 {room.name}</option>
            ))}
          </select>

          {/* Context Ingestion Checkbox */}
          {selectedRoomId && (
            <div className="mt-2 p-3 bg-teal-950/15 border border-teal-900/30 rounded-xl transition-all duration-300 animate-fadeIn">
              <button
                onClick={() => setAssociateToRoom(!associateToRoom)}
                className="flex items-start gap-2 text-[10px] text-zinc-400 hover:text-zinc-200 transition-colors font-mono select-none text-left"
              >
                {associateToRoom ? (
                  <CheckSquare className="h-4 w-4 text-teal-400 shrink-0 mt-0.5" />
                ) : (
                  <Square className="h-4 w-4 text-zinc-700 shrink-0 mt-0.5" />
                )}
                <div>
                  <span className="font-bold text-zinc-300 uppercase tracking-wider block mb-1">
                    Associar uploads permanentemente
                  </span>
                  Arquivos enviados serão indexados diretamente na sala de conhecimento selecionada.
                </div>
              </button>
            </div>
          )}
        </div>

        <hr className="border-zinc-800" />

        {/* Specialist Agent Section */}
        <div className="flex flex-col gap-2">
          <label className="flex items-center gap-1.5 text-[10px] font-bold uppercase tracking-widest text-zinc-400 font-mono">
            <Sparkles className="h-3.5 w-3.5 text-emerald-500" /> SPECIALIST AGENT
          </label>
          <p className="text-[11px] text-zinc-500 font-mono -mt-1 leading-relaxed">
            Ignora o roteador e força a execução do agente selecionado.
          </p>
          <select
            value={selectedAgentId}
            onChange={(event) => setSelectedAgentId(event.target.value)}
            className="w-full rounded-xl border border-zinc-800 bg-zinc-900/60 hover:bg-zinc-900 px-3 py-2 text-xs text-zinc-200 outline-none transition focus:border-teal-500 cursor-pointer font-mono"
          >
            <option value="">🤖 Intelligent Router</option>
            {agents.map(agent => (
              <option key={agent.name} value={agent.name}>👤 {agent.name}</option>
            ))}
          </select>
        </div>

        <hr className="border-zinc-800" />

        {/* Workflow Section */}
        <div className="flex flex-col gap-2">
          <label className="flex items-center gap-1.5 text-[10px] font-bold uppercase tracking-widest text-zinc-400 font-mono">
            <Workflow className="h-3.5 w-3.5 text-teal-500" /> RUN WORKFLOW
          </label>
          <p className="text-[11px] text-zinc-500 font-mono -mt-1 leading-relaxed">
            Dispare um fluxo de automação estruturado em background.
          </p>
          <select
            value={selectedWorkflowId}
            onChange={(event) => setSelectedWorkflowId(event.target.value)}
            className="w-full rounded-xl border border-zinc-800 bg-zinc-900/60 hover:bg-zinc-900 px-3 py-2 text-xs text-zinc-200 outline-none transition focus:border-teal-500 cursor-pointer font-mono mb-2"
          >
            <option value="">Selecione um fluxo...</option>
            {workflows.map(flow => (
              <option key={flow.id} value={flow.id}>⚡ {flow.name}</option>
            ))}
          </select>

          {selectedWorkflowId && (
            <button
              onClick={handleRunWorkflow}
              disabled={executingWorkflow}
              className="w-full flex h-10 items-center justify-center gap-2 border border-teal-800 bg-teal-950/65 hover:bg-teal-900 px-4 py-2 text-xs font-bold text-teal-300 font-mono transition duration-150 tracking-wider active:scale-[0.98] disabled:opacity-50 uppercase rounded-xl"
            >
              <Play className="h-3.5 w-3.5 fill-teal-400/20" /> EXECUTAR WORKFLOW
            </button>
          )}
        </div>

        {/* Info panel in footer of config */}
        <div className="mt-auto bg-zinc-900/40 border border-zinc-800/60 rounded-xl p-3 flex gap-2">
          <Info className="w-4 h-4 text-zinc-500 shrink-0 mt-0.5" />
          <p className="text-[10px] text-zinc-500 leading-relaxed font-mono">
            Os parâmetros de RAG e Agentes Especialistas são isolados por sessão. A remoção de sessões limpa arquivos temporários correspondentes.
          </p>
        </div>

      </div>
    </div>
  )
}
