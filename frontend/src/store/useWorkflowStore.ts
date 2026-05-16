import { create } from 'zustand';

console.log('Zustand create (workflow):', create);
import type { 
  Connection, 
  Edge, 
  EdgeChange, 
  Node, 
  NodeChange 
} from '@xyflow/react';
import { 
  addEdge, 
  applyNodeChanges,
  applyEdgeChanges
} from '@xyflow/react';

export interface WorkflowDefinition {
  id: string;
  name: string;
  steps: WorkflowStep[];
}

export const WorkflowStepType = {
  Action: 0,
  Decision: 1,
  Parallel: 2,
  Event: 3
} as const;

export type WorkflowStepType = typeof WorkflowStepType[keyof typeof WorkflowStepType];

export interface WorkflowStep {
  id: string;
  name: string;
  stepType: WorkflowStepType;
  dependsOn: string[];
  agentName?: string;
  toolName?: string;
  actionDescription?: string;
  input: Record<string, unknown>;
  conditionExpression?: string;
}

interface WorkflowState {
  nodes: Node[];
  edges: Edge[];
  onNodesChange: (changes: NodeChange[]) => void;
  onEdgesChange: (changes: EdgeChange[]) => void;
  onConnect: (connection: Connection) => void;
  addNode: (node: Node) => void;
  setNodes: (nodes: Node[]) => void;
  setEdges: (edges: Edge[]) => void;
  
  // Conversion logic
  toWorkflowDefinition: (name: string) => WorkflowDefinition;
}

export const useWorkflowStore = create<WorkflowState>()((set, get) => ({
  nodes: [],
  edges: [],

  onNodesChange: (changes: NodeChange[]) => {
    set({
      nodes: applyNodeChanges(changes, get().nodes),
    });
  },

  onEdgesChange: (changes: EdgeChange[]) => {
    set({
      edges: applyEdgeChanges(changes, get().edges),
    });
  },

  onConnect: (connection: Connection) => {
    set({
      edges: addEdge(connection, get().edges),
    });
  },

  addNode: (node: Node) => {
    set({
      nodes: [...get().nodes, node],
    });
  },

  setNodes: (nodes: Node[]) => set({ nodes }),
  setEdges: (edges: Edge[]) => set({ edges }),

  toWorkflowDefinition: (name: string): WorkflowDefinition => {
    const { nodes, edges } = get();
    
    const steps: WorkflowStep[] = nodes.map(node => {
      const incomingEdges = edges.filter(e => e.target === node.id);
      const dependsOn = incomingEdges.map(e => e.source);
      
      return {
        id: node.id,
        name: node.data.label as string || node.id,
        stepType: (node.data.stepType as WorkflowStepType) ?? WorkflowStepType.Action,
        dependsOn,
        agentName: node.data.agentName as string,
        toolName: node.data.toolName as string,
        actionDescription: node.data.description as string,
        input: (node.data.input as Record<string, unknown>) || {},
        conditionExpression: node.data.condition as string,
      };
    });

    return {
      id: crypto.randomUUID(),
      name,
      steps
    };
  }
}));
