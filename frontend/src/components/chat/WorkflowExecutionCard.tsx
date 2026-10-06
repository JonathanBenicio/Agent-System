import { 
  Play, 
  AlertCircle, 
  Loader2, 
  UserCheck, 
  Check, 
  X 
} from 'lucide-react'
import { cn } from '@/lib/utils'
import { useState } from 'react'
import { useWorkflowExecution } from '@/hooks/useWorkflowExecution'

interface WorkflowExecutionCardProps {
  executionId: string
  initialStatus?: number
  workflowName: string
}

export function WorkflowExecutionCard({ 
  executionId, 
  initialStatus = 1, 
  workflowName 
}: WorkflowExecutionCardProps) {
  const {
    execState,
    approving,
    approvedState,
    handleApprove,
    handleReject
  } = useWorkflowExecution(executionId, workflowName, initialStatus)
  const [selectedStepId, setSelectedStepId] = useState('')
  const pendingSteps = execState.steps.filter(step => step.status === 3)
  const decisionStepId = pendingSteps.some(step => step.stepId === selectedStepId)
    ? selectedStepId : pendingSteps[0]?.stepId

  const getStatusBadge = (status: number) => {
    switch (status) {
      case 0:
        return <span className="bg-zinc-800 text-zinc-400 text-xs px-2 py-0.5 font-mono uppercase border border-zinc-700">Pendente</span>
      case 1:
        return <span className="bg-teal-950/50 text-teal-300 text-xs px-2 py-0.5 font-mono uppercase border border-teal-800/80">Executando</span>
      case 2:
        return <span className="bg-amber-950/40 text-amber-300 text-xs px-2 py-0.5 font-mono uppercase border border-amber-800/80">Pausado</span>
      case 3:
        return <span className="bg-emerald-950/50 text-emerald-300 text-xs px-2 py-0.5 font-mono uppercase border border-emerald-800 animate-pulse">Aprovação Necessária</span>
      case 4:
        return <span className="bg-emerald-950/40 text-emerald-400 text-xs px-2 py-0.5 font-mono uppercase border border-emerald-900/60">Concluído</span>
      case 5:
        return <span className="bg-rose-950/40 text-rose-300 text-xs px-2 py-0.5 font-mono uppercase border border-rose-900/60">Falhou</span>
      case 6:
        return <span className="bg-zinc-900 text-zinc-500 text-xs px-2 py-0.5 font-mono uppercase border border-zinc-800">Cancelado</span>
      default:
        return <span className="bg-zinc-800 text-zinc-400 text-xs px-2 py-0.5 font-mono border border-zinc-700">Status {status}</span>
    }
  }

  return (
    <div className="w-full max-w-2xl border border-zinc-800 bg-zinc-950/80 backdrop-blur-md p-4 text-zinc-100 shadow-xl border-l-2 border-l-teal-500 transition-all duration-300 my-4 select-none animate-fadeIn">
      <div className="flex items-center justify-between border-b border-zinc-800 pb-3">
        <div className="flex items-center gap-3">
          <div className="flex h-10 w-10 items-center justify-center border border-zinc-800 bg-zinc-900/50">
            <Play className="h-4 w-4 text-teal-400 fill-teal-400/20" />
          </div>
          <div>
            <h3 className="text-sm font-semibold tracking-wide text-zinc-100 font-mono">
              WORKFLOW EXECUTION
            </h3>
            <p className="text-xs text-zinc-400 mt-0.5">{execState.workflowName}</p>
          </div>
        </div>
        <div className="flex items-center gap-2">
          {getStatusBadge(execState.status)}
          <span className="text-[10px] text-zinc-500 font-mono">ID: {executionId.slice(0, 8)}</span>
        </div>
      </div>

      {/* Timeline dos Steps */}
      {execState.steps.length > 0 && (
        <div className="mt-4 space-y-3 pl-2">
          <h4 className="text-[11px] font-semibold text-zinc-500 uppercase tracking-widest font-mono">
            Etapas de Processamento
          </h4>
          <div className="relative border-l border-zinc-800 pl-4 ml-2.5 space-y-4 py-1">
            {execState.steps.map((step, idx) => (
              <div key={step.stepId || idx} className="relative flex items-start gap-3">
                {/* Dot indicador */}
                <div className="absolute -left-[24.5px] top-1 bg-zinc-950 p-0.5 rounded-full">
                  <div className={cn(
                    "h-2.5 w-2.5 rounded-full border border-zinc-700 bg-zinc-800",
                    step.status === 1 && "bg-teal-400 border-teal-500 animate-pulse",
                    step.status === 4 && "bg-emerald-500 border-emerald-600",
                    step.status === 5 && "bg-rose-500 border-rose-600"
                  )} />
                </div>
                <div className="flex-1">
                  <div className="flex items-center justify-between">
                    <span className={cn(
                      "text-xs font-medium font-mono",
                      step.status === 1 ? "text-teal-300" : "text-zinc-300",
                      step.status === 4 && "text-emerald-400"
                    )}>
                      {step.stepName}
                    </span>
                    <span className="text-[10px] text-zinc-500 uppercase font-mono">
                      {step.status === 1 && "executando"}
                      {step.status === 4 && "concluído"}
                      {step.status === 5 && "falhou"}
                    </span>
                  </div>
                  {step.errorMessage && (
                    <p className="text-[11px] text-rose-400 mt-1 bg-rose-950/20 px-2 py-1 border border-rose-900/40 font-mono">
                      {step.errorMessage}
                    </p>
                  )}
                </div>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* Seção de Aprovação Gate */}
      {(execState.status === 3 && approvedState === 'pending') && (
        <div className="mt-5 border border-emerald-900/80 bg-emerald-950/10 p-4 transition-all animate-fadeIn">
          <div className="flex items-start gap-3">
            <div className="mt-0.5 text-emerald-400">
              <UserCheck className="h-5 w-5 animate-pulse" />
            </div>
            <div className="flex-1">
              <h4 className="text-xs font-semibold text-emerald-300 tracking-wider font-mono">
                APPROVAL GATE REQUIRED
              </h4>
              <p className="text-xs text-zinc-400 mt-1 leading-relaxed">
                Este fluxo exige a validação e autorização de um operador humano para prosseguir.
              </p>
              
              {pendingSteps.length > 1 && <label className="mt-3 block text-xs">
                Etapa para decidir
                <select aria-label="Etapa para decidir" value={decisionStepId}
                  onChange={event => setSelectedStepId(event.target.value)}
                  className="ml-2 rounded-xl bg-zinc-900 p-2">
                  {pendingSteps.map(step => <option key={step.stepId} value={step.stepId}>{step.stepName || step.stepId}</option>)}
                </select>
              </label>}
              <div className="flex items-center gap-3 mt-4">
                <button
                  onClick={() => handleApprove(decisionStepId)}
                  disabled={approving}
                  aria-label="Aprovar etapa"
                  className="flex items-center gap-1.5 px-3 py-1.5 border border-emerald-700 bg-emerald-900/40 hover:bg-emerald-900/70 text-emerald-200 text-xs font-semibold tracking-wide font-mono transition-all duration-150 disabled:opacity-50 select-none scale-100 hover:scale-[1.03] active:scale-95"
                >
                  {approving ? (
                    <Loader2 className="h-3.5 w-3.5 animate-spin" />
                  ) : (
                    <Check className="h-3.5 w-3.5" />
                  )}
                  APROVAR
                </button>
                
                <button
                  onClick={() => handleReject(decisionStepId)}
                  disabled={approving}
                  aria-label="Rejeitar etapa"
                  className="flex items-center gap-1.5 px-3 py-1.5 border border-rose-700 bg-rose-900/20 hover:bg-rose-900/50 text-rose-300 text-xs font-semibold tracking-wide font-mono transition-all duration-150 disabled:opacity-50 select-none scale-100 hover:scale-[1.03] active:scale-95"
                >
                  <X className="h-3.5 w-3.5" />
                  REJEITAR
                </button>
              </div>
            </div>
          </div>
        </div>
      )}

      {approvedState === 'approved' && (
        <div className="mt-4 flex items-center gap-2 border border-emerald-900/60 bg-emerald-950/20 px-3 py-2 text-xs text-emerald-400 font-mono">
          <Check className="h-4 w-4" />
          Execução autorizada pelo operador.
        </div>
      )}

      {approvedState === 'rejected' && (
        <div className="mt-4 flex items-center gap-2 border border-rose-950/60 bg-rose-950/20 px-3 py-2 text-xs text-rose-400 font-mono">
          <X className="h-4 w-4" />
          Execução rejeitada e interrompida.
        </div>
      )}

      {execState.errorMessage && execState.status === 5 && (
        <div className="mt-4 flex items-start gap-2 border border-rose-950/60 bg-rose-950/30 p-3 text-xs text-rose-300 font-mono">
          <AlertCircle className="h-4 w-4 mt-0.5 shrink-0" />
          <div>
            <span className="font-semibold block mb-0.5 uppercase tracking-wider">Falha de Execução:</span>
            {execState.errorMessage}
          </div>
        </div>
      )}
    </div>
  )
}
