import { create } from 'zustand';

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
  Action: 'action',
  Agent: 'agent',
  Decision: 'decision',
  Parallel: 'parallel',
  Wait: 'wait',
  Approval: 'approval',
  Subworkflow: 'subworkflow'
} as const;

interface WorkflowState {
  nodes: Node[];
  edges: Edge[];
  activeWorkflowId: string | null;
  workflowName: string;
  originalDefinition: WorkflowDefinition | null;
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
  originalDefinition: null,

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

  clear: () => set({ nodes: [], edges: [], activeWorkflowId: null, workflowName: 'New Workflow', originalDefinition: null }),

  toWorkflowDefinition: (): WorkflowDefinition => {
    const { nodes, edges, activeWorkflowId, workflowName, originalDefinition } = get();
    
    const steps: WorkflowStep[] = nodes.map(node => {
      const incomingEdges = edges.filter(e => e.target === node.id);
      const dependsOn = incomingEdges.map(e => e.source);
      
      return {
        ...node.data.originalStep as WorkflowStep | undefined,
        id: node.id,
        name: node.data.label as string || node.id,
        stepType: (node.data.stepType as WorkflowStep['stepType']) ?? WorkflowStepType.Action,
        dependsOn,
        agentName: node.data.agentName as string,
        toolName: node.data.toolName as string,
        actionDescription: node.data.description as string,
        input: (node.data.input as Record<string, unknown>) || {},
        output: (node.data.originalStep as WorkflowStep | undefined)?.output ?? {},
        conditionExpression: node.data.condition as string,
        parallelSteps: (node.data.originalStep as WorkflowStep | undefined)?.parallelSteps ?? [],
        maxRetries: (node.data.originalStep as WorkflowStep | undefined)?.maxRetries ?? 0,
        errorStrategy: (node.data.originalStep as WorkflowStep | undefined)?.errorStrategy ?? 0,
      };
    });

    return {
      ...originalDefinition,
      id: activeWorkflowId || crypto.randomUUID(),
      name: workflowName,
      version: originalDefinition?.version ?? 1,
      steps,
      variables: originalDefinition?.variables ?? {},
      triggerType: originalDefinition?.triggerType ?? 0,
      createdAt: originalDefinition?.createdAt ?? new Date().toISOString(),
    };
  },

  fromWorkflowDefinition: (def: WorkflowDefinition) => {
    // Basic layout algorithm or just use saved positions if available in steps?
    // Current WorkflowDefinition doesn't store positions. 
    // In a real app, we'd store them in DefinitionJson or a separate field.
    // For now, let's just arrange them horizontally.
    
    const getNodeType = (step: WorkflowStep): string => {
      if (step.stepType === WorkflowStepType.Decision) return 'decision';
      if (step.stepType === WorkflowStepType.Wait) return 'wait';
      if (step.agentName) return 'agent';
      if (step.toolName) return 'tool';
      return 'agent';
    };

    const nodes: Node[] = def.steps.map((step, index) => ({
      id: step.id,
      type: getNodeType(step),
      position: { x: 100 + (index * 250), y: 100 + (index % 2 * 100) },
      data: { 
        originalStep: step,
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
      ,originalDefinition: def
    });
  }
}));
