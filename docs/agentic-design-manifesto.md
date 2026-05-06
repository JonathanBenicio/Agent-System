# Agentic Design Manifesto

> Princípios fundamentais que guiam o design, a evolução e as decisões técnicas do Sistema Agentic.

---

## Declaração de Propósito

Este sistema existe para provar que **agentes de IA podem ser composíveis, transparentes e evolutivos** — sem sacrificar simplicidade. Cada decisão de design privilegia extensibilidade incremental sobre sofisticação prematura.

---

## Os 10 Princípios

### 1. Agents compõem, não competem

Nenhum agent tenta fazer tudo. O MetaAgent analisa e delega. Specialists executam com profundidade. Support agents fazem operações utilitárias. A complexidade flui para baixo na hierarquia — nunca para cima.

```
Tier 0 — Chief       → Analisa, roteia, orquestra
Tier 1 — Master      → Domínios amplos (pessoal, trabalho, aprendizado)
Tier 2 — Specialist  → Domínios profundos (criativo, análise, calendário)
Tier 3 — Support     → Operações atômicas (notificações, APIs)
```

**Implicação**: Trocar ou remover qualquer agent não impacta os demais. O sistema é uma composição, não um monolito disfarçado de agents.

---

### 2. Maturidade é incremental, não monolítica

O sistema evolui em **15 Maturity Levels (ML)**, cada um adicionando uma capacidade sem quebrar as anteriores. Nenhum ML depende de outro ML para funcionar.

| Camada | MLs | Tema | Essência |
|--------|-----|------|----------|
| Foundation | ML1-2 | Dados & Custo | Chunks expiram. Contexto tem preço. |
| Intelligence | ML3-5 | Planejamento & Correção | Decompor, refletir, aceitar correção humana. |
| Quality | ML6-7 | Confiança & Frescor | Detectar drift. Expor incerteza. |
| Compression | ML8-9 | Eficiência | Comprimir sem perder semântica. |
| Personalization | ML10 | Adaptação | Cada usuário é único. |
| Autonomy | ML11-15 | Auto-evolução | Criar agents, delegar, consolidar, rotear, guiar. |

**Regra de ouro**: Um deploy pode ativar qualquer subconjunto de MLs. ML15 sem ML14? Funciona. ML7 sem ML3? Funciona. Cada ML é um capability flag independente.

---

### 3. Contexto é cidadão de primeira classe

RAG não é um addon — é o pipeline central de decisão. Cada resposta é fundamentada em contexto recuperado, ranqueado e orçado.

```
Query → Compress(ML9) → Retrieve → ReRank → Budget(ML2) → Inject → Agent → Respond
```

**Trade-off aceito**: +50ms de latência por query em troca de precisão significativamente maior. Respostas inventadas são mais caras que respostas lentas.

---

### 4. Transparência sobre confiança

O sistema **nunca** finge saber o que não sabe. Todo output carrega um `ConfidenceScore` (ML7):

| Score | Comportamento |
|-------|--------------|
| > 0.85 | Resposta direta |
| 0.6 – 0.85 | Resposta com caveats |
| 0.3 – 0.6 | Disclaimers explícitos |
| < 0.3 | Recusa — pede intervenção humana |

**Convicção**: IA que parece confiante quando não é causa mais dano do que IA que admite limitação. Honestidade algorítmica não é fraqueza — é feature.

---

### 5. Humanos refinam, não apenas consomem

O `CorrectionLoop` (ML5) é um mecanismo **primário** de evolução, não um fallback.

```
Humano corrige → Sistema extrai regra → Regra aplica-se a futuras respostas → TimesApplied++
```

Regras são persistentes, escopadas (agent/domínio), e auto-expirantes. Se uma regra nunca dispara, ela eventualmente morre. O sistema aprende com correções reais, não com treinamento estático.

---

### 6. O sistema cresce com o uso

Com ML11 (Dynamic Agent Creation), o sistema não precisa prever todo domínio antecipadamente. Usuários criam agents especializados via linguagem natural — e esses agents herdam o mesmo contrato, tier system e quality gates dos agents fixos.

```
"Crie um agente especialista em compliance"
→ DynamicAgentService.CreateAgentAsync()
→ LLM gera spec (tier, domain, keywords, temperature)
→ Factory registra em runtime
→ SmartRouter já sabe delegar para ele
```

**Filosofia**: O catálogo de agents é uma semente, não um teto.

---

### 7. Delegação é cooperação, não falha

Com ML12 (Delegação Dinâmica), agents delegam contexto entre si mid-conversation. Não é fallback — é arquitetura intencional.

| Strategy | Quando usar |
|----------|-------------|
| `SingleDelegate` | Um agent sabe quem é melhor para a subtarefa |
| `FanOut` | Múltiplas perspectivas em paralelo |
| `Chain` | Pipeline sequencial — output de um é input do próximo |

**Anti-pattern**: Agent que tenta resolver tudo sozinho para "não perder contexto". O orquestrador preserva contexto entre delegações por design, usando sessão estruturada, bindings e canais compartilhados.

---

### 8. Esquecer com critério é lembrar melhor

Com ML13 (Session Consolidation), sessões longas são comprimidas em summaries estruturados — tópicos discutidos, agents utilizados, insights extraídos. O histórico bruto pode ser descartado sem perda de semântica.

**Convicção**: Memória ilimitada não é memória útil. Curadoria ativa supera acumulação passiva.

---

### 9. Decisão informada sobre decisão rápida

O SmartRouter (ML14) não roteia por keyword match simples. Combina:

- **Intent analysis**: O que o usuário quer?
- **Confidence scoring**: Quão seguro é o match?
- **Agent capability**: O agent tem skills/tools para isso?
- **Load awareness**: O agent está sobrecarregado?
- **Fallback chain**: Se o primário falhar, quem assume?

**Trade-off aceito**: Routing leva mais tempo que keyword match. Mas rotear para o agent errado desperdiça muito mais.

---

### 10. A primeira experiência define a adoção

O SetupFlowManager (ML15) garante que novos usuários sejam guiados — não abandonados — na primeira interação. Fluxo com steps validados, progresso persistente e rollback por step.

**Convicção**: Nenhuma arquitetura sobrevive se o onboarding é hostil. A complexidade interna do sistema é invisível para quem está começando.

---

## Anti-Patterns — O que este sistema recusa ser

| Anti-Pattern | Por que evitamos | Alternativa |
|-------------|------------------|-------------|
| **God Agent** | Um agent que processa tudo | MetaAgent delega, nunca executa |
| **RAG Brute-Force** | Injetar todo contexto no prompt | Context Budget (ML2) controla custo |
| **Confidence Theater** | Reportar alta confiança sem base | ConfidenceScore calibrado com RAG coverage |
| **Preference Dictatorship** | Forçar personalização sem opt-out | Profiles são sugestivos, nunca mandatórios |
| **Correction Overfit** | Regras de correção ultra-específicas | Scope broad primeiro, refine depois |
| **Static Catalog** | Prever todos os agents na compilação | Dynamic Agent Creation (ML11) em runtime |
| **Context Amnesia** | Perder contexto em delegações | Sessão estruturada + canais entre agents preservam state (ML12) |
| **Infinite Memory** | Guardar tudo sem curadoria | Session Consolidation (ML13) comprime |

---

## Decisões Arquiteturais Chave

### Singleton para Maturity Services
MLs mantêm estado em `ConcurrentDictionary`. Singleton garante consistência dentro do processo. Em produção, substituir por repositório persistente é trocar uma única implementação.

### Heuristic ReRanker em vez de Cross-Encoder
Cross-encoders: 200-500ms/query, qualidade superior. Heuristic: <5ms/query, qualidade suficiente. A interface `IReRanker` permite trocar sem impacto no pipeline.

### MetaAgent em vez de MediatR
MediatR é fire-and-forget. Agents precisam de orquestração hierárquica com contexto propagado e decisão de delegação. O MetaAgent mantém sessão e decide routing.

### Query Compression pré-retrieval
Comprimir depois do retrieval desperdiça tokens no vector search. Comprimir antes reduz ruído no embedding lookup e melhora precision@k.

### Multi-provider LLM com failover automático
OpenAI, Gemini, Claude, Ollama — todos atrás da mesma interface `ILLMProvider`. Se um cai, o próximo assume. Sem vendor lock-in. O `ServiceGateway` controla circuit breaker, rate limiter e cost tracker.

---

## Métricas de Saúde do Design

| Métrica | Alvo | Como medir |
|---------|------|------------|
| Agents com CanHandle claro | 100% | Nenhum agent aceita `*` |
| MLs independentes | 100% | Cada ML tem testes standalone |
| Confidence exposta | 100% | Todo `AgentResponse` tem score |
| Human correction loop ativo | > 0 regras | `CorrectionRule.TimesApplied > 0` |
| Dynamic agents criados | > 0 em uso real | `DynamicAgentService.GetDynamicAgentsAsync()` |
| Delegation success rate | > 95% | Delegações que retornam resultado válido |
| Session consolidation | < 30 min para consolidar | `SessionConsolidator` processa em batch |
| Setup completion rate | > 80% | Usuários que completam todos os steps |
| Test coverage | 243 tests, 0 failures | `dotnet test` green |

---

## Evolução Filosófica

```
v1.0 — Foundation (ML1-10)
       "Um sistema que responde com contexto e aprende com correções."

v2.0 — Autonomy (ML11-15)
       "Um sistema que se adapta, delega, consolida e guia."

v3.0 — [Futuro]
       "Um sistema que se auto-otimiza, comercializa agents
        e opera em federação com outros sistemas."
```

---

## Assinatura

Este manifesto é um documento vivo. Cada novo Maturity Level, cada decisão arquitetural, cada anti-pattern identificado deve ser registrado aqui. O manifesto evolui com o sistema — nunca atrás dele.

> *"Automatizar o repetitivo para focar no criativo."*
> — BaIAninho, Casas Bahia Tech
