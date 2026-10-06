import { test as base } from '@playwright/test';
import { ChatPage } from '../pages/ChatPage';
import { AgentsPage } from '../pages/AgentsPage';

export const test = base.extend<{ chatPage: ChatPage; agentsPage: AgentsPage }>({
  page: async ({ page, context, baseURL }, use) => {
    if (process.env.REAL_E2E === 'true') {
      const apiKey = process.env.E2E_API_KEY;
      if (!apiKey) throw new Error('REAL_E2E requires E2E_API_KEY and a configured API/provider/database.');
      const login = await page.request.post('/api/auth/login', { data: { apiKey } });
      if (!login.ok()) throw new Error(`Real cookie login failed: ${login.status()}`);
    } else {
      await context.addCookies([{ name: 'agentic_api_key', value: 'e2e-cookie', url: baseURL!, httpOnly: true, sameSite: 'Strict' }]);
      await page.route('**/hubs/**', route => route.abort());
      await page.route('**/api/**', async route => {
        const path = new URL(route.request().url()).pathname;
        if (!path.startsWith('/api/')) { await route.continue(); return; }
        let body: unknown = [];
        if (path === '/api/auth/session') body = { userId: 'e2e-user', tenantId: 'e2e-tenant', roles: ['Owner'] };
        if (path === '/api/chat/configuration') body = {
          providers: [{ name: 'Ollama', isEnabled: true, models: ['llama3', 'mistral'], defaultModel: 'llama3' }],
          defaultProvider: 'Ollama', preferredProvider: 'Ollama', preferredModel: 'llama3', canManageTenant: true,
        };
        if (path === '/api/agent/agents') body = [{ name: 'SpecialistAgent', description: 'Database specialist', tier: 2, domain: 'Database', isActive: true }];
        if (path === '/api/chat') body = { success: true, content: 'Resposta mockada do assistente.', agentName: 'ChiefAgent', agentTier: 0, sessionId: 'e2e-session' };
        await route.fulfill({ json: body });
      });
    }
    await use(page);
  },
  chatPage: async ({ page }, use) => { await use(new ChatPage(page)); },
  agentsPage: async ({ page }, use) => { await use(new AgentsPage(page)); },
});
export { expect } from '@playwright/test';
