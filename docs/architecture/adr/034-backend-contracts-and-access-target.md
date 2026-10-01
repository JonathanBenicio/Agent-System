# ADR-034 — Contratos do backend e modelo de acesso desejado
Data: 2026-09-28 • Issue: [#110](https://github.com/JonathanBenicio/Agent-System/issues/110)
Decisão: aceita para documentação e diagnóstico.
Implementação: hierarquia proposta; não implementada nesta entrega.
Validação: [relatório](../../backend/validation/2026-09-28.md).
Story: BACK-DOC-001 em [catálogo](../../USER-STORIES.md).
Plano: [execução](../../plan/backend-documentation-validation.md).

## Contexto
O crescimento do sistema produziu contratos desatualizados e modelos de limites concorrentes. Código comprova comportamento atual, não sua correção.

## Decisão
Topologia permanece em [arquitetura](../backend-architecture-explained.md); contratos operacionais em [docs/backend](../../backend/README.md). Cada regra indica implementação, enforcement e evidência. Separar observado, inferido por leitura, desejado e não validado.

No modelo desejado, Platform Admin gerencia tenants, planos e saúde. Conteúdo exige concessão de suporte explícita, temporária, delimitada, auditável, revogável. Usuário pode ter memberships em vários tenants com Owner/Admin/Operator/Viewer por membership. Owner/Admin não recebem leitura automática: ACL da sala é obrigatória.

## Alternativas
Apresentar hierarquia desejada como implementada ocultaria falhas. Corrigir produção junto da auditoria foi adiado para decisões e testes próprios.

## Consequências
Nenhuma mudança de autorização/schema nesta iniciativa. Lacunas geram issues. Implementação futura exige memberships, concessões, migração de chaves/papéis legados, autorização HTTP/SignalR/protocolos e prova de isolamento/revogação.
