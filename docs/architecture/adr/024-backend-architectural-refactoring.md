# ADR 024: Refatoração Arquitetural e Limpeza Técnica do Backend (.NET 10)

**Status:** Aprovado  
**Data:** 22 de Maio de 2026  
**Autor(es):** Antigravity Developer

---

## Contexto

A solução backend (.NET 10) do **AgenticSystem** acumulou complexidade acidental (Overengineering) e débito técnico devido ao rápido crescimento e agregação de pilares no chat principal. Três problemas críticos foram identificados:

1. **God Object (MetaAgentOrchestrator.cs):** O orquestrador central acumulou 14 dependências injetadas diretamente no construtor principal, misturando responsabilidades de controle de sessão, auditoria, execução de workflows, roteamento inteligente e isolamento de tenant. Isso violou o SRP (Single Responsibility Principle) e tornou os testes unitários extremamente frágeis e complexos.
2. **Bootstrap Poluído (Program.cs):** O arquivo de entrada principal continha mais de 600 linhas de código, misturando infraestrutura básica (Swagger, Segurança, Rate Limiting) com lógica de negócio complexa (endpoints inline de chat, DTOs de request/response e tratamento manual de Server-Sent Events).
3. **Controle de Tráfego Fragmentado:** Coexistiam 3 abordagens distintas de controle de tráfego, incluindo um dicionário em memória ad-hoc (`ConcurrentDictionary`) para aplicar rate limiting por tenant. Isso impossibilitava escalabilidade horizontal adequada e gerava redundância de lógica.
4. **Desalinhamento de Solution:** O projeto `AgenticSystem.FastPathTrainer` estava órfão na solution principal, enquanto um script duplicado inconsistente (`FastPathModelGenerator.cs`) existia no projeto de infraestrutura.

## Decisão

Adotamos uma refatoração arquitetural profunda estruturada em 4 frentes:

1. **Modularização de Dependências do Core:**
   * Criamos a abstração `ISessionLifecycleCoordinator` (e sua implementação `SessionLifecycleCoordinator`) para unificar as 4 dependências de ciclo de vida e auditoria de sessões.
   * Criamos a abstração `IChatWorkflowCommandHandler` (e sua implementação `ChatWorkflowCommandHandler`) para encapsular o parsing e execução de comandos de chat relacionados a workflows.
   * Reduzimos as dependências do construtor de `MetaAgentOrchestrator` de 14 para 7.

2. **Decoupling do Bootstrap (Program.cs):**
   * Extraímos a autenticação e segurança para `SecurityServiceCollectionExtensions.cs`.
   * Extraímos a configuração do gerador de documentação API para `SwaggerServiceCollectionExtensions.cs`.
   * Extraímos a lógica de rate limiting nativa para `RateLimitingServiceCollectionExtensions.cs`.
   * Reduzimos o tamanho do `Program.cs` de ~600 linhas para ~120 linhas.

3. **Arquitetura de Endpoints Baseada em Controllers:**
   * Substituímos os endpoints mínimos inline por um `ChatController` convencional em `AgenticSystem.Api/Controllers/`.
   * Unificamos o rate limiting sob o middleware nativo do ASP.NET Core usando uma política sliding window per-tenant customizada (`TenantChatLimit`), eliminando o rate limiter em memória manual.
   * Extraímos os DTOs para `ChatRequest.cs` e a escrita de SSE para `SseWriter.cs`.

4. **Sincronização de Projetos:**
   * Incluímos o `AgenticSystem.FastPathTrainer.csproj` no arquivo de solução `.sln`.
   * Excluímos o script duplicado órfão `FastPathModelGenerator.cs`.

## Justificativa

1. **Single Responsibility Principle (SRP):** O orquestrador agora foca puramente em triar intenções e rodar a execução lógica com agentes, delegando o ciclo de vida da sessão e comandos de workflow para coordenadores dedicados.
2. **Separação de Preocupações (SoC):** Configurações de segurança, infraestrutura e tráfego agora residem em arquivos de extensão isolados, e a API de chat segue o padrão MVC (ControllerBase) consagrado na arquitetura .NET corporativa.
3. **Escalabilidade & Padronização de Plataforma:** O Rate Limiting baseado no middleware nativo do .NET permite integração transparente com armazenamentos distribuídos (como Redis) no futuro, abandonando estados em memória locais frágeis.
4. **Alta Testabilidade:** A quebra do God Object facilitou imensamente a escrita de testes de unidade sem a necessidade de instanciar dezenas de mocks complexos.

## Consequências

### Positivas
* **Manutenibilidade Elevada:** Código extremamente limpo, legível e em total conformidade com o padrão C# Clean Code e as diretrizes do Master Roadmap Q2 2026.
* **100% de Sucesso em Testes:** A suíte completa de 642 testes unitários passou perfeitamente após a refatoração.
* **Retrocompatibilidade Preservada:** Os payloads de Request/Response e as rotas `/api/chat` e `/api/chat/stream` continuam exatamente idênticos para os clientes do frontend.

### Desafios / Pontos de Atenção (Negativas)
* **Maior Número de Arquivos:** Introdução de novas interfaces e classes utilitárias no Core e na Api, aumentando ligeiramente a quantidade de arquivos a gerenciar no monorepo, compensada amplamente pela drástica redução de complexidade ciclomática por arquivo.
