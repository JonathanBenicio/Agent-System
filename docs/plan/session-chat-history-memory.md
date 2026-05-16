# Project Plan: Session Chat History & Long-Term Memory

## 1. Background & Motivation

O sistema atual processa mensagens de chat em memória ou via chamadas *stateless* no `ChatHub`. Para melhorar a experiência do usuário (UX), garantindo que o histórico sobreviva a recarregamentos de página, e para aumentar a inteligência do agente (permitindo que ele lembre de interações passadas), precisamos de uma estratégia de persistência no backend.

Este plano aborda os dois casos solicitados pelo usuário:
1. **Histórico de Chat da Sessão (Foco em UX)**
2. **Memória de Longo Prazo do Agente (Foco em Capacidade)**

---

## 2. Scope & Impact

**Escopo:**
*   Modelagem e persistência de sessões e mensagens de chat no PostgreSQL.
*   Integração do `ChatHub` (SignalR) com o armazenamento persistente.
*   Estratégia de extração de fatos/resumos para memória de longo prazo.
*   Recuperação de memórias relevantes via busca vetorial (pgvector).

**Impacto:**
*   **UX Fluida:** Usuários não perdem o contexto ao atualizar a página ou alternar de dispositivo.
*   **Agente Inteligente:** O agente se torna mais personalizado e capaz de manter continuidade entre conversas.

---

## 3. Proposed Features & Architecture Changes

### Feature 1: Persistência de Sessão e Mensagens (Caso 1)
**Objetivo:** Garantir que o chat não seja perdido e seja acessível.

*   **Estratégia de Alto Nível:**
    *   Criar tabelas no PostgreSQL para `ChatSessions` e `ChatMessages`.
    *   Vincular mensagens a uma sessão específica.
    *   Carregar o histórico ao abrir uma sessão existente.

*   **Modelagem Sugerida (Backend):**
    *   `ChatSession`: `Id` (Guid), `Title` (String), `CreatedAt` (DateTime), `UpdatedAt` (DateTime), `UserId`/`TenantId`.
    *   `ChatMessage`: `Id` (Guid), `SessionId` (Guid, FK), `Role` (User/Assistant/System), `Content` (String), `CreatedAt` (DateTime), `Metadata` (JSON para tokens, modelo usado, etc.).

*   **Fluxo:**
    1. O usuário abre o chat -> Cria ou recupera uma `ChatSession`.
    2. O usuário envia mensagem -> Salva no banco e transmite via SignalR.
    3. O agente responde -> Salva no banco e transmite via SignalR.

### Feature 2: Memória de Longo Prazo e Sumarização (Caso 2)
**Objetivo:** Permitir que o agente lembre de fatos importantes de conversas passadas.

*   **Estratégia de Alto Nível:**
    *   **Extração Assíncrona:** Ao final de uma sessão (ou periodicamente), um serviço em background processa a conversa com um prompt específico para extrair "fatos" ou "preferências" (ex: "O usuário prefere código em Python", "O usuário está trabalhando no projeto X").
    *   **Armazenamento Híbrido:**
        *   **Sumarização:** Guardar um resumo do chat na tabela de sessões.
        *   **Fatos Vetorizados:** Guardar os fatos extraídos como pequenos documentos no banco de vetores (pgvector), associados ao usuário/inquilino.
    *   **Recuperação (RAG-Style):** No início de uma nova conversa, o sistema busca no banco de vetores por "memórias" relevantes ao prompt atual do usuário e as injeta no prompt do sistema como contexto.

---

## 4. Prioritization Matrix

| Feature | Esforço | Impacto | Prioridade |
| :--- | :--- | :--- | :--- |
| **Feature 1: Persistência de Sessão (UX)** | Médio | Alto | Alta (Base) |
| **Feature 2: Memória de Longo Prazo (RAG)** | Alto | Alto | Média (Evolução) |

---

## 5. Next Steps

1. **Aprovação da Estratégia**: Confirmar se a abordagem de banco relacional para histórico e vetorial para memória atende às expectativas.
2. **Modelagem**: Criar as entidades de domínio e as migrações do EF Core para o PostgreSQL.
3. **Refatoração do ChatHub**: Atualizar o hub para persistir as mensagens em tempo real.
4. **Implementação do Extrator de Memória**: Criar o serviço que analisa conversas passadas.
