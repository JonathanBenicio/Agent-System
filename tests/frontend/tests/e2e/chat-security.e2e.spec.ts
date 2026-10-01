import { test, expect } from '../../fixtures';

// Set global para rastreamento de IDs de sessões de chat criadas durante a execução da spec
const createdSessionIds = new Set<string>();

test.describe('Chat Security - Sanitização de Markdown e Proteção XSS', () => {
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

      console.log(`REAL_E2E (Chat Security): Encontradas ${createdSessionIds.size} sessões criadas nesta spec. Iniciando purga isolada...`);
      for (const sessionId of createdSessionIds) {
        try {
          const deleteSessionRes = await apiContext.delete(`/api/session/${sessionId}`);
          if (deleteSessionRes.ok()) {
            console.log(`REAL_E2E (Chat Security): Sessão de chat ${sessionId} deletada com sucesso.`);
          } else {
            console.warn(`REAL_E2E (Chat Security): Falha ao deletar sessão ${sessionId}`);
          }
        } catch (err) {
          console.error(`REAL_E2E (Chat Security): Erro ao deletar sessão ${sessionId}:`, err);
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
              console.log(`REAL_E2E (Chat Security - Tracker): Sessão detectada e registrada para limpeza: ${json.sessionId}`);
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

  test('deve sanitizar scripts maliciosos injetados e impedir execução XSS no DOM', async ({ chatPage, page }) => {
    const maliciousPayload = 'Tentativa de injeção: <script>window.xssExploit = "Vulnerável";</script><iframe src="javascript:alert(1)"></iframe>';

    // Interceptamos a resposta para simular um agente malicioso ou jailbreak injetando XSS
    await page.route('**/api/chat', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          response: maliciousPayload,
          agentUsed: 'SecurityAgent',
          agentTier: 0,
          success: true,
        }),
      });
    });

    await chatPage.goto();
    
    // Inicializa a variável no escopo global da janela para testar se ela é alterada
    await page.evaluate(() => {
      (window as any).xssExploit = undefined;
    });

    await chatPage.sendMessage('Teste de segurança contra XSS');
    await chatPage.waitForResponse();

    // 1. O script não deve ter sido executado, logo window.xssExploit deve continuar undefined
    const isExploited = await page.evaluate(() => {
      return (window as any).xssExploit;
    });
    expect(isExploited).toBeUndefined();

    // 2. Elementos como script e iframe não devem ser renderizados no DOM como elementos ativos
    const scriptTag = page.locator('.chat-markdown script');
    await expect(scriptTag).toHaveCount(0);

    const iframeTag = page.locator('.chat-markdown iframe');
    await expect(iframeTag).toHaveCount(0);
  });
});
