import { create } from 'zustand';
import { persist } from 'zustand/middleware';

interface KnowledgeState {
  activeWorkspaceId: string;
  setActiveWorkspace: (id: string) => void;
}

export const useKnowledgeStore = create<KnowledgeState>()(
  persist(
    (set) => ({
      activeWorkspaceId: '',
      setActiveWorkspace: (id) => set({ activeWorkspaceId: id }),
    }),
    {
      name: 'agentic-knowledge-storage',
    }
  )
);
