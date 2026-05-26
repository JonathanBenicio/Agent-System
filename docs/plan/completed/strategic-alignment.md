# Plano de Ação: Alinhamento Estratégico e de Testes

Este plano aborda o atraso identificado no Roadmap de Testes e a sincronização da documentação estratégica.

## 1. Modernização da Suíte de Testes
**Objetivo:** Reduzir a dependência de Mocks e usar testes de integração reais.

- [ ] **Testcontainers:** Configurar o `AgenticSystem.Tests` para utilizar `Testcontainers.PostgreSql`.
- [ ] **Integration Tests:** Implementar testes que validem o fluxo completo: `AgentController` -> `Service` -> `Database` sem mockar a camada de persistência.
- [ ] **SignalR Tests:** Criar testes de integração para o hub de SignalR no frontend utilizando Playwright.

## 2. Sincronização de Roadmap e Documentação
**Objetivo:** Refletir o estado real do projeto nos documentos de governança.

- [ ] **Atualizar ROADMAP.md:** Marcar a modernização do frontend como 100% concluída.
- [ ] **Atualizar ADRs:** Criar ADR para a decisão de remover binários do banco (Object Storage).
- [ ] **Auditoria de Documentação:** Garantir que os diagramas em `docs/architecture/` reflitam a nova divisão de controladores do backend.

## 3. Estabilização de Core (Roadmap Q2 2026)
**Objetivo:** Cumprir as metas de estabilidade do backend.

- [ ] **Health Checks:** Implementar verificações de saúde reais (Liveness/Readiness) que validem a conexão com o banco e disponibilidade de modelos ONNX.
- [ ] **Logging & Observability:** Padronizar os logs estruturados no backend para facilitar a depuração de workflows complexos.

## Critérios de Aceite
- Suíte de testes integrados rodando em < 5 minutos.
- Todos os documentos em `docs/` atualizados com as mudanças de Maio/2026.
- Scripts de migração de dados validados e versionados.
