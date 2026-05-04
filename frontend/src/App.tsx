import { BrowserRouter, Routes, Route } from 'react-router-dom'
import { Layout } from '@/components/layout/Layout'
import { ChatPage } from '@/components/chat/ChatPage'
import { AgentChatPage } from '@/components/chat/AgentChatPage'
import { DashboardPage } from '@/components/dashboard/DashboardPage'
import { AgentsPage } from '@/components/agents/AgentsPage'
import { ToolsPage } from '@/components/agents/ToolsPage'
import { SkillsPage } from '@/components/agents/SkillsPage'
import { ServicesPage } from '@/components/gateway/ServicesPage'
import { CostsPage } from '@/components/gateway/CostsPage'
import { HealthPage } from '@/components/gateway/HealthPage'
import { ProvidersPage } from '@/components/llm/ProvidersPage'
import { PluginsPage } from '@/components/plugins/PluginsPage'
import { SettingsPage } from '@/components/settings/SettingsPage'
import { RAGPage } from '@/components/rag/RAGPage'
import { ScheduledTasksPage } from '@/components/scheduled-tasks/ScheduledTasksPage'
import { ConfigAdvancedPage } from '@/components/config/ConfigAdvancedPage'
import { EmbeddingMigrationWizard } from '@/components/embedding-migration/EmbeddingMigrationWizard'
import { useChat } from '@/hooks/useChat'

export default function App() {
  const { messages, isConnected, isProcessing, connectionState, sendMessage, clearMessages } = useChat()

  return (
    <BrowserRouter>
      <Routes>
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
              <ChatPage
                messages={messages}
                isProcessing={isProcessing}
                isConnected={isConnected}
                onSend={sendMessage}
              />
            }
          />
          <Route path="/dashboard" element={<DashboardPage />} />
          <Route path="/agents" element={<AgentsPage />} />
          <Route path="/chat/:agentName" element={<AgentChatPage />} />
          <Route path="/tools" element={<ToolsPage />} />
          <Route path="/skills" element={<SkillsPage />} />
          <Route path="/rag" element={<RAGPage />} />
          <Route path="/gateway" element={<ServicesPage />} />
          <Route path="/gateway/health" element={<HealthPage />} />
          <Route path="/costs" element={<CostsPage />} />
          <Route path="/providers" element={<ProvidersPage />} />
          <Route path="/plugins" element={<PluginsPage />} />
          <Route path="/scheduled-tasks" element={<ScheduledTasksPage />} />
          <Route path="/config" element={<SettingsPage />} />
          <Route path="/config/advanced" element={<ConfigAdvancedPage />} />
          <Route path="/embedding-migration" element={<EmbeddingMigrationWizard />} />
        </Route>
      </Routes>
    </BrowserRouter>
  )
}
