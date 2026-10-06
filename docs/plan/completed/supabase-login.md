# Plano de Implementação: Autenticação com Supabase & Multi-Tenant

> **Status:** ✅ CONCLUÍDO (com variações de nomenclatura)
> **Revisado:** 2026-05-20

## 1. Objetivo
Implementar o fluxo de autenticação no frontend (React + Vite) utilizando o Supabase como provedor de identidade, Zustand para gerenciamento de estado global e React Router para proteção de rotas, integrando com a estratégia de isolamento multi-tenant.

---

## 2. Pilares da Arquitetura

### Pilar 1: Cliente Supabase (`frontend/src/lib/supabase.ts`)
*   Inicialização única usando `import.meta.env.VITE_SUPABASE_URL` e `VITE_SUPABASE_ANON_KEY`.
*   Gerenciamento automático de JWT no LocalStorage.

### Pilar 2: Estado Global (`frontend/src/store/authStore.ts`)
*   Uso de Zustand.
*   **Estado**: `user`, `token`, `apiKey`, `isAuthenticated`, `isLoading`.
*   **Ações**: `login` (Google OAuth), `logout`, `checkAuth`, `loginWithToken`, `loginWithApiKey`.
*   **Listener**: `supabase.auth.onAuthStateChange` configurado na store (linha 86).

### Pilar 3: Roteamento e Proteção
*   **`ProtectedRoute`** (`frontend/src/components/auth/ProtectedRoute.tsx`): Protege rotas privadas usando `<Outlet />` do React Router.
    *   Exibe `<PageLoading />` durante `isLoading`.
    *   Exibe `<LoginModal />` quando não autenticado.
*   **Inicialização**: `useEffect` no `App.tsx` chama `checkAuth()` no mount (não existe componente `AuthInitializer` separado).

---

## 3. Divergências do Plano Original

| Planejado | Implementado | Motivo |
|-----------|-------------|--------|
| `AuthGuard` | `ProtectedRoute` | Mesmo padrão, nome diferente |
| `AuthInitializer` | `useEffect` no `App.tsx` | Simples o suficiente para não precisar de componente separado |
| Fullscreen loading | `<PageLoading />` no `ProtectedRoute` | Mesmo efeito, componente compartilhado |

---

## 4. Backend

### Validação JWT Supabase
*   `SupabaseAuthExtensions.cs`: Configura `AddJwtBearer("Supabase")` com validação HS256.
*   `TenantMiddleware.cs`: Extrai `tenant_id` do claim `app_metadata` via JSON parsing.
*   `Program.cs`: `AddPolicyScheme("MultiAuth")` inclui Supabase como esquema de autenticação.

---

## 5. Tarefas de Implementação

### Task 1: Setup do Cliente Supabase
- [x] Criar `frontend/src/lib/supabase.ts`.
- [x] Configurar variáveis de ambiente no `.env`.

### Task 2: Store de Autenticação (Zustand)
- [x] Criar `frontend/src/store/authStore.ts`.
- [x] Implementar métodos `login`, `logout`, `checkAuth`.
- [x] Configurar listener `onAuthStateChange`.

### Task 3: Proteção de Rotas
- [x] Criar componente `ProtectedRoute` com loading e login modal.
- [x] Inicializar auth via `useEffect` no `App.tsx`.

### Task 4: Integração com o Backend
- [x] Backend valida JWT Supabase e extrai `tenant_id` do `app_metadata`.
- [x] MultiAuth policy scheme inclui Supabase.
