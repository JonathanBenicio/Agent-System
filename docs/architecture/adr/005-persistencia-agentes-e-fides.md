# ADR 005: Adoção de Persistência no Banco para Agentes Dinâmicos e Middleware FIDES

## Status
Aceito

## Contexto
Durante a atualização da arquitetura do AgenticSystem para suportar recursos mais dinâmicos baseados no MAF 1.6.2, identificamos duas lacunas críticas (Gaps 1 e 5):
1. **Agentes Efêmeros:** Agentes gerados via comandos dinâmicos (chat) residiam apenas no `ConcurrentDictionary` na memória e eram perdidos a cada reinício da aplicação. 
2. **Segurança de Dados Sensíveis:** Informações de PII dos usuários poderiam vazar para os LLMs sem um bloqueio sistêmico no pipeline do MAF.

## Decisão
1. **Persistência Híbrida de Agentes:** Adicionamos a entidade `DynamicAgentEntity` ao contexto EF Core PostgreSQL (`AgenticDbContext`). O `HierarchicalAgentFactory` agora atua como uma fachada inteligente, que carrega as definições de `AgentSpecification` dinâmicas em memória, mas as persiste no banco de dados para garantir sobrevivência e resiliência entre deploys. O isolamento multi-tenant já existente (via Global Query Filters no EF Core) se aplica de forma imediata aos agentes customizados.
2. **FIDES Data Protection Middleware:** `FidesDataProtectionMiddleware` atua no pipeline do MAF antes do provider. Decisão de produto (2026-09-29): usar regras built-in revisadas, sem regex definida pelo tenant; `Owner/Admin` controla toggles por tenant; tudo começa ativo e detectores obrigatórios de credenciais não podem ser desligados. A decisão é o alvo, não prova de que esses toggles ou toda a classificação estejam implementados.
3. **PowerFx RecalcEngine:** `AgentYamlValidator` usa `RecalcEngine.Check` para validação sintática. Decisão de produto (2026-09-29): não executar expressões PowerFx em runtime até haver um caso de uso aprovado; avaliar regras exige uma nova decisão e threat model.

## Consequências

### Positivas
- Agentes personalizados agora sobrevivem ao reinício do sistema, permitindo que a promessa "multi-agent chat" seja um fluxo durável.
- O middleware oferece mascaramento preventivo para os padrões estáticos implementados; cobertura abrangente de PII/compliance não foi comprovada.
- Agentes gerados são devidamente isolados por inquilinos (`TenantId`).
- `RecalcEngine.Check` valida sintaxe das expressões YAML; a execução de regras, funções permitidas e limites de avaliação precisam de confirmação separada.

### Negativas / Riscos
- O custo de latência do FIDES ainda não foi medido; não afirmar overhead sub-ms sem benchmark.
- Sobrecarga inicial (cold-start) ao carregar todos os agentes dinâmicos durante a primeira requisição do `HierarchicalAgentFactory`.

## Verificação de implementação — 2026-09-29

O estado do runtime prevalece sobre afirmações aspiracionais do texto histórico. `FidesDataProtectionMiddleware` existe e é registrado; o catálogo observado contém regex estáticas para CPF formatado, cartão, email e alguns tokens, mas a política/toggles por tenant ainda não estão implementados. `AgentYamlValidator` usa `RecalcEngine.Check`; fórmulas não são avaliadas pelo runtime. Ver [decisões e especificações das issues abertas](../../plan/open-issues-specification-audit-2026-09-29.md#issue-106).
