# Plano de Ação: Próximos Passos e Manutenção do Frontend

O Frontend está modernizado, mas requer ajustes finos para garantir a sustentabilidade das refatorações de React 19.

## 1. Consolidação de Estado (Zustand)
**Objetivo:** Finalizar a transição do estado local para o Store global onde necessário.

- [ ] **Chat Store:** Migrar os estados restantes de `useChatState.ts` que são úteis para outros componentes (ex: lista de sessões ativa) para o `chatStore.ts`.
- [ ] **Persistência Local:** Configurar o middleware `persist` do Zustand para manter preferências de UI do usuário (ex: tema, largura da sidebar).

## 2. Padronização de Componentes
**Objetivo:** Garantir que 100% dos formulários sigam o novo padrão de performance.

- [ ] **Revisão de Modais:** Verificar se `OnnxModelUploadModal.tsx` e `LoginModal.tsx` podem se beneficiar do `useActionState` para reduzir estados manuais de `loading`.
- [ ] **Tailwind 4 Refinement:** Revisar o uso de cores hexadecimais restantes em arquivos `.tsx` e substituir por variáveis do tema (`text-zinc-400`, `bg-teal-500/10`).

## 3. DX e Tipagem
**Objetivo:** Melhorar a experiência do desenvolvedor e segurança de tipos.

- [ ] **Tipos de API:** Gerar ou atualizar manualmente as interfaces em `types/api.ts` para refletir as mudanças recentes no backend (novas rotas de agentes).
- [ ] **Componentes de UI:** Extrair componentes pequenos e repetitivos de `AgentsPage.tsx` para arquivos individuais em `src/components/ui/`.

## Critérios de Aceite
- Build do Vite concluído sem avisos de tipos.
- Zero uso de `useState` para controle de "saving" em formulários complexos (usando Actions).
- Lighthouse Score para Performance > 90.
