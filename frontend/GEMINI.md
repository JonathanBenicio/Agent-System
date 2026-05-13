# GEMINI.md - Frontend Context

Este arquivo define as regras e padrões específicos para o desenvolvimento do Frontend neste projeto.

---

## 🚀 Stack Tecnológica (Conductor Aligned)
- **Framework / Build Tool**: Vite 8.x
- **Library**: React 19.x
- **Language**: TypeScript 6.x (Strict Mode)
- **Styling**: Tailwind CSS 4.x
- **Icons**: Lucide React
- **Real-time**: SignalR Client (Streaming & SSE)
- **State Management**: Zustand / Context API
- **Data Fetching**: TanStack Query

---

## 🎨 Design System & UI Patterns
- **Paleta**: Definida via variáveis de CSS e classes do Tailwind v4.
- **Dark Mode**: Priorizado e suportado nativamente conforme as diretrizes do produto.
- **Componentes Reutilizáveis**:
  - Priorize o uso de componentes atômicos, acessíveis e modulares.
  - Siga a estrutura de componentes em `src/components/`.

---

## 🏗️ Arquitetura do Frontend (SPA)

### Estrutura de Diretórios
- `src/components/`: Componentes React reutilizáveis e modulares (ex: `agents/`, `common/`).
- `src/hooks/`: Custom hooks para encapsulamento de estado e chamadas real-time.
- `src/lib/`: Lógica de cliente, clientes SignalR/SSE, utilitários.
- `src/store/`: Gerenciamento de estado global.
- `src/types/`: Definições globais de TypeScript.

### API & Real-time (SignalR / SSE)
- O frontend interage com o Runtime de Agentes via endpoints REST e conexões SignalR/SSE para streaming de execução.
- Trate desconexões, reconexões e estados de carregamento de forma robusta e reativa.

---

## 📏 Convenções de Código

### Naming
- **Components**: PascalCase (ex: `AgentFormModal.tsx`).
- **Hooks/Stores**: camelCase com prefixo `use` (ex: `useAgentStore.ts`).
- **Types/Interfaces**: PascalCase.
- **Files**: kebab-case ou PascalCase para componentes.

### Import Organization
1. React core e hooks.
2. Bibliotecas de terceiros (Lucide, Zustand).
3. Imports internos de componentes, hooks e tipos.

---

## ✅ Qualidade & Testes
- **Linting**: O código deve passar em todas as verificações de lint configuradas no Vite/ESLint.
- **Typing**: Zero tolerância para `any`. Use tipos explícitos de TypeScript 6.
- **Testing**: Testes unitários limpos focados no comportamento dos componentes.

---

## 🤖 Instruções para o Agente
Ao trabalhar no diretório de frontend:
1. Sempre consulte este arquivo e siga estritamente a stack de **React 19 + Vite 8** (Não utilize Next.js ou App Router).
2. Siga os padrões de design e componentes atômicos já existentes no projeto.
3. Valide as alterações garantindo que o build do Vite e a verificação de tipos passem sem erros.