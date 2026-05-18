import React, { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { X, Users, Shield, Loader2, Trash2 } from 'lucide-react';
import { knowledgeRoomApi } from '@/lib/api';
import type { KnowledgeRoomRole } from '@/types/api';

interface RoomAccessModalProps {
  roomId: string;
  onClose: () => void;
}

export function RoomAccessModal({ roomId, onClose }: RoomAccessModalProps) {
  const queryClient = useQueryClient();
  const [newUserId, setNewUserId] = useState('');
  const [newRole, setNewRole] = useState<KnowledgeRoomRole>('Reader');
  const [errorMsg, setErrorMsg] = useState('');

  const { data: permissions = [], isLoading } = useQuery({
    queryKey: ['knowledge-room-permissions', roomId],
    queryFn: () => knowledgeRoomApi.getPermissions(roomId),
    retry: false
  });

  const addMutation = useMutation({
    mutationFn: () => knowledgeRoomApi.updatePermission(roomId, { userId: newUserId, role: newRole }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['knowledge-room-permissions', roomId] });
      setNewUserId('');
      setErrorMsg('');
    },
    onError: (err: any) => {
      setErrorMsg(err?.message || 'Failed to update permission. Are you an Admin?');
    }
  });

  const removeMutation = useMutation({
    mutationFn: (targetUserId: string) => knowledgeRoomApi.deletePermission(roomId, targetUserId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['knowledge-room-permissions', roomId] });
    },
    onError: (err: any) => {
      setErrorMsg(err?.message || 'Failed to remove permission.');
    }
  });

  const handleAdd = (e: React.FormEvent) => {
    e.preventDefault();
    if (!newUserId.trim()) return;
    addMutation.mutate();
  };

  return (
    <div className="fixed inset-0 z-[60] flex items-center justify-center p-4 bg-black/80 backdrop-blur-sm animate-in fade-in duration-200">
      <div className="w-full max-w-lg bg-zinc-900 border border-zinc-800 rounded-3xl shadow-2xl overflow-hidden flex flex-col max-h-[85vh]">
        <div className="p-6 border-b border-zinc-800 bg-zinc-900/50 flex justify-between items-start">
          <div>
            <h3 className="text-xl font-bold text-white flex items-center gap-2">
              <Shield className="w-5 h-5 text-teal-400" />
              Manage Access
            </h3>
            <p className="text-zinc-500 text-sm mt-1">Control who can view and edit this room.</p>
          </div>
          <button onClick={onClose} className="p-2 text-zinc-500 hover:text-white rounded-lg hover:bg-zinc-800 transition-colors">
            <X className="w-5 h-5" />
          </button>
        </div>

        <div className="p-6 overflow-y-auto flex-1 space-y-6">
          {errorMsg && (
            <div className="p-3 rounded-xl bg-red-500/10 border border-red-500/20 text-red-400 text-sm">
              {errorMsg}
            </div>
          )}

          <form onSubmit={handleAdd} className="flex items-end gap-3 p-4 bg-zinc-950 rounded-2xl border border-zinc-800/50">
            <div className="flex-1">
              <label className="block text-xs font-medium text-zinc-400 mb-1.5 uppercase tracking-wider">User ID / Email</label>
              <input 
                type="text" 
                value={newUserId}
                onChange={(e) => setNewUserId(e.target.value)}
                placeholder="e.g. user-123 or email@domain.com"
                className="w-full px-3 py-2 bg-zinc-900 border border-zinc-800 rounded-lg text-sm text-white focus:outline-none focus:border-teal-500/50"
              />
            </div>
            <div>
              <label className="block text-xs font-medium text-zinc-400 mb-1.5 uppercase tracking-wider">Role</label>
              <select 
                value={newRole}
                onChange={(e) => setNewRole(e.target.value as KnowledgeRoomRole)}
                className="px-3 py-2 bg-zinc-900 border border-zinc-800 rounded-lg text-sm text-white focus:outline-none focus:border-teal-500/50"
              >
                <option value="Reader">Reader</option>
                <option value="Editor">Editor</option>
                <option value="Admin">Admin</option>
              </select>
            </div>
            <button 
              type="submit"
              disabled={addMutation.isPending || !newUserId.trim()}
              className="px-4 py-2 bg-teal-600 hover:bg-teal-500 disabled:opacity-50 disabled:cursor-not-allowed text-white rounded-lg text-sm font-medium transition-all shadow-lg shadow-teal-900/20 h-[38px] flex items-center justify-center min-w-[80px]"
            >
              {addMutation.isPending ? <Loader2 className="w-4 h-4 animate-spin" /> : 'Invite'}
            </button>
          </form>

          <div>
            <h4 className="text-sm font-medium text-zinc-400 mb-3 flex items-center gap-2 uppercase tracking-wider">
              <Users className="w-4 h-4" /> Current Members
            </h4>
            <div className="space-y-2">
              {isLoading ? (
                <div className="flex items-center justify-center p-8">
                  <Loader2 className="w-6 h-6 text-teal-400 animate-spin" />
                </div>
              ) : permissions.length === 0 ? (
                <div className="text-center p-6 text-zinc-500 text-sm border border-zinc-800 border-dashed rounded-xl">
                  Only you have access to this room right now.
                </div>
              ) : (
                permissions.map((p) => (
                  <div key={p.id} className="flex items-center justify-between p-3 bg-zinc-900/50 border border-zinc-800 rounded-xl">
                    <div>
                      <div className="text-white text-sm font-medium">{p.userId}</div>
                      <div className="text-zinc-500 text-xs">Granted {new Date(p.grantedAt).toLocaleDateString()}</div>
                    </div>
                    <div className="flex items-center gap-3">
                      <span className="px-2 py-1 bg-zinc-800 text-zinc-300 text-xs rounded-md font-medium">
                        {p.role}
                      </span>
                      <button 
                        onClick={() => removeMutation.mutate(p.userId)}
                        disabled={removeMutation.isPending}
                        className="p-1.5 hover:bg-red-500/10 text-zinc-500 hover:text-red-400 rounded-lg transition-colors"
                        title="Remove Access"
                      >
                        <Trash2 className="w-4 h-4" />
                      </button>
                    </div>
                  </div>
                ))
              )}
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}