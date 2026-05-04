# Design Philosophy — Agentic System

## Princípio Fundamental

> **"Agents compõem, não competem."**

O sistema é uma orquestração hierárquica de agentes especializados, onde cada camada tem responsabilidade clara e escopo limitado. Nenhum componente tenta fazer tudo — cada um faz uma coisa bem.

## Pilares

### 1. Hierarquia Deliberada

```
Tier 0 — MetaAgent (orquestra, nunca executa)
Tier 1 — Executors (tarefas atômicas, sem delegação)
Tier 2 — Specialists (domínio profundo, podem usar tools)
Tier 3 — Orchestrators (coordenam sub-agents)
```

**Regra**: A complexidade flui para baixo. O MetaAgent analisa, classifica e delega. Agents de baixo tier nunca chamam agents de tier superior.

**Motivo**: Evita ciclos, simplifica debugging, permite substituição de qualquer camada sem impacto nas demais.

### 2. RAG como Cidadão de Primeira Classe

O contexto não é um addon — é parte integral do pipeline de decisão.

```
Query → Compress → Retrieve → ReRank → Budget → Inject → Agent → Respond
```

Cada etapa é independente e substituível:
- **Compress** (ML9): Remove redundância antes do retrieval
- **Retrieve**: Busca vetorial por similaridade
- **ReRank**: Heurística de relevância
- **Budget** (ML2): Controla tokens gastos com contexto
- **Inject**: Monta prompt com contexto relevante

**Trade-off aceito**: Latência de ~50ms extra por query em troca de precisão significativamente maior.

### 3. Maturity Levels como Evolução Incremental

O sistema evolui em camadas de maturidade (ML1–ML10), cada uma adicionando uma capacidade sem quebrar as anteriores.

| ML | Capacidade | Princípio |
|----|-----------|-----------|
| 1 | Chunk Lifecycle | Dados têm prazo de validade |
| 2 | Context Budget | Contexto tem custo — otimize |
| 3 | Task Planning | Tarefas complexas se decompõem |
| 4 | Reflection | Agents aprendem com erros |
| 5 | Human Correction | Humanos refinam agents |
| 6 | Knowledge Freshness | Drift é inevitável — detecte-o |
| 7 | Confidence Score | Transparência > precisão ilusória |
| 8 | Semantic Compression | Histórico vira princípio |
| 9 | Query Compression | Menos ruído, mais sinal |
| 10 | User Personalization | Cada usuário é único |
| 11 | Dynamic Agent Creation | O sistema cresce com o usuário |
| 12 | Dynamic Handoffs | Delegação é cooperação, não falha |
| 13 | Session Consolidation | Esquecer com critério é lembrar melhor |
| 14 | Smart Routing | Decisão informada > decisão rápida |
| 15 | Setup Flow | Primeiras impressões definem adoção |
| 16 | Session Persistence | Dados de sessão sobrevivem ao processo |
| 17 | M.E.AI Adapter | Ecossistema aberto via padrão da indústria |
| 18 | Voice Interface | Acessibilidade é feature, não addon |

**Regra**: Cada ML funciona standalone. ML18 não depende de ML17. Um deploy pode ativar qualquer subconjunto.

### 4. Transparência sobre Confiança

O sistema expõe um `ConfidenceScore` em cada resposta (ML7). O usuário sabe quando o agent está incerto.

- `High` (>0.85): Resposta direta
- `Medium` (0.6–0.85): Resposta com caveats
- `Low` (0.3–0.6): Disclaimers explícitos
- `RequiresHumanReview` (<0.3): Não responde sozinho

**Motivo**: IA que parece confiante quando não é causa mais dano do que IA que admite limitação.

### 5. Human-in-the-Loop por Design

O `CorrectionLoop` (ML5) não é um fallback — é um mecanismo primário de evolução.

```
Human corrige → Sistema extrai regra → Regra aplica-se a futuras respostas
```

Regras são persistentes, escopadas por agent/domínio, e contáveis (TimesApplied). Regras que nunca disparam eventualmente expiram.

### 6. Personalização sem Lock-in

O `UserPreferenceEngine` (ML10) personaliza respostas sem forçar o usuário a um path fixo.

- Preferences são sugestivas, não mandatórias
- O usuário pode desativar a qualquer momento
- Satisfaction scores decaem (EMA α=0.3), evitando bias de primeiras impressões

### 7. Autonomia Progressiva (ML11-15)

Os maturity levels 11-15 transformam o sistema de um executor passivo em um organismo adaptativo:

- **ML11 — Dynamic Agent Creation**: O sistema cria agents especializados sob demanda via linguagem natural. Em vez de prever todo domínio antecipadamente, o sistema cresce com o uso.
- **ML12 — Dynamic Handoffs**: Agents delegam contexto entre si mid-conversation. Strategies: SingleDelegate, FanOut (paralelo), Chain (sequencial). Delegação é cooperação, não falha.
- **ML13 — Session Consolidation**: Sessões longas são comprimidas em summaries com extração de tópicos, agents utilizados e insights. Esquecer com critério é lembrar melhor.
- **ML14 — Smart Routing**: O SmartRouter combina intent, confiança, carga e especialidade do agent para tomar decisões de routing. Fallback automático se o agent primário falhar.
- **ML15 — Setup Flow**: Fluxo guiado de onboarding com validação por step. A primeira experiência define a adoção — o sistema guia, não assume.
- **ML16 — Session Persistence**: `ISessionStore` abstrai persistência de sessões. Default `InMemorySessionStore` para dev, `SqliteSessionStore` (file-based JSON) para produção leve. Swap via DI sem alterar consumers.
- **ML17 — M.E.AI Adapter**: `ChatClientProviderAdapter` faz bridge de qualquer `IChatClient` (Microsoft.Extensions.AI) para `ILLMProvider`. Zero config — registre o IChatClient e o adapter aparece automaticamente no ServiceGateway.
- **ML18 — Voice Interface**: `VoiceController` expõe `/api/voice/ask` com timeout de 7s e `StripMarkdown` para output TTS-friendly. Alexa/Google Assistant ready sem middleware adicional.

**Regra**: ML11-18 são composíveis. Um deploy pode usar handoffs sem smart routing, ou session consolidation sem agents dinâmicos.

### 8. Tools e Skills são Distintos

| | Tool | Skill |
|---|------|-------|
| O que é | Ação atômica | Pacote de conhecimento |
| Exemplo | Calculator, FileSearch | CodingAssistant, DevOps |
| Quem invoca | Agent | Agent (automaticamente) |
| Stateful | Não | Não |
| Registro | Runtime (IToolManager) | Runtime (ISkillManager) |

**Regra**: Se executa algo → Tool. Se enriquece o prompt → Skill.

## Anti-Patterns (Evitar)

### ❌ God Agent
Um agent que tenta processar todos os tipos de request. Use o MetaAgent para delegar.

### ❌ RAG Brute-Force
Injetar todo o contexto disponível no prompt. Use Context Budget (ML2) para limitar.

### ❌ Confidence Theater
Reportar confiança alta sem base factual. Calibre o ConfidenceScore com RAG coverage real.

### ❌ Preference Dictatorship
Forçar personalização sem opt-out. Profiles são sugestivos, não mandatórios.

### ❌ Correction Overfit
Criar regras de correção muito específicas que não generalizam. Scope broad primeiro, refine depois.

## Decisões Arquiteturais

### Por que Singleton para Maturity Services?

Maturity services mantêm estado em memória (ConcurrentDictionary/ConcurrentBag). Em produção, trocar por repositório persistente. Singleton garante consistência dentro do processo.

### Por que não usar MediatR para agents?

Agents precisam de orquestração hierárquica com contexto propagado. MediatR é fire-and-forget. O MetaAgent mantém o contexto de sessão e decide a delegação.

### Por que Heuristic ReRanker em vez de Cross-Encoder?

Cross-encoders têm qualidade superior mas latência de 200-500ms por query. O HeuristicReRanker opera em <5ms com qualidade suficiente para o caso de uso. Se necessário, trocar é uma única implementação de `IReRanker`.

### Por que compressão de query é pré-retrieval?

Comprimir depois do retrieval desperdiça tokens no vector search. Comprimir antes reduz ruído no embedding lookup e melhora precision@k.

## Roadmap Filosófico

1. **Completo**: Agents + RAG + Maturity Levels (ML1-10) + Agentic Autonomy (ML11-15)
2. **Próximo**: Multi-tenant profiles, persistent state, observability
3. **Futuro**: Agent marketplace, federated learning, self-healing agents
