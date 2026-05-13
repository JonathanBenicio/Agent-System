import { Bot } from 'lucide-react'

export function ProcessingIndicator() {
  return (
    <div className="flex gap-3 py-4">
      <div className="flex items-center justify-center w-7 h-7 rounded-lg bg-teal-600 shrink-0 mt-0.5">
        <Bot className="w-4 h-4 text-white animate-pulse" />
      </div>
      <div className="flex flex-col">
        <span className="text-xs font-medium text-zinc-400 mb-1">
          Processando...
        </span>
        <div className="bg-zinc-850 rounded-2xl rounded-bl-md px-4 py-3">
          <div className="flex items-center gap-1.5">
            <div className="w-2 h-2 rounded-full bg-zinc-500 animate-bounce [animation-delay:0ms]" />
            <div className="w-2 h-2 rounded-full bg-zinc-500 animate-bounce [animation-delay:150ms]" />
            <div className="w-2 h-2 rounded-full bg-zinc-500 animate-bounce [animation-delay:300ms]" />
          </div>
        </div>
      </div>
    </div>
  )
}
