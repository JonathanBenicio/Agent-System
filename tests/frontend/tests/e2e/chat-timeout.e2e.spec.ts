import { test, expect } from '../../fixtures';

// Set global para rastreamento de IDs de sessões de chat criadas durante a execução da spec
const createdSessionIds = new Set<string>();

test.describe('Chat Timeout - Indicador de Digitando (Gap 2)', () => {
  // Executa após a spec finalizar para limpar as sessões de chat criadas no banco real se houver
  test.afterAll(async ({ playwright }) => {
    if (process.env.REAL_E2E === 'true') {
      const baseURL = process.env.BASE_URL || 'http://localhost/';
      const apiContext = await playwright.request.newContext({
        baseURL,
        extraHTTPHeaders: {
          'X-Api-Key': 'minha-chave-secreta-admin-123',
        },
      });

      console.log(`REAL_E2E (Chat Timeout): Encontradas ${createdSessionIds.size} sessões criadas nesta spec. Iniciando purga isolada...`);
      for (const sessionId of createdSessionIds) {
        try {
          const deleteSessionRes = await apiContext.delete(`/api/session/${sessionId}`);
          if (deleteSessionRes.ok()) {
            console.log(`REAL_E2E (Chat Timeout): Sessão de chat ${sessionId} deletada com sucesso.`);
          } else {
            console.warn(`REAL_E2E (Chat Timeout): Falha ao deletar sessão ${sessionId}`);
          }
        } catch (err) {
          console.error(`REAL_E2E (Chat Timeout): Erro ao deletar sessão ${sessionId}:`, err);
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
              console.log(`REAL_E2E (Chat Timeout - Tracker): Sessão detectada e registrada para limpeza: ${json.sessionId}`);
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

  test('deve limpar automaticamente o indicador "Processando..." se a resposta travar (Autocleanup/Timeout)', async ({ chatPage, page }) => {
    if (process.env.REAL_E2E === 'true') {
      // Em REAL_E2E, esse teste específico de timeout sob travamento do backend é ignorado/pulado porque depende de mock controlado
      test.skip(true, 'Ignorado em E2E Real pois depende de simulação de travamento (mock)');
      return;
    }

    // Interceptamos a chamada de chat para nunca retornar resposta (simula travamento no backend ou queda de conexão)
    await page.route('**/api/chat', async (route) => {
      // Deixa a requisição pendente por tempo indeterminado
      await new Promise(() => {});
    });

    await chatPage.goto();
    
    // Envia mensagem
    await chatPage.sendMessage('Esta mensagem irá simular um travamento');

    // O typing indicator ("Processando...") deve aparecer imediatamente
    await expect(chatPage.typingIndicator).toBeVisible();
    await expect(chatPage.messageInput).toBeDisabled();

    // Se o frontend tiver o mecanismo de timeout de digitação de 10 segundos,
    // o typing indicator deve sumir automaticamente e o input deve ser reativado
    // após esse período, mesmo sem resposta do backend.
    
    // Aguardamos 12 segundos (timeout limite de segurança planejado para digitação)
    await page.waitForTimeout(12000);

    // O indicador de processamento deve ser limpo automaticamente
    await expect(chatPage.typingIndicator).toBeHidden();
    
    // O input do chat e botão devem voltar a estar habilitados para permitir nova interação do usuário
    await expect(chatPage.messageInput).toBeEnabled();
  });
});
