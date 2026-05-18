# Template: Estrutura de Pastas para Playwright (E2E e API)

> **Objetivo:** Padronizar a arquitetura do projeto de testes automatizados com Playwright, acomodando de forma escalável tanto testes de interface (E2E) quanto testes de serviços (API).
> **Padrões Adotados:** Page Object Model (POM), API Clients (AOM - API Object Model), Fixtures customizadas.

---

## Estrutura de Diretórios Recomendada

```text
/qa-automation
  ├── /api                  # API Object Model (Clients, Request Builders)
  │   ├── AuthClient.ts
  │   └── UserClient.ts
  │
  ├── /pages                # Page Object Model (POM) para UI
  │   ├── components/       # Componentes reutilizáveis (Menus, Modais)
  │   ├── LoginPage.ts
  │   └── DashboardPage.ts
  │
  ├── /tests                # Arquivos de Teste (Specs)
  │   ├── /api              # Testes focados puramente em API
  │   │   ├── auth.api.spec.ts
  │   │   └── user.api.spec.ts
  │   └── /e2e              # Testes focados em fluxo de interface
  │       ├── login.e2e.spec.ts
  │       └── checkout.e2e.spec.ts
  │
  ├── /fixtures             # Fixtures customizadas do Playwright
  │   ├── apiFixtures.ts    # Injeção de dependência para clients de API
  │   ├── pageFixtures.ts   # Injeção de dependência para pages de E2E
  │   └── index.ts          # Arquivo central para exportar o `test` customizado
  │
  ├── /data                 # Massa de dados estática (Mocks, JSONs, CSVs)
  │   ├── users.json
  │   └── responses/        # Respostas mockadas para interceptação de rede
  │
  ├── /utils                # Funções utilitárias (Helpers genéricos)
  │   ├── dataGenerator.ts  # Geradores de dados (Faker)
  │   └── logger.ts
  │
  ├── /config               # Configurações de Ambiente
  │   ├── .env.dev
  │   ├── .env.qa
  │   └── envConfig.ts      # Validação tipada de variáveis de ambiente
  │
  ├── playwright.config.ts  # Configuração central do Playwright
  ├── package.json
  └── tsconfig.json
```

---

## Responsabilidade de cada Camada

### 1. `/tests/api` e `/tests/e2e`
A separação dentro da pasta `tests` permite que você rode suítes de forma independente. 
- **Testes de API** costumam ser muito mais rápidos e menos suscetíveis a *flakiness*. Eles devem validar contratos, status codes e regras de negócio no backend.
- **Testes E2E** focam na renderização do frontend, integrações e jornada completa do usuário.

### 2. `/api` (API Object Model)
Assim como o POM abstrai a interface, o AOM abstrai as chamadas HTTP. Em vez de escrever `request.post('/users')` nos testes, você cria uma classe `UserClient` com o método `createUser()`.

### 3. `/pages` (Page Object Model)
Armazena as classes que representam as páginas da aplicação e seus locators.

### 4. `/fixtures`
O Playwright brilha no uso de **Fixtures**. Aqui você cria um `test` estendido que já inicializa e injeta as Pages e os API Clients nas suas specs, dispensando o uso de `new LoginPage(page)` nos testes.

---

## Exemplo de Configuração: `playwright.config.ts`

Para tirar proveito dessa estrutura, o arquivo de configuração deve definir `projects` separados para API e E2E, permitindo execução paralela ou seletiva via CI/CD.

```typescript
import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir: './tests',
  projects: [
    // Projeto de Testes de API
    {
      name: 'API Tests',
      testMatch: /.*\.api\.spec\.ts/,
    },
    // Projeto de Testes E2E (Chromium)
    {
      name: 'E2E Chromium',
      testMatch: /.*\.e2e\.spec\.ts/,
      use: { ...devices['Desktop Chrome'] },
    }
  ],
});
```