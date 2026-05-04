import { cn } from '@/lib/utils'

interface StatusBarProps {
  isConnected: boolean
  connectionState: string
}

export function StatusBar({ isConnected, connectionState }: StatusBarProps) {
  return (
    <footer className="flex items-center justify-between h-8 px-4 border-t border-zinc-800 bg-zinc-925 text-xs text-zinc-500">
      <div className="flex items-center gap-4">
        <span className="flex items-center gap-1.5">
          <span
            className={cn(
              'w-2 h-2 rounded-full',
              isConnected ? 'bg-emerald-500' : 'bg-red-500'
            )}
          />
          Status: {isConnected ? 'Online' : 'Offline'}
        </span>
        <span className="flex items-center gap-1.5">
          <span
            className={cn(
              'w-2 h-2 rounded-full',
              connectionState === 'Connected'
                ? 'bg-emerald-500'
                : connectionState === 'Reconnecting'
                ? 'bg-yellow-500 animate-pulse'
                : 'bg-red-500'
            )}
          />
          SignalR: {connectionState}
        </span>
      </div>
      <span>v1.0.0</span>
    </footer>
  )
}
