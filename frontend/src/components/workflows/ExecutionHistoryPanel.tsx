import { useState, useEffect } from 'react';
import { 
  Play, 
  CheckCircle2, 
  XCircle, 
  Clock, 
  ChevronRight,
  X,
  Activity,
  Loader2
} from 'lucide-react';
import { useWorkflows } from '@/hooks/useWorkflows';
import type { WorkflowExecution } from '@/types/api';

interface ExecutionHistoryPanelProps {
  workflowId: string | null;
  isOpen: boolean;
  onClose: () => void;
  onSelectExecution: (executionId: string) => void;
}

export function ExecutionHistoryPanel({ 
  workflowId, 
  isOpen, 
  onClose,
  onSelectExecution
}: ExecutionHistoryPanelProps) {
  const { listExecutions } = useWorkflows();
  const [executions, setExecutions] = useState<WorkflowExecution[]>([]);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    if (isOpen && workflowId) {
      loadExecutions();
    }
  }, [isOpen, workflowId]);

  const loadExecutions = async () => {
    if (!workflowId) return;
    setLoading(true);
    try {
      // Assuming listExecutions might not filter by workflowId currently, 
      // we might need to filter on client side if the API doesn't support it.
      // But let's assume we get all and filter, or the API gets all for the tenant.
      // We should ideally fetch by workflowId, but let's filter what we get.
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
    </div>
  );
}
