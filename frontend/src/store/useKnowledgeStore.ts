import { create } from 'zustand';
import { persist } from 'zustand/middleware';

export interface KnowledgeRoom {
  id: string;
  name: string;
  description: string;
  color: string;
  icon: string;
  documentCount: number;
  lastUpdated: string;
  tags: string[];
}

interface KnowledgeState {
  rooms: KnowledgeRoom[];
  activeRoomId: string | null;
  activeWorkspaceId: string;
  isLoading: boolean;
  error: string | null;
  
  // Actions
  setRooms: (rooms: KnowledgeRoom[]) => void;
  addRoom: (room: KnowledgeRoom) => void;
  updateRoom: (id: string, room: Partial<KnowledgeRoom>) => void;
  deleteRoom: (id: string) => void;
  setActiveRoom: (id: string | null) => void;
  setActiveWorkspace: (id: string) => void;
  setLoading: (isLoading: boolean) => void;
  setError: (error: string | null) => void;
}

export const useKnowledgeStore = create<KnowledgeState>()(
  persist(
    (set) => ({
      rooms: [],
      activeRoomId: null,
      activeWorkspaceId: 'tenant-default',
      isLoading: false,
      error: null,

      setRooms: (rooms) => set({ rooms }),
      addRoom: (room) => set((state) => ({ rooms: [...state.rooms, room] })),
      updateRoom: (id, updatedRoom) => set((state) => ({
        rooms: state.rooms.map((r) => r.id === id ? { ...r, ...updatedRoom } : r)
      })),
      deleteRoom: (id) => set((state) => ({
        rooms: state.rooms.filter((r) => r.id !== id),
        activeRoomId: state.activeRoomId === id ? null : state.activeRoomId
      })),
      setActiveRoom: (id) => set({ activeRoomId: id }),
      setActiveWorkspace: (id) => set({ activeWorkspaceId: id }),
      setLoading: (isLoading) => set({ isLoading }),
      setError: (error) => set({ error }),
    }),
    {
      name: 'agentic-knowledge-storage',
    }
  )
);
