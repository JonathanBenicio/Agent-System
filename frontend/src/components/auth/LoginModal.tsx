import { useState } from 'react'
import { Key, ShieldCheck, Bot, Loader2, AlertCircle } from 'lucide-react'
import { useAuthStore } from '../../store/authStore'

export function LoginModal() {
  const [activeTab, setActiveTab] = useState<'token' | 'apikey'>('token')
  const [inputValue, setInputValue] = useState('')
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const { loginWithToken, loginWithApiKey } = useAuthStore()

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setError(null)

    if (!inputValue.trim()) {
      setError('Por favor, preencha o campo antes de continuar.')
      return
    }

    setIsLoading(true)

    try {
      // Simula validação/tempo de resposta
      await new Promise((resolve) => setTimeout(resolve, 800))

      if (activeTab === 'token') {
        if (!inputValue.startsWith('eyJ') && inputValue.length < 20) {
          throw new Error('Token JWT em formato inválido.')
        }
        loginWithToken(inputValue)
      } else {
        if (inputValue.length < 10) {
          throw new Error('Chave de API inválida.')
        }
        const success = await loginWithApiKey(inputValue)
        if (!success) {
          throw new Error('Chave de API inválida ou recusada pelo servidor.')
        }
      }
    } catch (err: unknown) {
      if (err instanceof Error) {
        setError(err.message)
      } else {
        setError('Falha na autenticação. Verifique os dados inseridos.')
      }
    } finally {
      setIsLoading(false)
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-zinc-950/80 backdrop-blur-sm p-4">
      <div className="w-full max-w-md bg-zinc-900/90 backdrop-blur-md border border-zinc-800/80 rounded-2xl shadow-2xl overflow-hidden flex flex-col transition-all duration-300">
        {/* Header */}
        <div className="flex flex-col items-center pt-8 pb-6 px-6 border-b border-zinc-800/50">
          <div className="flex items-center justify-center w-14 h-14 rounded-2xl bg-teal-600/10 border border-teal-500/20 mb-4 shadow-inner">
            <Bot className="w-8 h-8 text-teal-500 animate-pulse" />
          </div>
          <h2 className="text-xl font-bold text-zinc-100 tracking-tight">
            Autenticação AgenticSystem
          </h2>
          <p className="text-xs text-zinc-400 mt-1 text-center">
            Acesse o ecossistema de IA e gateway avançado
          </p>
        </div>

        {/* Tabs */}
        <div className="flex border-b border-zinc-800/60 bg-zinc-925/50 px-4 pt-4 gap-2">
          <button
            type="button"
            onClick={() => {
              setActiveTab('token')
              setError(null)
              setInputValue('')
            }}
            className={`flex-1 flex items-center justify-center gap-2 pb-3 pt-2 text-xs font-medium border-b-2 transition-all ${
              activeTab === 'token'
                ? 'border-teal-500 text-teal-400 font-semibold'
                : 'border-transparent text-zinc-400 hover:text-zinc-200'
            }`}
          >
            <ShieldCheck className="w-4 h-4" />
            Token JWT
          </button>
          <button
            type="button"
            onClick={() => {
              setActiveTab('apikey')
              setError(null)
              setInputValue('')
            }}
            className={`flex-1 flex items-center justify-center gap-2 pb-3 pt-2 text-xs font-medium border-b-2 transition-all ${
              activeTab === 'apikey'
                ? 'border-cyan-500 text-cyan-400 font-semibold'
                : 'border-transparent text-zinc-400 hover:text-zinc-200'
            }`}
          >
            <Key className="w-4 h-4" />
            Chave de API
          </button>
        </div>

        {/* Form */}
        <form onSubmit={handleSubmit} className="p-6 flex flex-col gap-5 space-y-1">
          <div className="flex flex-col gap-2">
            <label className="text-xs font-medium text-zinc-300 flex items-center justify-between">
              <span>{activeTab === 'token' ? 'Seu Token JWT Bearer' : 'Sua Chave X-Api-Key'}</span>
              <span className="text-[10px] text-zinc-500">Obrigatório</span>
            </label>
            <div className="relative flex items-center">
              <div className="absolute left-3 text-zinc-500 shrink-0">
                {activeTab === 'token' ? (
                  <ShieldCheck className="w-4.5 h-4.5 text-teal-500/70" />
                ) : (
                  <Key className="w-4.5 h-4.5 text-cyan-500/70" />
                )}
              </div>
              <input
                type={activeTab === 'token' ? 'password' : 'text'}
                value={inputValue}
                onChange={(e) => setInputValue(e.target.value)}
                placeholder={
                  activeTab === 'token'
                    ? 'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...'
                    : 'agentic_live_key_xxx...'
                }
                className="w-full bg-zinc-950 border border-zinc-800 rounded-xl py-2.5 pl-10 pr-4 text-xs text-zinc-100 placeholder:text-zinc-600 focus:outline-none focus:border-teal-500 focus:ring-1 focus:ring-teal-500/50 transition-all shadow-inner"
              />
            </div>
            <p className="text-[11px] text-zinc-500 leading-normal mt-0.5">
              {activeTab === 'token'
                ? 'Utilizado para autenticação padrão com expiração gerida pelo servidor.'
                : 'Chave de longa duração para acesso de serviços e integrações de agentes.'}
            </p>
          </div>

          {/* Error display */}
          {error && (
            <div className="flex items-center gap-2 p-3 bg-red-950/40 border border-red-900/60 rounded-xl text-red-300 text-xs">
              <AlertCircle className="w-4 h-4 shrink-0 text-red-400" />
              <span className="flex-1 leading-snug">{error}</span>
            </div>
          )}

          {/* Action button */}
          <button
            type="submit"
            disabled={isLoading}
            className="w-full relative group overflow-hidden bg-gradient-to-r from-teal-600 to-cyan-600 hover:from-teal-500 hover:to-cyan-500 active:scale-[0.99] disabled:opacity-50 disabled:cursor-not-allowed text-zinc-950 font-semibold text-xs py-3 rounded-xl transition-all duration-200 shadow-lg shadow-teal-500/10 flex items-center justify-center gap-2 mt-2"
          >
            {isLoading ? (
              <>
                <Loader2 className="w-4 h-4 animate-spin text-zinc-950" />
                <span>Autenticando...</span>
              </>
            ) : (
              <span>Acessar Sistema</span>
            )}
          </button>
        </form>

        {/* Footer info */}
        <div className="py-3 px-6 bg-zinc-950/60 border-t border-zinc-800/40 text-center">
          <span className="text-[10px] text-zinc-500">
            Ambiente seguro • Criptografia ativa
          </span>
        </div>
      </div>
    </div>
  )
}
