# Workflow de registro e execução
Fonte canônica do processo. [Templates](../templates/README.md).

## Preparação
Capturar branch/SHA/alterações preexistentes. Issue antes dos artefatos estratégicos: issue → ADR → story → plano em docs/plan → execução → commits → PR. Atualizar corpo da issue com links assim que criados. conductor/tracks.md referencia plano, não o duplica. Mudanças pequenas podem justificar N/A para ADR/story.

### Destino de branches e PRs
- PRs de feature, correção e documentação têm como base `develop`.
- Quando o usuário autorizar uma stack, o PR filho pode apontar para a branch do PR precedente; registrar essa exceção no plano. O destino final da stack continua `develop`, e promoção para `master` continua exclusiva de `develop`.
- `master` recebe somente um PR de promoção com source branch `develop`; não abrir PR de feature diretamente para `master`.
- PRs antigos diretamente para `master` devem ser retargetados ou encerrados quando forem substituídos por uma consolidação em `develop`.

## Execução
Branch isolada, arquivos explícitos. Etapas já autorizadas prosseguem sem pausas artificiais. Pedir decisão só para informação indispensável ou ação fora da autorização. Auditoria documental não autoriza mudanças de produção.
TDD para comportamento quando apropriado; documentos: links/fontes/contratos/exemplos; scripts: sintaxe/reprodução; backend: build/testes/cobertura; frontend alterado: lint/build/E2E.

## Evidências
Comandos/revisão/ambiente/resultados no plano/relatório. Estados: passou, falhou, ignorado, não executado, capacidade ausente. ADR aceita, implementação e validação independentes. Unitários não provam integração. Lacunas fora do escopo geram issues; continuar trabalho independente.
Registrar a cobertura medida e qualquer lacuna no PR. Neste plano, cobertura é informativa (15,3% medidos no CI) e não bloqueia a correção funcional; build, testes, segurança e E2E continuam como gates.

## Entrega
[Commits](../templates/commit-rules.md): todo commit referencia issue(s), tem título e corpo em PT-BR e descreve os principais pontos alterados/adicionados; `Refs` para parcial, `Closes` só com todos os critérios cumpridos e fechamento intencional. Preservar staging alheio, usar caminhos explícitos e não usar `git add .`. Sem commits vazios/git notes obrigatórias. [PR](../templates/pr-template.md) com resultados reais/gaps; draft quando qualidade/isolamento não comprovados.
Sincronizar README, docs/INDEX, CONSOLIDATED_DOCS, stories e tracks. Merge/deploy requerem autorização própria.
