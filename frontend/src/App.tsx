import { lazy, Suspense, type ReactNode } from 'react'
import { BrowserRouter, Navigate, Routes, Route } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { Layout } from '@/components/layout/Layout'
import { PageLoading } from '@/components/shared/Loading'
import { ChatProvider, useChat } from '@/hooks/useChat'
import { useSignalRAuth } from '@/hooks/useSignalRAuth'
import { ProtectedRoute } from '@/components/auth/ProtectedRoute'

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      retry: 1,
      refetchOnWindowFocus: false,
    },
  },
})

const ChatPage = lazy(() => import('@/components/chat/ChatPage').then(module => ({ default: module.ChatPage })))
const AgentChatPage = lazy(() => import('@/components/chat/AgentChatPage').then(module => ({ default: module.AgentChatPage })))
const DashboardPage = lazy(() => import('@/components/dashboard/DashboardPage').then(module => ({ default: module.DashboardPage })))
const AgentsPage = lazy(() => import('@/components/agents/AgentsPage').then(module => ({ default: module.AgentsPage })))
const ToolsPage = lazy(() => import('@/components/agents/ToolsPage').then(module => ({ default: module.ToolsPage })))
const SkillsPage = lazy(() => import('@/components/agents/SkillsPage').then(module => ({ default: module.SkillsPage })))
const ServicesPage = lazy(() => import('@/components/gateway/ServicesPage').then(module => ({ default: module.ServicesPage })))
const CostsPage = lazy(() => import('@/components/gateway/CostsPage').then(module => ({ default: module.CostsPage })))
const HealthPage = lazy(() => import('@/components/gateway/HealthPage').then(module => ({ default: module.HealthPage })))
const ProvidersPage = lazy(() => import('@/components/llm/ProvidersPage').then(module => ({ default: module.ProvidersPage })))
const PluginsPage = lazy(() => import('@/components/plugins/PluginsPage').then(module => ({ default: module.PluginsPage })))
const SettingsPage = lazy(() => import('@/components/settings/SettingsPage').then(module => ({ default: module.SettingsPage })))
const RAGPage = lazy(() => import('@/components/rag/RAGPage').then(module => ({ default: module.RAGPage })))
const ScheduledTasksPage = lazy(() => import('@/components/scheduled-tasks/ScheduledTasksPage').then(module => ({ default: module.ScheduledTasksPage })))
const ConfigAdvancedPage = lazy(() => import('@/components/config/ConfigAdvancedPage').then(module => ({ default: module.ConfigAdvancedPage })))
const EmbeddingMigrationWizard = lazy(() => import('@/components/embedding-migration/EmbeddingMigrationWizard').then(module => ({ default: module.EmbeddingMigrationWizard })))

function RouteBoundary({ children }: { children: ReactNode }) {
  return <Suspense fallback={<PageLoading />}>{children}</Suspense>
}

function AppRoutes() {
  useSignalRAuth()

  const {
    messages,
    isConnected,
    isProcessing,
    connectionState,
    providers,
    selectedProvider,
    selectedModel,
    setSelectedProvider,
    setSelectedModel,
    sendMessage,
    clearMessages,
  } = useChat()

  return (
    <Routes>
      <Route element={<ProtectedRoute />}>
        <Route
          element={
            <Layout
              isConnected={isConnected}
              connectionState={connectionState}
              onNewChat={clearMessages}
            />
          }
        >
          <Route
            index
            element={
              <RouteBoundary>
                <ChatPage
                  messages={messages}
                  isProcessing={isProcessing}
                  isConnected={isConnected}
                  providers={providers}
                  selectedProvider={selectedProvider}
                  selectedModel={selectedModel}
                  onProviderChange={setSelectedProvider}
                  onModelChange={setSelectedModel}
                  onSend={sendMessage}
                />
              </RouteBoundary>
            }
          />
          <Route path="/dashboard" element={<RouteBoundary><DashboardPage /></RouteBoundary>} />
          <Route path="/agents" element={<RouteBoundary><AgentsPage /></RouteBoundary>} />
          <Route path="/chat/:agentName" element={<RouteBoundary><AgentChatPage /></RouteBoundary>} />
          <Route path="/tools" element={<RouteBoundary><ToolsPage /></RouteBoundary>} />
          <Route path="/skills" element={<RouteBoundary><SkillsPage /></RouteBoundary>} />
          <Route path="/rag" element={<RouteBoundary><RAGPage /></RouteBoundary>} />
          <Route path="/gateway" element={<RouteBoundary><ServicesPage /></RouteBoundary>} />
          <Route path="/gateway/health" element={<RouteBoundary><HealthPage /></RouteBoundary>} />
          <Route path="/costs" element={<RouteBoundary><CostsPage /></RouteBoundary>} />
          <Route path="/ai" element={<RouteBoundary><ProvidersPage /></RouteBoundary>} />
          <Route path="/providers" element={<Navigate to="/ai" replace />} />
          <Route path="/plugins" element={<RouteBoundary><PluginsPage /></RouteBoundary>} />
          <Route path="/scheduled-tasks" element={<RouteBoundary><ScheduledTasksPage /></RouteBoundary>} />
          <Route path="/config" element={<RouteBoundary><SettingsPage /></RouteBoundary>} />
          <Route path="/config/advanced" element={<RouteBoundary><ConfigAdvancedPage /></RouteBoundary>} />
          <Route path="/embedding-migration" element={<RouteBoundary><EmbeddingMigrationWizard /></RouteBoundary>} />
        </Route>
      </Route>
    </Routes>
  )
}

export default function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <ChatProvider>
          <AppRoutes />
        </ChatProvider>
      </BrowserRouter>
    </QueryClientProvider>
  )
}
