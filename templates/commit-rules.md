# 📝 Guia de Commits Semânticos (Conventional Commits)

> **Objetivo:** Manter o histórico do Git limpo, legível e automatizável.

## 🏗️ Estrutura do Commit

```text
<tipo>(<escopo>): <descrição curta>

[corpo do commit - opcional]

[rodapé - opcional]
```

### Exemplo:
```text
feat(auth): adiciona suporte a login com MFA

Implementado o fluxo de autenticação em duas etapas usando TOTP.

Closes #123
```

---

## 🏷️ Tipos de Commit

| Tipo | Descrição | Exemplo |
| :--- | :--- | :--- |
| `feat` | Uma nova funcionalidade | `feat(api): add endpoint for user search` |
| `fix` | Correção de um bug | `fix(auth): prevent token leak in logs` |
| `docs` | Alterações apenas na documentação | `docs(readme): update install instructions` |
| `style` | Alterações que não afetam o significado do código (espaço em branco, formatação, ponto e vírgula faltando, etc) | `style(css): fix grid alignment on mobile` |
| `refactor` | Uma alteração de código que não corrige um bug nem adiciona uma funcionalidade | `refactor(db): use async/await instead of promises` |
| `perf` | Uma alteração de código que melhora o desempenho | `perf(image): add lazy loading to gallery` |
| `test` | Adicionando testes ausentes ou corrigindo testes existentes | `test(e2e): add test for checkout flow` |
| `chore` | Atualizações de tarefas de build, configurações de pacotes, etc. (sem alteração no código de produção) | `chore(npm): update react to v19` |

---

## 🔗 Vinculação com Github Issues

**🚨 REGRA OBRIGATÓRIA:** É **estritamente proibido** realizar um commit do tipo `feat` ou `fix` sem referenciar uma GitHub Issue. Se você é um agente autônomo e não sabe o ID da issue, **PARE E PERGUNTE AO USUÁRIO** antes de rodar o `git commit`.

Sempre referencie a issue no rodapé ou no final da descrição:

- `Closes #123` - Fecha a issue quando o PR for mergeado.
- `Fixes #123` - Indica que o commit corrige o problema da issue.
- `Refs #123` - Apenas referencia a issue sem fechá-la.

## 🚫 Regras de Ouro
1. **Obrigatório referenciar Issues** em `feat` e `fix`.
2. **Use o imperativo** na descrição ("add" em vez de "added", "fix" em vez de "fixed").
3. **Não use letras maiúsculas** no início da descrição (mantenha tudo em minúsculo após o tipo).
4. **Seja conciso** na primeira linha (máximo 50-72 caracteres).
