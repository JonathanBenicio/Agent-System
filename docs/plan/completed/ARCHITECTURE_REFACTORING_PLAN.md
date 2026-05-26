# Plano de Ação e Refatoração Arquitetural

Este documento detalha os pontos críticos de Overengineering, Código Morto e Gaps Arquiteturais identificados no sistema (Backend e Frontend), juntamente com o plano de ação para mitigar cada problema, visando melhorar a manutenibilidade, segurança e escalabilidade.

## 1. Backend (C# / Clean Architecture)

### 1.1. Overengineering & Código Morto (Interfaces Especulativas)
* **Localização:** `src/AgenticSystem.Core/Interfaces/IMaturityServices.cs`
* **Descrição do Problema:** O arquivo contém diversas interfaces focadas em níveis de maturidade (ex: `IQueryCompressor`, `ISemanticCompressor`, `IUserPreferenceEngine`) que não possuem implementações reais no fluxo principal do sistema.
* **Impacto:** Complexidade desnecessária e código órfão (YAGNI), o que adiciona ruído cognitivo.
* **Plano de Ação:**
  - [ ] Realizar uma busca em todo o repositório por referências a essas interfaces.
  - [ ] Remover interfaces não implementadas ou não utilizadas.
  - [ ] Caso haja implementações parciais ou em desuso, removê-las para limpar a camada Core.

### 1.2. Gap Arquitetural (Gargalo de Escalabilidade com Binários)
* **Localização:** `src/AgenticSystem.Infrastructure/Persistence/Entities/PersistenceEntities.cs`
* **Descrição do Problema:** Entidades como `CustomOnnxModelEntity` e `RerankingAssetEntity` armazenam binários pesados (`byte[]`) diretamente no banco de dados relacional.
* **Impacto:** Degrada o desempenho de I/O, encarece o storage e backup do banco de dados relacional, limitando a escalabilidade do sistema.
* **Plano de Ação:**
  - [ ] Alterar o design das entidades para armazenar apenas metadados (como nome do arquivo, tamanho, hash e URL/caminho).
  - [ ] Implementar integração com um serviço de Object Storage (S3, Azure Blob Storage ou MinIO para ambiente local).
  - [ ] Criar scripts de migração (se necessário) para mover os dados binários existentes no banco para o Object Storage.

### 1.3. Gap Arquitetural (Risco Crítico de Segurança e Configuração)
* **Localização:** `src/AgenticSystem.Core/Services/AesConfigEncryptionService.cs`
* **Descrição do Problema:** A classe possui uma chave de encriptação estática (`hardcoded`) que atua como fallback caso a chave não seja informada por variável de ambiente ou configuração.
* **Impacto:** Vulnerabilidade de segurança gravíssima. Permite que a aplicação inicie com criptografia insegura e conhecida.
* **Plano de Ação:**
  - [ ] Remover a chave de fallback `hardcoded`.
  - [ ] Implementar a validação da configuração no momento da inicialização da aplicação (Startup/DI).
  - [ ] Adotar a estratégia de *Fail-Fast*: lançar uma exceção clara (`InvalidOperationException`) impedindo o sistema de subir se a chave de encriptação não estiver configurada corretamente no ambiente.

### 1.4. Dívida Técnica e Complexidade Excessiva (God Class)
* **Localização:** `src/AgenticSystem.Infrastructure/AI/AgentCollaborationWorkflow.cs`
* **Descrição do Problema:** A classe é gigantesca (+1600 linhas) e mantém misturados caminhos de execução novos (WorkflowBuilder) com antigos/legados (ex: `ExecuteLegacyAsync`).
* **Impacto:** Difícil compreensão, alto risco de regressões durante refatorações e testes difíceis de manter (anti-padrão *God Class*).
* **Plano de Ação:**
  - [ ] Isolar e deletar o método `ExecuteLegacyAsync` e quaisquer dependências legadas que não sejam mais necessárias.
  - [ ] Quebrar a classe utilizando padrões como *Strategy*, *Chain of Responsibility* ou *Mediator* para as diferentes etapas do workflow.
  - [ ] Mover responsabilidades de sub-processos para serviços menores e testáveis.

---

## 2. Frontend (React / TypeScript)

### 2.1. Gap Arquitetural (God-Hook / Violação de SRP)
* **Localização:** `frontend/src/hooks/useChat.tsx`
* **Descrição do Problema:** O hook atua como um monólito (~20KB). Ele centraliza conexões WebSockets (SignalR), chamadas de APIs REST, e todo o controle de estado da interface.
* **Impacto:** Viola o Princípio de Responsabilidade Única (SRP), gera acoplamento extremo e dificulta a evolução (ex: múltiplos chats abertos simultaneamente).
* **Plano de Ação:**
  - [ ] **Extração de Estado:** Migrar a gestão de estado do chat para o gerenciador de estado global do projeto (**Zustand**, que já consta no `package.json`). Arquivo sugerido: `src/store/chatStore.ts`.
  - [ ] **Isolamento de Transporte:** Criar um hook/serviço dedicado apenas à conexão em tempo real. Arquivo sugerido: `src/hooks/useChatSignalR.ts`.
  - [ ] **Isolamento de API:** Isolar chamadas REST em serviços específicos (ex: `src/services/api/chatApi.ts`).
  - [ ] O arquivo `useChat.tsx` passará a ser apenas um orquestrador leve e limpo que consome as partes citadas acima.

---

## Próximos Passos de Execução
Para iniciar a resolução, recomendamos abordar na seguinte ordem de prioridade:
1. **Segurança (Crítica):** `AesConfigEncryptionService`
2. **Qualidade e Estabilidade:** Extração do `useChat.tsx` no Frontend.
3. **Limpeza Técnica:** Remoção do código morto em `IMaturityServices`.
4. **Desempenho e Arquitetura:** Refatoração de `AgentCollaborationWorkflow` e das entidades de banco com binários.
