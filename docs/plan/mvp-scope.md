# Plano de Escopo: MVP - Segundo Cérebro & Automatizador

## 1. Objetivo
Definir o escopo do Mínimo Produto Viável (MVP) do sistema, focado em ser um "Segundo Cérebro" pessoal e assistente de automação. O diferencial central é a capacidade de interagir e criar recursos (como workflows) diretamente através do chat, utilizando PDFs como principal fonte de conhecimento.

---

## 2. Escopo do MVP (Must Have)

### A. Hub Conversacional (Interface Central)
*   **Chat em Tempo Real**: Interface de chat fluida com suporte a streaming de respostas (via SignalR).
*   **Persistência de Sessão**: Histórico básico de mensagens persistido no backend para sobrevivência a recarregamentos de página.
*   **Criação via Chat (Prompt-to-Resource)**: Capacidade do Meta-Agente de interpretar comandos em linguagem natural para gerar estruturas de workflows ou conhecimento.
    *   *Exemplo*: "Crie um fluxo que leia o PDF e me avise se houver pendências." -> O agente gera a estrutura no store.

### B. Segundo Cérebro (Gestão de Conhecimento)
*   **Ingestão de PDF**: Upload manual de arquivos PDF na interface.
*   **RAG (Retrieval-Augmented Generation)**: Busca semântica sobre os PDFs ingeridos para responder perguntas com base no contexto do documento.

### C. Automatizador (Workflows)
*   **Visualização de Fluxos**: Renderização visual dos workflows criados via chat usando `@xyflow/react` (React Flow).
*   **Execução Linear**: Capacidade de executar sequências simples de passos geradas pelo chat.

### D. Segurança & Acesso
*   **Autenticação Supabase**: Login/Logout seguro.
*   **Isolamento de Dados**: Cada usuário acessa apenas seus próprios documentos e workflows.

---

## 3. Fora do Escopo do MVP (Fases Futuras)
*   **Memória de Longo Prazo Avançada**: Extração de fatos e sumarização automática de sessões passadas (planejada em `session-chat-history-memory.md` como evolução).
*   **Edição Visual Completa**: Arrastar e soltar nós para criar fluxos do zero (foco inicial é a criação via chat).
*   **Extração Multimodal**: Leitura de imagens e gráficos complexos em PDFs.
*   **Arena Mode**: Comparação lado a lado de modelos de IA.

---

## 4. Próximos Passos Priorizados
1.  **Implementar Autenticação Supabase** (Conforme `plan/supabase-login.md`).
2.  **Implementar Persistência de Sessão no Backend** (Conforme `plan/session-chat-history-memory.md` - Feature 1).
3.  **Refinar a Criação de Workflows via Chat** (Integrando o chat com o `useWorkflowStore.ts`).
