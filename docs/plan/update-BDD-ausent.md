# Plano de Atualização de BDDs Ausentes

Este documento detalha o plano para preencher as lacunas de documentação BDD (Behavior-Driven Development) identificadas após auditoria do Front-end e Back-end.

## 🎯 Objetivo
Garantir que todas as funcionalidades críticas implementadas no sistema possuam especificações de comportamento formalizadas, servindo como base para testes automatizados e documentação viva.

## 🔍 Lacunas Identificadas

### 1. 📂 RAG & Knowledge Rooms
- **Componentes:** `KnowledgeRooms.tsx`, `RAGPage.tsx`, `RoomAccessModal.tsx`.
- **Backend:** `KnowledgeRoomController.cs`.
- **Scenarios:** Criação de salas, upload/associação de documentos, gestão de permissões de acesso e busca semântica contextualizada por sala.

### 2. 🖼️ Gerenciamento ONNX (UI)
- **Componentes:** `OnnxGallery.tsx`, `OnnxModelUploadModal.tsx`, `OnnxModelInspectModal.tsx`.
- **Backend:** `OnnxModelController.cs`.
- **Scenarios:** Fluxo completo de upload de modelos, edição de metadados na galeria, visualização de detalhes técnicos (inspeção de tensores) e ativação/desativação de modelos.

### 3. ⚙️ Configurações Avançadas & HotSwap
- **Componentes:** `ConfigAdvancedPage.tsx`, `HotSwapPanel.tsx`.
- **Backend:** `ConfigController.cs`.
- **Scenarios:** Modificação de parâmetros avançados em tempo real, validação do mecanismo de HotSwap (troca de implementações sem restart) e logs de auditoria de configuração.

### 4. 🧠 Insights & Histórico de Execução
- **Componentes:** `SessionInsights.tsx`, `ExecutionHistoryPanel.tsx`.
- **Backend:** `SessionController.cs`, `WorkflowController.cs`.
- **Scenarios:** Exibição de resumos automáticos de chat (insights), navegação no histórico detalhado de execuções de workflows e visualização de logs de erro por nó do workflow.

### 5. 🔐 Fluxo de Autenticação (Interface)
- **Componentes:** `LoginModal.tsx`, `ProtectedRoute.tsx`.
- **Backend:** `AuthController.cs`.
- **Scenarios:** Experiência de login bem-sucedido e falho, proteção de rotas privadas (redirecionamento) e feedback visual de expiração de token JWT.

---

## 🛠️ Plano de Implementação

### Fase 1: Escrita dos Arquivos .feature ✅
- Criar `docs/bdd/knowledge-rooms.feature` ✅
- Criar `docs/bdd/onnx-management-ui.feature` ✅
- Criar `docs/bdd/advanced-config-hotswap.feature` ✅
- Criar `docs/bdd/session-insights.feature` ✅
- Criar `docs/bdd/auth-ui-flow.feature` ✅

### Fase 2: Revisão Técnica ✅
- Validar se os nomes dos endpoints e propriedades JSON nos BDDs correspondem exatamente à implementação atual no C#. ✅
- Garantir que os termos de UI (labels de botões, nomes de páginas) correspondam ao React. ✅

### Fase 3: Integração com Ciclo de Testes ✅
- Identificar cenários críticos para futura automação. ✅

#### 🚀 Mapeamento para Automação (Prioridade Alta)
| Módulo | Cenário Crítico | Ferramenta Sugerida |
| :--- | :--- | :--- |
| **Auth** | Redirecionamento de usuário não autenticado | Cypress (E2E) |
| **RAG** | Ingerir documentos diretamente em uma sala | xUnit (Integration) |
| **ONNX** | Disparar inferência de teste manual | Cypress (E2E) |
| **Config** | Trocar Vector Store Provider via Hot-Swap | xUnit (Integration) |
| **Insights** | Visualizar insights automáticos de uma conversa | xUnit (Unit/Logic) |

---

## ✅ Conclusão
Plano executado com sucesso em 20/05/2024. Todas as lacunas identificadas foram documentadas com BDDs tecnicamente validados.

