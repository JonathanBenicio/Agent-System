# ADR 005: Adoção de Persistência no Banco para Agentes Dinâmicos e Middleware FIDES

## Status
Aceito

## Contexto
Durante a atualização da arquitetura do AgenticSystem para suportar recursos mais dinâmicos baseados no MAF 1.6.2, identificamos duas lacunas críticas (Gaps 1 e 5):
1. **Agentes Efêmeros:** Agentes gerados via comandos dinâmicos (chat) residiam apenas no `ConcurrentDictionary` na memória e eram perdidos a cada reinício da aplicação. 
2. **Segurança de Dados Sensíveis:** Informações de PII dos usuários poderiam vazar para os LLMs sem um bloqueio sistêmico no pipeline do MAF.

## Decisão
1. **Persistência Híbrida de Agentes:** Adicionamos a entidade `DynamicAgentEntity` ao contexto EF Core PostgreSQL (`AgenticDbContext`). O `HierarchicalAgentFactory` agora atua como uma fachada inteligente, que carrega as definições de `AgentSpecification` dinâmicas em memória, mas as persiste no banco de dados para garantir sobrevivência e resiliência entre deploys. O isolamento multi-tenant já existente (via Global Query Filters no EF Core) se aplica de forma imediata aos agentes customizados.
2. **FIDES Data Protection Middleware:** Criamos o `FidesDataProtectionMiddleware.cs` encapsulando as mensagens no pipeline nativo do MAF (`builder.Use(...)` via `DelegatingAIAgent`). A validação foi alocada logo antes do envio para o modelo de linguagem, garantindo que CPFs, tokens, cartões de crédito e e-mails sejam identificados e substituídos por máscaras de ofuscação de forma determinística, sem depender do próprio LLM para ofuscação.
3. **PowerFx RecalcEngine:** A fim de dar mais segurança para regras de comportamento definidas nos YAMLs, adicionamos a biblioteca nativa `Microsoft.PowerFx` e acoplamos o `RecalcEngine` ao `AgentYamlValidator`, substituindo a validação frágil de parênteses pela compilação real da Microsoft.

## Consequências

### Positivas
- Agentes personalizados agora sobrevivem ao reinício do sistema, permitindo que a promessa "multi-agent chat" seja um fluxo durável.
- Dados sensíveis nunca mais serão transmitidos inadvertidamente ao LLM subjacente, reforçando o compliance de nível corporativo do AgenticSystem.
- Agentes gerados são devidamente isolados por inquilinos (`TenantId`).
- Validação determinística do modelo YAML impede corrupção ou quebra da aplicação no runtime.

### Negativas / Riscos
- Um acréscimo mínimo (sub-ms) na latência de geração de requisições de LLM devido à avaliação do RegEx para ofuscação (FIDES).
- Sobrecarga inicial (cold-start) ao carregar todos os agentes dinâmicos durante a primeira requisição do `HierarchicalAgentFactory`.
