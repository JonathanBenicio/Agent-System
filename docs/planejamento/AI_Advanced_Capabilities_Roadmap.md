# Roadmap de Capacidades Avancadas de IA

> **Status documental:** Roadmap de planejamento futuro.
> **Escopo:** sequenciar capacidades ainda não tratadas como parte da documentação viva do runtime atual.
> **Fonte de verdade operacional:** [../architecture/backend-architecture-explained.md](../architecture/backend-architecture-explained.md).
> **Nota de leitura:** nomes de vendors e plataformas citados como exemplo neste roadmap são ilustrativos e não representam compromisso de implementação do runtime atual.

> Gerado em: 2026-05-05  
> Projeto: AgenticSystem  
> Escopo: Planejamento de implantacao para GraphRAG, Semantic Caching, Self-Critique Loops e Event-Driven Autonomous Agents

---

## Objetivo

Consolidar um plano de execucao para a proxima onda de capacidades do AgenticSystem sem introduzir uma segunda arquitetura paralela. A diretriz deste roadmap e evoluir o runtime atual reaproveitando os componentes que ja existem no projeto, principalmente:

- `RAGService`
- `AgentExecutionWorkflow`
- `IReflectionEngine`
- `RuntimeEvaluatorService`
- `ScheduledTaskManager`
- pipeline de `IChatClient`
- governanca de approvals e artefatos do runtime

## Referencias de Arquitetura

- Diagnostico atual: [AI_Capabilities_Gaps.md](AI_Capabilities_Gaps.md)
- Arquitetura tecnica: [../TECHNICAL_ARCHITECTURE_GUIDE.md](../TECHNICAL_ARCHITECTURE_GUIDE.md)
- Fluxos e backlog funcional: [../USER-STORIES.md](../USER-STORIES.md)

## Principios de Implantacao

1. Toda capability nova precisa nascer atras de feature flag.
2. O fallback para o comportamento atual deve existir desde a primeira entrega.
3. Observabilidade e avaliacao entram antes da autonomia operacional.
4. O scheduler, o runtime e os quality gates existentes devem ser estendidos, nao substituidos.
5. Cada fase precisa sair com criterio de aceite objetivo e comparavel contra baseline.

---

## Core de Produto

Este roadmap nao redefine o runtime principal do AgenticSystem. O core de produto continua sendo a jornada padrao, obrigatoria e estavel do sistema:

- chat principal
- ciclo de vida de sessao
- streaming fim a fim
- um caminho principal de execucao
- observabilidade minima para operar o produto

Qualquer proposta deste roadmap que exija uma segunda arquitetura paralela, um segundo caminho padrao de chat ou um desvio permanente do fluxo principal deve permanecer fora do core e ser tratada como laboratorio por padrao.

## Trilhas de Laboratorio

As frentes abaixo entram como trilhas de laboratorio. Elas nao fazem parte do core por default e so podem tocar o fluxo principal por extensao controlada, atras de flag, com modulo separado e rollout opcional.

| Trilha | Papel no portfolio | Hipotese principal | Regra de isolamento |
|---|---|---|---|
| Semantic Caching | Eficiencia operacional | reduzir latencia e custo sem degradar qualidade | interceptar antes do provider, com bypass explicito e invalidacao por contexto |
| Self-Critique Loops | Qualidade em runtime | aumentar groundedness antes da entrega final | acionar por politica de risco, sem virar novo caminho obrigatorio global |
| GraphRAG | Retrieval relacional | melhorar perguntas trans-documentais e explicabilidade | operar como extensao/decorator do RAG atual com fallback imediato |
| Event-Driven Agents | Autonomia operacional | permitir execucao assincrona auditavel e idempotente | manter separado do request/response principal e protegido por policies/approvals |

`Fase 0 - Foundations Obrigatorias` e habilitadora dessas trilhas. Ela nao representa, por si so, uma nova capability de produto.

## Criterios de Incubacao e Descarte de Capacidades

| Decisao | Criterios minimos |
|---|---|
| Entrar em incubacao | hipotese explicita, criterio de sucesso, criterio de remocao, baseline medido, feature flag definida, fallback para o comportamento atual e dono responsavel pela avaliacao |
| Permanecer em laboratorio | ganho inicial promissor, mas ainda dependente de rollout parcial, instrumentacao comparativa ou modulo isolado para reduzir blast radius |
| Promover para o core | ganho recorrente e comprovado contra baseline, SLO estavel, operacao simplificada sem abrir segundo caminho principal, observabilidade suficiente e custo de manutencao menor que manter a capability como experimento |
| Descartar ou fazer rollback | ausencia de ganho mensuravel, regressao de custo/latencia/qualidade, aumento de risco operacional, necessidade de arquitetura paralela, ou manutencao mais cara do que remover a capability |

Regras de governanca para todas as capacidades experimentais:

1. Nenhuma capability entra em incubacao sem plano de remocao desde o inicio.
2. Nenhuma capability sai do laboratorio apenas por entusiasmo tecnico; precisa melhorar metricas de produto ou operacao.
3. Se a capability exigir excecoes permanentes ao chat principal, a sessao, ao streaming ou ao caminho principal de execucao, ela continua fora do core.
4. Se o rollback nao for barato e previsivel, a capability nao esta pronta para rollout real.

---

## Sequenciamento das Trilhas de Laboratorio

As frentes abaixo permanecem em laboratorio ate cumprirem os criterios de promocao definidos nesta secao.

| Ordem | Frente | Motivo do sequenciamento |
|---|---|---|
| 1 | Semantic Caching | Menor blast radius, retorno rapido em custo/latencia e reaproveita a stack atual de embeddings |
| 2 | Self-Critique Loops | Estende o workflow existente e melhora qualidade antes de ampliar autonomia operacional |
| 3 | GraphRAG | Mudanca mais profunda no pipeline de ingestao/retrieval, com dependencia forte de observabilidade e avaliacao |
| 4 | Event-Driven Autonomous Agents | Maior impacto operacional; deve entrar quando retrieval, critica e governanca ja estiverem maduros |

---

## Fase 0 - Foundations Obrigatorias para as Trilhas de Laboratorio

Antes das quatro frentes, consolidar a base minima de medicao e governanca.

### Entregas

- Padronizar feature flags por capability: `SemanticCache`, `SelfCritique`, `GraphRag`, `EventDrivenAgents`
- Expandir `RuntimeEvaluatorService` e `IAgentRuntimeCoordinator` para persistir metricas por etapa: cache hit, retrieval hit-rate, critique pass-rate, revisao por ferramenta e execucao autonoma
- Criar datasets de avaliacao por cenario: RAG factual, execucao com tools, resposta sensivel e incidente operacional
- Versionar contratos de metadata em `AgentResponse`, `RAGContext` e artefatos operacionais para suportar rollout incremental
- Definir SLOs por capability antes do desenvolvimento

### SLOs sugeridos

| Capability | SLO inicial |
|---|---|
| Semantic Caching | hit com latencia significativamente menor que o baseline do provider |
| Self-Critique | aumento de groundedness sem romper budget operacional por tier |
| GraphRAG | melhoria comprovada em perguntas trans-documentais |
| Event-Driven Agents | execucao assicrona idempotente, auditavel e recuperavel |

### Criterio de saida

Dashboard e telemetria prontos para comparar baseline versus capability ligada por flag.

---

## 1. GraphRAG (Knowledge Graph + RAG Vetorial)

### Por que implementar

O retrieval atual ja e maduro para similaridade vetorial, compressao semantica e expansao de query. O gap remanescente esta em relacoes trans-documentais: dependencia entre servicos, ownership, relacionamento entre classes, correlacao entre issue, endpoint e artefato tecnico.

### Objetivo

Transformar o retrieval atual de "chunk similar" em um retrieval orientado a evidencia relacional, no qual o vetor encontra o ponto de entrada e o grafo expande entidades, dependencias e ligacoes trans-documentais.

### Arquitetura-alvo

```text
Documento
	-> chunking atual
	-> entity/relation extraction
	-> graph projection

Query
	-> vector retrieval inicial
	-> entity resolution
	-> k-hop traversal com limites
	-> evidence pack (chunks + edges + provenance)
	-> re-rank/compressao
	-> prompt final
```

### Componentes propostos

| Camada | Componente | Papel |
|---|---|---|
| Ingestao | `IGraphExtractionService` | Extrair entidades, relacoes e aliases durante a ingestao |
| Persistencia | `IGraphStore` | Armazenar nos, arestas, tipos de relacao e proveniencia |
| Retrieval | `IGraphTraversalService` | Partir dos chunks vetoriais e navegar no grafo com limites de profundidade e custo |
| Orquestracao | `GraphRagService` ou decorator do `IRAGService` | Combinar retrieval vetorial, expansao relacional e compressao |
| Observabilidade | `GraphRagMetrics` | Medir cobertura relacional, custo de traversal e ganho de recall |

### Modelo de dados inicial

```text
graph_nodes
|- id
|- tenant_id
|- node_type          -- service | class | endpoint | concept | issue | person
|- canonical_name
|- aliases            -- jsonb
|- source_document_id
`- metadata           -- jsonb

graph_edges
|- id
|- tenant_id
|- from_node_id
|- to_node_id
|- relation_type      -- depends_on | owned_by | calls | mentions | blocks
|- weight
|- source_chunk_id
`- metadata           -- jsonb
```

### Plano por etapas

1. Spike de modelagem: definir ontologia minima por dominio e os 5 a 8 tipos de relacao que geram valor imediato.
2. Ingestao incremental: estender o pipeline atual para persistir chunk, entidades e relacoes sem quebrar o fluxo vetorial existente.
3. Retrieval hibrido: aplicar vector search como shortlist e so entao expandir por grafo com `maxDepth`, `maxNodes` e `maxTokens`.
4. Prompting: anexar ao contexto nao so texto, mas trilhas de proveniencia do tipo "A depende de B porque...".
5. Avaliacao: comparar GraphRAG versus RAG atual em cenarios trans-documentais e de codigo legado.

### Criterios de aceite

- Consultas trans-documentais superam o baseline vetorial em recall e groundedness.
- Toda evidencia relacional enviada ao prompt possui proveniencia para documento ou chunk.
- O traversal nunca extrapola o budget de contexto definido pelo `IContextBudgetManager`.
- A capability e desligavel por flag, com fallback automatico para o `RAGService` atual.

### Riscos e mitigacoes

| Risco | Mitigacao |
|---|---|
| Grafo superconectado e caro | Limites duros de profundidade, degree cap e filtros por tipo de relacao |
| Extracao ruidosa | Comecar com ontologia pequena e revisao humana das relacoes mais criticas |
| Duplicidade de entidades | Resolver aliases e canonicalizacao antes de habilitar traversal amplo |

---

## 2. Semantic Caching Automatico

### Por que implementar

O custo e a latencia de loops de prompt tendem a crescer com roteamento, RAG, tools e critica. Ha ganho claro em responder localmente quando a intencao semantica ja foi resolvida recentemente com contexto equivalente.

### Objetivo

Reduzir custo e latencia no hot path do LLM, respondendo localmente quando a intencao semantica ja tiver sido resolvida recentemente com alto grau de similaridade e contexto compativel.

### Decisao arquitetural recomendada

Meta de produto: cache vetorial com baixa latencia e TTL operacional.

Estrategia recomendada para o AgenticSystem:

1. Pilotar com a infraestrutura vetorial ja existente para validar politica de cache, invalidation e chave semantica.
2. Migrar para Redis apenas se o piloto nao atingir o SLO de latencia desejado ou se a concorrencia justificar um cache dedicado.

### Ponto de insercao

Inserir a decisao de cache antes do despacho final ao provider LLM, reaproveitando o pipeline atual de `IChatClient` e `LLMManager`.

### Componentes propostos

| Componente | Papel |
|---|---|
| `ISemanticCacheService` | API de lookup, upsert e invalidate |
| `SemanticCacheMiddleware` | Interceptar request, calcular embedding, procurar hit e short-circuitar resposta |
| `SemanticCachePolicy` | Definir escopo da chave: agent, model, tools, tenant, idioma, versao do system prompt |
| `SemanticCacheStore` | Backend inicial no store atual ou cache dedicado |
| `SemanticCacheMetrics` | Hit-rate, economia de tokens, latencia evitada e invalidacoes |

### Chave semantica minima

O cache nao deve depender apenas da frase do usuario. A identidade minima do item cacheado deve incluir:

- embedding da intencao ou pergunta
- agent selecionado
- versao do system prompt
- conjunto de tools disponiveis
- modelo ou roteador efetivo
- tenant e dominio

### Politica de uso

- Cachear apenas respostas com score minimo de qualidade e sem pendencia de aprovacao humana
- Nao cachear respostas com side effects ou que dependam de dados altamente volateis
- Aplicar TTL curto por padrao e invalidacao ativa quando houver mudanca de skill, tool, prompt ou fonte RAG relevante
- Guardar traco de proveniencia para explicar por que houve cache hit

### Plano por etapas

1. Criar servico e politica de identidade semantica.
2. Habilitar somente para perguntas informacionais sem tools destrutivas.
3. Publicar metricas de hit-rate e token savings.
4. Avaliar migracao do backend piloto para Redis com base em SLO real.
5. Expandir para cenarios multi-turn apenas apos validar consistencia em single-turn.

### Criterios de aceite

- Cache hit reduz latencia perceptivel sem degradar qualidade.
- Toda resposta servida do cache informa metadata de origem e versao do contexto.
- Mudancas de prompt, tool ou model invalidam entradas incompativeis.
- Respostas com tools, aprovacoes pendentes ou contexto sensivel seguem bypass explicito.

---

## 3. Agentic Reflection / Self-Critique Loops Nativo

### Por que implementar

O sistema ja faz reflexao e avaliacao pos-execucao. O proximo passo e mover parte desse aprendizado para dentro do fluxo principal, aumentando groundedness e qualidade antes da entrega final.

### Objetivo

Sair do modelo exclusivamente pos-mortem e introduzir um loop de critica em runtime, no qual a resposta e produzida em rascunho, criticada e eventualmente revisada antes de seguir para aprovacao final ou entrega ao usuario.

### Ponto de partida arquitetural

O projeto ja possui `IReflectionEngine`, `CorrectionLoopService`, `IQualityGateService` e `RuntimeEvaluatorService`. O plano deve estender esses componentes para critica sincrona no fluxo principal, sem substituir a reflexao operacional ja existente.

### Fluxo-alvo

```text
agent
	-> draft answer
	-> critique pass
	-> revision pass (opcional)
	-> quality gate
	-> final approval / resposta
```

### Componentes propostos

| Componente | Papel |
|---|---|
| `IResponseCritiqueService` | Executar critica estruturada sobre rascunho, evidencias e uso de tools |
| `CritiquePolicy` | Definir quando o loop e obrigatorio: tool use, baixa confianca, dominio sensivel, multiplos agentes |
| `CriticAgent` | Agente especializado em groundedness, uso correto de tool, completude e aderencia ao pedido |
| `RevisionService` | Aplicar feedback do critico e gerar versao revisada quando necessario |
| `CritiqueArtifact` | Persistir draft, critica, revisao e motivo de aceite ou reprovacao |

### Estrategia de rollout

1. Ativar so para cenarios com uso de tools ou baixa confianca.
2. Operar com no maximo 1 ciclo de revisao na primeira fase.
3. Persistir artefatos para auditoria via `IAgentRuntimeCoordinator`.
4. Integrar o resultado da critica ao score final e ao approval flow.
5. So depois expandir para multi-agent collaboration.

### Criterios de aceite

- O fluxo registra draft, critica e versao final com provenance suficiente para auditoria.
- O critico detecta pelo menos as classes principais de erro: tool nao validada, contradicao com contexto, omissao de passos mandatorios e extrapolacao sem evidencia.
- O loop possui termination condition explicita para evitar custo ou latencia nao controlados.
- O usuario continua recebendo resposta dentro do budget operacional definido por tier.

### Riscos e mitigacoes

| Risco | Mitigacao |
|---|---|
| Latencia excessiva | Acionar o loop apenas por politica de risco; limitar a 1 revisao inicialmente |
| Critico redundante com quality gate | Separar responsabilidades: quality gate valida contrato; critico valida raciocinio e evidencia |
| Overfitting ao critico | Rodar avaliacoes offline com exemplos onde a primeira resposta ja era suficiente |

---

## 4. Arquitetura Orientada a Eventos para Agentes Autonomos

### Por que implementar

Hoje o runtime e centrado em API e tarefas agendadas. A evolucao natural para operacao mais autonoma e adicionar workers reativos a eventos, mantendo governanca, idempotencia e rastreabilidade.

### Objetivo

Evoluir do modelo request/response centrado em API para um runtime hibrido, no qual agentes tambem possam operar como workers reativos a eventos, com governanca, idempotencia e trilha de auditoria.

### Principio de arquitetura

Nao acoplar agentes diretamente ao broker. Introduzir uma camada explicita de contrato de evento e execucao, preservando `ScheduledTaskManager`, approvals e metricas como blocos reutilizaveis.

### Componentes propostos

| Camada | Componente | Papel |
|---|---|---|
| Entrada | `IAgentEventBus` | Abstrair broker: RabbitMQ, Kafka ou Service Bus |
| Contrato | `AgentEventEnvelope` | CorrelationId, tenant, source, type, payload e dedup key |
| Execucao | `AgentEventWorker` | Consumir eventos, validar politicas e disparar workflow ou autonomous task |
| Orquestracao | `AgentRunbookWorkflow` | Encadear steps com o scheduler DAG-lite e handoffs |
| Seguranca | `AutonomousActionPolicy` | Restringir tools, side effects e dominios por tipo de evento |
| Auditoria | `AutonomousExecutionLog` | Persistir evento, decisoes, acoes, aprovacoes e compensacoes |

### Casos de uso iniciais

- Alerta operacional recebido de uma plataforma de observabilidade ou webhook interno
- Evento de PR ou issue para abertura de investigacao automatica
- Mudanca de estado em fila ou tarefa que dispara handoff entre agentes
- Reprocessamento programado de conhecimento quando uma fonte documental muda

### Estrategia de implantacao

1. Comecar por eventos observacionais e nao destrutivos.
2. Introduzir outbox, inbox e deduplicacao antes de qualquer acao externa automatica.
3. Mapear cada tipo de evento para uma policy de tools e approvals.
4. Reusar `ScheduledTaskManager` para runbooks multi-step em vez de criar um segundo orquestrador.
5. So liberar side effects externos automaticos quando auditoria e rollback estiverem prontos.

### Criterios de aceite

- Todo processamento assincrono e idempotente e correlacionavel ponta a ponta.
- Cada execucao autonoma registra evento de origem, politicas aplicadas, tools usadas e resultado final.
- Acoes de alto risco exigem approval humano ou politica explicita de autoaprovacao.
- Falhas transitorias entram no mecanismo de retry e dead-letter ja consolidado.

---

## Roadmap Integrado Sugerido

### Onda 1 - Fundamentos e Quick Wins

- [x] Fechar Fase 0 de telemetria, datasets e feature flags
- [x] Implantar semantic caching single-turn em modo informacional (Implementado com PostgresSemanticCacheService e SemanticCacheChatClient)
- [ ] Publicar baseline de custo, latencia e quality score por agente e tier

### Onda 2 - Qualidade em Runtime

- [ ] Implementar self-critique para casos com tools ou baixa confianca
- [ ] Persistir artefatos de draft, critica e revisao
- [ ] Ajustar `FinalResponseApproval` para considerar saida do critico

### Onda 3 - Retrieval Relacional

- [ ] Criar ontologia minima e `IGraphStore`
- [ ] Estender ingestao para extracao de entidades e relacoes
- [ ] Liberar GraphRAG com fallback para o pipeline vetorial atual

### Onda 4 - Autonomia Operacional

- [ ] Introduzir `IAgentEventBus` e envelopes idempotentes
- [ ] Reusar DAG-lite para runbooks dirigidos por evento
- [ ] Liberar primeiros workers autonomos somente em fluxos nao destrutivos

---

## Dependencias Cruzadas

| Capability | Depende fortemente de |
|---|---|
| Semantic Caching | embeddings consistentes, versionamento de prompt, tool e model, metricas por request |
| Self-Critique | reflection atual, quality gates, artefatos e budget de execucao |
| GraphRAG | ingestao extensivel, vector store estavel, budget manager e avaliacao comparativa |
| Event-Driven Agents | retry, dead-letter, approvals, observabilidade, politicas de tool e auditoria |

## Recomendacao Final

Se houver espaco para apenas uma frente imediata, comecar por **Semantic Caching**. Se a meta for aumentar qualidade antes de autonomia, priorizar **Self-Critique** logo depois. **GraphRAG** deve entrar quando houver corpus suficiente para justificar relacoes explicitas. **Event-Driven Agents** deve ser a ultima onda, porque amplia o raio operacional e exige governanca mais madura do que o modo request/response.
