# ADR 026: Auto-Bootstrap e Expurgo do Fallback Tenant "Default"

**Status:** Aprovado  
**Data:** 22 de Maio de 2026  
**Autor(es):** Antigravity, Specialist Orchestrator Agent

---

## Contexto

Atualmente, o AgenticSystem é configurado com acoplamento a um tenant fixo e estático denominado `"default"` (`Tenant.DefaultTenantId = "default"`). Isso se reflete nos seguintes pontos do sistema:
1. **Fallback Estático de Identidade**: Múltiplos componentes do ecossistema (Core, Infrastructure e Api) utilizam o fallback automático `"default"` para preencher propriedades de tenant quando a resolução do contexto falha ou não é fornecida explicitamente.
2. **Autenticação Insegura por API Key**: O handler de autenticação `ApiKeyAuthenticationHandler` compara a chave enviada no cabeçalho ou cookies diretamente com uma chave em formato puro configurada estaticamente no `appsettings.json` (`AgenticSystem:AdminApiKey`), atribuindo a essa autenticação o tenant estático `"default"`.
3. **Complexidade e Falta de Isolamento**: Um ambiente multi-tenant real (SaaS) exige isolamento estrito de dados baseado em banco de dados, no qual as chaves de API sejam individuais de cada tenant, associadas dinamicamente e armazenadas de forma segura. O modelo atual gera riscos de poluição ou vazamento de dados caso a resolução de escopo falhe no pipeline de execução de threads de background ou hubs em tempo real.

Precisamos definir padrões técnicos para:
- Armazenar e autenticar chaves de API dinâmicas a partir do banco de dados PostgreSQL.
- Eliminar por completo a constante `"default"` e impor verificação estrita em tempo de compilação e execução.
- Implementar um processo seguro de inicialização automática (auto-bootstrap) do tenant de administração e de suas credenciais no primeiro boot do servidor para evitar cenários de lockout.

---

## Decisão

Tomamos a decisão de arquitetar a transição para multitenancy estrito e auto-bootstrap da seguinte forma:

1. **Gestão Segura de Credenciais Baseada em Hashing SHA-256**:
   - Criar a entidade `AccessApiKeyEntity` (tabela `access_api_keys`) contendo `(Id, TenantId, KeyHash, Name, IsEnabled, CreatedAt, LastUsedAt)`.
   - As chaves de API em formato puro geradas pelo usuário **nunca** serão salvas no banco de dados. Armazenaremos apenas o seu hash SHA-256 (`KeyHash`). A chave pura será retornada exclusivamente uma única vez no payload da requisição de criação.
   - Adicionar um índice único na coluna `KeyHash` no Entity Framework Core para garantir buscas com desempenho $O(1)$ e proteção contra timing attacks no banco de dados.

2. **Refatoração do Handler de Autenticação (`ApiKeyAuthenticationHandler`)**:
   - O handler de autenticação não fará mais validação contra chaves fixas em arquivos de configuração.
   - Ao receber o token/chave (via header `X-Api-Key`, query string ou cookies), o handler calculará o hash SHA-256 correspondente e buscará a entidade ativa no banco de dados.
   - Em caso de sucesso, injetará o `tenant_id` real e as claims do usuário de forma dinâmica no `ClaimsPrincipal`.

3. **Auto-Bootstrap Seguro (SystemBootstrapService)**:
   - Implementar o `SystemBootstrapService` executando como um `IHostedService` no pipeline de startup, executado imediatamente após o término das migrações do banco de dados em `Program.cs`.
   - Se a tabela `Tenants` estiver vazia (primeiro boot), o serviço criará o primeiro tenant administrativo `admin` (com limites e plano `Pro`).
   - O serviço lerá de forma transitória a configuração `AdminApiKey` do `appsettings.json`. Se presente, calculará seu hash e inserirá como a primeira chave de acesso do tenant `admin` na tabela `access_api_keys`, mitigando qualquer indisponibilidade de acesso após o deploy.

4. **Expurgo Total de "default"**:
   - Remover a constante estática `Tenant.DefaultTenantId`.
   - Modificar os modelos de contexto (`TenantContext`, `UserContext`, `SessionData`) para exigir um `TenantId` válido sem fallbacks de string vazia.
   - Modificar o middleware `TenantMiddleware` para rejeitar (retornando HTTP 403 Forbidden) requisições autenticadas nas quais o tenant_id associado não possa ser resolvido de forma estrita a partir das claims ou headers.

---

## Justificativa

1. **Isolamento de Dados de Produção (SaaS Estrito)**: Remover a dependência de um fallback "default" força os desenvolvedores e o compilador a tratarem a identificação de tenant explicitamente em todas as frentes (SignalR hubs, API endpoints, background jobs), eliminando falhas de vazamento de dados em múltiplos clientes.
2. **Criptografia Unilateral Segura**: Armazenar chaves como hashes SHA-256 garante conformidade com padrões modernos de segurança de credenciais, protegendo os tenants mesmo em caso de vazamento de dump do banco de dados PostgreSQL.
3. **Zero-Downtime e Facilidade de Transição**: O reaproveitamento da `AdminApiKey` existente na configuração do `appsettings.json` pelo bootstrap garante que os deploys atuais e ambientes de desenvolvimento locais continuem funcionando imediatamente com as mesmas credenciais, sem exigir passos manuais ou scripts de migração de dados.
4. **Desempenho de Busca e Segurança contra Timing Attacks**: A busca direta pelo hash único evita varreduras lentas na tabela e elimina o risco de vazamento de informações baseado em tempo de resposta (Timing Attacks) que ocorrem em comparações iterativas de strings.

---

## Consequências

### Positivas
* **Multitenancy Estrito e Nativo**: Todo o ecossistema é obrigado a resolver um tenant válido a partir da requisição ou do escopo de thread, garantindo isolamento total de ponta a ponta.
* **Segurança Aprimorada**: Credenciais armazenadas apenas como hash de mão única no banco de dados. Chaves de API podem ser desativadas manualmente e rotacionadas dinamicamente sem necessidade de alterar arquivos de configuração e reiniciar o contêiner Docker.
* **Compilação como Barreira de Segurança**: Remover a constante global estática impede que novas funcionalidades sejam escritas assumindo fallbacks implícitos de tenant, pegando erros no pipeline de CI/CD.

### Desafios / Pontos de Atenção (Negativas)
* **Ajuste na Suite de Testes**: Como o tenant `"default"` deixa de existir, todos os testes unitários e de integração (Playwright/Cypress/xUnit) que dependem de bancos vazios devem ser ajustados para incluir sementes (seeds) que cadastrem o tenant `admin` e chaves válidas.
* **Gestão de Chaves de Agentes Externos**: Serviços ou plugins MCP externos que integram com o backend precisarão obter credenciais dinâmicas do banco ou receber um cabeçalho explícito de Tenant ID na sua autenticação.

## Decisões de produto vigentes — 2026-09-29

- O bootstrap de tenant só pode ocorrer quando `AdminApiKey` estiver explicitamente configurada. Banco vazio sem essa chave inicia sem tenant nem credencial; operações que exigem tenant continuam indisponíveis até provisionamento explícito. Configure `AgenticSystem__AdminApiKey` para criar o tenant inicial `admin`. Em banco que já tem tenant, a chave de bootstrap não é exigida.
- O bootstrap não cria `Platform Admin` e não semeia agentes de produto/demo como `VisionAnalyst` ou `EditorChefe`. A inicialização de plataforma permanece separada do papel `Admin` do tenant.
- Fallbacks `TenantId ?? "default"` em código de runtime devem ser removidos; o identificador `default` fica restrito a fixtures ou migrações legadas e é rejeitado como tenant de runtime.
- Operações de sistema/background não são tenants. O escopo aprovado em 2026-10-01 é um `SystemOperationContext` tipado e interno, separado de `TenantId`; nunca pode ser escolhido por header ou claim. Dados tenant-owned exigem um tenant real. Jobs que processam dados tenant-specific enumeram tenants e executam cada operação sob seu contexto; recursos/quota global de plataforma usam store de plataforma, sem IDs sintéticos em `TenantId`.
- Este bloco substitui as partes da decisão original que implicavam criar tenant sem chave, iniciar sem tenant, semear agentes específicos do Banner ou aceitar fallback/pseudo-tenant no runtime. A implementação do fallback, catálogo, quotas, outbox, alertas e jobs está em validação no plano #97; a migração separa dados globais legados e mantém a propriedade de dados tenant-owned.
