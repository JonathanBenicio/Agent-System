import React, { useState } from 'react';
import { 
  FolderOpen, 
  Plus, 
  MoreVertical, 
  Clock, 
  Database, 
  Tag, 
  Search, 
  Upload, 
  FileText, 
  Trash2, 
  ExternalLink,
  Users,
  Lock,
  Settings2,
  Loader2
} from 'lucide-react';
import { useKnowledgeStore, KnowledgeRoom } from '@/store/useKnowledgeStore';
import { Badge } from '@/components/shared/Badge';
import { cn } from '@/lib/utils';
import { ragApi } from '@/lib/api';

export function KnowledgeRooms() {
  const { rooms, activeRoomId, activeWorkspaceId, addRoom, deleteRoom, setActiveRoom, updateRoom, setActiveWorkspace } = useKnowledgeStore();
  const [showCreateModal, setShowCreateModal] = useState(false);
  const [newRoomName, setNewRoomName] = useState('');
  const [newRoomDesc, setNewRoomDesc] = useState('');
  const [isDragging, setIsDragging] = useState(false);
  const [isUploading, setIsUploading] = useState(false);

  const activeRoom = rooms.find(r => r.id === activeRoomId);

  const handleCreateRoom = (e: React.FormEvent) => {
    e.preventDefault();
    if (!newRoomName) return;

    const colors = ['bg-blue-500', 'bg-teal-500', 'bg-purple-500', 'bg-amber-500', 'bg-rose-500', 'bg-indigo-500'];
    const randomColor = colors[Math.floor(Math.random() * colors.length)];

    const room: KnowledgeRoom = {
      id: crypto.randomUUID(),
      name: newRoomName,
      description: newRoomDesc,
      color: randomColor,
      icon: 'FolderOpen',
      documentCount: 0,
      lastUpdated: new Date().toISOString(),
      tags: ['Local']
    };

    addRoom(room);
    setNewRoomName('');
    setNewRoomDesc('');
    setShowCreateModal(false);
    setActiveRoom(room.id);
  };

  const handleDragOver = (e: React.DragEvent) => {
    e.preventDefault();
    setIsDragging(true);
  };

  const handleDragLeave = (e: React.DragEvent) => {
    e.preventDefault();
    setIsDragging(false);
  };

  const handleDrop = async (e: React.DragEvent) => {
    e.preventDefault();
    setIsDragging(false);
    if (e.dataTransfer.files && e.dataTransfer.files.length > 0) {
      await handleFileUpload(e.dataTransfer.files);
    }
  };

  const handleFileChange = async (e: React.ChangeEvent<HTMLInputElement>) => {
    if (e.target.files && e.target.files.length > 0) {
      await handleFileUpload(e.target.files);
    }
  };

  const handleFileUpload = async (files: FileList | File[]) => {
    if (!activeRoomId) return;
    setIsUploading(true);
    try {
      const result = await ragApi.ingestBatch(files, activeRoomId);
      if (activeRoom) {
        updateRoom(activeRoom.id, {
          documentCount: activeRoom.documentCount + result.succeeded,
          lastUpdated: new Date().toISOString()
        });
      }
    } catch (error) {
      console.error('Error uploading files:', error);
    } finally {
      setIsUploading(false);
    }
  };

  return (
    <div className="space-y-6">
      {!activeRoomId ? (
        <div className="space-y-4">
          <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
            <h2 className="text-lg font-semibold text-white flex items-center gap-2">
              <Database className="w-5 h-5 text-teal-400" />
              Your Knowledge Rooms
            </h2>
            <div className="flex items-center gap-3">
              <select 
                value={activeWorkspaceId}
                onChange={(e) => setActiveWorkspace(e.target.value)}
                className="bg-zinc-900 border border-zinc-800 text-zinc-300 text-sm rounded-lg px-3 py-1.5 focus:outline-none focus:border-teal-500/50"
              >
                <option value="tenant-default">Default Workspace</option>
                <option value="tenant-engineering">Engineering</option>
                <option value="tenant-legal">Legal</option>
                <option value="tenant-hr">HR</option>
              </select>
              <button 
                onClick={() => setShowCreateModal(true)}
                className="flex items-center gap-2 px-3 py-1.5 bg-teal-600 hover:bg-teal-500 text-white rounded-lg text-sm font-medium transition-all shadow-lg shadow-teal-900/20"
              >
                <Plus className="w-4 h-4" />
                New Room
              </button>
            </div>
          </div>

          {rooms.length === 0 ? (
            <div className="flex flex-col items-center justify-center py-20 bg-zinc-900/50 border border-zinc-800 border-dashed rounded-2xl">
              <div className="w-16 h-16 bg-zinc-800 rounded-full flex items-center justify-center mb-4">
                <Database className="w-8 h-8 text-zinc-500" />
              </div>
              <h3 className="text-zinc-300 font-medium">No Knowledge Rooms yet</h3>
              <p className="text-zinc-500 text-sm mt-1 mb-6 text-center max-w-xs">
                Create a room to organize your documents and enable specialized agent search.
              </p>
              <button 
                onClick={() => setShowCreateModal(true)}
                className="px-4 py-2 bg-zinc-800 hover:bg-zinc-700 text-zinc-300 border border-zinc-700 rounded-lg text-sm transition-all"
              >
                Get Started
              </button>
            </div>
          ) : (
            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
              {rooms.map((room) => (
                <div 
                  key={room.id}
                  onClick={() => setActiveRoom(room.id)}
                  className="group relative flex flex-col p-5 bg-zinc-900 border border-zinc-800 hover:border-zinc-700 rounded-2xl cursor-pointer transition-all hover:shadow-xl hover:shadow-black/40"
                >
                  <div className="flex items-start justify-between mb-4">
                    <div className={cn("w-10 h-10 rounded-xl flex items-center justify-center shadow-inner", room.color + "/20")}>
                      <FolderOpen className={cn("w-5 h-5", room.color.replace('bg-', 'text-'))} />
                    </div>
                    <div className="flex items-center gap-1 opacity-0 group-hover:opacity-100 transition-opacity">
                      <button 
                        onClick={(e) => { e.stopPropagation(); deleteRoom(room.id); }}
                        className="p-1.5 hover:bg-red-500/10 text-zinc-500 hover:text-red-400 rounded-lg"
                      >
                        <Trash2 className="w-4 h-4" />
                      </button>
                      <button className="p-1.5 hover:bg-zinc-800 text-zinc-500 hover:text-zinc-300 rounded-lg">
                        <MoreVertical className="w-4 h-4" />
                      </button>
                    </div>
                  </div>

                  <h3 className="text-white font-semibold mb-1 group-hover:text-teal-400 transition-colors">{room.name}</h3>
                  <p className="text-zinc-500 text-xs line-clamp-2 mb-6 h-8">{room.description || 'No description provided.'}</p>

                  <div className="mt-auto flex items-center justify-between pt-4 border-t border-zinc-800/50">
                    <div className="flex items-center gap-3 text-zinc-400">
                      <div className="flex items-center gap-1.5 text-[10px]">
                        <FileText className="w-3 h-3 text-zinc-500" />
                        {room.documentCount} docs
                      </div>
                      <div className="flex items-center gap-1.5 text-[10px]">
                        <Clock className="w-3 h-3 text-zinc-500" />
                        {new Date(room.lastUpdated).toLocaleDateString()}
                      </div>
                    </div>
                    <div className="flex -space-x-1.5">
                      {room.tags.slice(0, 2).map(tag => (
                        <div key={tag} className="px-1.5 py-0.5 bg-zinc-800 border border-zinc-700 rounded text-[9px] text-zinc-500">
                          {tag}
                        </div>
                      ))}
                    </div>
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      ) : (
        <div className="space-y-6 animate-in fade-in slide-in-from-bottom-2 duration-300">
          <div className="flex items-center gap-4">
            <button 
              onClick={() => setActiveRoom(null)}
              className="p-2 hover:bg-zinc-900 text-zinc-500 hover:text-zinc-300 rounded-xl border border-transparent hover:border-zinc-800 transition-all"
            >
              <Database className="w-5 h-5" />
            </button>
            <div className="flex-1">
              <div className="flex items-center gap-2">
                <h2 className="text-xl font-bold text-white">{activeRoom?.name}</h2>
                <Badge className="bg-teal-500/10 text-teal-400 border-teal-500/20">Active Room</Badge>
              </div>
              <p className="text-zinc-500 text-sm mt-0.5">{activeRoom?.description || 'Collection of documents and context.'}</p>
            </div>
            <div className="flex items-center gap-2">
              <button className="flex items-center gap-2 px-3 py-1.5 bg-zinc-900 border border-zinc-800 hover:border-zinc-700 text-zinc-300 rounded-lg text-sm font-medium transition-all">
                <Users className="w-4 h-4" />
                Manage Access
              </button>
              <button className="flex items-center gap-2 px-3 py-1.5 bg-zinc-900 border border-zinc-800 hover:border-zinc-700 text-zinc-300 rounded-lg text-sm font-medium transition-all">
                <Settings2 className="w-4 h-4" />
                Settings
              </button>
            </div>
          </div>

          <div className="grid grid-cols-1 lg:grid-cols-4 gap-6">
            <div className="lg:col-span-3 space-y-6">
              {/* Drop Zone */}
              <div 
                onDragOver={handleDragOver}
                onDragLeave={handleDragLeave}
                onDrop={handleDrop}
                className={cn(
                  "group relative flex flex-col items-center justify-center p-12 border-2 border-dashed rounded-3xl transition-all cursor-pointer",
                  isDragging 
                    ? "bg-teal-500/10 border-teal-500 shadow-lg shadow-teal-900/20 scale-[1.02]" 
                    : "bg-zinc-950 border-zinc-800 hover:border-teal-500/50 hover:bg-teal-500/[0.02]"
                )}
              >
                <div className="w-16 h-16 bg-zinc-900 rounded-2xl flex items-center justify-center mb-4 group-hover:scale-110 transition-transform">
                  {isUploading ? (
                    <Loader2 className="w-8 h-8 text-teal-400 animate-spin" />
                  ) : (
                    <Upload className={cn("w-8 h-8", isDragging ? "text-teal-400" : "text-teal-400/80 group-hover:text-teal-400")} />
                  )}
                </div>
                <h3 className="text-zinc-300 font-semibold text-lg">
                  {isUploading ? "Ingesting documents..." : isDragging ? "Drop files to ingest" : "Ingest new knowledge"}
                </h3>
                <p className="text-zinc-500 text-sm mt-1 text-center max-w-sm">
                  {isUploading 
                    ? "Processing your documents. This may take a moment."
                    : "Drag and drop files here, or click to browse. We support PDF, Markdown, DOCX and Text."}
                </p>
                <input 
                  type="file" 
                  multiple 
                  disabled={isUploading}
                  onChange={handleFileChange}
                  className="absolute inset-0 opacity-0 cursor-pointer disabled:cursor-default" 
                />
              </div>

              {/* Document List */}
              <div className="bg-zinc-900 border border-zinc-800 rounded-2xl overflow-hidden">
                <div className="p-4 border-b border-zinc-800 flex items-center justify-between bg-zinc-900/50">
                  <h3 className="font-semibold text-white flex items-center gap-2">
                    <FileText className="w-4 h-4 text-zinc-500" />
                    Documents Ingested
                  </h3>
                  <div className="relative">
                    <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-zinc-500" />
                    <input 
                      type="text" 
                      placeholder="Search room..."
                      className="pl-9 pr-4 py-1.5 bg-zinc-950 border border-zinc-800 rounded-lg text-xs text-zinc-300 focus:outline-none focus:border-teal-500/50 transition-all w-64"
                    />
                  </div>
                </div>
                <div className="divide-y divide-zinc-800/50">
                  {/* Empty State Document List */}
                  <div className="p-12 text-center">
                    <div className="w-12 h-12 bg-zinc-950 rounded-full flex items-center justify-center mx-auto mb-3">
                      <FileText className="w-6 h-6 text-zinc-700" />
                    </div>
                    <p className="text-zinc-500 text-sm">This room is currently empty.</p>
                  </div>
                </div>
              </div>
            </div>

            <div className="space-y-6">
              {/* Room Stats */}
              <div className="bg-zinc-900 border border-zinc-800 rounded-2xl p-5">
                <h3 className="text-white font-semibold mb-4 text-sm">Room Analytics</h3>
                <div className="space-y-4">
                  <div>
                    <div className="flex items-center justify-between text-xs text-zinc-500 mb-1.5">
                      <span>Neural Recall Accuracy</span>
                      <span className="text-teal-400">94%</span>
                    </div>
                    <div className="w-full h-1.5 bg-zinc-950 rounded-full overflow-hidden">
                      <div className="h-full bg-teal-500 w-[94%]" />
                    </div>
                  </div>
                  <div>
                    <div className="flex items-center justify-between text-xs text-zinc-500 mb-1.5">
                      <span>Index Freshness</span>
                      <span className="text-blue-400">100%</span>
                    </div>
                    <div className="w-full h-1.5 bg-zinc-950 rounded-full overflow-hidden">
                      <div className="h-full bg-blue-500 w-full" />
                    </div>
                  </div>
                  <div className="pt-2">
                    <div className="grid grid-cols-2 gap-3">
                      <div className="bg-zinc-950 p-3 rounded-xl border border-zinc-800/50">
                        <div className="text-[10px] text-zinc-500 uppercase tracking-wider mb-1">Total Chunks</div>
                        <div className="text-lg font-bold text-white">0</div>
                      </div>
                      <div className="bg-zinc-950 p-3 rounded-xl border border-zinc-800/50">
                        <div className="text-[10px] text-zinc-500 uppercase tracking-wider mb-1">Search Ops</div>
                        <div className="text-lg font-bold text-white">12</div>
                      </div>
                    </div>
                  </div>
                </div>
              </div>

              {/* Connected Agents */}
              <div className="bg-zinc-900 border border-zinc-800 rounded-2xl p-5">
                <h3 className="text-white font-semibold mb-4 text-sm">Agents with Access</h3>
                <div className="flex flex-wrap gap-2">
                  <Badge className="bg-zinc-950 border-zinc-800 text-zinc-400">General Orchestrator</Badge>
                  <Badge className="bg-zinc-950 border-zinc-800 text-zinc-400">Knowledge Specialist</Badge>
                  <button className="w-8 h-8 rounded-full border border-zinc-800 border-dashed flex items-center justify-center text-zinc-600 hover:text-teal-400 hover:border-teal-500/50 transition-all">
                    <Plus className="w-4 h-4" />
                  </button>
                </div>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* Create Room Modal */}
      {showCreateModal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/80 backdrop-blur-sm animate-in fade-in duration-200">
          <div className="w-full max-w-md bg-zinc-900 border border-zinc-800 rounded-3xl shadow-2xl overflow-hidden">
            <div className="p-6 border-b border-zinc-800 bg-zinc-900/50">
              <h3 className="text-xl font-bold text-white">Create Knowledge Room</h3>
              <p className="text-zinc-500 text-sm mt-1">Isolate and organize context for your agents.</p>
            </div>
            <form onSubmit={handleCreateRoom} className="p-6 space-y-4">
              <div>
                <label className="block text-xs font-medium text-zinc-400 mb-1.5 uppercase tracking-wider">Room Name</label>
                <input 
                  autoFocus
                  type="text" 
                  value={newRoomName}
                  onChange={(e) => setNewRoomName(e.target.value)}
                  placeholder="e.g. Legal Documents, Product Wiki"
                  className="w-full px-4 py-2.5 bg-zinc-950 border border-zinc-800 rounded-xl text-zinc-200 focus:outline-none focus:border-teal-500/50 transition-all"
                />
              </div>
              <div>
                <label className="block text-xs font-medium text-zinc-400 mb-1.5 uppercase tracking-wider">Description (Optional)</label>
                <textarea 
                  value={newRoomDesc}
                  onChange={(e) => setNewRoomDesc(e.target.value)}
                  placeholder="What is this room about?"
                  rows={3}
                  className="w-full px-4 py-2.5 bg-zinc-950 border border-zinc-800 rounded-xl text-zinc-200 focus:outline-none focus:border-teal-500/50 transition-all resize-none"
                />
              </div>
              <div className="flex items-center gap-3 pt-4">
                <button 
                  type="button"
                  onClick={() => setShowCreateModal(false)}
                  className="flex-1 px-4 py-2.5 bg-zinc-800 hover:bg-zinc-700 text-zinc-300 rounded-xl text-sm font-medium transition-all"
                >
                  Cancel
                </button>
                <button 
                  type="submit"
                  className="flex-1 px-4 py-2.5 bg-teal-600 hover:bg-teal-500 text-white rounded-xl text-sm font-medium transition-all shadow-lg shadow-teal-900/20"
                >
                  Create Room
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
