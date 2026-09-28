import { test, expect } from '../../fixtures';

// Set global para rastreamento de IDs de sessões de chat criadas durante a execução da spec
const createdSessionIds = new Set<string>();

test.describe('Chat Flow - Geral', () => {
  // Executa uma única vez após a spec finalizar para limpar as sessões de chat criadas no banco real
  test.afterAll(async ({ playwright }) => {
    if (process.env.REAL_E2E === 'true') {
      const baseURL = process.env.BASE_URL || 'http://localhost/';
      const apiContext = await playwright.request.newContext({
        baseURL,
        extraHTTPHeaders: {
          'X-Api-Key': 'minha-chave-secreta-admin-123',
        },
      });

      console.log(`REAL_E2E (Chat Flow): Encontradas ${createdSessionIds.size} sessões criadas nesta spec. Iniciando purga isolada...`);
      for (const sessionId of createdSessionIds) {
        try {
          const deleteSessionRes = await apiContext.delete(`/api/session/${sessionId}`);
          if (deleteSessionRes.ok()) {
            console.log(`REAL_E2E (Chat Flow): Sessão de chat ${sessionId} deletada com sucesso.`);
          } else {
            console.warn(`REAL_E2E (Chat Flow): Falha ao deletar sessão ${sessionId}`);
          }
        } catch (err) {
          console.error(`REAL_E2E (Chat Flow): Erro ao deletar sessão ${sessionId}:`, err);
        }
      }

      await apiContext.dispose();
    }
  });

  test.beforeEach(async ({ page }) => {
    if (process.env.REAL_E2E === 'true') {
      // Registrar listener para capturar de forma isolada todas as sessões criadas no backend real
      page.on('response', async (response) => {
        const url = response.url();
        if (url.includes('/api/chat') && response.request().method() === 'POST') {
          try {
            const json = await response.json();
            if (json && json.sessionId) {
              createdSessionIds.add(json.sessionId);
              console.log(`REAL_E2E (Chat Flow - Tracker): Sessão detectada e registrada para limpeza: ${json.sessionId}`);
            }
          } catch (e) {
            // Ignora silenciosamente se o corpo não for JSON
          }
        }
      });
      return;
    }

    // Intercepta configuração de LLM para retornar dados consistentes
    await page.route('**/api/llm/configuration', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          defaultProvider: 'Ollama',
          defaultModel: 'llama3',
          providers: [
            {
              name: 'Ollama',
              isEnabled: true,
              defaultModel: 'llama3',
              models: ['llama3', 'mistral'],
            },
          ],
        }),
      });
    });

    // Intercepta a chamada de envio de mensagem via REST (fallback do SignalR)
    await page.route('**/api/chat', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          response: 'Olá! Sou o Assistente Chief do Agentic System. Como posso ajudar você hoje?',
          agentUsed: 'ChiefAgent',
          agentTier: 0,
          actionsPerformed: ['Análise de Contexto', 'Geração de Resposta'],
          success: true,
        }),
      });
    });
  });

  test('deve carregar o chat geral, enviar uma mensagem e receber resposta do assistente', async ({ chatPage }) => {
    if (process.env.REAL_E2E === 'true') {
      test.setTimeout(90000);
    }

    await chatPage.goto();

    // Verifica se a caixa de texto está visível e vazia
    await expect(chatPage.messageInput).toBeVisible();
    await expect(chatPage.messageInput).toHaveValue('');

    // Envia uma mensagem
    await chatPage.sendMessage('Olá, sistema de agentes!');

    // Aguarda o processamento terminar
    await chatPage.waitForResponse(process.env.REAL_E2E === 'true' ? 90000 : 30000);

    // Obtém as mensagens exibidas na tela
    const messages = await chatPage.getMessages();
    
    // Deve conter a mensagem do usuário e a resposta do assistente
    expect(messages.length).toBeGreaterThanOrEqual(2);
    expect(messages[0]).toContain('Olá, sistema de agentes!');
    
    if (process.env.REAL_E2E === 'true') {
      // Asserção flexível tolerante para o backend/LLM real
      expect(messages[1]).not.toBeNull();
      expect(messages[1].trim().length).toBeGreaterThan(0);
    } else {
      // Asserção estrita para o mock
      expect(messages[1]).toContain('Olá! Sou o Assistente Chief do Agentic System.');
    }

    // Valida se o badge do agente foi renderizado corretamente
    const badges = await chatPage.getAgentBadges();
    expect(badges.length).toBeGreaterThan(0);
    
    if (process.env.REAL_E2E !== 'true') {
      expect(badges[0].agentName).toBe('ChiefAgent');
    }
  });

  test('deve suportar alteração de modelo de LLM usando a pílula de seleção', async ({ chatPage, page }) => {
    await chatPage.goto();

    // Abre o dropdown de seleção de modelo
    await chatPage.modelSelectorButton.click();

    // O dropdown de modelos deve ficar visível
    const dropdown = page.locator('text=Modelos Disponíveis');
    await expect(dropdown).toBeVisible();

    // Seleciona o modelo mistral
    await page.locator('button:has-text("mistral")').first().click();

    // A pílula de modelo deve exibir o novo modelo selecionado (exibido como 'mistral' formatado)
    await expect(chatPage.modelSelectorButton).toContainText('mistral');
  });
});
