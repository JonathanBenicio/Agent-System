import { X, Bot } from 'lucide-react'
import { Badge } from '@/components/shared/Badge'
import { TierLabels, TierColors } from '@/types/api'
import type { AgentInfo } from '@/types/api'
import { cn } from '@/lib/utils'

interface Props {
  agent: AgentInfo
  onClose: () => void
}

export function AgentDetailModal({ agent, onClose }: Props) {
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center">
      <div className="absolute inset-0 bg-black/60" onClick={onClose} />
      <div className="relative bg-zinc-900 border border-zinc-700 rounded-xl w-full max-w-lg max-h-[90vh] overflow-y-auto shadow-2xl">
        <div className="sticky top-0 bg-zinc-900 px-6 py-4 border-b border-zinc-800 flex items-center justify-between">
          <div className="flex items-center gap-3">
            <div className={cn('flex items-center justify-center w-10 h-10 rounded-xl', TierColors[agent.tier] ?? 'bg-zinc-600')}>
              <Bot className="w-5 h-5 text-white" />
            </div>
            <div>
              <h2 className="text-lg font-semibold text-zinc-100">{agent.name}</h2>
              <p className="text-xs text-zinc-500">{agent.domain}</p>
            </div>
          </div>
          <button onClick={onClose} className="text-zinc-500 hover:text-zinc-300">
            <X className="w-5 h-5" />
          </button>
        </div>
        <div className="p-6 space-y-5">
          <div className="flex gap-2">
            <Badge variant="teal">{TierLabels[agent.tier]}</Badge>
            <Badge variant={agent.isActive ? 'success' : 'default'}>{agent.isActive ? 'Ativo' : 'Inativo'}</Badge>
          </div>

          <Section title="Descrição">
            <p className="text-sm text-zinc-300">{agent.description}</p>
          </Section>

          {agent.capabilities.length > 0 && (
            <Section title="Capabilities">
              <div className="flex flex-wrap gap-1">
                {agent.capabilities.map(c => (
                  <Badge key={c}>{c}</Badge>
                ))}
              </div>
            </Section>
          )}

          {agent.systemPrompt && (
            <Section title="System Prompt">
              <pre className="text-xs text-zinc-400 bg-zinc-950 border border-zinc-800 rounded-lg p-3 overflow-x-auto whitespace-pre-wrap">
                {agent.systemPrompt}
              </pre>
            </Section>
          )}

          <Section title="Configuração">
            <div className="grid grid-cols-2 gap-3 text-sm">
              <ConfigItem label="Max Concurrency" value={agent.maxConcurrency} />
              <ConfigItem label="Timeout" value={`${agent.timeoutSeconds}s`} />
              <ConfigItem label="Tools" value={agent.toolNames?.length ?? 0} />
              <ConfigItem label="Skills" value={agent.skillNames?.length ?? 0} />
            </div>
          </Section>

          {agent.toolNames && agent.toolNames.length > 0 && (
            <Section title="Tools">
              <div className="flex flex-wrap gap-1">
                {agent.toolNames.map(t => <Badge key={t}>{t}</Badge>)}
              </div>
            </Section>
          )}

          {agent.skillNames && agent.skillNames.length > 0 && (
            <Section title="Skills">
              <div className="flex flex-wrap gap-1">
                {agent.skillNames.map(s => <Badge key={s}>{s}</Badge>)}
              </div>
            </Section>
          )}
        </div>
      </div>
    </div>
  )
}

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <div>
      <h3 className="text-xs font-semibold text-zinc-500 uppercase tracking-wider mb-2">{title}</h3>
      {children}
    </div>
  )
}

function ConfigItem({ label, value }: { label: string; value: string | number }) {
  return (
    <div className="bg-zinc-950 border border-zinc-800 rounded-lg px-3 py-2">
      <p className="text-xs text-zinc-500">{label}</p>
      <p className="text-zinc-200 font-medium">{value}</p>
    </div>
  )
}
