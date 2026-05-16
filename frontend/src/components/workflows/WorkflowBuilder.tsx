import { useState } from 'react';
import { 
  ReactFlow, 
  Background, 
  Controls, 
  Panel,
  useReactFlow,
  ReactFlowProvider
} from '@xyflow/react';
import type { Node, Edge } from '@xyflow/react';
import '@xyflow/react/dist/style.css';
import { 
  Play, 
  Save, 
  Plus, 
  Bot, 
  Wrench, 
  Trash2, 
  Zap, 
  FolderOpen,
  ChevronLeft,
  Clock,
  GitBranch,
  Activity
} from 'lucide-react';
import { useWorkflowStore } from '@/store/useWorkflowStore';
import { useNavigate } from 'react-router-dom';
import { useWorkflows, useWorkflowExecution } from '@/hooks/useWorkflows';
import { useToast } from '@/components/shared/Toast';
import { ExecutionHistoryPanel } from './ExecutionHistoryPanel';

// Helper to determine node border color based on status
const getBorderClass = (status?: number) => {
  if (status === 1) return 'border-teal-500 shadow-[0_0_15px_rgba(20,184,166,0.3)]'; // Success
  if (status === 2) return 'border-rose-500 shadow-[0_0_15px_rgba(244,63,94,0.3)]';  // Error
  if (status === 0) return 'border-blue-500 animate-pulse shadow-[0_0_15px_rgba(59,130,246,0.3)]'; // Running
  return 'border-zinc-800'; // Default
};

// Simple Custom Node Components
const AgentNode = ({ data }: any) => (
  <div className={`px-4 py-3 shadow-xl rounded-xl bg-zinc-900 border-2 transition-all min-w-[150px] ${data.executionStatus !== undefined ? getBorderClass(data.executionStatus) : 'border-teal-500/50'}`}>
    <div className="flex items-center gap-2 mb-1">
      <Bot className="w-4 h-4 text-teal-400" />
      <span className="text-xs font-bold text-teal-400 uppercase tracking-wider">Agent</span>
    </div>
    <div className="text-sm font-semibold text-white">{data.label}</div>
    <div className="text-[10px] text-zinc-500 mt-1">{data.agentName}</div>
  </div>
);

const ToolNode = ({ data }: any) => (
  <div className={`px-4 py-3 shadow-xl rounded-xl bg-zinc-900 border-2 transition-all min-w-[150px] ${data.executionStatus !== undefined ? getBorderClass(data.executionStatus) : 'border-blue-500/50'}`}>
    <div className="flex items-center gap-2 mb-1">
      <Wrench className="w-4 h-4 text-blue-400" />
      <span className="text-xs font-bold text-blue-400 uppercase tracking-wider">Tool</span>
    </div>
    <div className="text-sm font-semibold text-white">{data.label}</div>
    <div className="text-[10px] text-zinc-500 mt-1">{data.toolName}</div>
  </div>
);

const DecisionNode = ({ data }: any) => (
  <div className={`px-4 py-3 shadow-xl rounded-xl bg-zinc-900 border-2 transition-all min-w-[150px] ${data.executionStatus !== undefined ? getBorderClass(data.executionStatus) : 'border-amber-500/50'}`}>
    <div className="flex items-center gap-2 mb-1">
      <Zap className="w-4 h-4 text-amber-400" />
      <span className="text-xs font-bold text-amber-400 uppercase tracking-wider">Decision</span>
    </div>
    <div className="text-sm font-semibold text-white">{data.label}</div>
    <div className="text-[10px] text-zinc-500 mt-1 font-mono">{data.condition || 'No condition'}</div>
  </div>
);

const WaitNode = ({ data }: any) => (
  <div className={`px-4 py-3 shadow-xl rounded-xl bg-zinc-900 border-2 transition-all min-w-[150px] ${data.executionStatus !== undefined ? getBorderClass(data.executionStatus) : 'border-purple-500/50'}`}>
    <div className="flex items-center gap-2 mb-1">
      <Clock className="w-4 h-4 text-purple-400" />
      <span className="text-xs font-bold text-purple-400 uppercase tracking-wider">Wait</span>
    </div>
    <div className="text-sm font-semibold text-white">{data.label}</div>
    <div className="text-[10px] text-zinc-500 mt-1 font-mono">{data.timeout || 'No timeout'}</div>
  </div>
);

const nodeTypes = {
  agent: AgentNode,
  tool: ToolNode,
  decision: DecisionNode,
  wait: WaitNode,
};

export function WorkflowBuilderPage() {
  const navigate = useNavigate();
  const { workflows, saveWorkflow, deleteWorkflow, getWorkflow, executeWorkflow } = useWorkflows();
  const { addToast } = useToast();
  const [selectedNodeId, setSelectedNodeId] = useState<string | null>(null);
  const [isHistoryOpen, setIsHistoryOpen] = useState(false);
  const [selectedExecutionId, setSelectedExecutionId] = useState<string | null>(null);

  const { data: executionDetails } = useWorkflowExecution(selectedExecutionId);

  const { 
    nodes, 
    edges, 
    activeWorkflowId,
    workflowName,
    onNodesChange, 
    onEdgesChange, 
    onConnect, 
    addNode,
    setNodes,
    setEdges,
    setWorkflowName,
    setActiveWorkflowId,
    toWorkflowDefinition,
    fromWorkflowDefinition,
    clear
  } = useWorkflowStore();

  const { screenToFlowPosition } = useReactFlow();

  // Sync execution status to nodes visually
  import { useEffect } from 'react';
  useEffect(() => {
    if (!selectedExecutionId) {
      // Clear execution status from all nodes
      setNodes(nodes.map(n => ({
        ...n,
        data: { ...n.data, executionStatus: undefined }
      })));
      return;
    }

    if (executionDetails?.stepExecutions) {
      setNodes(nodes.map(n => {
        // Match step execution by node Id (stepName is the nodeId in this architecture, or we need to find how they match)
        // Wait, in DefaultWorkflowEngine, stepName is saved. Let's assume stepName == node.id or node.data.label.
        // Actually, looking at the store `toWorkflowDefinition`, the node.id is the Key of the step. So stepName = node.id
        const step = executionDetails.stepExecutions.find((s: any) => s.stepName === n.id);
        return {
          ...n,
          data: { ...n.data, executionStatus: step?.status }
        };
      }));
    }
  }, [executionDetails, selectedExecutionId]); // Omit nodes to avoid infinite loop when updating


  const handleSave = async () => {
    try {
      const definition = toWorkflowDefinition();
      const saved = await saveWorkflow(definition);
      setActiveWorkflowId(saved.id);
      addToast('Workflow salvo com sucesso', 'success');
    } catch (err) {
      addToast('Erro ao salvar workflow', 'error');
    }
  };

  const onAddAgent = () => {
    const id = `node_${Date.now()}`;
    const position = screenToFlowPosition({
      x: window.innerWidth / 2 + (Math.random() - 0.5) * 100,
      y: window.innerHeight / 2 + (Math.random() - 0.5) * 100,
    });
    addNode({ id, type: 'agent', position, data: { label: 'New Agent Task', agentName: 'orchestrator', stepType: 0 } });
  };

  const loadWorkflow = async (id: string) => {
    try {
      const def = await getWorkflow(id);
      fromWorkflowDefinition(def);
      addToast('Workflow carregado', 'success');
    } catch {
      addToast('Erro ao carregar workflow', 'error');
    }
  };

  const onAddTool = () => {
    const id = `node_${Date.now()}`;
    const position = screenToFlowPosition({
      x: window.innerWidth / 2 + (Math.random() - 0.5) * 100,
      y: window.innerHeight / 2 + (Math.random() - 0.5) * 100,
    });
    addNode({ id, type: 'tool', position, data: { label: 'New Tool Task', toolName: 'http_tool', stepType: 0 } });
  };

  const onAddDecision = () => {
    const id = `node_${Date.now()}`;
    const position = screenToFlowPosition({
      x: window.innerWidth / 2 + (Math.random() - 0.5) * 100,
      y: window.innerHeight / 2 + (Math.random() - 0.5) * 100,
    });
    addNode({ id, type: 'decision', position, data: { label: 'New Decision', condition: '{{previous.result}} == true', stepType: 1 } });
  };

  const onAddWait = () => {
    const id = `node_${Date.now()}`;
    const position = screenToFlowPosition({
      x: window.innerWidth / 2 + (Math.random() - 0.5) * 100,
      y: window.innerHeight / 2 + (Math.random() - 0.5) * 100,
    });
    addNode({ id, type: 'wait', position, data: { label: 'Wait Event', timeout: '00:05:00', stepType: 3 } });
  };

  return (
    <div className="h-full w-full bg-zinc-950 flex flex-col">
      {/* Toolbar */}
      <div className="h-16 border-b border-zinc-800 bg-zinc-900/50 flex items-center justify-between px-6 shrink-0">
        <div className="flex items-center gap-4 flex-1">
          <button 
            onClick={() => navigate(-1)}
            className="p-2 hover:bg-zinc-800 rounded-lg text-zinc-400 transition-all"
          >
            <ChevronLeft className="w-5 h-5" />
          </button>
          <div className="flex flex-col">
            <input 
              value={workflowName}
              onChange={(e) => setWorkflowName(e.target.value)}
              className="bg-transparent border-none text-lg font-bold text-white leading-none focus:outline-none focus:ring-0 p-0"
              placeholder="Workflow Name"
            />
            <p className="text-[10px] text-zinc-500 mt-1 uppercase tracking-widest font-medium">Visual Orchestration Engine</p>
          </div>
        </div>

        <div className="flex items-center gap-3">
          <button 
            onClick={onAddAgent}
            className="flex items-center gap-2 px-3 py-1.5 bg-zinc-800 hover:bg-zinc-700 text-zinc-300 border border-zinc-700 rounded-lg text-xs font-medium transition-all"
          >
            <Bot className="w-3.5 h-3.5 text-teal-400" />
            Add Agent
          </button>
          <button 
            onClick={onAddTool}
            className="flex items-center gap-2 px-3 py-1.5 bg-zinc-800 hover:bg-zinc-700 text-zinc-300 border border-zinc-700 rounded-lg text-xs font-medium transition-all"
          >
            <Wrench className="w-3.5 h-3.5 text-blue-400" />
            Add Tool
          </button>
          <button 
            onClick={onAddDecision}
            className="flex items-center gap-2 px-3 py-1.5 bg-zinc-800 hover:bg-zinc-700 text-zinc-300 border border-zinc-700 rounded-lg text-xs font-medium transition-all"
          >
            <GitBranch className="w-3.5 h-3.5 text-amber-400" />
            Decision
          </button>
          <button 
            onClick={onAddWait}
            className="flex items-center gap-2 px-3 py-1.5 bg-zinc-800 hover:bg-zinc-700 text-zinc-300 border border-zinc-700 rounded-lg text-xs font-medium transition-all"
          >
            <Clock className="w-3.5 h-3.5 text-purple-400" />
            Wait
          </button>
          <div className="w-px h-6 bg-zinc-800 mx-1" />
          <button 
            onClick={handleSave}
            className="flex items-center gap-2 px-4 py-2 bg-teal-600 hover:bg-teal-500 text-white rounded-lg text-sm font-bold transition-all shadow-lg shadow-teal-900/20"
          >
            <Save className="w-4 h-4" />
            Save Workflow
          </button>
          <button 
            onClick={() => setIsHistoryOpen(!isHistoryOpen)}
            className={`flex items-center gap-2 px-3 py-2 rounded-lg text-sm font-bold transition-all ${isHistoryOpen ? 'bg-zinc-800 text-teal-400' : 'bg-transparent text-zinc-400 hover:bg-zinc-800 hover:text-zinc-200'}`}
          >
            <Activity className="w-4 h-4" />
            History
          </button>
          <div className="w-px h-6 bg-zinc-800 mx-1" />
          <button 
            onClick={async () => {
              if (!activeWorkflowId) {
                addToast('Salve o workflow antes de executar', 'warning');
                return;
              }
              try {
                const execution = await executeWorkflow(activeWorkflowId);
                addToast('Execução iniciada!', 'success');
                setIsHistoryOpen(true);
                setSelectedExecutionId(execution.id);
              } catch {
                addToast('Erro ao iniciar execução', 'error');
              }
            }}
            className="flex items-center gap-2 px-4 py-2 bg-zinc-100 hover:bg-white text-zinc-900 rounded-lg text-sm font-bold transition-all">
            <Play className="w-4 h-4 fill-current" />
            Run
          </button>
        </div>
      </div>

      <div className="flex-1 relative flex overflow-hidden">
        {/* Sidebar */}
        <div className="w-64 bg-zinc-925 border-r border-zinc-800 flex flex-col shrink-0">
          <div className="p-4 border-b border-zinc-800 flex items-center justify-between">
            <h3 className="text-sm font-semibold text-zinc-200">Workflows</h3>
            <button 
              onClick={() => clear()}
              className="p-1.5 hover:bg-zinc-800 rounded-md text-zinc-400 hover:text-zinc-100"
              title="Novo Workflow"
            >
              <Plus className="w-4 h-4" />
            </button>
          </div>
          <div className="flex-1 overflow-y-auto p-2 space-y-1">
            {workflows.map(w => (
              <div 
                key={w.id}
                onClick={() => loadWorkflow(w.id)}
                className={`flex items-center justify-between p-2 rounded-lg cursor-pointer group ${
                  activeWorkflowId === w.id ? 'bg-zinc-800 text-teal-400' : 'text-zinc-400 hover:bg-zinc-800/50 hover:text-zinc-200'
                }`}
              >
                <div className="flex items-center gap-2 overflow-hidden">
                  <FolderOpen className="w-4 h-4 shrink-0" />
                  <span className="text-sm truncate">{w.name}</span>
                </div>
                <button
                  onClick={(e) => {
                    e.stopPropagation();
                    if(confirm('Deletar workflow?')) deleteWorkflow(w.id);
                  }}
                  className="opacity-0 group-hover:opacity-100 p-1 hover:bg-zinc-700 rounded text-red-400"
                >
                  <Trash2 className="w-3.5 h-3.5" />
                </button>
              </div>
            ))}
            {workflows.length === 0 && (
              <p className="text-xs text-zinc-500 text-center py-4">Nenhum workflow salvo.</p>
            )}
          </div>
        </div>

        {/* Canvas */}
        <div className="flex-1 relative">
          <ReactFlow
            nodes={nodes}
            edges={edges}
            onNodesChange={onNodesChange}
            onEdgesChange={onEdgesChange}
            onConnect={onConnect}
            onNodeClick={(_e, node) => setSelectedNodeId(node.id)}
            onPaneClick={() => setSelectedNodeId(null)}
            nodeTypes={nodeTypes}
            colorMode="dark"
            fitView
          >
            <Background color="#27272a" gap={20} />
            <Controls className="bg-zinc-900 border-zinc-800 fill-zinc-400" />
            
            <Panel position="top-right" className="bg-zinc-900/80 backdrop-blur-md p-4 border border-zinc-800 rounded-2xl m-4 w-64 shadow-2xl">
              <h3 className="text-white font-bold text-sm mb-3 flex items-center gap-2">
                <Zap className="w-4 h-4 text-amber-400" />
                Engine Status
              </h3>
              <div className="space-y-3">
                <div className="flex items-center justify-between text-xs">
                  <span className="text-zinc-500">Nodes Active</span>
                  <span className="text-zinc-200 font-mono">{nodes.length}</span>
                </div>
                <div className="flex items-center justify-between text-xs">
                  <span className="text-zinc-500">Connections</span>
                  <span className="text-zinc-200 font-mono">{edges.length}</span>
                </div>
                <div className="pt-2 border-t border-zinc-800">
                  <div className="flex items-center gap-2 text-[10px] text-teal-500">
                    <div className="w-1.5 h-1.5 rounded-full bg-teal-500 animate-pulse" />
                    Ready to Orchestrate
                  </div>
                </div>
              </div>
            </Panel>
          </ReactFlow>
        </div>

        {/* Properties Panel */}
        {selectedNodeId && (
          <div className="w-80 bg-zinc-925 border-l border-zinc-800 flex flex-col shrink-0">
            <div className="p-4 border-b border-zinc-800 flex items-center justify-between">
              <h3 className="text-sm font-semibold text-zinc-200">Node Properties</h3>
              <button onClick={() => setSelectedNodeId(null)} className="text-zinc-500 hover:text-zinc-300">
                <ChevronLeft className="w-4 h-4 rotate-180" />
              </button>
            </div>
            <div className="flex-1 p-4 overflow-y-auto">
              {nodes.filter(n => n.id === selectedNodeId).map(node => (
                <div key={node.id} className="space-y-4">
                  <div>
                    <label className="block text-xs text-zinc-400 mb-1">Label</label>
                    <input 
                      value={node.data.label as string || ''}
                      onChange={(e) => {
                        setNodes(nodes.map(n => n.id === node.id ? { ...n, data: { ...n.data, label: e.target.value } } : n));
                      }}
                      className="w-full px-3 py-2 bg-zinc-900 border border-zinc-700 rounded-lg text-sm text-zinc-200"
                    />
                  </div>
                  {node.type === 'agent' && (
                    <div>
                      <label className="block text-xs text-zinc-400 mb-1">Agent Name</label>
                      <input 
                        value={node.data.agentName as string || ''}
                        onChange={(e) => {
                          setNodes(nodes.map(n => n.id === node.id ? { ...n, data: { ...n.data, agentName: e.target.value } } : n));
                        }}
                        className="w-full px-3 py-2 bg-zinc-900 border border-zinc-700 rounded-lg text-sm text-zinc-200"
                      />
                    </div>
                  )}
                  {node.type === 'tool' && (
                    <div>
                      <label className="block text-xs text-zinc-400 mb-1">Tool Name</label>
                      <input 
                        value={node.data.toolName as string || ''}
                        onChange={(e) => {
                          setNodes(nodes.map(n => n.id === node.id ? { ...n, data: { ...n.data, toolName: e.target.value } } : n));
                        }}
                        className="w-full px-3 py-2 bg-zinc-900 border border-zinc-700 rounded-lg text-sm text-zinc-200"
                      />
                    </div>
                  )}
                  {node.type === 'decision' && (
                    <div>
                      <label className="block text-xs text-zinc-400 mb-1">Condition Expression</label>
                      <input 
                        value={node.data.condition as string || ''}
                        onChange={(e) => {
                          setNodes(nodes.map(n => n.id === node.id ? { ...n, data: { ...n.data, condition: e.target.value } } : n));
                        }}
                        className="w-full px-3 py-2 bg-zinc-900 border border-zinc-700 rounded-lg text-sm text-zinc-200 font-mono"
                      />
                    </div>
                  )}
                  {node.type === 'wait' && (
                    <div>
                      <label className="block text-xs text-zinc-400 mb-1">Timeout Duration</label>
                      <input 
                        value={node.data.timeout as string || ''}
                        onChange={(e) => {
                          setNodes(nodes.map(n => n.id === node.id ? { ...n, data: { ...n.data, timeout: e.target.value } } : n));
                        }}
                        className="w-full px-3 py-2 bg-zinc-900 border border-zinc-700 rounded-lg text-sm text-zinc-200 font-mono"
                        placeholder="HH:mm:ss"
                      />
                    </div>
                  )}
                  <div>
                    <label className="block text-xs text-zinc-400 mb-1">Description</label>
                    <textarea 
                      value={node.data.description as string || ''}
                      onChange={(e) => {
                        setNodes(nodes.map(n => n.id === node.id ? { ...n, data: { ...n.data, description: e.target.value } } : n));
                      }}
                      className="w-full px-3 py-2 bg-zinc-900 border border-zinc-700 rounded-lg text-sm text-zinc-200 resize-none h-20"
                    />
                  </div>
                </div>
              ))}
            </div>
          </div>
        )}

        <ExecutionHistoryPanel 
          workflowId={activeWorkflowId}
          isOpen={isHistoryOpen}
          onClose={() => setIsHistoryOpen(false)}
          onSelectExecution={setSelectedExecutionId}
        />
      </div>
    </div>
  );
}

export default function WorkflowBuilder() {
  return (
    <ReactFlowProvider>
      <WorkflowBuilderPage />
    </ReactFlowProvider>
  );
}
