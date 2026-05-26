# ADR 030: Migração para Microsoft.Agents.AI.DurableTask com PostgreSQL

**Status:** Aprovado  
**Data:** 26 de Maio de 2026  
**Autor(es):** Antigravity / Jonathan Benicio  
**Issue Relacionada:** #108

---

## Contexto

Atualmente, o Agentic System gerencia o estado das sessões dos agentes do Microsoft Agent Framework (MAF) na memória RAM ou no PostgreSQL através de um adaptador personalizado chamado `SimpleSessionStoreAdapter` que consome a interface simplificada `ISessionStore`.

Embora essa solução funcione perfeitamente para interações de chat síncronas/rápidas e seja altamente performática, ela possui limitações severas quando o assunto é resiliência de longa duração e orquestrações complexas:
1. **Sem checkpoints automáticos:** Se um contêiner cair no meio da execução de um workflow complexo de múltiplos passos (como geração de banners), o estado é perdido e o workflow não pode ser retomado do ponto de falha.
2. **Ausência de Replay e Idempotência nativos:** A engine do MAF possui suporte de primeira classe para workflows duráveis resilientes através do pacote `Microsoft.Agents.AI.DurableTask`, mas nós não o utilizávamos.
3. **Acoplamento In-Process:** O compilador de grafos dinâmicos (`DynamicMafWorkflowCompiler`) operava inteiramente in-process e síncrono.

## Decisão

Decidimos migrar completamente a gestão de sessões e a orquestração do MAF de in-process/síncrono para o **DurableTask** nativo (`Microsoft.Agents.AI.DurableTask`).

Especificamente:
1. Adotaremos o **provedor oficial PostgreSQL** para a engine durável do DurableTask, compartilhando o mesmo banco de dados da aplicação.
2. Como o DurableTask gerencia suas tabelas internas de forma otimizada via ADO.NET (com locks de banco de alta performance e streaming de histórico), suas tabelas **não** serão integradas diretamente no mapeamento de entidades do **Entity Framework Core (EF Core)**. Em vez disso, usaremos a inicialização de schema nativa do próprio DurableTask no startup da API.
3. O isolamento rígido de Multi-Tenant (`X-Tenant-Id`) será garantido através de **particionamento lógico na chave da instância da orquestração**, prefixando o identificador da sessão com o ID do tenant: `InstanceId = "{TenantId}:{SessionId}"`.
4. Refatoraremos o `IWorkflowCompiler` transparente para os agentes para que ele compile grafos de banco de dados diretamente em Atividades (Activities) e sub-orquestrações do DurableTask.

## Justificativa

1. **[Resiliência Nativa]:** Retomada automática de workflows longos, tratamento de falhas e replay transparente com checkpoints periódicos no banco de dados.
2. **[Multi-Tenancy Eficiente]:** O particionamento lógico com prefixo no `InstanceId` permite usar uma única engine centralizada no PostgreSQL, reduzindo o custo operacional e evitando a complexidade extrema de provisionar uma engine por tenant.
3. **[Desempenho Otimizado]:** O uso direto de ADO.NET pelo DurableTask contorna o overhead do EF Core, garantindo leitura e gravação assíncrona de logs de histórico e enfileiramento em sub-milissegundos.

## Consequências

### Positivas
* **Resiliência a reinicializações:** Falhas de contêiner ou restarts de servidor não quebram workflows em andamento.
* **Histórico persistente confiável:** Histórico de decisões do agente é gravado em logs de eventos duráveis estruturados no PostgreSQL.
* **Orquestração complexa nativa:** Habilidade de suspender a execução aguardando aprovações humanas (Human-in-the-loop) de forma durável.

### Desafios / Pontos de Atenção (Negativas)
* **Complexidade de Schema:** O banco de dados PostgreSQL passará a conter tabelas do sistema de orquestração do DurableTask não gerenciadas por migrations do EF Core.
* **Curva de Aprendizado:** Desenvolvedores devem respeitar as regras rígidas do DurableTask (ex: restrição a operações não determinísticas dentro do corpo do orquestrador).
