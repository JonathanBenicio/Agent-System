---
name: Lacuna ou regressão do backend
about: Registrar falha reproduzida ou capacidade ausente sem presumir implementação
title: '[P?][BUG] '
---

## Problema, impacto e baseline
SHA/data/ambiente; esperado versus observado. Prioridade P0 (boundary/segurança), P1 (núcleo/confiabilidade) ou P2 (demais), com justificativa.

## Reprodução e evidência
Comandos, fixtures sintéticas, serviços reais/mocks, resultado e relatório sanitizado. Hipótese de causa separada da evidência.

## Critérios de correção
- [ ] Teste positivo preserva comportamento autorizado.
- [ ] Teste negativo impede falha/vazamento observado.
- [ ] Contratos, migração/compatibilidade e operação documentados quando aplicável.

## Rastreabilidade
Epic, ADR, story com ID único, plano em docs/plan, PR e evidências. Atualizar links assim que criados. Artefatos da auditoria não substituem plano da correção de produção.

## Limites e prontidão
Separar reproduzido/inferido/não executado/capacidade ausente. Não fechar por teste ignorado ou apenas por alteração documental. Ver [processo](../../templates/README.md).
