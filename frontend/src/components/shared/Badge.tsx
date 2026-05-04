import { cn } from '@/lib/utils'

interface BadgeProps {
  children: React.ReactNode
  variant?: 'default' | 'success' | 'warning' | 'danger' | 'violet'
  className?: string
}

const variants = {
  default: 'bg-zinc-800 text-zinc-300 border-zinc-700',
  success: 'bg-emerald-900/50 text-emerald-300 border-emerald-800',
  warning: 'bg-amber-900/50 text-amber-300 border-amber-800',
  danger: 'bg-red-900/50 text-red-300 border-red-800',
  violet: 'bg-violet-900/50 text-violet-300 border-violet-800',
}

export function Badge({ children, variant = 'default', className }: BadgeProps) {
  return (
    <span
      className={cn(
        'inline-flex items-center px-2 py-0.5 rounded text-xs font-medium border',
        variants[variant],
        className
      )}
    >
      {children}
    </span>
  )
}
