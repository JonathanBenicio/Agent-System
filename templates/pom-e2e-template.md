# Template: Page Object Model (POM) para Testes E2E

> **Objetivo:** Padronizar a criação de Page Objects para testes End-to-End (E2E), promovendo reutilização de código, manutenibilidade e clareza.  
> **Framework Sugerido:** [Playwright | Selenium | Cypress]  
> **Linguagem Sugerida:** [TypeScript | C# | Java | Python]

---

## Estrutura de Arquivos Recomendada

Uma boa organização de pastas é crucial para a manutenibilidade dos testes.

```text
/tests
  /pages                # Contém as classes do Page Object Model
    LoginPage.ts
    DashboardPage.ts
    HeaderComponent.ts
  /specs                # Contém os arquivos de teste (cenários)
    login.spec.ts
    dashboard.spec.ts
  /utils                # Contém helpers e utilitários
    auth.helper.ts
```

---

## 1. Template da Page Class

A Page Class é o coração do padrão. Ela centraliza os seletores e as interações de uma página ou componente específico.

### Exemplo: `LoginPage.ts`

```typescript
import { type Page, type Locator } from '@playwright/test';

export class LoginPage {
  // 1. Propriedades da Classe (Readonly para seletores)
  readonly page: Page;
  readonly usernameInput: Locator;
  readonly passwordInput: Locator;
  readonly loginButton: Locator;
  readonly errorMessage: Locator;

  // 2. Construtor: Inicializa a página e mapeia os seletores
  constructor(page: Page) {
    this.page = page;

    // Mapeamento de Seletores (Locators)
    this.usernameInput = page.locator('#username');
    this.passwordInput = page.locator('input[name="password"]');
    this.loginButton = page.locator('button[type="submit"]');
    this.errorMessage = page.locator('.error-message-class');
  }

  // 3. Ações do Usuário (Métodos que abstraem interações)
  async navigate() {
    await this.page.goto('/login');
  }

  async login(username: string, password: string) {
    await this.usernameInput.fill(username);
    await this.passwordInput.fill(password);
    await this.loginButton.click();
  }

  // 4. Getters (Métodos para obter estado da UI para asserções)
  async getErrorMessage(): Promise<string | null> {
    return this.errorMessage.textContent();
  }
}
```

---

## 2. Template do Arquivo de Teste (Spec)

O arquivo de teste utiliza a Page Class para interagir com a UI de forma abstrata, focando no "o quê" e não no "como".

### Exemplo: `login.spec.ts`

```typescript
import { test, expect } from '@playwright/test';
import { LoginPage } from '../pages/LoginPage';
import { DashboardPage } from '../pages/DashboardPage';

test.describe('Login Scenarios', () => {
  test('should display an error for invalid credentials', async ({ page }) => {
    const loginPage = new LoginPage(page);

    await loginPage.navigate();
    await loginPage.login('invalid-user', 'wrong-password');

    // Asserções são feitas no teste, não no Page Object
    await expect(loginPage.errorMessage).toBeVisible();
    await expect(loginPage.errorMessage).toContainText('Invalid credentials');
  });

  test('should login successfully with valid credentials', async ({ page }) => {
    const loginPage = new LoginPage(page);
    const dashboardPage = new DashboardPage(page);

    await loginPage.navigate();
    await loginPage.login('valid-user', 'correct-password');

    // A asserção verifica o resultado da ação na próxima página
    await expect(page).toHaveURL('/dashboard');
    await expect(dashboardPage.welcomeMessage).toBeVisible();
  });
});
```

---

## Boas Práticas

1.  **Nomenclatura Clara:** Nomes de métodos devem refletir a ação do usuário (ex: `login`, `search`), não o detalhe da implementação (ex: `clickSubmitButton`).
2.  **Sem Asserções no POM:** Page Objects não devem conter `expect` ou `assert`. Eles fornecem o estado da página; os testes fazem as asserções.
3.  **Abstração:** Métodos devem encapsular uma ação completa do usuário. Por exemplo, `login()` preenche ambos os campos e clica no botão.
4.  **Aguarde Ações:** Sempre use `await` para ações assíncronas para garantir que os passos do teste executem na ordem correta.
5.  **Uma Classe por Página/Componente:** Mantenha uma correspondência clara entre as páginas da sua aplicação e as classes do POM. Componentes reutilizáveis (cabeçalho, menu) podem ter suas próprias classes.