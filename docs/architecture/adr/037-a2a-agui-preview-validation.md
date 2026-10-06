# ADR-037 — Despriorizar validação E2E de A2A e AG-UI enquanto hosting for preview

Data: 2026-09-29 · Issue: [#121](https://github.com/JonathanBenicio/Agent-System/issues/121) · Story: BACK-PROTO-121 · [Plano](../../plan/a2a-agui-preview-validation.md). Relacionada: [ADR-036](036-maf-122-protocols-and-gateway.md) / #120.

Decisão: aceita · Implementação: pendente · Validação: não executada.

## Contexto

Os pacotes de hosting A2A e AG-UI são publicados pela Microsoft como preview, inclusive na linha 1.22.0-preview.260918.1. Eles estão referenciados no backend, mas o host de validação desliga os endpoints e ainda não há comprovação E2E de autorização, tenant, sessão e streaming. O objetivo imediato da issue #120 é atualizar o core MAF e fechar o caminho real de provider pelo Gateway.

## Decisão

- Tratar a validação E2E A2A/AG-UI como prioridade secundária e não como gate para concluir o core MAF/Gateway.
- Preservar as feature flags e a configuração atual enquanto compatibilidade/build das dependências são validados; não declarar suporte estável nem ampliar habilitação operacional com base apenas em compilação.
- Executar posteriormente uma validação própria com autenticação, membership/tenant, sessão, streaming, cancelamento e isolamento entre tenants; registrar quais cenários dependem de fixture ou modelo local.
- Revisar a prioridade quando hosting deixar preview ou quando o produto declarar esses protocolos como requisito de release.

## Alternativas e trade-offs

- Tornar os protocolos gate imediato aumenta a superfície de risco e mistura testes de adapters preview com a correção do runtime de produção.
- Ignorar indefinidamente os protocolos deixa endpoints configuráveis sem evidência de operação; por isso a validação permanece em issue/story/plan próprios.
- Desabilitar ou remover os endpoints agora mudaria o contrato operacional sem necessidade demonstrada; isso não faz parte desta decisão.

## Consequências e migração

Build/restore podem confirmar apenas compatibilidade binária/compilação, nunca segurança ou interoperabilidade de ponta a ponta. Relatórios devem classificar hosting como preview e diferenciar E2E real, fixture, skip e não executado. Nenhuma alteração de schema é esperada.

## Verificação e gaps

ADR-037 não atesta funcionalidade dos endpoints. A issue #121 continua aberta até que todos os critérios de autenticação, isolamento e streaming sejam executados ou uma nova decisão de produto retire esses protocolos do escopo.
