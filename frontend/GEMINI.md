# GEMINI.md - Frontend Context (Tabatine)

Este arquivo define as regras e padrões específicos para o desenvolvimento do Frontend (Next.js) no projeto Tabatine.

---

## 🚀 Stack Tecnológica
- **Next.js 16.1.6** (App Router)
- **React 19**
- **TypeScript** (Strict Mode)
- **Tailwind CSS 4**
- **Zustand** (State Management)
- **TanStack Query** (Data Fetching)
- **Lucide React** (Icons)

---

## 🎨 Design System & UI Patterns
- **Paleta**: Tailwind CSS (principalmente `zinc` para neutros).
- **Dark Mode**: Fundo escuro obrigatório (`bg-black` ou `bg-zinc-950`).
- **Bordas**: `border-zinc-800/50` para separação no dark mode.
- **Componentes Reutilizáveis**:
  - `TableContainer`: Wrapper principal para tabelas.
  - `TableSearch`: Input de busca padronizado.
  - `TableSummaryCard`: Cards de estatísticas.
  - `PageHeader`: Cabeçalho de página com breadcrumbs.
  - `SectionCard`: Agrupamento de informações com ícone.
  - `StatCard`: Exibição de métricas.
- **Cores de Status**:
  - Sucesso: `text-emerald-400`.
  - Alerta: `text-amber-400`.
  - Erro: `text-rose-400`.
  - Info: `text-blue-400`.

---

## 🏗️ Arquitetura do Frontend

### Diretórios Principais
- `src/app/`: App Router (Pages & API Routes).
- `src/components/`: Componentes React reutilizáveis.
- `src/hooks/`: Custom hooks (TanStack Query wrappers).
- `src/lib/`: Lógica de negócio, mappers, parsers.
- `src/store/`: Stores do Zustand (`use*Store.ts`).
- `src/types/`: Definições globais de TypeScript.

### Data Fetching (TanStack Query)
- Hooks em `src/hooks/use*Query.ts`.
- Query Keys: `['resource', page, search, filters]`.
- Use `placeholderData: (previousData) => previousData` para paginação suave.

### State Management (Zustand)
- Stores em `src/store/use*Store.ts`.
- Interface de estado explícita.
- Gerenciar `loading` e `error` dentro da store.

### API Routes & Integração Omie
- Proxy Omie: `/api/omie/[resource]/route.ts`.
- Banco (Supabase): `/api/supabase/[resource]/route.ts`.
- Webhooks: `/api/webhooks/[provider]/route.ts`.
- **CRÍTICO**: Chamadas ao banco/Supabase APENAS no lado do servidor. Proibido usar client SDK em `'use client'`.

---

## 📏 Convenções de Código

### Naming
- **Components**: PascalCase (`LayoutWrapper.tsx`).
- **Hooks/Stores**: camelCase com prefixo `use` (`useNfStore.ts`).
- **Types/Interfaces**: PascalCase.
- **Files**: kebab-case (`nf-mapper.ts`).

### Import Organization
1. React/Next.js core.
2. Bibliotecas de terceiros.
3. Imports internos (usando alias `@/`).

---

## ✅ Qualidade & Testes
- **Zero Tolerance**: `npm run lint` deve retornar 0 erros/avisos.
- **Typing**: `any` é proibido. Use interfaces ou `Record<string, unknown>`.
- **Unit Tests**: `*.test.ts` usando `node:test`.
- **E2E Tests**: `*.spec.ts` usando Playwright.
- **Validação**: Use **Zod** para todas as novas rotas de API e formulários.

---

## 🤖 Instruções para o Agente
Ao trabalhar no diretório `frontend/`:
1. Sempre verifique este arquivo.
2. Siga os padrões de componentes em `.agents/rules/front.md` (se ainda existir) ou conforme definido aqui.
3. Use `npx tsc --noEmit` para validar tipos antes de finalizar.
