# Plano de Implementação: Autenticação com Supabase & Multi-Tenant

## 1. Objetivo
Implementar o fluxo de autenticação no frontend (React + Vite) utilizando o Supabase como provedor de identidade, Zustand para gerenciamento de estado global e TanStack Router para proteção de rotas, integrando com a estratégia de isolamento multi-tenant.

---

## 2. Pilares da Arquitetura

### Pilar 1: Cliente Supabase (`frontend/src/lib/supabase.ts`)
*   Inicialização única usando `import.meta.env.VITE_SUPABASE_URL` e `VITE_SUPABASE_ANON_KEY`.
*   Gerenciamento automático de JWT no LocalStorage.

### Pilar 2: Estado Global (`frontend/src/store/authStore.ts`)
*   Uso de Zustand.
*   **Estado**: `user`, `isAuthenticated`, `isLoading`.
*   **Ações**: `login`, `logout`, `checkAuth`.

### Pilar 3: Roteamento e Proteção (`frontend/src/router.tsx`)
*   `AuthInitializer`: Executa `checkAuth` no carregamento e configura o listener `onAuthStateChange`.
*   `AuthGuard`: Protege rotas privadas e exibe indicador de carregamento em tela cheia durante `isLoading`.

---

## 3. Tratamento de Casos de Borda & Decisões

*   **Falha na Renovação do Token**: Forçar logout e redirecionar para `/login`. O listener `onAuthStateChange` (evento `SIGNED_OUT`) garantirá essa reação.
*   **Prevenção de "Flash" de Conteúdo**: Um componente de carregamento em tela cheia será exibido pelo `AuthInitializer` enquanto `isLoading` for true, impedindo a renderização de rotas filhas.
*   **Isolamento Multi-Tenant**: O `tenant_id` será armazenado no `app_metadata` do usuário no Supabase. Isso garante que o ID esteja no JWT e permita o uso de Row Level Security (RLS) no PostgreSQL do Supabase.

---

## 4. Tarefas de Implementação

### Task 1: Setup do Cliente Supabase
- [ ] Criar `frontend/src/lib/supabase.ts`.
- [ ] Configurar variáveis de ambiente no `.env`.

### Task 2: Store de Autenticação (Zustand)
- [ ] Criar `frontend/src/store/authStore.ts`.
- [ ] Implementar métodos `login`, `logout` e `checkAuth`.

### Task 3: Inicializador e Guarda de Rotas
- [ ] Criar componente `AuthInitializer` com listener de estado.
- [ ] Criar componente `AuthGuard` para envolver rotas protegidas.
- [ ] Implementar tela de carregamento (Spinner/Logo) para o estado `isLoading`.

### Task 4: Integração com o Backend (Opcional/Futuro)
- [ ] Garantir que o backend ASP.NET valide o JWT do Supabase e extraia o `tenant_id` do `app_metadata`.
