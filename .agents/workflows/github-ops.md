# Workflow: Automação GitHub (Issues & PRs)

Este workflow orienta o agente na criação e gerenciamento de Issues e Pull Requests no GitHub, garantindo o cumprimento da Regra de Ouro de documentação.

## 🎯 Gatilho
- Quando o usuário solicita a criação de uma nova feature ou correção de bug.
- Quando o usuário pede para abrir um PR.

## 🛠️ Ferramentas Utilizadas
- Servidor `mcp_github` (ferramentas como `create_issue`, `create_pull_request`, `update_issue`).

## 📝 Passo a Passo

### 1. Criação de Issue
Antes de iniciar qualquer desenvolvimento de nova funcionalidade:
1. Use a ferramenta `mcp_github_create_issue` para abrir uma issue.
2. O corpo da issue deve seguir estritamente o template em `templates/issue-template.md`.
3. Preencha o título com o prefixo apropriado (`[FEAT]`, `[BUG]`, etc.).

### 2. Vinculação de Documentos
Assim que os documentos forem criados pelo agente (ADR, Story, Plan):
1. Atualize a descrição da issue no GitHub com os links ou IDs dos documentos.
2. Use a ferramenta `mcp_github_update_issue` para isso.

### 3. Criação de Pull Request
Após concluir a implementação, passar nos testes e realizar o commit:
1. Use a ferramenta `mcp_github_create_pull_request`.
2. O corpo do PR deve seguir o template em `templates/pr-template.md`.
3. Certifique-se de preencher a seção `Closes #ID` na descrição do PR para fechar a issue automaticamente.

## 🔴 Regras de Ouro
- **Zero Tolerância**: Nunca inicie código sem uma issue aberta ou vinculada.
- **Uso de Templates**: Sempre use os templates fornecidos na pasta `templates/`.
- **Commits**: Respeite o padrão de commits semânticos descrito em `templates/commit-rules.md`.
