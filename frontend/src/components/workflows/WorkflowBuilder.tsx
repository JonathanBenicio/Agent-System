// import React from 'react';
import { 
  ReactFlow, 
  Background, 
  Controls, 
  Panel,
  useReactFlow,
  ReactFlowProvider
} from '@xyflow/react';
import type { Node } from '@xyflow/react';
import '@xyflow/react/dist/style.css';
import { 
  Play, 
  Save, 
  // Plus, 
  Bot, 
  Wrench, 
  // Split, 
  Zap, 
  // Trash2,
  ChevronLeft
} from 'lucide-react';
import { useWorkflowStore, WorkflowStepType } from '@/store/useWorkflowStore';
import { useNavigate } from 'react-router-dom';

// Simple Custom Node Components (Internal for now)
const AgentNode = ({ data }: any) => (
  <div className="px-4 py-3 shadow-xl rounded-xl bg-zinc-900 border-2 border-teal-500/50 min-w-[150px]">
    <div className="flex items-center gap-2 mb-1">
      <Bot className="w-4 h-4 text-teal-400" />
      <span className="text-xs font-bold text-teal-400 uppercase tracking-wider">Agent</span>
    </div>
    <div className="text-sm font-semibold text-white">{data.label}</div>
    <div className="text-[10px] text-zinc-500 mt-1">{data.agentName}</div>
  </div>
);

const ToolNode = ({ data }: any) => (
  <div className="px-4 py-3 shadow-xl rounded-xl bg-zinc-900 border-2 border-blue-500/50 min-w-[150px]">
    <div className="flex items-center gap-2 mb-1">
      <Wrench className="w-4 h-4 text-blue-400" />
      <span className="text-xs font-bold text-blue-400 uppercase tracking-wider">Tool</span>
    </div>
    <div className="text-sm font-semibold text-white">{data.label}</div>
    <div className="text-[10px] text-zinc-500 mt-1">{data.toolName}</div>
  </div>
);

const nodeTypes = {
  agent: AgentNode,
  tool: ToolNode,
};

export function WorkflowBuilderPage() {
  const navigate = useNavigate();
  const { 
    nodes, 
    edges, 
    onNodesChange, 
    onEdgesChange, 
    onConnect, 
    addNode, 
    toWorkflowDefinition 
  } = useWorkflowStore();
  
  const { screenToFlowPosition } = useReactFlow();

  const onAddAgent = () => {
    const id = `node_${Date.now()}`;
    // Adiciona no centro da tela com um pequeno offset aleatório para não sobrepor
    const position = screenToFlowPosition({
      x: window.innerWidth / 2 + (Math.random() - 0.5) * 100,
      y: window.innerHeight / 2 + (Math.random() - 0.5) * 100,
    });

    const newNode: Node = {
      id,
      type: 'agent',
      position,
      data: { 
        label: 'New Agent Task', 
        agentName: 'orchestrator',
        stepType: WorkflowStepType.Action 
      },
    };
    addNode(newNode);
  };

  const onAddTool = () => {
    const id = `node_${Date.now()}`;
    // Adiciona no centro da tela com um pequeno offset aleatório para não sobrepor
    const position = screenToFlowPosition({
      x: window.innerWidth / 2 + (Math.random() - 0.5) * 100,
      y: window.innerHeight / 2 + (Math.random() - 0.5) * 100,
    });

    const newNode: Node = {
      id,
      type: 'tool',
      position,
      data: { 
        label: 'New Tool Task', 
        toolName: 'http_tool',
        stepType: WorkflowStepType.Action 
      },
    };
    addNode(newNode);
  };

  const handleSave = () => {
    const definition = toWorkflowDefinition("Visual Workflow " + new Date().toLocaleDateString());
    console.log("Saving Workflow Definition:", definition);
    // TODO: Send to API
    alert("Workflow Definition generated (check console). API integration coming next.");
  };

  return (
    <div className="h-full w-full bg-zinc-950 flex flex-col">
      {/* Toolbar */}
      <div className="h-16 border-b border-zinc-800 bg-zinc-900/50 flex items-center justify-between px-6 shrink-0">
        <div className="flex items-center gap-4">
          <button 
            onClick={() => navigate(-1)}
            className="p-2 hover:bg-zinc-800 rounded-lg text-zinc-400 transition-all"
          >
            <ChevronLeft className="w-5 h-5" />
          </button>
          <div>
            <h1 className="text-lg font-bold text-white leading-none">Workflow Builder</h1>
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
          <div className="w-px h-6 bg-zinc-800 mx-1" />
          <button 
            onClick={handleSave}
            className="flex items-center gap-2 px-4 py-2 bg-teal-600 hover:bg-teal-500 text-white rounded-lg text-sm font-bold transition-all shadow-lg shadow-teal-900/20"
          >
            <Save className="w-4 h-4" />
            Save Workflow
          </button>
          <button className="flex items-center gap-2 px-4 py-2 bg-zinc-100 hover:bg-white text-zinc-900 rounded-lg text-sm font-bold transition-all">
            <Play className="w-4 h-4 fill-current" />
            Run
          </button>
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
