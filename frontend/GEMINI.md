# GEMINI.md - Frontend Context

Este arquivo define as regras e padrões específicos para o desenvolvimento do Frontend neste projeto.

---

## 🚀 Stack Tecnológica
- **Framework**: Next.js (App Router preferencialmente)
- **Library**: React
- **Language**: TypeScript (Strict Mode)
- **Styling**: Tailwind CSS
- **State Management**: Zustand / Context API
- **Data Fetching**: TanStack Query / SWR

---

## 🎨 Design System & UI Patterns
- **Paleta**: Definida via variáveis de CSS ou classes do Tailwind.
- **Dark Mode**: Deve ser suportado conforme a configuração do projeto.
- **Componentes Reutilizáveis**:
  - Priorize o uso de componentes atômicos e padrões de design estabelecidos.
  - Siga a estrutura de componentes em `src/components/`.

---

## 🏗️ Arquitetura do Frontend

### Diretórios Sugeridos
- `src/app/` ou `src/pages/`: Roteamento.
- `src/components/`: Componentes React reutilizáveis.
- `src/hooks/`: Custom hooks.
- `src/lib/`: Lógica de negócio, mappers, utilitários.
- `src/store/`: Gerenciamento de estado.
- `src/types/`: Definições globais de TypeScript.

### API & Data Fetching
- Centralize chamadas de API em serviços ou hooks dedicados.
- Trate estados de carregamento e erro de forma consistente.

---

## 📏 Convenções de Código

### Naming
- **Components**: PascalCase.
- **Hooks/Stores**: camelCase com prefixo `use`.
- **Types/Interfaces**: PascalCase.
- **Files**: kebab-case.

### Import Organization
1. React/Next.js core.
2. Bibliotecas de terceiros.
3. Imports internos (usando alias `@/` se configurado).

---

## ✅ Qualidade & Testes
- **Linting**: O código deve passar em todas as verificações de lint.
- **Typing**: Evite o uso de `any`. Use interfaces e tipos explícitos.
- **Testing**: Implemente testes unitários e de integração conforme necessário.

---

## 🤖 Instruções para o Agente
Ao trabalhar no diretório de frontend:
1. Sempre verifique este arquivo e os arquivos de configuração local.
2. Siga os padrões de design e componentes já existentes no projeto.
3. Valide as alterações com comandos de verificação de tipos e lint.