# ADR 027: Migração Completa para o Microsoft Agent Framework (MAF) 1.6.1

> **Decisão vigente (2026-09-29):** PowerFx é usado apenas para validação sintática (`RecalcEngine.Check`). Não avaliar fórmulas em runtime até existir caso de uso aprovado e nova especificação de funções/contexto/limites. A integração Hyperlight desta ADR é aspiracional; a decisão atual é Preview atrás de flag global desligada, somente em Lab, conforme [ADR-006](006-manutencao-custom-session-e-sandbox.md). Não tratar o plano de migração 1.6.1 abaixo como descrição do runtime atual.

**Status:** Aprovado  
**Data:** 24 de Maio de 2026  
**Autor(es):** Principal .NET Architect & Execution Lead

---

## Contexto

O **AgenticSystem** opera o seu núcleo multi-agente sobre o **Microsoft Agent Framework (MAF)**. A versão atualmente implantada em produção é a **1.5.0** (com dependências de infraestrutura HTTP em pré-visualização, como `1.5.0-preview.260507.1`). Embora o sistema atual seja funcional, ele depende de implementações proprietárias e customizadas no C# para lidar com RAG dinâmico, segurança e ciclos de auto-ajuste e colaboração, o que aumenta a complexidade de manutenção.

Com o lançamento do **MAF `dotnet-1.6.1`**, uma série de atualizações estruturais e correções fundamentais foram introduzidas pela Microsoft. Essas novidades tratam de forma nativa e segura aspectos antes contornados de forma customizada, como handoffs de orquestração, injeção dinâmica de mensagens no loop de funções, isolamento seguro (sandbox WASM) e avaliações contínuas de workflows.

Esta decisão descreve o plano e os critérios arquiteturais para realizar a migração integral e sequenciada (todas as 6 fases do roadmap) do framework MAF de `1.5.0` para `1.6.1`.

## Decisão

Será executada a migração completa e faseada para a versão **1.6.1** do Microsoft Agent Framework em todos os projetos da solução (`AgenticSystem.Core`, `AgenticSystem.Infrastructure` e `AgenticSystem.Api`), adotando as seguintes definições e estratégias de design acordadas no alinhamento estratégico:

1. **Escopo Completo Phased:** A migração cobrirá as 6 fases previstas no roadmap ([maf-migration-roadmap.md](../../plan/maf-migration-roadmap.md)), iniciando pelo upgrade de dependências físicas, padronização do Skills Framework, migração para Durable Sessions, configuração de expressões dinâmicas declarativas, sandbox Hyperlight e observabilidade integrada com DevUI.
2. **Isolamento de Tenant no Durable Sessions (Single DB):** A persistência física de estados do `DurableTask` do MAF ocorrerá na mesma base PostgreSQL operacional do projeto. Para evitar vazamento cross-tenant de orquestrações:
   * **Prefixação de Identificador:** Todo `InstanceId` de orquestração durável deve seguir obrigatoriamente o padrão estrito `"{TenantId}:{SessionId}"`.
   * **Middlewares e Interceptação:** Implementar um interceptor customizado no pipeline do Durable Task Client do MAF C# que valida se o `InstanceId` consultado ou manipulado inicia estritamente com o `CurrentTenantId` resolvido pela requisição autenticada, barrando tentativas fraudulentas com `UnauthorizedAccessException`.
   * **Segregação Física no EF Core:** As tabelas internas que mapeiam o estado das execuções dinâmicas de workflows devem implementar `ITenantEntity`, permitindo que os filtros de consulta globais (`e.TenantId == CurrentTenantId`) do Entity Framework Core bloqueiem acessos indevidos de forma transparente a nível de banco de dados.
3. **Sandbox Estrito para Execução de Códigos (Hyperlight WASM):** Fica estritamente bloqueada qualquer inicialização direta de subprocessos do sistema operacional local (`process.Start`) gerados dinamicamente pelos agentes. Scripts gerados por agentes devem rodar estritamente dentro da sandbox isolada em nível de micro-VM/WASM fornecida pela nova `HyperlightExecuteCodeTool` do MAF 1.6.1.
4. **Avaliação sob Demanda, Cache e Sandboxing Fino em PowerFx:** Para evitar gargalos de performance e manter o SLO de latência de chat sob controle (< 50ms de overhead), as fórmulas lógicas e matemáticas definidas nos manifestos declarativos YAML dos agentes serão avaliadas dinamicamente sob as seguintes restrições de segurança:
   * **Configuração Restrita da RecalcEngine:** O motor `RecalcEngine` (Microsoft.PowerFx) será instanciado de forma isolada, permitindo apenas um conjunto higienizado de funções matemáticas/lógicas básicas (`If`, `And`, `Or`, `Sum`, `Abs`, `Concat`, `Len`, `Upper`, `Lower`). Funções com potencial de loops longos ou acessos externos são estritamente desabilitadas.
   * **Contexto Higienizado:** Apenas objetos fortemente tipados e serializados contendo metadados de sessão e variáveis não confidenciais de Tenant (`TenantContextRecord`) serão injetados como escopo de execução do PowerFx.
   * **Timeout de Execução:** Toda avaliação de fórmulas é atrelada a um timeout rígido de 50ms via `CancellationToken`.
   * **Validação Estática na UI:** A API de validação (`AgentYamlValidator`) compilará estaticamente a expressão PowerFx durante o cadastro do manifesto na tela, retornando erros sintáticos em tempo real para o frontend antes da persistência.

## Justificativa

1. **Segurança Corporativa (Sandbox WASM):** O uso da sandbox do Hyperlight isola o servidor de qualquer risco associado à execução de scripts Python ou CLI maliciosos sugeridos dinamicamente por LLMs.
2. **Baixa Complexidade de Infraestrutura (Single DB Tenant Isolation):** Manter o isolamento multi-tenant por filtro de coluna no banco único PostgreSQL evita a sobrecarga e o custo operacional de manter múltiplos esquemas físicos ou instâncias dedicadas de banco para o Durable Task Framework.
3. **Alta Performance (PowerFx Caching):** A pré-compilação e o cache de fórmulas em memória evitam a penalização do tempo de processamento por token (Time-to-First-Token) do chat na execução de regras declarativas por Tenant.
4. **Resiliência a Falhas (Durable Sessions):** O uso do Durable Task Framework nativo do MAF 1.6.1 adiciona resiliência de estado automática no backend, permitindo salvar de forma segura o progresso de workflows complexos e possibilitando interrupções humanas (HITL) de longa duração.

## Consequências

### Positivas
* **Eliminação de Código Customizado:** Substitui dezenas de classes customizadas de rastreamento de sessão e RAG dinâmico por abstrações consolidadas e testadas oficialmente pela Microsoft.
* **Segurança Aumentada:** O isolamento rígido em micro-VMs/WASM elimina vetores de ataque por injeção de prompt no ambiente operacional do servidor.
* **Telemetria Padronizada:** A auto-fiação de telemetria otimiza o rastreamento OpenTelemetry de tokens e custos de agentes nativamente.

### Desafios / Pontos de Atenção (Negativas)
* **Breaking Change de Telemetria:** Exige refatoração fina em `ObservabilityExtensions.cs` para evitar redundância de spans e conflitos com a nossa camada de MAS (Multi-Agent System) logging.
* **Ajustes de Compilação:** Pode haver quebras pontuais de assinaturas obsoletas de builders e context providers do MAF da versão 1.5.0 para 1.6.1, necessitando correções cirúrgicas imediatas.
