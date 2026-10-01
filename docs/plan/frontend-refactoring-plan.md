# Plano de Refatoração do Frontend (React/Next.js)

## Background & Motivação
A auditoria no frontend revelou uma arquitetura mista, onde padrões modernos (Zustand, React 19, Tailwind 4) coexistem com dívidas técnicas significativas. O principal gargalo é o hook `useChat.tsx`, que atua como um "God Hook", centralizando excessiva lógica de estado, conexões SignalR e chamadas de API. Além disso, a subutilização do React Query em favor de `useEffect` manuais prejudica a performance e a experiência de desenvolvimento.

## Escopo & Impacto
As modificações se concentrarão em:
- `src/hooks/` (Decomposição de hooks e adoção de React Query)
- `src/lib/api.ts` (Modularização do módulo de API)
- `src/components/chat/` (Extração de lógica de negócio para hooks)
- `src/index.css` (Padronização de tokens Tailwind 4)

**Impacto:** Risco moderado a alto. `useChat` é o coração da aplicação. Refatorações nesta área exigem testes rigorosos para garantir que a comunicação em tempo real (SignalR) permaneça íntegra.

## Solução Proposta

1. **Decomposição do God Hook (`useChat.tsx`):**
   - Extrair a lógica de **Conectividade SignalR** para um hook dedicado `useSignalR`.
   - Extrair a lógica de **Configuração de LLM** (providers, models) para um hook `useLLMConfiguration` ou um Zustand store específico.
   - Extrair a lógica de **Gestão de Histórico e Mensagens** para `useChatMessages`.
   - Manter o `ChatProvider` apenas como um orquestrador leve ou migrar o estado compartilhado para Zustand para evitar re-renders massivos do Context.

2. **Padronização de Data Fetching (React Query):**
   - Migrar `useAgents.ts`, `useSessions.ts` e `useSettings.ts` para `@tanstack/react-query`.
   - Eliminar `useState` e `useEffect` manuais para controle de `loading`/`error`.
   - Aproveitar cache e invalidação automática (ex: invalidar `agents` após um `createAgent`).

3. **Modularização da API (`lib/api.ts`):**
   - Dividir o arquivo `api.ts` em módulos menores: `agent-api.ts`, `session-api.ts`, `llm-api.ts`, etc.
   - Centralizar a configuração do cliente HTTP (Axios ou Fetch) em um único lugar.

4. **Adoção de Padrões React 19:**
   - Substituir formulários baseados em `onSubmit` por **React Actions**.
   - Utilizar `useActionState` para gerenciar estados de pendência e erros de formulários (ex: criação de agentes).
   - Explorar o hook `use` para resolução de promessas em componentes de UI.

5. **Limpeza e Design (Tailwind 4 & UI):**
   - Substituir cores hexadecimais _hardcoded_ por variáveis definidas no bloco `@theme` do CSS.
   - Refatorar `MessageBubble.tsx` e componentes de lista para usar `React.memo` onde a renderização é frequente e custosa.

## Implementação em Fases

### Fase 1: Padronização e Limpeza (Risco Baixo)
- Decompor `lib/api.ts` em módulos específicos.
- Atualizar `index.css` para usar exclusivamente variáveis de tema do Tailwind 4.
- Migrar hooks simples (`useAgents`, `useSettings`) para React Query.

### Fase 2: Refatoração de Componentes e React 19 (Risco Médio)
- Implementar React Actions nos formulários de configuração.
- Aplicar memoização em componentes críticos da thread de chat.
- Extrair lógica de SignalR do `WorkflowExecutionCard.tsx` para hooks.

### Fase 3: Decomposição do useChat (Risco Alto)
- Criar `useSignalR` e `useChatMessages`.
- Refatorar o `ChatProvider` para consumir esses hooks granulares.
- Validar a sincronização de mensagens entre canais após a separação.

## Verificação e Testes
- Executar linting e verificação de tipos (`npm run lint`, `tsc`).
- Realizar testes manuais de fluxo de chat (envio, recebimento, streaming).
- Validar se o histórico de sessões continua carregando corretamente após a migração para React Query.
- Utilizar as ferramentas de dev do React para monitorar re-renders desnecessários no Context de Chat.
