import { useState, useEffect } from 'react';
import { 
  Play, 
  CheckCircle2, 
  XCircle, 
  Clock, 
  ChevronRight,
  X,
  Activity,
  Loader2,
  ChevronLeft,
  TerminalSquare
} from 'lucide-react';
import { useWorkflows, useWorkflowExecution } from '@/hooks/useWorkflows';
import type { WorkflowExecution } from '@/types/api';

interface ExecutionHistoryPanelProps {
  workflowId: string | null;
  isOpen: boolean;
  onClose: () => void;
  selectedExecutionId: string | null;
  onSelectExecution: (executionId: string | null) => void;
}

export function ExecutionHistoryPanel({ 
  workflowId, 
  isOpen, 
  onClose,
  selectedExecutionId,
  onSelectExecution
}: ExecutionHistoryPanelProps) {
  const { listExecutions } = useWorkflows();
  const [executions, setExecutions] = useState<WorkflowExecution[]>([]);
  const [loading, setLoading] = useState(false);

  const { data: executionDetails, isLoading: loadingDetails } = useWorkflowExecution(selectedExecutionId);

  useEffect(() => {
    if (isOpen && workflowId && !selectedExecutionId) {
      loadExecutions();
    }
  }, [isOpen, workflowId, selectedExecutionId]);

  const loadExecutions = async () => {
    if (!workflowId) return;
    setLoading(true);
    try {
      const data = await listExecutions();
      const filtered = data.filter(e => e.workflowId === workflowId);
      setExecutions(filtered);
    } catch (error) {
      console.error("Failed to load executions", error);
    } finally {
      setLoading(false);
    }
  };

  if (!isOpen) return null;

  return (
    <div className="w-80 bg-zinc-925 border-l border-zinc-800 flex flex-col absolute right-0 top-16 bottom-0 z-40 shadow-2xl animate-in slide-in-from-right-8 duration-200">
      {selectedExecutionId ? (
        <>
          <div className="p-4 border-b border-zinc-800 flex items-center justify-between bg-zinc-900/50">
            <h3 className="text-sm font-bold text-white flex items-center gap-2">
              <button 
                onClick={() => onSelectExecution(null)}
                className="hover:bg-zinc-800 p-1 rounded-md text-zinc-400 hover:text-white transition-colors"
              >
                <ChevronLeft className="w-4 h-4" />
              </button>
              Execution Details
            </h3>
            <button 
              onClick={onClose}
              className="p-1.5 hover:bg-zinc-800 rounded-md text-zinc-400 hover:text-zinc-100"
            >
              <X className="w-4 h-4" />
            </button>
          </div>
          
          <div className="flex-1 overflow-y-auto p-4 space-y-4">
            {loadingDetails || !executionDetails ? (
              <div className="flex justify-center py-8">
                <Loader2 className="w-6 h-6 text-teal-500 animate-spin" />
              </div>
            ) : (
              <div className="space-y-4">
                <div className="flex items-center gap-2 text-sm font-semibold">
                  Status: 
                  {executionDetails.status === 1 ? (
                    <span className="text-teal-400 flex items-center gap-1"><CheckCircle2 className="w-4 h-4"/> Completed</span>
                  ) : executionDetails.status === 2 ? (
                    <span className="text-rose-500 flex items-center gap-1"><XCircle className="w-4 h-4"/> Failed</span>
                  ) : (
                    <span className="text-blue-400 flex items-center gap-1"><Loader2 className="w-4 h-4 animate-spin"/> Running</span>
                  )}
                </div>

                {executionDetails.errorMessage && (
                  <div className="p-3 rounded-lg bg-rose-500/10 border border-rose-500/20 text-xs text-rose-400 font-mono break-all">
                    {executionDetails.errorMessage}
                  </div>
                )}

                <div className="space-y-2 relative before:absolute before:inset-0 before:ml-[11px] before:-translate-x-px md:before:mx-auto md:before:translate-x-0 before:h-full before:w-0.5 before:bg-gradient-to-b before:from-transparent before:via-zinc-800 before:to-transparent">
                  {executionDetails.stepExecutions?.map((step, idx) => (
                    <div key={idx} className="relative flex items-start gap-3">
                      <div className={`w-6 h-6 rounded-full shrink-0 flex items-center justify-center border-4 border-zinc-925 z-10 ${
                        step.status === 1 ? 'bg-teal-500' :
                        step.status === 2 ? 'bg-rose-500' : 'bg-blue-500 animate-pulse'
                      }`}>
                        {step.status === 1 ? <CheckCircle2 className="w-3 h-3 text-white" /> :
                         step.status === 2 ? <XCircle className="w-3 h-3 text-white" /> :
                         <Play className="w-3 h-3 text-white" />}
                      </div>
                      <div className="flex-1 bg-zinc-900 border border-zinc-800 rounded-lg p-3">
                        <div className="text-xs font-bold text-white mb-1">{step.stepName}</div>
                        {step.errorMessage && (
                          <div className="text-[10px] text-rose-400 mt-1 font-mono break-all">
                            {step.errorMessage}
                          </div>
                        )}
                        {Object.keys(step.output || {}).length > 0 && (
                          <div className="mt-2 pt-2 border-t border-zinc-800">
                            <div className="text-[10px] text-zinc-500 flex items-center gap-1 mb-1">
                              <TerminalSquare className="w-3 h-3" /> Output
                            </div>
                            <pre className="text-[10px] text-zinc-300 font-mono bg-zinc-950 p-2 rounded break-all whitespace-pre-wrap">
                              {JSON.stringify(step.output, null, 2)}
                            </pre>
                          </div>
                        )}
                      </div>
                    </div>
                  ))}
                </div>
              </div>
            )}
          </div>
        </>
      ) : (
        <>
          <div className="p-4 border-b border-zinc-800 flex items-center justify-between bg-zinc-900/50">
            <h3 className="text-sm font-bold text-white flex items-center gap-2">
              <Activity className="w-4 h-4 text-teal-400" />
              Execution History
            </h3>
            <div className="flex items-center gap-2">
              <button 
                onClick={loadExecutions}
                className="p-1.5 hover:bg-zinc-800 rounded-md text-zinc-400 hover:text-zinc-100"
                title="Refresh"
              >
                <Clock className="w-4 h-4" />
              </button>
              <button 
                onClick={onClose}
                className="p-1.5 hover:bg-zinc-800 rounded-md text-zinc-400 hover:text-zinc-100"
              >
                <X className="w-4 h-4" />
              </button>
            </div>
          </div>

          <div className="flex-1 overflow-y-auto p-3 space-y-2">
            {!workflowId && (
              <p className="text-xs text-zinc-500 text-center py-4">Save the workflow first to see history.</p>
            )}
            {workflowId && loading && (
              <div className="flex justify-center py-8">
                <Loader2 className="w-6 h-6 text-teal-500 animate-spin" />
              </div>
            )}
            {workflowId && !loading && executions.length === 0 && (
              <p className="text-xs text-zinc-500 text-center py-4">No executions found for this workflow.</p>
            )}
            {executions.map(exec => (
              <div 
                key={exec.id}
                onClick={() => onSelectExecution(exec.id)}
                className="p-3 bg-zinc-900 border border-zinc-800 hover:border-zinc-700 rounded-xl cursor-pointer group transition-all"
              >
                <div className="flex items-center justify-between mb-2">
                  <div className="flex items-center gap-2">
                    {exec.status === 1 ? ( // Completed
                      <CheckCircle2 className="w-4 h-4 text-teal-400" />
                    ) : exec.status === 2 ? ( // Failed
                      <XCircle className="w-4 h-4 text-rose-500" />
                    ) : (
                      <Play className="w-4 h-4 text-blue-400 animate-pulse" />
                    )}
                    <span className="text-xs font-semibold text-zinc-200">
                      {exec.status === 1 ? 'Completed' : exec.status === 2 ? 'Failed' : 'Running'}
                    </span>
                  </div>
                  <ChevronRight className="w-4 h-4 text-zinc-600 group-hover:text-zinc-300 transition-colors" />
                </div>
                <div className="text-[10px] text-zinc-500 flex items-center gap-3">
                  <span>{new Date(exec.startedAt).toLocaleString()}</span>
                  {exec.completedAt && (
                    <span>
                      {Math.round((new Date(exec.completedAt).getTime() - new Date(exec.startedAt).getTime()) / 1000)}s
                    </span>
                  )}
                </div>
              </div>
            ))}
          </div>
        </>
      )}
    </div>
  );
}
