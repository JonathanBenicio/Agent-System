import { useState, useRef, useEffect, type KeyboardEvent } from 'react'
import { SendHorizontal } from 'lucide-react'
import { cn } from '@/lib/utils'

interface ChatInputProps {
  onSend: (message: string) => void
  disabled?: boolean
  isProcessing?: boolean
  placeholder?: string
}

const SEND_COOLDOWN_MS = 500

export function ChatInput({ onSend, disabled, isProcessing, placeholder }: ChatInputProps) {
  const [text, setText] = useState('')
  const textareaRef = useRef<HTMLTextAreaElement>(null)
  const lastSentRef = useRef<number>(0)

  useEffect(() => {
    if (!isProcessing) {
      textareaRef.current?.focus()
    }
  }, [isProcessing])

  const handleSubmit = () => {
    if (!text.trim() || disabled) return
    const now = Date.now()
    if (now - lastSentRef.current < SEND_COOLDOWN_MS) return
    lastSentRef.current = now
    onSend(text.trim())
    setText('')
    // Reset textarea height
    if (textareaRef.current) {
      textareaRef.current.style.height = 'auto'
    }
  }

  const handleKeyDown = (e: KeyboardEvent<HTMLTextAreaElement>) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault()
      handleSubmit()
    }
  }

  const handleInput = () => {
    const textarea = textareaRef.current
    if (textarea) {
      textarea.style.height = 'auto'
      textarea.style.height = Math.min(textarea.scrollHeight, 200) + 'px'
    }
  }

  return (
    <div className="border-t border-zinc-800 bg-zinc-950 p-4">
      <div className="max-w-3xl mx-auto">
        <div className="flex items-end gap-2 bg-zinc-900 border border-zinc-700 rounded-xl px-4 py-2 focus-within:border-teal-600 transition-colors">
          <textarea
            ref={textareaRef}
            value={text}
            onChange={(e) => setText(e.target.value)}
            onKeyDown={handleKeyDown}
            onInput={handleInput}
            placeholder={isProcessing ? 'Aguardando resposta...' : (placeholder ?? 'Envie uma mensagem...')}
            disabled={disabled}
            rows={1}
            className="flex-1 bg-transparent text-sm text-zinc-100 placeholder-zinc-500 resize-none outline-none py-1.5 max-h-[200px]"
          />
          <button
            onClick={handleSubmit}
            disabled={!text.trim() || disabled}
            className={cn(
              'flex items-center justify-center w-8 h-8 rounded-lg transition-colors shrink-0',
              text.trim() && !disabled
                ? 'bg-teal-600 text-white hover:bg-teal-500'
                : 'text-zinc-600'
            )}
          >
            <SendHorizontal className="w-4 h-4" />
          </button>
        </div>
        <p className="text-[10px] text-zinc-600 text-center mt-2">
          O AgenticSystem seleciona automaticamente o melhor agent para cada solicitação.
        </p>
      </div>
    </div>
  )
}
