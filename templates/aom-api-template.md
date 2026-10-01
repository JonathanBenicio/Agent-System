# Template: API Object Model (AOM) para Testes de API

> **Objetivo:** Padronizar a criação de clientes de API para testes automatizados, encapsulando URLs, endpoints, métodos HTTP, headers e formatação de payloads.
> **Framework Sugerido:** [Playwright | RestSharp | Axios | requests]
> **Linguagem Sugerida:** [TypeScript | C# | Python]

---

## 1. Template da Classe Client (O Modelo)

A classe Client centraliza as chamadas para um domínio ou microsserviço específico da API.

### Exemplo: `UserClient.ts`

```typescript
import { type APIRequestContext, type APIResponse } from '@playwright/test';

export class UserClient {
  readonly request: APIRequestContext;
  readonly basePath: string;

  constructor(request: APIRequestContext) {
    this.request = request;
    this.basePath = '/api/users'; // Base path para este domínio
  }

  // Método encapsulado para criação (POST)
  async createUser(payload: object, headers?: object): Promise<APIResponse> {
    return await this.request.post(this.basePath, {
      data: payload,
      headers: headers,
    });
  }

  // Método encapsulado para busca (GET)
  async getUser(userId: string | number): Promise<APIResponse> {
    return await this.request.get(`${this.basePath}/${userId}`);
  }
}
```

---

## 2. Template de Uso no Teste (Spec)

O arquivo de teste consome o Client e foca apenas nas asserções (regras de negócio, status code e validações de contrato).

### Exemplo: `user.api.spec.ts`

```typescript
import { test, expect } from '@playwright/test';
import { UserClient } from '../../api/UserClient';

test('Deve criar um usuário com sucesso', async ({ request }) => {
  const userClient = new UserClient(request);
  const response = await userClient.createUser({ name: 'John Doe', role: 'Admin' });
  
  // Asserções
  expect(response.status()).toBe(201);
  const data = await response.json();
  expect(data.name).toBe('John Doe');
});
```