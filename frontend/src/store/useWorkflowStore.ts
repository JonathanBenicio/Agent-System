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
import type { WorkflowDefinition, WorkflowStep } from '@/types/api';

export const WorkflowStepType = {
  Action: 0,
  Decision: 1,
  Parallel: 2,
  Wait: 3,
  Approval: 4,
  Subworkflow: 5
} as const;

interface WorkflowState {
  nodes: Node[];
  edges: Edge[];
  activeWorkflowId: string | null;
  workflowName: string;
  onNodesChange: (changes: NodeChange[]) => void;
  onEdgesChange: (changes: EdgeChange[]) => void;
  onConnect: (connection: Connection) => void;
  addNode: (node: Node) => void;
  setNodes: (nodes: Node[]) => void;
  setEdges: (edges: Edge[]) => void;
  setWorkflowName: (name: string) => void;
  setActiveWorkflowId: (id: string | null) => void;
  
  // Conversion logic
  toWorkflowDefinition: () => WorkflowDefinition;
  fromWorkflowDefinition: (def: WorkflowDefinition) => void;
  clear: () => void;
}

export const useWorkflowStore = create<WorkflowState>()((set, get) => ({
  nodes: [],
  edges: [],
  activeWorkflowId: null,
  workflowName: 'New Workflow',

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
  setWorkflowName: (workflowName: string) => set({ workflowName }),
  setActiveWorkflowId: (activeWorkflowId: string | null) => set({ activeWorkflowId }),

  clear: () => set({ nodes: [], edges: [], activeWorkflowId: null, workflowName: 'New Workflow' }),

  toWorkflowDefinition: (): WorkflowDefinition => {
    const { nodes, edges, activeWorkflowId, workflowName } = get();
    
    const steps: WorkflowStep[] = nodes.map(node => {
      const incomingEdges = edges.filter(e => e.target === node.id);
      const dependsOn = incomingEdges.map(e => e.source);
      
      return {
        id: node.id,
        name: node.data.label as string || node.id,
        stepType: (node.data.stepType as number) ?? 0,
        dependsOn,
        agentName: node.data.agentName as string,
        toolName: node.data.toolName as string,
        actionDescription: node.data.description as string,
        input: (node.data.input as Record<string, unknown>) || {},
        output: {},
        conditionExpression: node.data.condition as string,
        parallelSteps: [],
        maxRetries: 0,
        errorStrategy: 0,
      };
    });

    return {
      id: activeWorkflowId || crypto.randomUUID(),
      name: workflowName,
      version: 1,
      steps,
      variables: {},
      triggerType: 0,
      createdAt: new Date().toISOString(),
    };
  },

  fromWorkflowDefinition: (def: WorkflowDefinition) => {
    // Basic layout algorithm or just use saved positions if available in steps?
    // Current WorkflowDefinition doesn't store positions. 
    // In a real app, we'd store them in DefinitionJson or a separate field.
    // For now, let's just arrange them horizontally.
    
    const getNodeType = (step: WorkflowStep): string => {
      if (step.stepType === 1) return 'decision';
      if (step.stepType === 3) return 'wait';
      if (step.agentName) return 'agent';
      if (step.toolName) return 'tool';
      return 'agent';
    };

    const nodes: Node[] = def.steps.map((step, index) => ({
      id: step.id,
      type: getNodeType(step),
      position: { x: 100 + (index * 250), y: 100 + (index % 2 * 100) },
      data: { 
        label: step.name,
        stepType: step.stepType,
        agentName: step.agentName,
        toolName: step.toolName,
        description: step.actionDescription,
        input: step.input,
        condition: step.conditionExpression,
      },
    }));

    const edges: Edge[] = [];
    def.steps.forEach(step => {
      step.dependsOn.forEach(depId => {
        edges.push({
          id: `e-${depId}-${step.id}`,
          source: depId,
          target: step.id,
        });
      });
    });

    set({ 
      nodes, 
      edges, 
      activeWorkflowId: def.id, 
      workflowName: def.name 
    });
  }
}));
