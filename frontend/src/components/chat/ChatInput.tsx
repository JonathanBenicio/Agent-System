import { useState, useRef, useEffect, type KeyboardEvent } from 'react'
import { SendHorizontal, ChevronDown } from 'lucide-react'
import { cn } from '@/lib/utils'
import { useChat } from '@/hooks/useChat'

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
  const dropdownRef = useRef<HTMLDivElement>(null)

  // Estados locais para controle do dropdown estilo Gemini
  const [showDropdown, setShowDropdown] = useState(false)
  const [showProviderMenu, setShowProviderMenu] = useState(false)

  const {
    providers,
    selectedProvider,
    selectedModel,
    setSelectedProvider,
    setSelectedModel
  } = useChat()

  useEffect(() => {
    if (!isProcessing) {
      textareaRef.current?.focus()
    }
  }, [isProcessing])

  // Fecha o dropdown ao clicar fora dele
  useEffect(() => {
    function handleClickOutside(event: MouseEvent) {
      if (dropdownRef.current && !dropdownRef.current.contains(event.target as Node)) {
        setShowDropdown(false)
        setShowProviderMenu(false)
      }
    }
    document.addEventListener('mousedown', handleClickOutside)
    return () => document.removeEventListener('mousedown', handleClickOutside)
  }, [])

  const handleSubmit = () => {
    if (!text.trim() || disabled) return
    const now = Date.now()
    if (now - lastSentRef.current < SEND_COOLDOWN_MS) return
    lastSentRef.current = now
    onSend(text.trim())
    setText('')
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

  // Formata o nome do modelo para caber de forma compacta na pílula
  const formatModelName = (modelName: string) => {
    if (!modelName) return 'Flash'
    const base = modelName.split('/').pop() || modelName
    
    // Tratamentos amigáveis para famílias conhecidas
    if (base.toLowerCase().includes('flash-lite')) return 'Flash-Lite'
    if (base.toLowerCase().includes('flash')) return 'Flash'
    if (base.toLowerCase().includes('pro')) return 'Pro'
    if (base.toLowerCase().includes('deepseek') && base.toLowerCase().includes('r1')) return 'DeepSeek R1'
    if (base.toLowerCase().includes('gpt-4o-mini')) return '4o Mini'
    if (base.toLowerCase().includes('gpt-4o')) return 'GPT-4o'
    return base.substring(0, 12)
  }

  // Descrições ricas estilo Gemini para o dropdown de alta fidelidade
  const getModelDescription = (model: string) => {
    const lower = model.toLowerCase()
    if (lower.includes('flash-lite') || lower.includes('lite')) {
      return 'Respostas ultrarrápidas para tarefas simples'
    }
    if (lower.includes('flash')) {
      return 'Ajuda para tarefas gerais e produtividade'
    }
    if (lower.includes('pro')) {
      return 'Raciocínio complexo, matemática e codificação'
    }
    if (lower.includes('deepseek') && lower.includes('r1')) {
      return 'Raciocínio lógico estruturado e profundo'
    }
    if (lower.includes('gpt-4o-mini')) {
      return 'Velocidade e inteligência equilibradas'
    }
    if (lower.includes('gpt-4o')) {
      return 'Inteligência premium e multimodal avançada'
    }
    return 'Modelo operacional padrão do ecossistema'
  }

  const getModelBadge = (model: string) => {
    const lower = model.toLowerCase()
    if (lower.includes('lite')) return 'Rápido'
    if (lower.includes('pro') || lower.includes('r1')) return 'Pro'
    return null
  }

  const activeProvider = providers.find(p => p.name === selectedProvider) ?? providers[0]
  const models = activeProvider?.models ?? []

  return (
    <div className="border-t border-zinc-800 bg-zinc-950 p-4">
      <div className="max-w-3xl mx-auto">
        <div className="flex items-end gap-2 bg-zinc-900 border border-zinc-700 rounded-xl px-4 py-2 focus-within:border-teal-600 transition-colors relative">
          <textarea
            ref={textareaRef}
            value={text}
            onChange={(e) => setText(e.target.value)}
            onKeyDown={handleKeyDown}
            onInput={handleInput}
            placeholder={isProcessing ? 'Aguardando resposta...' : (placeholder ?? 'Envie uma mensagem...')}
            disabled={disabled || isProcessing}
            rows={1}
            className="flex-1 bg-transparent text-sm text-zinc-100 placeholder-zinc-500 resize-none outline-none py-1.5 max-h-[200px]"
          />
          
          {/* Seletor Estilo Gemini em Pílula */}
          <div className="relative shrink-0 flex items-center mb-1" ref={dropdownRef}>
            <button
              onClick={() => {
                setShowDropdown(!showDropdown)
                setShowProviderMenu(false)
              }}
              disabled={disabled || isProcessing || providers.length === 0}
              type="button"
              className={cn(
                "flex items-center gap-1.5 px-3.5 py-1.5 rounded-full text-xs font-mono font-bold bg-zinc-800/80 hover:bg-zinc-800 border border-zinc-700/80 hover:border-zinc-600 hover:text-zinc-100 active:scale-[0.97] transition-all cursor-pointer h-[32px] select-none uppercase tracking-wider",
                (disabled || isProcessing || providers.length === 0) && "opacity-50 pointer-events-none"
              )}
            >
              <span>{formatModelName(selectedModel)}</span>
              <ChevronDown className={cn("w-3 h-3 text-zinc-500 transition-transform duration-200", showDropdown && "rotate-180")} />
            </button>

            {/* Dropdown Principal (Abraçado para Cima) */}
            {showDropdown && (
              <div className="absolute bottom-full right-0 mb-2.5 bg-zinc-950/95 border border-zinc-800 backdrop-blur-xl shadow-2xl rounded-2xl w-80 p-2 z-50 flex flex-col gap-1 select-none animate-in fade-in slide-in-from-bottom-2 duration-150">
                <div className="px-3 py-2 text-[9px] font-bold tracking-widest text-zinc-500 uppercase font-mono border-b border-zinc-900/60 mb-1 flex items-center justify-between">
                  <span>Modelos Disponíveis</span>
                  <span className="text-teal-500 font-semibold">{selectedProvider}</span>
                </div>

                <div className="max-h-60 overflow-y-auto custom-scrollbar flex flex-col gap-1">
                  {models.map(model => {
                    const isSelected = model === selectedModel
                    const description = getModelDescription(model)
                    const badge = getModelBadge(model)

                    return (
                      <button
                        key={model}
                        onClick={() => {
                          setSelectedModel(model)
                          setShowDropdown(false)
                        }}
                        type="button"
                        className={cn(
                          "w-full flex items-center justify-between p-2.5 rounded-xl transition-all text-left group border border-transparent",
                          isSelected
                            ? "bg-teal-950/40 border-teal-800/50 shadow-sm"
                            : "hover:bg-zinc-900"
                        )}
                      >
                        <div className="flex items-center gap-3">
                          <span className={cn(
                            "w-4 h-4 flex items-center justify-center shrink-0 text-teal-400 font-bold text-sm transition-opacity",
                            isSelected ? "opacity-100" : "opacity-0 group-hover:opacity-30"
                          )}>
                            ✓
                          </span>
                          <div>
                            <div className="text-xs font-bold text-zinc-200 group-hover:text-white transition-colors">
                              {model.split('/').pop() || model}
                            </div>
                            <div className="text-[10px] text-zinc-500 mt-0.5 leading-normal">
                              {description}
                            </div>
                          </div>
                        </div>
                        {badge && (
                          <span className={cn(
                            "text-[9px] px-1.5 py-0.5 font-bold uppercase tracking-wider rounded-md shrink-0 ml-2 border",
                            badge === 'Pro' 
                              ? "bg-emerald-950/50 text-emerald-400 border-emerald-800/40" 
                              : "bg-teal-950/50 text-teal-400 border-teal-800/40"
                          )}>
                            {badge}
                          </span>
                        )}
                      </button>
                    )
                  })}
                </div>

                {/* Sub-menu Lateral de Provedor */}
                <div className="border-t border-zinc-900/80 my-1 pt-1 relative">
                  <button
                    onClick={() => setShowProviderMenu(!showProviderMenu)}
                    type="button"
                    className={cn(
                      "w-full flex items-center justify-between p-2.5 rounded-xl hover:bg-zinc-900 transition-colors text-left",
                      showProviderMenu && "bg-zinc-900/60"
                    )}
                  >
                    <div className="flex items-center gap-3 pl-7">
                      <div>
                        <div className="text-xs font-bold text-zinc-400">Alterar Provedor</div>
                        <div className="text-[9px] text-zinc-500 mt-0.5 font-mono uppercase tracking-wider">
                          Ativo: {selectedProvider}
                        </div>
                      </div>
                    </div>
                    <span className={cn("text-xs text-zinc-500 pr-1 transition-transform", showProviderMenu && "-translate-x-1")}>◀</span>
                  </button>

                  {/* Dropdown de Provedores */}
                  {showProviderMenu && (
                    <div className="absolute bottom-0 right-full mr-2 bg-zinc-950 border border-zinc-800 backdrop-blur-xl shadow-2xl rounded-2xl w-60 p-2 z-50 flex flex-col gap-1 animate-in fade-in slide-in-from-right-2 duration-150">
                      <div className="px-2.5 py-1.5 text-[9px] font-bold tracking-widest text-zinc-500 uppercase font-mono border-b border-zinc-900/60 mb-1">
                        Provedores Disponíveis
                      </div>
                      {providers.map(provider => {
                        const isSelected = provider.name === selectedProvider
                        return (
                          <button
                            key={provider.name}
                            onClick={() => {
                              setSelectedProvider(provider.name)
                              setShowProviderMenu(false)
                            }}
                            type="button"
                            className={cn(
                              "w-full flex items-center gap-3 p-2.5 rounded-xl text-left transition-colors border border-transparent",
                              isSelected
                                ? "bg-teal-950/40 border-teal-800/40 text-teal-400 font-semibold"
                                : "hover:bg-zinc-900 text-zinc-400 hover:text-zinc-200"
                            )}
                          >
                            <span className={cn(
                              "text-xs font-bold w-4 text-center shrink-0",
                              isSelected ? "text-teal-400" : "text-zinc-500"
                            )}>
                              {isSelected ? '✓' : '•'}
                            </span>
                            <div>
                              <div className="text-xs font-bold font-mono">{provider.name}</div>
                              <div className="text-[9px] text-zinc-500">{provider.models.length} modelos habilitados</div>
                            </div>
                          </button>
                        )
                      })}
                    </div>
                  )}
                </div>
              </div>
            )}
          </div>

          {/* Botão de Envio de Mensagem */}
          <button
            onClick={handleSubmit}
            disabled={!text.trim() || disabled || isProcessing}
            className={cn(
              'flex items-center justify-center w-8 h-8 rounded-lg transition-colors shrink-0 mb-1',
              text.trim() && !disabled && !isProcessing
                ? 'bg-teal-600 text-white hover:bg-teal-500'
                : 'text-zinc-600'
            )}
          >
            <SendHorizontal className="w-4 h-4" />
          </button>
        </div>
        <p className="text-[10px] text-zinc-600 text-center mt-2 font-mono">
          O AgenticSystem seleciona automaticamente o melhor agente para cada solicitação.
        </p>
      </div>
    </div>
  )
}
