# ADR-041 — Correção dos contratos e limites após revisão do PR #132
Data: 2026-10-02 · Issue: [#139](https://github.com/JonathanBenicio/Agent-System/issues/139) · Stories: [BACK-REVIEW-139](../../USER-STORIES.md#back-review-139--corrigir-os-32-achados-do-pr-132) · [Plano](../../plan/pr132-review-remediation.md)
Decisão: aceita para execução autorizada das correções; decisões de produto anteriores preservadas.
Implementação: R01–R34 passaram na CI de `0749b36`; PR #152 continua draft.
Validação: CI em `0749b36` passou com 924 aprovados, 26 ignorados, zero falhas; security scan, lint/build, Playwright 48/48 e Cypress 1/1 verdes. Hyperlight passou 8/8 em host compatível; as regressões de outbox passaram no PostgreSQL Compose e a guarda R34 falhou como esperado em database temporário, preservando os dados. A descrição pública de #97 ainda precisa ser reconciliada antes da promoção.

## Contexto
A revisão da integração encontrou 32 defeitos em caminhos de produto e suporte, apesar de evidências anteriores de testes. É necessário tornar as mesmas invariantes eficazes em todos os callers, sem introduzir um runtime paralelo.

## Decisão
- Auth OpenAI cru usa o mesmo policy/tenant/membership dos demais endpoints; revoked/inactive nega. Login/logout browser permanecem cookie HttpOnly, sem credencial persistida em JavaScript.
- FIDES protege despacho direto antes do provider; decisões de detectores obrigatórios/fail-closed preservadas. Markdown tenant-controlled não vira HTML ativo.
- ONNX valida dimensões/canais e orçamento com overflow seguro no recebimento e antes da alocação.
- Dados InMemory também são tenant-owned; room_ids representa uma allow-list com vazio negado. Ingestão exige ACL Editor/Admin; concessão suporte não elimina ACL.
- Quota diária reseta e cancelamento contabiliza uso observado/estimado explicitamente. Teto impede nova sessão uniformemente; retomada autorizada não consome nova vaga.
- Workflow mantém enum compatível, valida o grafo DependsOn efetivo, decide approval por step e faz merge seguro de outputs. Claim global exige capability de sistema interna.
- Cache inclui consulta/filtros; lifetime DI cobre a consulta inteira; UI só confirma operações persistidas.
- CI/E2E inicia servidores e usa contratos atuais. Docs têm IDs únicos, estados e evidência por SHA.
- DurableTask continua opcional: reparar SQL e provar lotes em PostgreSQL 16; o engine padrão dinâmico é preservado.

## Alternativas e trade-offs
Corrigir os callers existentes (escolhido) conserva jornadas e evita duplicação. Remover fallbacks, descartar APIs compatíveis ou desabilitar recursos para passar testes não cumpre o objetivo. Um único PR contra a branch #132 com commits por contexto facilita integração; vários PRs em stack seriam possíveis, mas aumentariam coordenação sobre arquivos comuns.
Não reescrever a pilha #132 nem migrar MAF nesta entrega. Para workflow, strings no JSON e números históricos explícitos evitam reinterpretar passos.

## Consequências, migração e rollback
Servidor/cliente precisam ser entregues juntos para contratos corrigidos. Banco de produto será criado do zero; validar todas as migrations/tabelas. Correção SQL opcional pode exigir migration explícita, sem transformar DurableTask em padrão.
Rollback por revert do commit de contexto, observando dependências declaradas; não remover volume, dados de terceiros ou history artificialmente. Regressões de segurança não devem ser reabertas em ambiente exposto.
PR stack alvo `integration/develop-pr-stack-2026-09-30` é exceção autorizada à regra geral de feature→develop; promoção master permanece develop→master squash.

## Verificação e gaps
[Plano/matriz32](../../plan/pr132-review-remediation.md) e [evidência](../../backend/validation/pr132-review-remediation-2026-10-02.md). Aceitar este desenho não prova implementação ou validação. Issues #133–#135/#121 ficam futuras. Refs nos commits não neutralizam Closes históricos; nenhum fechamento antigo é presumido.
