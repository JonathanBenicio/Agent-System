# Roadmap: Refatoração Arquitetural e Limpeza Técnica - Fase 2

> **Status documental:** Draft
> **Escopo:** Refatoração de binários pesados no PostgreSQL, mitigação de chave AES estática, desacoplamento do AgentCollaborationWorkflow (God Class) e modularização do hook useChat (React) com Zustand.
> **Fonte de verdade operacional:** [backend-architecture-explained.md](file:///c:/Users/Jonathan/Documents/Developer/GitHub/Agent-System/docs/architecture/backend-architecture-explained.md)
> **Gerado em:** 2026-05-22
> **Projeto:** AgenticSystem

---

## Objetivo

Consolidar a integridade, segurança e desempenho do ecossistema AgenticSystem através da mitigação de débitos técnicos críticos no Backend e Frontend. Esta iniciativa visa remover binários pesados de ONNX e Reranking do banco de dados relacional, eliminar a brecha de segurança de criptografia estática, quebrar a classe gigante `AgentCollaborationWorkflow` (reduzindo acoplamento) e reestruturar o hook monolítico `useChat.tsx` do Frontend usando um gerenciador de estado leve e modular (Zustand).

## Princípios de Implantação

1. **Segurança Máxima e Fail-Fast:** Eliminar qualquer segredo estático em código. A aplicação deve falhar imediatamente na inicialização se parâmetros criptográficos essenciais não estiverem presentes.
2. **Desempenho Relacional Saudável:** O banco relacional PostgreSQL deve armazenar exclusivamente metadados estruturados. Arquivos binários pesados devem ser persistidos no sistema de arquivos ou object storage.
3. **Responsabilidade Única (SRP):** Componentes grandes com múltiplas responsabilidades (como hooks de 500+ linhas e orquestradores de 1600+ linhas) devem ser fragmentados em submódulos focados e testáveis de forma isolada.
4. **Preservação de APIs:** As rotas HTTP externas, eventos do SignalR e contratos de DTOs devem se manter estáveis e retrocompatíveis.

## Fases e Sequenciamento

| Ordem | Frente/Fase | Motivo do sequenciamento |
|---|---|---|
| 1 | **Segurança Crítica & Fail-Fast (Fase 1)** | Remover a chave de criptografia de fallback estática em `AesConfigEncryptionService` e validar as chaves no bootstrapping da API. |
| 2 | **Otimização de Banco e Object Storage (Fase 2)** | Alterar `CustomOnnxModelEntity`, `CustomOnnxModelFileEntity` e `RerankingAssetEntity` para salvar arquivos pesados no disco/storage e apenas metadados no Postgres. Gerar migração correspondente. |
| 3 | **Desacoplamento de Workflow Colaborativo (Fase 3)** | Isolar os estágios de planejamento, execução e revisão em serviços menores e limpar a God Class `AgentCollaborationWorkflow.cs`, removendo o fluxo legado `ExecuteLegacyAsync`. |
| 4 | **Reestruturação e Modularização do useChat (Fase 4)** | Extrair o estado global do chat para o Zustand (`chatStore.ts`), isolar a conexão SignalR (`useChatSignalR.ts`) e manter o hook `useChat.tsx` como um orquestrador leve compatível com as telas existentes. |

---

## Detalhamento: Segurança Crítica & Fail-Fast (Fase 1)

### Por que implementar?
A classe `AesConfigEncryptionService` possui uma chave de encriptação fallback estática e visível em código fonte (`"AgenticSystem_Development_StaticSecretKey_Fallback"`). Isso representa um risco severo (CWE-321), permitindo que credenciais sejam criptografadas com uma chave comprometida caso a configuração de ambiente falhe.

### Componentes propostos
* `AesConfigEncryptionService`: Remoção da string de fallback fixa. Exigir o parâmetro obrigatoriamente.
* `ServiceCollectionExtensions`: Garantir que a DI lance `InvalidOperationException` se a chave estiver vazia, impedindo a aplicação de subir de forma insegura.

---

## Detalhamento: Otimização de Storage de Binários (Fase 2)

### Por que implementar?
Entidades como `CustomOnnxModelEntity` e `RerankingAssetEntity` armazenam arrays de bytes pesados (`byte[] Content` e `byte[] ModelData` de até 50MB) diretamente no banco relacional PostgreSQL. Isso causa fragmentação severa de tabelas, estouro de memória durante backups e gargalos extremos de I/O de rede e disco.

### Componentes propostos
* `ILocalFileStorageService` / `LocalFileStorageService`: Nova infraestrutura para gerenciar salvamento, leitura e deleção de arquivos físicos no diretório local do servidor (ex: `wwwroot/assets/onnx-models/` e `wwwroot/assets/reranking/`).
* Entidades atualizadas: Remoção ou substituição dos campos de `byte[]` por campos de metadados contendo caminhos físicos (`FilePath`), hash SHA-256 (`ContentHash`) e tamanho (`FileSizeBytes`).
* Script de Migração: Migração do EF Core que cria as novas tabelas/colunas de metadados e exclui as colunas de dados binários pesados após migrar os binários existentes.

---

## Detalhamento: Desacoplamento de Workflow Colaborativo (Fase 3)

### Por que implementar?
O `AgentCollaborationWorkflow.cs` é um monólito de +1600 linhas que mistura a execução legada de workflows em loops sequenciais clássicos com a execução moderna baseada em `AgentWorkflowBuilder`. A complexidade do construtor e o acoplamento de dependências tornam os testes unitários frágeis e a manutenção dolorosa.

### Componentes propostos
* Remoção de `ExecuteLegacyAsync`: Excluir o fluxo antigo e todas as referências redundantes.
* `CollaborationPlanningStage`: Nova classe focada em coordenar o estágio de planejamento do workflow.
* `CollaborationExecutionStage`: Nova classe focada em executar os passos planejados interagindo com os agentes de domínio.
* `CollaborationReviewStage`: Nova classe focada na revisão dos resultados da execução.
* `AgentCollaborationWorkflow`: Passa a ser apenas um orquestrador limpo e focado, consumindo os novos submódulos especializados.

---

## Detalhamento: Modularização do useChat no Frontend (Fase 4)

### Por que implementar?
O hook e provedor `useChat.tsx` acumula responsabilidades de gerenciamento de estado de mensagens, canais, provedores de LLM, reconexões SignalR e tratamento de erros do Hub de transporte, totalizando ~20KB em um único arquivo. Isso prejudica a performance e impede a evolução de fluxos paralelos (como múltiplos chats ativos).

### Componentes propostos
* `chatStore.ts` (Zustand): Centralizar o estado do chat (mensagens, estado de conexão, provedores ativos) em uma store reativa global.
* `useChatSignalR.ts`: Hook especializado em gerenciar o ciclo de vida da conexão do SignalR e registrar os listeners que atualizam o `chatStore`.
* `chatApi.ts`: Serviço isolado para chamadas HTTP REST (carregar histórico, enviar mensagens REST fallback).
* `useChat.tsx`: Interface simplificada de fachada que consome a store e os hooks especializados para manter compatibilidade absoluta com os componentes visuais do frontend.

## Critérios de Aceite e SLOs
* [ ] A suíte de backend compila com sucesso com o .NET 10 SDK (`dotnet build`).
* [ ] Cobertura de testes de unidade do Backend se mantém acima do patamar de **80%**.
* [ ] Nenhum binário de arquivo (ONNX/Reranker) é salvo em colunas `bytea` do banco relacional PostgreSQL; verificação via auditoria do schema.
* [ ] O frontend compila sem warnings de ESLint (`npm run lint && npm run build`).
* [ ] Os testes E2E do chat no Cypress/Playwright continuam passando sem alterações no comportamento visual.

## Riscos e Mitigações
| Risco | Mitigação |
|---|---|
| Perda de arquivos binários existentes durante a migração do banco. | O script de migração do banco extrairá e salvará os binários existentes no disco físico antes de remover as colunas `bytea`. |
| Dessincronização do estado em abas do navegador após mover o estado para Zustand. | Configurar persistência ou sincronismo sob demanda nas stores se houver necessidade (embora o comportamento atual in-memory já seja suficiente). |
