import React, { useState, useEffect } from 'react';
import { 
  Cable, 
  Plus, 
  Trash2, 
  Copy, 
  Check, 
  Zap, 
  Clock, 
  Shield, 
  RefreshCw,
  Search,
  ExternalLink,
  Bot,
  Workflow as WorkflowIcon
} from 'lucide-react';
import { Badge } from '@/components/shared/Badge';
import { useToast } from '@/components/shared/Toast';

import { webhookApi, type InboundWebhook } from '@/lib/api';

export function WebhooksPage() {
  const [webhooks, setWebhooks] = useState<InboundWebhook[]>([]);
  const [loading, setLoading] = useState(true);
  const [showForm, setShowForm] = useState(false);
  const [newName, setNewName] = useState('');
  const [targetWorkflowId, setTargetWorkflowId] = useState('');
  const [targetAgentName, setTargetAgentName] = useState('');
  const [copiedId, setCopiedId] = useState<string | null>(null);
  const { addToast } = useToast();

  const fetchWebhooks = async () => {
    setLoading(true);
    try {
      const data = await webhookApi.list();
      setWebhooks(data);
    } catch (err) {
      addToast('Erro ao carregar webhooks', 'error');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchWebhooks();
  }, []);

  const handleCreate = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      await webhookApi.create({
        name: newName,
        targetWorkflowId: targetWorkflowId || undefined,
        targetAgentName: targetAgentName || undefined,
      });
      addToast('Webhook criado com sucesso', 'success');
      setNewName('');
      setTargetWorkflowId('');
      setTargetAgentName('');
      setShowForm(false);
      fetchWebhooks();
    } catch (err) {
      addToast('Erro ao criar webhook', 'error');
    }
  };

  const handleDelete = async (id: string) => {
    if (!confirm('Tem certeza que deseja remover este webhook?')) return;
    try {
      await webhookApi.delete(id);
      addToast('Webhook removido', 'success');
      fetchWebhooks();
    } catch (err) {
      addToast('Erro ao remover webhook', 'error');
    }
  };

  const copyToClipboard = (text: string, id: string) => {
    navigator.clipboard.writeText(text);
    setCopiedId(id);
    setTimeout(() => setCopiedId(null), 2000);
    addToast('Copiado para a área de transferência', 'success');
  };

  const getWebhookUrl = (id: string) => `${window.location.origin}/api/webhooks/receive/${id}`;

  return (
    <div className="h-full overflow-y-auto bg-zinc-950 text-zinc-100">
      <div className="max-w-7xl mx-auto px-6 py-8 space-y-8">
        
        {/* Header */}
        <div className="flex flex-col md:flex-row md:items-center justify-between gap-4 border-b border-zinc-800/80 pb-6">
          <div>
            <div className="flex items-center gap-3">
              <h1 className="text-2xl font-bold tracking-tight text-white">Dynamic Webhooks Engine</h1>
              <Badge variant="success" className="bg-amber-500/10 text-amber-400 border border-amber-500/20 px-2 py-0.5">
                Phase 2 Universal
              </Badge>
            </div>
            <p className="text-sm text-zinc-400 mt-1">
              Gerencie pontos de entrada dinâmicos para disparar workflows e agentes a partir de sistemas externos.
            </p>
          </div>
          <div className="flex gap-2">
            <button
              onClick={fetchWebhooks}
              className="p-2 bg-zinc-900 border border-zinc-800 hover:border-zinc-700 text-zinc-400 rounded-lg transition-all"
            >
              <RefreshCw className={`w-4 h-4 ${loading ? 'animate-spin' : ''}`} />
            </button>
            <button
              onClick={() => setShowForm(true)}
              className="flex items-center gap-2 px-4 py-2 bg-teal-600 hover:bg-teal-500 text-white rounded-lg text-sm font-bold transition-all shadow-lg shadow-teal-900/20"
            >
              <Plus className="w-4 h-4" />
              Novo Webhook
            </button>
          </div>
        </div>

        {showForm && (
          <div className="bg-zinc-900/80 border border-zinc-800 rounded-2xl p-6 animate-in fade-in slide-in-from-top-4 duration-300">
            <h3 className="text-lg font-semibold text-white mb-4">Configurar Novo Ponto de Entrada</h3>
            <form onSubmit={handleCreate} className="grid grid-cols-1 md:grid-cols-2 gap-6">
              <div className="space-y-4">
                <label className="block">
                  <span className="text-xs font-medium text-zinc-400 uppercase tracking-wider">Nome Amigável</span>
                  <input
                    type="text"
                    required
                    value={newName}
                    onChange={e => setNewName(e.target.value)}
                    placeholder="ex: GitHub Issues Trigger"
                    className="w-full mt-1.5 bg-zinc-950 border border-zinc-800 rounded-xl px-4 py-2.5 text-sm text-white focus:outline-none focus:border-teal-500 transition-all"
                  />
                </label>
                <label className="block">
                  <span className="text-xs font-medium text-zinc-400 uppercase tracking-wider">Agente Alvo (Opcional)</span>
                  <input
                    type="text"
                    value={targetAgentName}
                    onChange={e => setTargetAgentName(e.target.value)}
                    placeholder="ex: orchestrator"
                    className="w-full mt-1.5 bg-zinc-950 border border-zinc-800 rounded-xl px-4 py-2.5 text-sm text-white focus:outline-none focus:border-teal-500 transition-all"
                  />
                </label>
              </div>
              <div className="space-y-4">
                <label className="block">
                  <span className="text-xs font-medium text-zinc-400 uppercase tracking-wider">Workflow ID (Opcional)</span>
                  <input
                    type="text"
                    value={targetWorkflowId}
                    onChange={e => setTargetWorkflowId(e.target.value)}
                    placeholder="ex: workflow_123"
                    className="w-full mt-1.5 bg-zinc-950 border border-zinc-800 rounded-xl px-4 py-2.5 text-sm text-white focus:outline-none focus:border-teal-500 transition-all"
                  />
                </label>
                <div className="flex items-end h-full pb-0.5">
                  <div className="flex gap-2 w-full">
                    <button
                      type="button"
                      onClick={() => setShowForm(false)}
                      className="flex-1 px-4 py-2.5 bg-zinc-800 hover:bg-zinc-700 text-zinc-300 rounded-xl text-sm font-medium transition-all"
                    >
                      Cancelar
                    </button>
                    <button
                      type="submit"
                      className="flex-1 px-4 py-2.5 bg-teal-600 hover:bg-teal-500 text-white rounded-xl text-sm font-bold transition-all"
                    >
                      Salvar Webhook
                    </button>
                  </div>
                </div>
              </div>
            </form>
          </div>
        )}

        {/* Webhooks Grid */}
        <div className="grid grid-cols-1 gap-4">
          {webhooks.map(w => (
            <div key={w.id} className="group bg-zinc-900/40 border border-zinc-800/80 hover:border-zinc-700/80 backdrop-blur-md rounded-2xl p-6 transition-all shadow-sm">
              <div className="flex flex-col md:flex-row justify-between gap-6">
                <div className="flex gap-4">
                  <div className="w-12 h-12 rounded-2xl bg-teal-500/10 border border-teal-500/20 flex items-center justify-center shrink-0">
                    <Zap className="w-6 h-6 text-teal-400" />
                  </div>
                  <div className="space-y-1">
                    <div className="flex items-center gap-2">
                      <h3 className="font-bold text-white text-lg">{w.name}</h3>
                      <Badge variant={w.isActive ? 'success' : 'default'} className={w.isActive ? 'bg-teal-500/10 text-teal-400' : ''}>
                        {w.isActive ? 'Ativo' : 'Inativo'}
                      </Badge>
                    </div>
                    <div className="flex flex-wrap items-center gap-x-4 gap-y-1 text-xs text-zinc-500">
                      <div className="flex items-center gap-1.5">
                        <Clock className="w-3.5 h-3.5" />
                        Criado em {new Date(w.createdAt).toLocaleDateString()}
                      </div>
                      <div className="flex items-center gap-1.5">
                        <Shield className="w-3.5 h-3.5" />
                        Secret: <code className="text-teal-500 bg-zinc-950 px-1 rounded">HIDDEN</code>
                      </div>
                      {w.lastTriggeredAt && (
                        <div className="flex items-center gap-1.5 text-teal-400 font-medium">
                          <RefreshCw className="w-3.5 h-3.5" />
                          Último disparo: {new Date(w.lastTriggeredAt).toLocaleString()}
                        </div>
                      )}
                    </div>
                  </div>
                </div>

                <div className="flex flex-col justify-center space-y-3 min-w-[280px]">
                  <div className="bg-zinc-950 border border-zinc-800 rounded-xl p-2 flex items-center gap-2 group-hover:border-zinc-700 transition-all">
                    <input 
                      readOnly 
                      value={getWebhookUrl(w.id)} 
                      className="bg-transparent border-none text-[10px] font-mono text-zinc-400 flex-1 px-1 focus:outline-none"
                    />
                    <button 
                      onClick={() => copyToClipboard(getWebhookUrl(w.id), w.id)}
                      className="p-1.5 hover:bg-zinc-800 rounded-lg text-zinc-500 hover:text-teal-400 transition-all"
                    >
                      {copiedId === w.id ? <Check className="w-3.5 h-3.5" /> : <Copy className="w-3.5 h-3.5" />}
                    </button>
                  </div>
                  <div className="flex items-center gap-2">
                    {w.targetAgentName && (
                      <div className="flex items-center gap-1 px-2 py-1 bg-zinc-800 rounded-lg border border-zinc-700 text-[10px] font-medium text-zinc-300">
                        <Bot className="w-3 h-3 text-teal-400" />
                        {w.targetAgentName}
                      </div>
                    )}
                    {w.targetWorkflowId && (
                      <div className="flex items-center gap-1 px-2 py-1 bg-zinc-800 rounded-lg border border-zinc-700 text-[10px] font-medium text-zinc-300">
                        <WorkflowIcon className="w-3 h-3 text-amber-400" />
                        {w.targetWorkflowId}
                      </div>
                    )}
                    <div className="flex-1" />
                    <button 
                      onClick={() => handleDelete(w.id)}
                      className="p-2 text-zinc-500 hover:text-red-400 hover:bg-red-500/10 rounded-xl transition-all"
                    >
                      <Trash2 className="w-4 h-4" />
                    </button>
                  </div>
                </div>
              </div>
            </div>
          ))}

          {webhooks.length === 0 && !loading && (
            <div className="text-center py-20 bg-zinc-900/20 border border-dashed border-zinc-800 rounded-3xl">
              <Cable className="w-12 h-12 text-zinc-700 mx-auto mb-4" />
              <h3 className="text-lg font-medium text-zinc-400">Nenhum webhook configurado</h3>
              <p className="text-sm text-zinc-500 mt-1 max-w-xs mx-auto">
                Crie um ponto de entrada para permitir que sistemas externos como GitHub ou Stripe falem com seus agentes.
              </p>
              <button
                onClick={() => setShowForm(true)}
                className="mt-6 inline-flex items-center gap-2 px-4 py-2 bg-zinc-800 hover:bg-zinc-700 text-zinc-200 rounded-xl text-sm transition-all"
              >
                <Plus className="w-4 h-4" />
                Configurar Primeiro Webhook
              </button>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
