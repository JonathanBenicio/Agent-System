import { Outlet } from 'react-router-dom'
import { Sidebar } from './Sidebar'
import { StatusBar } from './StatusBar'
import { QuotaAlertListener } from '@/components/gateway/QuotaAlertListener'

interface LayoutProps {
  isConnected: boolean
  connectionState: string
  onNewChat: () => void
}

export function Layout({ isConnected, connectionState, onNewChat }: LayoutProps) {
  return (
    <div className="flex h-screen bg-zinc-950 text-zinc-100">
      <QuotaAlertListener />
      <Sidebar onNewChat={onNewChat} />
      <div className="flex flex-col flex-1 min-w-0">
        <main className="flex-1 overflow-hidden">
          <Outlet />
        </main>
        <StatusBar isConnected={isConnected} connectionState={connectionState} />
      </div>
    </div>
  )
}
