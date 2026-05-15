# Plano: Auditoria e Limpeza de Documentação

## Overview
Este plano descreve as etapas para limpar a pasta `docs`, movendo arquivos legados para uma pasta de arquivo (`docs/old`) e criando novas documentações essenciais (ADRs, Glossários e Conceitos) para garantir que a documentação reflita o estado atual do sistema e seja útil para o time.

## Project Type
AGNOSTIC (Documentação)

## Success Criteria
- Arquivos legados movidos para `docs/old` ou removidos.
- 2 novos ADRs criados (Retrospectivo e Recente).
- 2 novos Glossários criados (Técnico e Negócio).
- 1 novo documento de Conceitos criado.
- `docs/INDEX.md` atualizado.

## Tech Stack & Trade-offs
| Opção | Prós | Contras |
| --- | --- | --- |
| Mover legados para `docs/old` | Mantém o histórico acessível na branch principal | Adiciona uma pasta extra |
| Deletar arquivos legados | Estrutura mais limpa possível | Dificulta o acesso ao contexto histórico sem comandos git |

*Decisão*: Mover para `docs/old` para preservar o contexto enquanto limpamos as áreas ativas.

## Risk Assessment
| Risco | Probabilidade | Impacto | Mitigação |
| --- | --- | --- | --- |
| Deleção acidental de docs úteis | Baixa | Médio | Mover para `docs/old` em vez de deletar (exceto arquivos vazios). |

## File Structure
- `docs/old/` [NEW]
- `docs/adr/` [NEW]
- `docs/glossary/` [NEW]
- `docs/concepts-and-architecture.md` [NEW]

## Task Breakdown

### Fase 1: Limpeza (Cleanup)

#### Task: `cleanup-01`
- **Nome**: Criar diretório `docs/old`
- **Agente**: `project-planner`
- **Prioridade**: P0
- **Dependências**: Nenhuma
- **INPUT**: N/A
- **OUTPUT**: Diretório `docs/old` criado
- **VERIFY**: Verificar se o diretório existe

#### Task: `cleanup-02`
- **Nome**: Mover arquivos de `docs/historico/` para `docs/old/`
- **Agente**: `project-planner`
- **Prioridade**: P0
- **Dependências**: `cleanup-01`
- **INPUT**: Arquivos em `docs/historico/`
- **OUTPUT**: Arquivos movidos
- **VERIFY**: Verificar se os arquivos estão em `docs/old/` e não em `docs/historico/`

#### Task: `cleanup-03`
- **Nome**: Mover checkpoints de refatoração para `docs/old/`
- **Agente**: `project-planner`
- **Prioridade**: P1
- **Dependências**: `cleanup-01`
- **INPUT**: `docs/planejamento/REFACTORING_CHECKPOINT_PHASE*.md`
- **OUTPUT**: Arquivos movidos
- **VERIFY**: Verificar se estão em `docs/old/`

#### Task: `cleanup-04`
- **Nome**: Remover arquivo vazio `docs/plan/claude.md`
- **Agente**: `project-planner`
- **Prioridade**: P1
- **Dependências**: Nenhuma
- **INPUT**: `docs/plan/claude.md`
- **OUTPUT**: Arquivo removido
- **VERIFY**: Verificar se o arquivo não existe mais

### Fase 2: Criação (Creation)

#### Task: `create-01`
- **Nome**: Criar ADR Retrospectivo (.NET 10 / DDD)
- **Agente**: `project-planner`
- **Prioridade**: P1
- **Dependências**: Nenhuma
- **INPUT**: Conhecimento da arquitetura base
- **OUTPUT**: `docs/adr/001-base-architecture.md`
- **VERIFY**: Arquivo criado com conteúdo válido

#### Task: `create-02`
- **Nome**: Criar ADR Recente (Smart Triage / Fast Path)
- **Agente**: `project-planner`
- **Prioridade**: P1
- **Dependências**: Nenhuma
- **INPUT**: Conhecimento do Smart Triage
- **OUTPUT**: `docs/adr/002-smart-triage.md`
- **VERIFY**: Arquivo criado com conteúdo válido

#### Task: `create-03`
- **Nome**: Criar Glossário Técnico
- **Agente**: `project-planner`
- **Prioridade**: P2
- **Dependências**: Nenhuma
- **INPUT**: Termos técnicos do projeto
- **OUTPUT**: `docs/glossary/technical.md`
- **VERIFY**: Arquivo criado

#### Task: `create-04`
- **Nome**: Criar Glossário de Negócio
- **Agente**: `project-planner`
- **Prioridade**: P2
- **Dependências**: Nenhuma
- **INPUT**: Termos de negócio
- **OUTPUT**: `docs/glossary/business.md`
- **VERIFY**: Arquivo criado

#### Task: `create-05`
- **Nome**: Criar Documento de Conceitos Aplicados (.NET/IA/MAF)
- **Agente**: `project-planner`
- **Prioridade**: P1
- **Dependências**: Nenhuma
- **INPUT**: Conhecimento dos conceitos
- **OUTPUT**: `docs/concepts-and-architecture.md`
- **VERIFY**: Arquivo criado

### Fase 3: Atualização (Update)

#### Task: `update-01`
- **Nome**: Atualizar `docs/INDEX.md`
- **Agente**: `project-planner`
- **Prioridade**: P2
- **Dependências**: Todas as anteriores
- **INPUT**: Nova estrutura de arquivos
- **OUTPUT**: `docs/INDEX.md` atualizado
- **VERIFY**: Verificar links no arquivo

## Rollback Strategy
- Arquivos movidos podem ser movidos de volta para suas pastas originais.
- Arquivos deletados podem ser recuperados via Git (`git checkout` ou `git restore`).

## Phase X: Verification
- [ ] Todos os novos arquivos existem e têm conteúdo.
- [ ] A pasta `docs/old` contém os arquivos legados.
- [ ] O arquivo `docs/plan/claude.md` foi removido.
