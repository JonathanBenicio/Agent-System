import { test, expect } from '../../fixtures';

// Set global para rastreamento de IDs de sessões de chat criadas durante a execução da spec
const createdSessionIds = new Set<string>();

test.describe('Chat Agent Tiers - Nomenclatura e Visual (Gap 1)', () => {
  // Configuração de Tiers Oficiais do Backend MAF
  const EXPECTED_TIERS = {
    0: 'Chief',
    1: 'Master',
    2: 'Specialist',
    3: 'Support',
  };

  // Executa após a spec finalizar para limpar as sessões de chat criadas no banco real
  test.afterAll(async ({ playwright }) => {
    if (process.env.REAL_E2E === 'true') {
      const baseURL = process.env.BASE_URL || 'http://localhost/';
      const apiContext = await playwright.request.newContext({
        baseURL,
        extraHTTPHeaders: {
          'X-Api-Key': 'minha-chave-secreta-admin-123',
        },
      });

      console.log(`REAL_E2E (Chat Tiers): Encontradas ${createdSessionIds.size} sessões criadas nesta spec. Iniciando purga isolada...`);
      for (const sessionId of createdSessionIds) {
        try {
          const deleteSessionRes = await apiContext.delete(`/api/session/${sessionId}`);
          if (deleteSessionRes.ok()) {
            console.log(`REAL_E2E (Chat Tiers): Sessão de chat ${sessionId} deletada com sucesso.`);
          } else {
            console.warn(`REAL_E2E (Chat Tiers): Falha ao deletar sessão ${sessionId}`);
          }
        } catch (err) {
          console.error(`REAL_E2E (Chat Tiers): Erro ao deletar sessão ${sessionId}:`, err);
        }
      }

      await apiContext.dispose();
    }
  });

  test.beforeEach(async ({ page }) => {
    if (process.env.REAL_E2E === 'true') {
      page.on('response', async (response) => {
        const url = response.url();
        if (url.includes('/api/chat') && response.request().method() === 'POST') {
          try {
            const json = await response.json();
            if (json && json.sessionId) {
              createdSessionIds.add(json.sessionId);
              console.log(`REAL_E2E (Chat Tiers - Tracker): Sessão detectada e registrada para limpeza: ${json.sessionId}`);
            }
          } catch (e) {
            // Ignora
          }
        }
      });
      return;
    }

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
  });

  test('deve renderizar badges de tier alinhados com a especificação MAF oficial do backend', async ({ chatPage, page }) => {
    // Interceptamos para que o primeiro assistente que responda seja de Tier 1 (Master no backend)
    await page.route('**/api/chat', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          response: 'Eu sou um agente de nível intermediário superior.',
          agentUsed: 'MasterAgent',
          agentTier: 1, // Tier 1 no backend = Master
          success: true,
        }),
      });
    });

    await chatPage.goto();
    await chatPage.sendMessage('Quem é você?');
    await chatPage.waitForResponse();

    const badges = await chatPage.getAgentBadges();
    expect(badges.length).toBeGreaterThan(0);
    
    const matchedBadge = badges.find(b => b.agentName === 'MasterAgent');
    expect(matchedBadge).toBeDefined();

    // ASSERÇÃO CRÍTICA (GAP 1): O frontend deve exibir 'Master' para o Tier 1.
    // Atualmente, o frontend exibe 'Specialist' para Tier 1. Esta asserção documenta a regressão/desalinhamento.
    expect(matchedBadge?.tierLabel).toBe(EXPECTED_TIERS[1]);
  });

  test('deve renderizar badges corretos para todos os níveis de agentes MAF', async ({ chatPage, page }) => {
    // Lista de testes para simular todos os tiers de agentes
    const tiersToTest = [
      { tier: 0, name: 'ChiefAgent', expected: 'Chief' },
      { tier: 1, name: 'MasterAgent', expected: 'Master' },
      { tier: 2, name: 'SpecialistAgent', expected: 'Specialist' },
      { tier: 3, name: 'SupportAgent', expected: 'Support' },
    ];

    for (const testTier of tiersToTest) {
      await page.route('**/api/chat', async (route) => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            response: `Resposta do agente de Tier ${testTier.tier}`,
            agentUsed: testTier.name,
            agentTier: testTier.tier,
            success: true,
          }),
        });
      });

      await chatPage.goto();
      await chatPage.sendMessage(`Acionar agente de Tier ${testTier.tier}`);
      await chatPage.waitForResponse();

      const badges = await chatPage.getAgentBadges();
      const currentBadge = badges.find(b => b.agentName === testTier.name);
      
      expect(currentBadge).toBeDefined();
      
      // Valida se o texto exibido coincide com os Tiers oficiais da especificação
      expect(currentBadge?.tierLabel).toBe(testTier.expected);
    }
  });
});
