import { test as base } from '@playwright/test';
import { ChatPage } from '../pages/ChatPage';
import { AgentsPage } from '../pages/AgentsPage';

// Define a estrutura das fixtures customizadas
type MyFixtures = {
  chatPage: ChatPage;
  agentsPage: AgentsPage;
};

// Estende a fixture base do Playwright
export const test = base.extend<MyFixtures>({
  page: async ({ page, context, baseURL }, use) => {
    const isRealE2E = process.env.REAL_E2E === 'true';
    const testInfo = test.info();
    const isLoginTest = testInfo && testInfo.file && testInfo.file.includes('login-apikey');

    // Se estiver em modo REAL_E2E e não for o teste de login, injeta a API Key administrativa real no cookie e no localStorage
    if (isRealE2E && !isLoginTest) {
      await context.addCookies([
        {
          name: 'agentic_api_key',
          value: 'minha-chave-secreta-admin-123',
          domain: 'localhost',
          path: '/',
        }
      ]);

      // Estabelece a origem e grava no localStorage real antes de expor a página ao teste
      try {
        const targetURL = baseURL || 'http://localhost/';
        await page.goto(targetURL);
        await page.evaluate(() => {
          localStorage.setItem('agentic_api_key', 'minha-chave-secreta-admin-123');
          console.log('PLAYWRIGHT_DEBUG: Pre-seeded agentic_api_key in physical localStorage.');
        });
        // Recarrega a página para que a aplicação inicialize já com a chave de API presente no localStorage
        await page.goto(targetURL);
      } catch (e) {
        console.error('PLAYWRIGHT_DEBUG: Error pre-seeding localStorage in page fixture:', e);
      }
    }

    // Repassa as mensagens de console do navegador para o terminal do Playwright para depuração em tempo real
    page.on('console', msg => {
      console.log(`[BROWSER_CONSOLE] [${msg.type()}] ${msg.text()}`);
    });

    // Escuta erros de execução não tratados no navegador
    page.on('pageerror', err => {
      console.log(`[BROWSER_ERROR] ${err.message}\nStack:\n${err.stack}`);
    });

    // Intercepta todas as requisições de autenticação do Supabase para simular usuário logado na rede
    await page.route('**/auth/v1/**', async (route) => {
      if (isRealE2E) {
        // Evita delay ou falha de rede/DNS para domínios Supabase dummy inalcançáveis no E2E real.
        // Retorna instantaneamente resposta nula de sessão para que o checkAuth do frontend
        // faça o fallback imediato e rápido para a Chave de API baseada em localStorage.
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ session: null, user: null }),
        });
        return;
      }
      if (isLoginTest || page.url().includes('no-bypass=true')) {
        await route.continue();
        return;
      }
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          access_token: 'fake-jwt-token',
          token_type: 'bearer',
          expires_in: 3600,
          refresh_token: 'fake-refresh-token',
          user: {
            id: 'fake-user-id',
            email: 'test@example.com',
            user_metadata: { name: 'Test User' },
            role: 'authenticated',
            aud: 'authenticated',
            created_at: new Date().toISOString(),
          },
          session: {
            access_token: 'fake-jwt-token',
            token_type: 'bearer',
            expires_in: 3600,
            user: {
              id: 'fake-user-id',
              email: 'test@example.com',
              user_metadata: { name: 'Test User' },
              role: 'authenticated',
            }
          }
        }),
      });
    });

    // Função auxiliar para injetar cabeçalhos CORS dinâmicos e corretos nas respostas mockadas
    const getCorsHeaders = (requestHeaders: Record<string, string>) => ({
      'Access-Control-Allow-Origin': requestHeaders['origin'] || 'http://localhost',
      'Access-Control-Allow-Methods': 'GET, POST, PUT, DELETE, OPTIONS',
      'Access-Control-Allow-Headers': 'Content-Type, Authorization, X-Tenant-Id',
      'Access-Control-Allow-Credentials': 'true',
    });

    // Função auxiliar para continuar requisições removendo token Supabase falso para evitar erro 500 no backend real
    const continueRequest = async (route: any) => {
      const headers = { ...route.request().headers() };
      let url = route.request().url();
      
      console.log(`[PLAYWRIGHT_DEBUG_HEADERS] URL: ${url} | Headers before:`, JSON.stringify(headers));

      // Se estiver em modo REAL_E2E e não for o teste de login, injeta a chave de API real nos cabeçalhos
      if (isRealE2E && !isLoginTest) {
        // Remove qualquer cabeçalho de API Key existente de forma case-insensitive primeiro
        for (const key of Object.keys(headers)) {
          if (key.toLowerCase() === 'x-api-key') {
            delete headers[key];
          }
        }
        headers['X-Api-Key'] = 'minha-chave-secreta-admin-123';
        
        // Remove incondicionalmente qualquer cabeçalho de Authorization para evitar conflito com JwtBearer no backend
        for (const key of Object.keys(headers)) {
          if (key.toLowerCase() === 'authorization') {
            console.log(`[PLAYWRIGHT_DEBUG] Deleting Authorization header from real request to: ${url}`);
            delete headers[key];
          }
        }
      } else {
        // Remove qualquer cabeçalho de Authorization contendo o token JWT fake de forma case-insensitive no modo mock
        for (const key of Object.keys(headers)) {
          if (key.toLowerCase() === 'authorization' && headers[key] === 'Bearer fake-jwt-token') {
            delete headers[key];
          }
        }
      }
      
      const hasFakeToken = url.includes('access_token=fake-jwt-token');
      
      if (hasFakeToken) {
        url = url.replace('access_token=fake-jwt-token', '');
      }

      // Se for conexão do SignalR (/hubs) no backend real, garante que a api_key administrativa seja passada na query
      if (isRealE2E && !isLoginTest && url.includes('/hubs/')) {
        const separator = url.includes('?') ? '&' : '?';
        url = url + separator + 'api_key=minha-chave-secreta-admin-123';
      }

      console.log(`[PLAYWRIGHT_DEBUG_HEADERS] URL: ${url} | Headers after:`, JSON.stringify(headers));

      if (hasFakeToken || (isRealE2E && !isLoginTest && url.includes('/hubs/'))) {
        // Limpa delimitadores query adicionais que sobram
        url = url.replace('?&', '?').replace('&&', '&').replace(/\?$/, '').replace(/&$/, '');
        await route.continue({ url, headers });
        return;
      }
      
      await route.continue({ headers });
    };

    // Interceptador genérico para qualquer outra chamada de API sob /api/ que possa falhar com 401
    // Registrado primeiro para que tenha menor prioridade que as rotas mais específicas registradas em seguida.
    // Trata também requisições preflight OPTIONS com sucesso e CORS.
    await page.route('**/api/**', async (route) => {
      if (process.env.REAL_E2E === 'true' || page.url().includes('no-bypass=true')) {
        await continueRequest(route);
        return;
      }
      const request = route.request();
      const headers = request.headers();
      const corsHeaders = getCorsHeaders(headers);

      if (request.method() === 'OPTIONS') {
        await route.fulfill({
          status: 204,
          headers: corsHeaders,
        });
        return;
      }

      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        headers: corsHeaders,
        body: JSON.stringify({}),
      });
    });

    // Intercepta e simula negociações SignalR com suporte a CORS
    await page.route('**/hubs/**/negotiate**', async (route) => {
      if (process.env.REAL_E2E === 'true' || page.url().includes('no-bypass=true')) {
        await continueRequest(route);
        return;
      }
      const request = route.request();
      const headers = request.headers();
      const corsHeaders = getCorsHeaders(headers);

      if (request.method() === 'OPTIONS') {
        await route.fulfill({
          status: 204,
          headers: corsHeaders,
        });
        return;
      }

      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        headers: corsHeaders,
        body: JSON.stringify({
          negotiateVersion: 1,
          connectionId: 'fake-connection-id',
          connectionToken: 'fake-connection-token',
          availableTransports: [] // Força o fallback sem transportes ou falha silenciosa
        }),
      });
    });

    // Intercepta chamadas genéricas de API de listagem de agentes para manter a UI estável, com CORS
    await page.route('**/api/agent/agents**', async (route) => {
      if (process.env.REAL_E2E === 'true' || page.url().includes('no-bypass=true')) {
        await continueRequest(route);
        return;
      }
      const request = route.request();
      const headers = request.headers();
      const corsHeaders = getCorsHeaders(headers);

      if (request.method() === 'OPTIONS') {
        await route.fulfill({
          status: 204,
          headers: corsHeaders,
        });
        return;
      }

      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        headers: corsHeaders,
        body: JSON.stringify([
          {
            name: 'ChiefAgent',
            description: 'Chief Orchestrator Agent',
            tier: 0,
            isEnabled: true,
            skills: ['Orchestration']
          }
        ]),
      });
    });

    // Intercepta chamadas de configuração de LLM como fallback global, com CORS
    await page.route('**/api/admin/llm/configuration', async (route) => {
      if (process.env.REAL_E2E === 'true' || page.url().includes('no-bypass=true')) {
        await continueRequest(route);
        return;
      }
      const request = route.request();
      const headers = request.headers();
      const corsHeaders = getCorsHeaders(headers);

      if (request.method() === 'OPTIONS') {
        await route.fulfill({
          status: 204,
          headers: corsHeaders,
        });
        return;
      }

      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        headers: corsHeaders,
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

    // Intercepta chamadas genéricas para sessões antigas, com CORS
    await page.route('**/api/session**', async (route) => {
      if (process.env.REAL_E2E === 'true' || page.url().includes('no-bypass=true')) {
        await continueRequest(route);
        return;
      }
      const request = route.request();
      const headers = request.headers();
      const corsHeaders = getCorsHeaders(headers);

      if (request.method() === 'OPTIONS') {
        await route.fulfill({
          status: 204,
          headers: corsHeaders,
        });
        return;
      }

      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        headers: corsHeaders,
        body: JSON.stringify([]),
      });
    });

    // Intercepta alertas do sistema, com CORS
    await page.route('**/api/v1/alerts**', async (route) => {
      if (process.env.REAL_E2E === 'true' || page.url().includes('no-bypass=true')) {
        await continueRequest(route);
        return;
      }
      const request = route.request();
      const headers = request.headers();
      const corsHeaders = getCorsHeaders(headers);

      if (request.method() === 'OPTIONS') {
        await route.fulfill({
          status: 204,
          headers: corsHeaders,
        });
        return;
      }

      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        headers: corsHeaders,
        body: JSON.stringify([]),
      });
    });




    // Injeta um script de inicialização executado antes de qualquer script da página carregar (incluindo React e Supabase)
    // Esse script intercepta chamadas ao localStorage.getItem para retornar a sessão Supabase mockada sob qualquer chave dinâmica
    await page.addInitScript(({ disableBypass, isRealE2E }) => {
      // Desabilita WebSockets para forçar fallbacks HTTP (ServerSentEvents / LongPolling)
      // que são interceptáveis pelo Playwright e evitam vazamento de tokens de autenticação falsos
      const urlParams = new URLSearchParams(window.location.search);
      const shouldDisableBypass = disableBypass || urlParams.get('no-bypass') === 'true' || isRealE2E;
      
      if (!shouldDisableBypass) {
        try {
          const OriginalWebSocket = window.WebSocket;
          if (OriginalWebSocket) {
            const CustomWebSocket = function (url: any, protocols: any) {
              if (typeof url === 'string' && url.includes('/hubs/')) {
                console.log('PLAYWRIGHT_DEBUG: Intercepted and blocked WebSocket connection to hub: ' + url);
                throw new Error("WebSocket connection blocked by Playwright E2E fixture to force SSE/LongPolling fallback.");
              }
              return Reflect.construct(OriginalWebSocket, arguments);
            };
            CustomWebSocket.prototype = OriginalWebSocket.prototype;
            Object.assign(CustomWebSocket, OriginalWebSocket);
            (window as any).WebSocket = CustomWebSocket;
            console.log('PLAYWRIGHT_DEBUG: WebSocket wrapper injected to block /hubs/ connections selectively.');
          }
        } catch (e) {
          console.error('PLAYWRIGHT_DEBUG: Failed to wrap WebSocket', e);
        }
      }

      // Função auxiliar segura para verificar se podemos ler/escrever no localStorage sem SecurityError
      const isLocalStorageSafe = () => {
        try {
          return typeof window !== 'undefined' && 
                 !!window.localStorage && 
                 window.location.origin !== 'null' && 
                 window.location.origin !== 'about:blank';
        } catch (e) {
          return false;
        }
      };

      if (shouldDisableBypass) {
        console.log('PLAYWRIGHT_DEBUG: Authentication bypass disabled for this page load.');
        // Se for E2E real e não for o teste de login, injeta a chave de API real no localStorage
        if (isRealE2E && !disableBypass && isLocalStorageSafe()) {
          try {
            localStorage.setItem('agentic_api_key', 'minha-chave-secreta-admin-123');
            console.log('PLAYWRIGHT_DEBUG: Pre-seeded agentic_api_key in physical localStorage for real E2E.');
          } catch (e) {
            console.error('PLAYWRIGHT_DEBUG: Failed to write agentic_api_key to localStorage:', e);
          }
          
          try {
            console.log('PLAYWRIGHT_DEBUG: Injecting localStorage.getItem wrapper to dynamically return real agentic_api_key for real E2E as fallback.');
            const originalGetItem = localStorage.getItem;
            localStorage.getItem = function(key: string) {
              if (key === 'agentic_api_key') {
                console.log('PLAYWRIGHT_DEBUG: Dynamically intercepted localStorage.getItem for key "agentic_api_key" returning real admin key');
                return 'minha-chave-secreta-admin-123';
              }
              return originalGetItem.apply(this, arguments as any);
            };
          } catch (e) {
            console.error('PLAYWRIGHT_DEBUG: Failed to wrap localStorage.getItem for real E2E:', e);
          }
        }
        return;
      }

      // Sessão mockada na raiz compatível com Supabase v2
      const sessionData = {
        access_token: 'fake-jwt-token',
        token_type: 'bearer',
        expires_in: 3600,
        expires_at: Math.floor(Date.now() / 1000) + 3600,
        refresh_token: 'fake-refresh-token',
        user: {
          id: 'fake-user-id',
          email: 'test@example.com',
          user_metadata: { name: 'Test User' },
          role: 'authenticated',
          aud: 'authenticated',
          created_at: new Date().toISOString(),
        }
      };
      const sessionStr = JSON.stringify(sessionData);

      if (isLocalStorageSafe()) {
        try {
          // Sobrescreve o protótipo de localStorage.getItem para lidar com chaves dinâmicas do Supabase e tokens customizados do frontend
          const originalGetItem = localStorage.getItem;
          localStorage.getItem = function(key: string) {
            if (key && key.startsWith('sb-') && key.endsWith('-auth-token')) {
              console.log(`PLAYWRIGHT_DEBUG: Dynamically intercepted localStorage.getItem for key "${key}"`);
              return sessionStr;
            }
            if (key === 'agentic_auth_token') {
              console.log('PLAYWRIGHT_DEBUG: Dynamically intercepted localStorage.getItem for key "agentic_auth_token"');
              return 'fake-jwt-token';
            }
            return originalGetItem.apply(this, arguments as any);
          };

          // Garante também o mock de chaves explícitas no carregamento
          localStorage.setItem('agentic_auth_token', 'fake-jwt-token');
          localStorage.setItem('sb-dummy-project-auth-token', sessionStr);
          localStorage.setItem('sb-localhost-auth-token', sessionStr);
          localStorage.setItem('sb-supabase-auth-token', sessionStr);
          localStorage.setItem('sb-auth-token', sessionStr);
        } catch (e) {
          console.error('PLAYWRIGHT_DEBUG: Failed to initialize mock localStorage session:', e);
        }
      }
    }, { disableBypass: isLoginTest, isRealE2E });


    await use(page);
  },
  chatPage: async ({ page }, use) => {
    const chatPage = new ChatPage(page);
    await use(chatPage);
  },
  agentsPage: async ({ page }, use) => {
    const agentsPage = new AgentsPage(page);
    await use(agentsPage);
  },
});

export { expect } from '@playwright/test';

