# Plano de Ação: Remediação Arquitetural do Backend

Este plano visa resolver as dívidas técnicas críticas e falhas de segurança identificadas na auditoria de 23/05/2026.

## 1. Segurança: Criptografia de Configuração (Alta Prioridade)
**Objetivo:** Eliminar a chave de fallback hardcoded e garantir que o sistema não inicie sem configuração segura.

- [ ] **Remover Fallback:** Editar `AesConfigEncryptionService.cs` para remover a string estática e lançar `InvalidOperationException` se a chave for nula.
- [ ] **Validação no Startup:** Atualizar `Program.cs` para validar a presença da variável de ambiente `ENCRYPTION_KEY` antes de registrar o serviço.
- [ ] **Documentação:** Atualizar o `README.md` do backend com as novas exigências de configuração.

## 2. Escalabilidade: Storage de Binários
**Objetivo:** Remover binários (PDFs, Modelos ONNX, Embeddings) do banco de dados relacional.

- [ ] **Abstração de Storage:** Criar interface `IObjectStorageProvider` em `Core`.
- [ ] **Implementação Local:** Criar `FileSystemStorageProvider` para salvar arquivos em disco (com mapeamento de volume no Docker).
- [ ] **Refatorar Entidades:** Alterar `byte[]` para `string FilePath` ou `string StorageUrl` em:
    - `VectorDocumentEntity`
    - `CustomOnnxModelEntity`
    - `RerankingAssetEntity`
- [ ] **Migração:** Criar script SQL para limpar as colunas binárias pesadas após a migração dos dados.

## 3. Limpeza: Código Morto e God Class
**Objetivo:** Reduzir o ruído cognitivo e a complexidade do motor de workflow.

- [ ] **Interfaces Especulativas:** Excluir `IMaturityServices.cs` e os serviços `QueryCompressorService`, `SemanticCompressorService` e `UserPreferenceEngine`.
- [ ] **Refatorar Workflow:**
    - [ ] Deletar o método `ExecuteLegacyAsync` em `AgentCollaborationWorkflow.cs`.
    - [ ] Extrair a lógica de "Planning" e "Execution" do `AgentCollaborationWorkflow.cs` para classes separadas (padrão Strategy).
    - [ ] Reduzir o arquivo para menos de 500 linhas.

## Critérios de Aceite
- Build do Backend passando com `dotnet build`.
- Sistema falha ao iniciar sem `ENCRYPTION_KEY`.
- Tabelas de metadados no Postgres não excedem 1MB por linha (sem binários).
