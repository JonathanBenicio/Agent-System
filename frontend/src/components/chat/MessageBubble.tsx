import Markdown from 'react-markdown'
import { Bot, User, AlertTriangle } from 'lucide-react'
import { cn } from '@/lib/utils'
import type { ChatMessage } from '@/types/chat'

const tierColors: Record<number, string> = {
  0: 'bg-teal-600',
  1: 'bg-blue-600',
  2: 'bg-emerald-600',
  3: 'bg-amber-600',
}

const tierLabels: Record<number, string> = {
  0: 'Chief',
  1: 'Specialist',
  2: 'Worker',
  3: 'Helper',
}

interface MessageBubbleProps {
  message: ChatMessage
}

export function MessageBubble({ message }: MessageBubbleProps) {
  const isUser = message.role === 'user'
  const isSystem = message.role === 'system'

  if (isSystem) {
    return (
      <div className="flex items-start gap-3 py-3 px-4 rounded-lg bg-red-950/30 border border-red-900/50 my-2">
        <AlertTriangle className="w-4 h-4 text-red-400 mt-0.5 shrink-0" />
        <span className="text-sm text-red-300">{message.content}</span>
      </div>
    )
  }

  return (
    <div
      className={cn(
        'group flex gap-3 py-4',
        isUser ? 'justify-end' : 'justify-start'
      )}
    >
      {/* Avatar */}
      {!isUser && (
        <div
          className={cn(
            'flex items-center justify-center w-7 h-7 rounded-lg shrink-0 mt-0.5',
            message.agentTier !== undefined
              ? tierColors[message.agentTier] ?? 'bg-zinc-600'
              : 'bg-teal-600'
          )}
        >
          <Bot className="w-4 h-4 text-white" />
        </div>
      )}

      {/* Content */}
      <div
        className={cn(
          'flex flex-col min-w-0',
          isUser ? 'items-end max-w-[80%]' : 'max-w-[85%]'
        )}
      >
        {/* Agent badge */}
        {!isUser && message.agentName && (
          <div className="flex items-center gap-2 mb-1">
            <span className="text-xs font-medium text-zinc-300">
              {message.agentName}
            </span>
            {message.agentTier !== undefined && (
              <span
                className={cn(
                  'text-[10px] px-1.5 py-0.5 rounded font-medium text-white',
                  tierColors[message.agentTier] ?? 'bg-zinc-600'
                )}
              >
                {tierLabels[message.agentTier] ?? `T${message.agentTier}`}
              </span>
            )}
          </div>
        )}

        {/* Message body */}
        <div
          className={cn(
            'rounded-2xl px-4 py-2.5 text-sm leading-relaxed',
            isUser
              ? 'bg-teal-600 text-white rounded-br-md'
              : 'bg-zinc-850 text-zinc-200 rounded-bl-md'
          )}
        >
          {isUser ? (
            <p className="whitespace-pre-wrap">{message.content}</p>
          ) : (
            <div className="chat-markdown">
              <Markdown
                disallowedElements={['script', 'iframe', 'object', 'embed', 'form']}
                unwrapDisallowed
              >
                {message.content}
              </Markdown>
            </div>
          )}
        </div>

        {/* Actions/tools badges */}
        {!isUser && (message.actions?.length || message.tools?.length) ? (
          <div className="flex flex-wrap gap-1 mt-1.5">
            {message.actions?.map((action, i) => (
              <span
                key={`a-${i}`}
                className="text-[10px] px-1.5 py-0.5 rounded bg-zinc-800 text-zinc-400 border border-zinc-700"
              >
                ⚡ {action}
              </span>
            ))}
            {message.tools?.map((tool, i) => (
              <span
                key={`t-${i}`}
                className="text-[10px] px-1.5 py-0.5 rounded bg-zinc-800 text-zinc-400 border border-zinc-700"
              >
                🔧 {tool}
              </span>
            ))}
          </div>
        ) : null}

        {/* Timestamp */}
        <span className="text-[10px] text-zinc-600 mt-1 opacity-0 group-hover:opacity-100 transition-opacity">
          {new Date(message.timestamp).toLocaleTimeString('pt-BR')}
        </span>
      </div>

      {/* User avatar */}
      {isUser && (
        <div className="flex items-center justify-center w-7 h-7 rounded-lg bg-zinc-700 shrink-0 mt-0.5">
          <User className="w-4 h-4 text-zinc-300" />
        </div>
      )}
    </div>
  )
}
