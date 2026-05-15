# GEMINI.md - Frontend Context & Governance

Este arquivo define as regras canônicas, arquitetura e padrões específicos para o desenvolvimento do Frontend neste monorepo, subordinado à governança mestre do diretório `conductor/`.

---

## 🚀 Stack Tecnológica Deliberada (`conductor/tech-stack.md`)
- **Build Tool / Bundler**: Vite 8.x (SPA)
- **Library**: React 19.x
- **Language**: TypeScript 6.x (Strict Mode, zero `any`)
- **Styling**: Tailwind CSS 4.x (Design Token Architecture)
- **Icons**: Lucide React
- **Real-time Engine**: SignalR Client (Streaming & SSE via `@microsoft/signalr`)
- **State Management**: Zustand (Stores atômicos e imutáveis)
- **Data Fetching**: TanStack Query / Fetch API nativa

> [!IMPORTANT]
> **PROIBIÇÃO EXPRESSA**: O uso de Next.js, App Router ou Server Components é terminantemente vedado neste ecossistema frontend. A aplicação é estritamente uma Single Page Application (SPA) empacotada pelo Vite 8.

---

## 🏗️ Arquitetura e Estrutura de Diretórios (SPA)

```markdown
src/
├── components/          # Componentes visuais atômicos e acessíveis (PascalCase.tsx)
├── hooks/               # Custom hooks de encapsulamento lógico e reatividade (useFeature.ts)
├── lib/                 # Utilitários, cn() (clsx + twMerge), singletons SignalR/SSE
├── store/               # Zustand stores isolados por domínio funcional
└── types/               # Definições globais de TypeScript 6
```

### 🔌 Diretrizes de Real-time e Estado Global
1. **SignalR Singletons**: As conexões aos hubs (`/hubs/chat`, `/hubs/gateway`) devem operar como singletons em `lib/signalr.ts` com auto-reconnect configurado e tratamento robusto de queda de conexão.
2. **Zustand Immutability**: Atualizações de estado global devem ser imutáveis, granulares e sem sobreposição de re-renderizações desnecessárias.

---

## 🎨 Design System & UI Patterns
- **Paleta**: Definida via variáveis HSL no `index.css` e classes utilitárias do Tailwind v4.
- **Dark Mode**: Padrão nativo do produto (`zinc-850`, `zinc-925`).
- **Composição de Classes**: Utilize o utilitário `cn()` para mesclar condicionalmente classes Tailwind sem conflitos.

---

## 📏 Convenções de Código
- **Components**: PascalCase (ex: `AgentFormModal.tsx`).
- **Hooks/Stores**: camelCase com prefixo `use` (ex: `useAgentStore.ts`).
- **Types/Interfaces**: PascalCase.

---

## ✅ Qualidade & Testes (AAA Pattern)
- **Linting & Typing**: Zero tolerância para erros no `tsc --noEmit` e avisos do ESLint.
- **Testing**: Cobertura via Cypress (E2E) e testes de componentes.