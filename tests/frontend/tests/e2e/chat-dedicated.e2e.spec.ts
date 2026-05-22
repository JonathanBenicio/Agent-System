import { test, expect } from '../../fixtures';

// Payload para criação do SpecialistAgent no back-end real
const SPECIALIST_AGENT_PAYLOAD = {
  name: 'SpecialistAgent',
  description: 'Agente de banco de dados especialista',
  tier: 2,
  domain: 'Database',
  allowedTools: [],
  capabilities: [],
  autonomyLevel: 2, // Supervised
  policyIds: [],
  instructions: 'Você é SpecialistAgent, um especialista em banco de dados. Responda de forma focada e técnica.',
  configuration: {}
};

// Set global para rastreamento de IDs de sessões de chat criadas durante a execução da spec
const createdSessionIds = new Set<string>();

test.describe('Chat Dedicado - Isolamento de Canais (Gap 3 / US-33)', () => {
  
  // Executa uma única vez antes de rodar os testes da spec para registrar o agente se REAL_E2E for ativo
  test.beforeAll(async ({ playwright }) => {
    if (process.env.REAL_E2E === 'true') {
      const baseURL = process.env.BASE_URL || 'http://localhost/';
      console.log(`REAL_E2E: Inicializando request context para baseURL: ${baseURL}`);
      const apiContext = await playwright.request.newContext({
        baseURL,
        extraHTTPHeaders: {
          'X-Api-Key': 'minha-chave-secreta-admin-123',
          'Content-Type': 'application/json',
        },
      });

      console.log('REAL_E2E: Registrando SpecialistAgent no back-end real do Docker...');
      const response = await apiContext.post('/api/agent/agents', {
        data: SPECIALIST_AGENT_PAYLOAD,
      });

      if (response.ok()) {
        console.log('REAL_E2E: SpecialistAgent registrado com sucesso!');
      } else {
        const text = await response.text();
        console.warn(`REAL_E2E: Alerta ao registrar SpecialistAgent (pode já existir): ${text}`);
      }

      await apiContext.dispose();
    }
  });

  // Executa uma única vez após a spec finalizar para limpar o agente e as sessões de chat criadas no banco real
  test.afterAll(async ({ playwright }) => {
    if (process.env.REAL_E2E === 'true') {
      const baseURL = process.env.BASE_URL || 'http://localhost/';
      const apiContext = await playwright.request.newContext({
        baseURL,
        extraHTTPHeaders: {
          'X-Api-Key': 'minha-chave-secreta-admin-123',
        },
      });

      console.log('REAL_E2E: Removendo SpecialistAgent do banco de dados real do Docker...');
      const deleteAgentResponse = await apiContext.delete('/api/agent/agents/SpecialistAgent');
      if (deleteAgentResponse.ok()) {
        console.log('REAL_E2E: SpecialistAgent removido com sucesso!');
      } else {
        console.warn('REAL_E2E: Falha ao remover SpecialistAgent ou ele já foi removido.');
      }

      console.log(`REAL_E2E (Chat Dedicated): Encontradas ${createdSessionIds.size} sessões criadas nesta spec. Iniciando purga isolada...`);
      for (const sessionId of createdSessionIds) {
        try {
          const deleteSessionRes = await apiContext.delete(`/api/session/${sessionId}`);
          if (deleteSessionRes.ok()) {
            console.log(`REAL_E2E (Chat Dedicated): Sessão de chat ${sessionId} deletada com sucesso.`);
          } else {
            console.warn(`REAL_E2E (Chat Dedicated): Falha ao deletar sessão ${sessionId}`);
          }
        } catch (err) {
          console.error(`REAL_E2E (Chat Dedicated): Erro ao deletar sessão ${sessionId}:`, err);
        }
      }

      await apiContext.dispose();
    }
  });

  test.beforeEach(async ({ page }) => {
    // Se estiver em modo REAL_E2E, registra o listener de sessões e ignora interceptores
    if (process.env.REAL_E2E === 'true') {
      page.on('response', async (response) => {
        const url = response.url();
        if (url.includes('/api/chat') && response.request().method() === 'POST') {
          try {
            const json = await response.json();
            if (json && json.sessionId) {
              createdSessionIds.add(json.sessionId);
              console.log(`REAL_E2E (Chat Dedicated - Tracker): Sessão detectada e registrada para limpeza: ${json.sessionId}`);
            }
          } catch (e) {
            // Ignora
          }
        }
      });
      return;
    }

    // Intercepta configurações de LLM
    await page.route('**/api/llm/configuration', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          defaultProvider: 'Ollama',
          defaultModel: 'llama3',
          providers: [{ name: 'Ollama', isEnabled: true, defaultModel: 'llama3', models: ['llama3'] }],
        }),
      });
    });

    // Mock das chamadas de chat geral
    await page.route('**/api/chat', async (route) => {
      const payload = JSON.parse(route.request().postData() || '{}');
      const isDedicated = !!payload.targetAgent;
      
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          response: isDedicated 
            ? `Olá! Sou o ${payload.targetAgent} respondendo no canal dedicado.`
            : 'Olá! Sou o Assistente Geral.',
          agentUsed: payload.targetAgent || 'ChiefAgent',
          agentTier: isDedicated ? 2 : 0,
          success: true,
        }),
      });
    });

    // Mock da lista de agentes na página de agentes
    await page.route('**/api/agent/agents**', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          {
            name: 'SpecialistAgent',
            description: 'Agente de banco de dados especialista',
            domain: 'Database',
            tier: 2,
            isActive: true,
          }
        ]),
      });
    });
  });

  test('deve isolar completamente as mensagens do chat geral em relação ao chat dedicado (US-33)', async ({ chatPage, agentsPage, page }) => {
    // Aumenta o tempo limite caso esteja rodando com LLM real no Docker
    const isRealE2E = process.env.REAL_E2E === 'true';
    if (isRealE2E) {
      test.setTimeout(90000);
    }

    // 1. Acessa o chat geral e envia uma mensagem
    await chatPage.goto();
    await chatPage.sendMessage('Esta é uma mensagem no chat GERAL');
    await chatPage.waitForResponse(isRealE2E ? 90000 : 30000);
    
    let messages = await chatPage.getMessages();
    expect(messages).toContain('Esta é uma mensagem no chat GERAL');

    // 2. Navega para a listagem de agentes e entra no chat dedicado do SpecialistAgent usando navegação SPA (clique no Sidebar)
    await page.locator('aside nav a[href="/agents"]').click();
    
    // Aguarda o spinner de loading sumir e o primeiro card aparecer
    await page.locator('h3').first().waitFor({ state: 'visible', timeout: 5000 }).catch(() => {});
    const h3Texts = await page.locator('h3').allTextContents();
    console.log('PLAYWRIGHT_DEBUG: H3 texts found on /agents page after wait:', h3Texts);

    await agentsPage.startChatWith('SpecialistAgent');
    
    // Confirma que a URL mudou para o canal dedicado
    await expect(page).toHaveURL(/\/chat\/SpecialistAgent/);

    // 3. ASSERÇÃO CRÍTICA (GAP 3): Mensagens do chat geral não devem vazar para o chat dedicado!
    let dedicatedMessages = await chatPage.getMessages();
    expect(dedicatedMessages).not.toContain('Esta é uma mensagem no chat GERAL');

    // 4. Envia mensagem no chat dedicado
    await chatPage.sendMessage('Olá SpecialistAgent, preciso de ajuda com SQL');
    await chatPage.waitForResponse(isRealE2E ? 90000 : 30000);
    
    dedicatedMessages = await chatPage.getMessages();
    expect(dedicatedMessages).toContain('Olá SpecialistAgent, preciso de ajuda com SQL');
    
    if (isRealE2E) {
      expect(dedicatedMessages[1]).not.toBeNull();
      expect(dedicatedMessages[1].trim().length).toBeGreaterThan(0);
    } else {
      expect(dedicatedMessages).toContain('Olá! Sou o SpecialistAgent respondendo no canal dedicado.');
    }

    // 5. Retorna ao chat geral usando navegação SPA (clique no Sidebar) para preservar estado em memória
    await page.locator('aside nav a[href="/"]').click();
    
    // Aguarda o término da navegação SPA e atualização do canal ativo
    await expect(page).not.toHaveURL(/\/chat\//);
    
    // Aguarda até que a mensagem do chat geral seja renderizada novamente na timeline
    await page.locator('.chat-markdown, p.whitespace-pre-wrap')
      .filter({ hasText: 'Esta é uma mensagem no chat GERAL' })
      .waitFor({ state: 'visible', timeout: 5000 });
    
    // 6. ASSERÇÃO CRÍTICA (GAP 3): Mensagens do chat dedicado não devem vazar para a timeline do chat geral!
    const generalMessages = await chatPage.getMessages();
    expect(generalMessages).toContain('Esta é uma mensagem no chat GERAL');
    expect(generalMessages).not.toContain('Olá SpecialistAgent, preciso de ajuda com SQL');
  });
});
