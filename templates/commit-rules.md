# Commits
## Formato obrigatório
```text
tipo(escopo): resumo do resultado em PT-BR

- Descreve o ponto alterado ou adicionado.
- Descreve outro ponto relevante, quando houver.

Refs #ID
```

Tipos: `feat`, `fix`, `docs`, `test`, `refactor`, `chore`, `perf`, `build`, `ci`.
O tipo e os identificadores técnicos permanecem na forma convencional; resumo e corpo devem estar em português brasileiro (PT-BR). O corpo deve listar de forma específica os principais pontos alterados/adicionados; não use descrições vagas como “ajustes diversos”.

Todo commit deve referenciar uma ou mais issues relacionadas. Use `Refs #ID` para progresso parcial, diagnóstico ou trabalho cuja issue continua aberta. Use `Closes #ID` somente quando este commit cumpre todos os critérios da issue e o fechamento é intencional. Para várias issues, inclua um trailer por issue.

### Exemplo — progresso parcial
```text
docs(backend): esclarece o contrato de isolamento por tenant

- Documenta a precedência do cabeçalho sobre a claim JWT.
- Registra os casos em que o acesso é negado.

Refs #110
```

### Exemplo — issue concluída
```text
fix(auth): preserva o papel da membership na API key

- Impede elevação de Viewer para Admin durante a autenticação.
- Rejeita troca de tenant sem vínculo ou concessão válida.

Closes #111
```

Revisar diff/staging; caminhos explícitos; preservar trabalho alheio; não usar git add . nem incluir segredos/logs brutos.
Sem commits vazios/git notes obrigatórios. Evidências no plano/relatório.
