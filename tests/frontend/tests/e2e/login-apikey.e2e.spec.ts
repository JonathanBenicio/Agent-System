import { test, expect } from '../../fixtures';

test.describe('Login E2E - Chave de API', () => {
  test.beforeEach(async ({ context, page }) => {
    if (process.env.REAL_E2E !== 'true') {
      test.skip(true, 'Skip real login E2E test when REAL_E2E is not active');
      return;
    }

    // Garante isolamento completo limpando cookies e localStorage antes de cada teste
    await context.clearCookies();
    await page.goto('/?no-bypass=true');
    await page.evaluate(() => {
      localStorage.clear();
      sessionStorage.clear();
    });
    // Recarrega para garantir que a aplicação inicie do estado limpo na tela de login
    await page.goto('/?no-bypass=true');
  });

  test('deve realizar login com sucesso usando uma chave de API válida real do backend', async ({ page, chatPage }) => {
    // Valida que a modal de login está visível
    const loginHeader = page.locator('text=Autenticação AgenticSystem');
    await expect(loginHeader).toBeVisible();

    // Seleciona a aba de Chave de API
    const apiKeyTab = page.locator('button:has-text("Chave de API")');
    await expect(apiKeyTab).toBeVisible();
    await apiKeyTab.click();

    // Localiza o campo de entrada da Chave de API
    const apiKeyInput = page.locator('input[placeholder*="agentic_live_key"]');
    await expect(apiKeyInput).toBeVisible();

    // Insere a chave de API de admin real configurada no Docker Compose
    const realAdminKey = 'minha-chave-secreta-admin-123';
    await apiKeyInput.fill(realAdminKey);

    // Clica no botão para acessar o sistema
    const submitButton = page.locator('button:has-text("Acessar Sistema")');
    await expect(submitButton).toBeVisible();
    await submitButton.click();

    // A modal de login deve sumir após a autenticação bem-sucedida
    await expect(loginHeader).toBeHidden({ timeout: 5000 });

    // O input do chat deve ficar visível no chat geral, confirmando acesso autorizado
    await expect(chatPage.messageInput).toBeVisible({ timeout: 5000 });
    await expect(chatPage.messageInput).toHaveValue('');
  });

  test('deve exibir mensagem de erro ao tentar fazer login com chave de API inválida', async ({ page }) => {
    // Valida que a modal de login está visível
    const loginHeader = page.locator('text=Autenticação AgenticSystem');
    await expect(loginHeader).toBeVisible();

    // Seleciona a aba de Chave de API
    const apiKeyTab = page.locator('button:has-text("Chave de API")');
    await expect(apiKeyTab).toBeVisible();
    await apiKeyTab.click();

    // Insere uma chave de API incorreta
    const invalidKey = 'chave-com-tamanho-suficiente-mas-invalida-no-servidor';
    await page.locator('input[placeholder*="agentic_live_key"]').fill(invalidKey);

    // Clica para acessar o sistema
    await page.locator('button:has-text("Acessar Sistema")').click();

    // Deve exibir uma mensagem de erro na modal
    const errorMessage = page.locator('text=Chave de API inválida ou recusada pelo servidor');
    await expect(errorMessage).toBeVisible({ timeout: 5000 });

    // A modal de login deve continuar visível
    await expect(loginHeader).toBeVisible();
  });
});
