import { test, expect } from '../../fixtures';

// Set global para rastreamento de IDs de sessões de chat criadas durante a execução da spec
const createdSessionIds = new Set<string>();

test.describe('Chat Advanced Features - RAG, Citations & Workflows', () => {
  // Executa após a spec finalizar para limpar as sessões de chat e agentes do banco real
  test.afterAll(async ({ playwright }) => {
    if (process.env.REAL_E2E === 'true') {
      const baseURL = process.env.BASE_URL || 'http://localhost/';
      const apiContext = await playwright.request.newContext({
        baseURL,
        extraHTTPHeaders: {
          'X-Api-Key': 'minha-chave-secreta-admin-123',
        },
      });

      try {
        console.log('REAL_E2E (Chat RAG): Removendo ZeroTrustAgent do banco real...');
        const deleteZeroTrustAgentRes = await apiContext.delete('/api/agent/agents/ZeroTrustAgent');
        if (deleteZeroTrustAgentRes.ok()) {
          console.log('REAL_E2E (Chat RAG): ZeroTrustAgent removido com sucesso!');
        } else {
          console.warn('REAL_E2E (Chat RAG): Falha ao remover ZeroTrustAgent ou agente inexistente.');
        }
      } catch (err) {
        console.error('REAL_E2E (Chat RAG): Erro ao tentar remover ZeroTrustAgent:', err);
      }

      console.log(`REAL_E2E (Chat RAG): Encontradas ${createdSessionIds.size} sessões criadas nesta spec. Iniciando purga isolada...`);
      for (const sessionId of createdSessionIds) {
        try {
          const deleteSessionRes = await apiContext.delete(`/api/session/${sessionId}`);
          if (deleteSessionRes.ok()) {
            console.log(`REAL_E2E (Chat RAG): Sessão de chat ${sessionId} deletada com sucesso (RAG purgado).`);
          } else {
            console.warn(`REAL_E2E (Chat RAG): Falha ao deletar sessão ${sessionId}.`);
          }
        } catch (err) {
          console.error(`REAL_E2E (Chat RAG): Erro ao deletar sessão ${sessionId}:`, err);
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
              console.log(`REAL_E2E (Chat RAG - Tracker): Sessão detectada e registrada para limpeza: ${json.sessionId}`);
            }
          } catch (e) {
            // Ignora silenciosamente se o corpo não for JSON
          }
        }
      });
      return;
    }

    // Mock do provedor e modelo LLM
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
              models: ['llama3'],
            },
          ],
        }),
      });
    });

    // Mock do endpoint de ingestão em lote (RAG Drag and Drop)
    await page.route('**/api/document/ingest/batch*', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          total: 2,
          succeeded: 2,
          failed: 0,
          results: [],
        }),
      });
    });
  });

  test('deve exibir overlay de drag e realizar ingestão de arquivos RAG via Drag and Drop', async ({ chatPage, page }) => {
    await chatPage.goto();
    // Aguarda a hidratação completa esperando pelo input de mensagem ficar visível
    await chatPage.messageInput.waitFor({ state: 'visible' });

    // 1. Simula o evento de arrastar um arquivo sobre a timeline (Drag Over)
    await page.evaluate(() => {
      const target = document.querySelector('.flex-1.flex.flex-col.min-w-0.h-full.relative');
      if (target) {
        const dt = new DataTransfer();
        const dragEvent = new DragEvent('dragover', {
          bubbles: true,
          cancelable: true,
          dataTransfer: dt
        });
        target.dispatchEvent(dragEvent);
      }
    });

    // O overlay deve aparecer na tela contendo o termo "Ingestão RAG Contextual"
    const overlay = page.locator('text=Ingestão RAG Contextual');
    await expect(overlay).toBeVisible();

    // 2. Simula o cancelamento do arrasto (Drag Leave)
    await page.evaluate(() => {
      const target = document.querySelector('.flex-1.flex.flex-col.min-w-0.h-full.relative');
      if (target) {
        const dragEvent = new DragEvent('dragleave', {
          bubbles: true,
          cancelable: true
        });
        target.dispatchEvent(dragEvent);
      }
    });
    
    // O overlay deve sumir da tela
    await expect(overlay).not.toBeVisible();

    // 3. Simula a soltura real do arquivo (Drop)
    // Criamos um mock de arquivo no navegador e disparados o evento drop
    await page.evaluate(() => {
      const target = document.querySelector('.flex-1.flex.flex-col.min-w-0.h-full.relative');
      if (target) {
        const dt = new DataTransfer();
        const file1 = new File(['conteúdo fictício do doc 1'], 'documento_1.pdf', { type: 'application/pdf' });
        const file2 = new File(['conteúdo fictício do doc 2'], 'documento_2.txt', { type: 'text/plain' });
        dt.items.add(file1);
        dt.items.add(file2);

        const dropEvent = new DragEvent('drop', {
          bubbles: true,
          cancelable: true,
          dataTransfer: dt
        });
        target.dispatchEvent(dropEvent);
      }
    });

    // Deve aparecer a notificação (Toast) ou a mensagem especial de sistema
    // A UI exibe um banner de processamento e depois insere uma mensagem de sistema
    const systemMessage = page.locator('.flex.items-start.gap-3.py-3.px-4.rounded-lg.bg-red-950\\/30');
    await expect(systemMessage).toContainText('Sucesso: 2 de 2 documento(s) indexado(s)');
  });

  test('deve renderizar as citações bibliográficas e interagir com o popover de RAG', async ({ chatPage, page }) => {
    // Intercepta a chamada REST de chat para fornecer uma resposta com fontes
    await page.route('**/api/chat', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          response: 'De acordo com a política corporativa, o isolamento multi-tenant é garantido nativamente no MAF.',
          agentUsed: 'ChiefAgent',
          agentTier: 0,
          success: true,
          citations: [
            {
              id: 'c1',
              sourceDocumentName: 'diretrizes_sistema.pdf',
              relevantExcerpt: 'O Agentic System opera com isolamento de tenant rígido via headers.',
              confidence: 0.95,
              pageNumber: 4,
            },
          ],
        }),
      });
    });

    await chatPage.goto();
    // Aguarda a hidratação completa
    await chatPage.messageInput.waitFor({ state: 'visible' });

    await chatPage.sendMessage('Como funciona o multi-tenant?');
    await chatPage.waitForResponse();

    // Valida se a seção "Fontes" foi exibida na resposta
    const sourcesHeader = page.locator('text=Fontes (1)');
    await expect(sourcesHeader).toBeVisible();

    // Valida se a pílula da fonte bibliográfica está presente
    const citationButton = page.locator('button[aria-label="Ver citação"]').first();
    await expect(citationButton).toContainText('diretrizes_sistema');

    // Clica na citação para expandir o popover
    await citationButton.click();

    // O popover de detalhes deve aparecer contendo o trecho citado e confiança.
    // Usamos um substring match parcial e flexível sem aspas para evitar falsos negativos com o invólucro de aspas da renderização.
    const excerptText = page.locator('text=O Agentic System opera com isolamento de tenant');
    await expect(excerptText).toBeVisible();

    const confidenceText = page.locator('text=Confiança: 95%');
    await expect(confidenceText).toBeVisible();

    const pageText = page.locator('text=Pág. 4');
    await expect(pageText).toBeVisible();
  });

  test('deve renderizar badges de ferramentas, ações, memória injetada e alertas do sistema', async ({ chatPage, page }) => {
    // Intercepta a chamada REST de chat para incluir ações e ferramentas
    await page.route('**/api/chat', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          response: 'Executei as rotinas e consolidei os dados da pesquisa.',
          agentUsed: 'MasterAgent',
          agentTier: 1,
          success: true,
          actions: ['Buscar Dados', 'Consolidar Relatório'],
          tools: ['brave_web_search', 'postgresql_reader'],
        }),
      });
    });

    await chatPage.goto();
    // Aguarda a hidratação completa
    await chatPage.messageInput.waitFor({ state: 'visible' });

    await chatPage.sendMessage('Consolide os dados');
    await chatPage.waitForResponse();

    // 1. Valida se os badges de ações foram criados
    const actionBadge1 = page.locator('text=⚡ Buscar Dados');
    await expect(actionBadge1).toBeVisible();
    const actionBadge2 = page.locator('text=⚡ Consolidar Relatório');
    await expect(actionBadge2).toBeVisible();

    // 2. Valida se os badges de ferramentas foram criados
    const toolBadge1 = page.locator('text=🔧 brave_web_search');
    await expect(toolBadge1).toBeVisible();
    const toolBadge2 = page.locator('text=🔧 postgresql_reader');
    await expect(toolBadge2).toBeVisible();

    // 3. Simula uma mensagem com Memória Recuperada no histórico
    const targetSessionId = 'session-com-memoria-123';

    await page.route('**/api/session**', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          {
            id: targetSessionId,
            title: 'Sessão com Memória',
            updatedAt: new Date().toISOString(),
          }
        ]),
      });
    });

    await page.route(`**/api/session/${targetSessionId}/messages`, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          {
            id: 'm1',
            role: 'user',
            content: 'Preciso da minha chave antiga',
            timestamp: new Date().toISOString(),
            memoryInjected: true,
          },
          {
            id: 'm2',
            role: 'system',
            content: 'Aviso do Sistema: Erro ao persistir sessão.',
            timestamp: new Date().toISOString(),
          }
        ]),
      });
    });

    // Recarrega a página de chat para ler o histórico contendo memória injetada e mensagem de sistema
    await page.reload();
    // Aguarda a hidratação completa pós-reload
    await chatPage.messageInput.waitFor({ state: 'visible' });

    // Clica explicitamente na sessão no sidebar para carregar o histórico correspondente
    await page.locator('text=Sessão com Memória').click();

    // Valida se o indicador visual de Memória Recuperada é renderizado na mensagem do usuário
    const memoryBadge = page.locator('text=Memória Recuperada');
    await expect(memoryBadge).toBeVisible();

    // Valida se o banner especial de sistema de erro com triângulo vermelho é renderizado
    const alertSystemMessage = page.locator('.flex.items-start.gap-3.py-3.px-4.rounded-lg.bg-red-950\\/30');
    await expect(alertSystemMessage).toBeVisible();
    await expect(alertSystemMessage).toContainText('Aviso do Sistema: Erro ao persistir sessão.');
  });

  test('deve bloquear o acesso ao RAG se o agente não possuir nenhuma sala de conhecimento associada (Zero Trust - Opção B)', async ({ chatPage, agentsPage, page }) => {
    const isRealE2E = process.env.REAL_E2E === 'true';
    if (isRealE2E) {
      test.setTimeout(90000);
      
      const baseURL = process.env.BASE_URL || 'http://localhost/';
      const apiContext = await page.request.newContext({
        baseURL,
        extraHTTPHeaders: {
          'X-Api-Key': 'minha-chave-secreta-admin-123',
          'Content-Type': 'application/json',
        },
      });

      // 1. Cria um agente no backend real SEM salas associadas
      const zeroTrustAgentPayload = {
        name: 'ZeroTrustAgent',
        description: 'Agente sem salas para teste de Zero Trust',
        tier: 2,
        domain: 'Security',
        allowedTools: [],
        capabilities: [],
        autonomyLevel: 2,
        policyIds: [],
        instructions: 'Você é ZeroTrustAgent. Responda apenas com base no seu conhecimento geral.',
        configuration: {}
      };
      
      const createRes = await apiContext.post('/api/agent/agents', {
        data: zeroTrustAgentPayload,
      });
      expect(createRes.ok()).toBeTruthy();

      // Garante que não há salas associadas (envia array vazio para set-rooms via PUT para conformidade perfeita de rotas)
      const roomsRes = await apiContext.put(`/api/agent/agents/ZeroTrustAgent/rooms`, {
        data: []
      });
      expect(roomsRes.ok()).toBeTruthy();

      // 2. Inicia chat com o agente
      await agentsPage.goto();
      await agentsPage.startChatWith('ZeroTrustAgent');
      await expect(page).toHaveURL(/\/chat\/ZeroTrustAgent/);

      // 3. Envia uma mensagem e valida que não há citação (RAG bloqueado)
      await chatPage.sendMessage('Como funciona o multi-tenant? Buscar nos arquivos.');
      await chatPage.waitForResponse(90000);

      // 4. Valida se o cabeçalho "Fontes" não é visível na tela
      const sourcesHeader = page.locator('text=Fontes');
      await expect(sourcesHeader).toBeHidden();

      // Limpeza imediata do agente temporário
      await apiContext.delete('/api/agent/agents/ZeroTrustAgent');
      await apiContext.dispose();
    } else {
      // Teste mockado
      await page.route('**/api/agent/agents/ZeroTrustAgent/rooms', async (route) => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify([]),
        });
      });

      await page.route('**/api/chat', async (route) => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            response: 'Olá, sou o ZeroTrustAgent. Meu acesso RAG foi bloqueado.',
            agentUsed: 'ZeroTrustAgent',
            agentTier: 2,
            success: true,
            citations: [], // Sem citações!
          }),
        });
      });

      await chatPage.goto();
      // Aguarda a hidratação completa
      await chatPage.messageInput.waitFor({ state: 'visible' });

      await chatPage.sendMessage('Teste RAG');
      await chatPage.waitForResponse();

      const sourcesHeader = page.locator('text=Fontes');
      await expect(sourcesHeader).toBeHidden();
    }
  });
});
