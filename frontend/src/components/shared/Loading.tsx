import { Loader2 } from 'lucide-react'

export function Spinner({ className = 'w-5 h-5' }: { className?: string }) {
  return <Loader2 className={`animate-spin text-zinc-400 ${className}`} />
}

export function PageLoading() {
  return (
    <div className="flex items-center justify-center h-full">
      <Spinner className="w-8 h-8" />
    </div>
  )
}

export function PageError({ message, onRetry }: { message: string; onRetry?: () => void }) {
  return (
    <div className="flex flex-col items-center justify-center h-full gap-4">
      <p className="text-sm text-red-400">{message}</p>
      {onRetry && (
        <button
          onClick={onRetry}
          className="px-4 py-2 text-sm rounded-lg bg-zinc-800 border border-zinc-700 text-zinc-300 hover:bg-zinc-700"
        >
          Tentar novamente
        </button>
      )}
    </div>
  )
}
