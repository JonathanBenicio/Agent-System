# Plano de Implementação: Frontend SPA & Autenticação Simples

## 🎯 Objetivo
Implementar um sistema de autenticação robusto e reativo no Frontend (SPA) do AgenticSystem, integrando Zustand, TanStack Query, interceptores de HTTP e SignalR, além de uma interface visual premium com Glassmorphism (StitchMCP).

---

## 🏗️ Decisões Arquiteturais Validadas pelo Usuário
- **Gerenciamento de Estado de Auth**: Zustand (`useAuthStore`) com persistência via `lib/auth.ts`.
- **Camada de Transporte HTTP**: Fetch com interceptor de erro 401 para logout automático.
- **Sincronização SignalR**: Hook `useSignalRAuth` gerenciando conexão e desconexão baseado no estado de autenticação.
- **Interface Visual**: Estética Glassmorphism escura com toques de Teal e Cyan (StitchMCP), respeitando o *Purple Ban*.

---

## 🚀 Fases da Implementação

### Fase 1: Fundação de Autenticação & Estado (P0)
- [x] **Task 1.1**: Criar `src/store/authStore.ts` com Zustand, expondo `loginWithToken`, `loginWithApiKey` e `logout`.
- [x] **Task 1.2**: Adicionar interceptor 401 no client HTTP (`src/lib/api.ts`) integrado ao Zustand.
- [x] **Task 1.3**: Envelopar a aplicação no `App.tsx` com `QueryClientProvider` (TanStack Query).
- [x] **Task 1.4**: Criar `useSignalRAuth.ts` para conectar/desconectar os Hubs de Chat e Gateway sob demanda.

### Fase 2: UI Premium & Roteamento Protegido (P1)
- [x] **Task 2.1**: Criar modal premium `LoginModal.tsx` com Glassmorphism (`bg-zinc-900/90 backdrop-blur-md`), alternância de abas (JWT vs API Key), tratamento visual de erros e botões brilhantes.
- [x] **Task 2.2**: Criar `ProtectedRoute.tsx` para interceptar acessos não autenticados e acionar o `LoginModal`.
- [x] **Task 2.3**: Refatorar `App.tsx` para aplicar o roteamento protegido.
- [x] **Task 2.4**: Inserir indicador visual de usuário ativo (JWT Bearer vs API Key) e botão de Logout no rodapé de `Sidebar.tsx`.

---

## 🧪 Verificação & Qualidade
- [x] Build TypeScript sem erros (`tsc -b`).
- [x] Bundle de produção Vite / Rolldown construído com sucesso.
