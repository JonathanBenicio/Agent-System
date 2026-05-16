import { useState } from 'react'
import { Link, useLocation } from 'react-router-dom'
import {
  MessageSquare,
  Bell,
  LayoutDashboard,
  Bot,
  Wrench,
  Sparkles,
  FileText,
  Workflow,
  Cable,
  DollarSign,
  Settings,
  ChevronLeft,
  ChevronRight,
  Plus,
  Cpu,
  Plug,
  HeartPulse,
  Clock,
  Shield,
  ArrowLeftRight,
  LogOut,
  User,
} from 'lucide-react'
import { cn } from '@/lib/utils'
import { useAuthStore } from '@/store/authStore'

interface NavItem {
  icon: React.ElementType
  label: string
  path: string
}

const navItems: NavItem[] = [
  { icon: MessageSquare, label: 'Chat', path: '/' },
  { icon: LayoutDashboard, label: 'Dashboard', path: '/dashboard' },
  { icon: Bot, label: 'Agents', path: '/agents' },
  { icon: Wrench, label: 'Tools', path: '/tools' },
  { icon: Sparkles, label: 'Skills', path: '/skills' },
  { icon: FileText, label: 'RAG & Docs', path: '/rag' },
  { icon: Workflow, label: 'Workflows', path: '/workflows' },
  { icon: Cable, label: 'Webhooks', path: '/webhooks' },
  { icon: Cable, label: 'Gateway', path: '/gateway' },
  { icon: HeartPulse, label: 'Saúde', path: '/gateway/health' },
  { icon: DollarSign, label: 'Custos', path: '/costs' },
  { icon: Bell, label: 'Alertas', path: '/alerts' },
  { icon: Cpu, label: 'IAs', path: '/ai' },
  { icon: Plug, label: 'Plugins', path: '/plugins' },
  { icon: Clock, label: 'Scheduled Tasks', path: '/scheduled-tasks' },
  { icon: Settings, label: 'Config', path: '/config' },
  { icon: Shield, label: 'Config Avançada', path: '/config/advanced' },
  { icon: ArrowLeftRight, label: 'Embeddings', path: '/embedding-migration' },
]

interface SidebarProps {
  onNewChat?: () => void
}

export function Sidebar({ onNewChat }: SidebarProps) {
  const [collapsed, setCollapsed] = useState(false)
  const location = useLocation()
  const { token, apiKey, logout } = useAuthStore()

  return (
    <aside
      className={cn(
        'flex flex-col bg-zinc-925 border-r border-zinc-800 transition-all duration-200',
        collapsed ? 'w-16' : 'w-60'
      )}
    >
      {/* Logo area */}
      <div className="flex items-center h-14 px-3 border-b border-zinc-800">
        <div className="flex items-center gap-2 min-w-0">
          <div className="flex items-center justify-center w-8 h-8 rounded-lg bg-teal-600 shrink-0">
            <Bot className="w-5 h-5 text-white" />
          </div>
          {!collapsed && (
            <span className="text-sm font-semibold text-zinc-100 truncate">
              AgenticSystem
            </span>
          )}
        </div>
      </div>

      {/* New Chat button */}
      <div className="px-3 py-3">
        <button
          onClick={onNewChat}
          className={cn(
            'flex items-center gap-2 w-full rounded-lg border border-zinc-700 px-3 py-2 text-sm text-zinc-300 hover:bg-zinc-800 hover:text-zinc-100 transition-colors',
            collapsed && 'justify-center px-0'
          )}
        >
          <Plus className="w-4 h-4 shrink-0" />
          {!collapsed && <span>Novo Chat</span>}
        </button>
      </div>

      {/* Navigation */}
      <nav className="flex-1 px-2 py-1 space-y-0.5 overflow-y-auto">
        {navItems.map((item) => {
          const isActive = location.pathname === item.path
          return (
            <Link
              key={item.path}
              to={item.path}
              className={cn(
                'flex items-center gap-3 px-3 py-2 rounded-lg text-sm transition-colors',
                isActive
                  ? 'bg-zinc-800 text-zinc-100'
                  : 'text-zinc-400 hover:bg-zinc-850 hover:text-zinc-200',
                collapsed && 'justify-center px-0'
              )}
              title={collapsed ? item.label : undefined}
            >
              <item.icon className="w-4.5 h-4.5 shrink-0" />
              {!collapsed && <span>{item.label}</span>}
            </Link>
          )
        })}
      </nav>

      {/* User Status / Logout */}
      <div className="p-3 border-t border-zinc-800 bg-zinc-925/50 flex flex-col gap-2">
        {!collapsed ? (
          <div className="flex items-center justify-between min-w-0">
            <div className="flex items-center gap-2 min-w-0">
              <div className="w-7 h-7 rounded-lg bg-teal-600/20 border border-teal-500/30 flex items-center justify-center shrink-0">
                <User className="w-3.5 h-3.5 text-teal-400" />
              </div>
              <div className="flex flex-col min-w-0">
                <span className="text-xs font-semibold text-zinc-200 truncate">
                  Administrador
                </span>
                <span className="text-[10px] text-teal-500 truncate font-mono">
                  {token ? 'JWT Bearer' : apiKey ? 'API Key Ativa' : 'Desconectado'}
                </span>
              </div>
            </div>
            <button
              onClick={logout}
              className="text-zinc-500 hover:text-red-400 p-1.5 rounded-lg hover:bg-zinc-850 transition-colors"
              title="Sair do sistema"
            >
              <LogOut className="w-4 h-4" />
            </button>
          </div>
        ) : (
          <button
            onClick={logout}
            className="flex items-center justify-center w-full py-1.5 rounded-lg text-zinc-500 hover:text-red-400 hover:bg-zinc-850 transition-colors"
            title="Sair do sistema"
          >
            <LogOut className="w-4 h-4" />
          </button>
        )}
      </div>

      {/* Collapse toggle */}
      <div className="p-2 border-t border-zinc-800">
        <button
          onClick={() => setCollapsed(!collapsed)}
          className="flex items-center justify-center w-full py-1.5 rounded-lg text-zinc-500 hover:bg-zinc-850 hover:text-zinc-300 transition-colors"
        >
          {collapsed ? (
            <ChevronRight className="w-4 h-4" />
          ) : (
            <ChevronLeft className="w-4 h-4" />
          )}
        </button>
      </div>
    </aside>
  )
}
