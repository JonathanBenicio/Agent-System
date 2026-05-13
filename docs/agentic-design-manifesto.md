# Agentic Design Manifesto & Philosophy

> Princípios fundamentais que guiam o design, a filosofia de arquitetura, a evolução e as decisões técnicas do Sistema Agentic.

---

## Declaração de Propósito

Este sistema existe para provar que **agentes de IA podem ser composíveis, transparentes e evolutivos** — sem sacrificar simplicidade. Cada decisão de design privilegia extensibilidade incremental, responsabilidade única por camada e soberania dos agentes sobre sofisticação prematura ou monolítica.

---

## Princípio Fundamental

> 💡 **"Agents compõem, não competem."**
> 
> O sistema é uma orquestração hierárquica de agentes especializados, onde cada camada tem responsabilidade clara e escopo limitado. Nenhum componente tenta fazer tudo — cada um faz uma única coisa com excelência.

---

## Os Pilares e Diretrizes Conceptuais

### 1. Hierarquia Deliberada (Tiers)

A complexidade flui de forma estrita para baixo. O `MetaAgent` analisa, classifica e delega. Agents de baixo tier nunca chamam ou dependem de agents de tier superior.

```
Tier 0 — Chief       → Analisa, roteia, orquestra (MetaAgent - nunca executa)
Tier 1 — Master      → Domínios funcionais amplos (pessoal, trabalho, aprendizado)
Tier 2 — Specialist  → Domínios profundos (criativo, análise, calendário)
Tier 3 — Support     → Operações utilitárias atômicas (notificações, APIs, executors)
```

*   **Implicação:** Trocar ou remover qualquer agent não impacta os demais. Evita ciclos de execução infinita, simplifica drasticamente o debugging e permite substituição de qualquer camada em runtime. O sistema é uma composição de micro-agentes, não um monolito disfarçado de IA.

---

### 2. Capacidades Nativas e Evolução (Core Baseline)

O sistema abandonou o modelo de "Platform Capabilities" (ML) isolados em favor de uma **Baseline de Framework Unificada**. Todas as capacidades técnicas agora são integradas nativamente via Microsoft Agent Framework (MAF) e Antigravity Kit.

#### Tabela de Capacidades do Ecossistema

| Domínio | Capacidade | Essência |
| :--- | :--- | :--- |
| **Foundation** | Chunk & Budget | Chunks expiram; contexto tem custo controlado via `ContextBudget`. |
| **Reasoning** | Adaptive Planning | Decomposição multi-step e roteamento baseado em Handoffs nativos. |
| **Quality** | Trust & Reflection | Score de confiança e auto-reflexão pós-resposta integrados via Middleware. |
| **Memory** | Semantic Memory | Compressão de sessões e histórico episódico via Obsidian + pgvector. |
| **Autonomy** | Native Workflows | Orquestração autônoma (Sequential, Concurrent, Handoff) via `AgentWorkflowBuilder`. |
| **Ops** | Native Hosting | Hosting resiliente com persistência de sessão isolada por agente (`AddAIAgent`). |

*   **Padrão de Ouro:** A arquitetura é orientada a **Capability Flags**. O sistema não é medido por "níveis", mas pela ativação de skills e protocolos (A2A, AG-UI, MCP) sobre o core estável.

---

### 3. Contexto (RAG) como Pipeline Nativo

O RAG não é um addon — ele é injetado via `AIContextProvider` de forma transparente.

```
Query → Compress → Retrieve → ReRank → Budget → Inject → Agent → Respond
```

Cada etapa é isolada e substituível:
*   **Compress :** Remove ruídos e redundâncias da query do usuário antes do vetorizador.
*   **Retrieve:** Busca vetorial robusta em bancos como PostgreSQL (pgvector).
*   **ReRank:** Re-ranqueamento neural com scoring de relevância heurístico ou ONNX Cross-Encoder.
*   **Budget :** Limita de forma rigorosa os tokens consumidos pelo contexto na janela do LLM.
*   **Inject:** Injeta os blocos de contexto como mensagens `system` estruturadas para evitar loops de tool calling.

> [!NOTE]
> **Trade-off aceito:** Aceitamos voluntariamente uma latência adicional de ~50ms no pipeline RAG para garantir uma precisão absurdamente superior. Respostas inventadas (alucinações) custam muito mais caro para a reputação do produto do que milissegundos adicionais de processamento estruturado.

---

### 4. Transparência de Confiança (Honestidade Algorítmica)

O sistema **nunca** finge saber o que não sabe. Todo output gerado carrega um `ConfidenceScore` calibrado :

| Score | Comportamento do Sistema |
| :--- | :--- |
| **High** (>0.85) | Resposta direta e sem ressalvas ao usuário. |
| **Medium** (0.6 – 0.85) | Resposta entregue acompanhada de advertências (*caveats*). |
| **Low** (0.3 – 0.6) | Resposta cercada de disclaimers e avisos de imprecisão explícitos. |
| **RequiresReview** (<0.3)| Recusa formal — o agente se cala e solicita intervenção humana imediata. |

*   **Filosofia:** IA que parece confiante quando está errada gera prejuízos reais. Honestidade algorítmica não é sinal de limitação — é uma feature de confiabilidade corporativa.

---

### 5. Human-in-the-Loop por Design

O `CorrectionLoop`  não é tratado como um fallback para falhas, mas sim como o motor primário de aprendizado contínuo do ecossistema.

```
Humano corrige → Sistema extrai regra → Regra aplica-se a futuras respostas → TimesApplied++
```

*   **Regras de Correção:** São persistentes, escopadas rigorosamente por usuário/agente/domínio, e contáveis. Regras que deixam de disparar ao longo do tempo decaem e expiram. O sistema aprende com ajustes humanos reais do dia a dia, eliminando dependência exclusiva de re-treinamento estático.

---

### 6. Personalização sem Lock-in

O `UserPreferenceEngine`  personaliza as interações de forma sugestiva e adaptativa, sem forçar o usuário a um fluxo engessado.

*   Perfis de preferência do usuário são usados para modular prompts, mas nunca para restringir escolhas.
*   O usuário possui total transparência e pode desligar a personalização com um clique (*opt-out*).
*   Os scores de satisfação decaem ao longo do tempo usando média móvel exponencial (EMA com $\alpha=0.3$) para evitar que o histórico inicial vicie as recomendações correntes.

---

### 7. Diferenciação Clara: Tools vs. Skills

Para manter a clareza arquitetural e evitar confusão de responsabilidades, diferenciamos rigorosamente as ferramentas (*Tools*) de capacidades comportamentais (*Skills*):

| Critério | Tool (Ferramenta) | Skill (Habilidade) |
| :--- | :--- | :--- |
| **O que é** | Ação atômica, imperativa e executável | Pacote de conhecimento temático/semântico |
| **Exemplo** | `Calculator`, `FileSearch`, `SendEmail` | `CodingAssistantPrompt`, `DevOpsKnowledge` |
| **Como atua** | É invocada ativamente pelo LLM do agente | Enriquece e formata as instruções do prompt |
| **Stateful** | Não | Não |
| **Gerenciador**| Registrado no `IToolManager` do runtime | Registrado no `ISkillManager` do runtime |

*   **Regra Prática:** Se executa código ou consulta uma API externa, é uma **Tool**. Se altera o comportamento, formata ou enriquece a inteligência e o prompt do agente, é uma **Skill**.

---

## Anti-Patterns — O que este sistema recusa ser

| Anti-Pattern | Descrição | Alternativa do Sistema |
| :--- | :--- | :--- |
| **God Agent** | Um agente massivo que tenta processar todas as intenções e fluxos. | O `MetaAgent` apenas analisa e delega, nunca executa nada diretamente. |
| **RAG Brute-Force** | "Inundar" o prompt com gigabytes de chunks de documentos brutos. | `ContextBudgetManager`  controla rigorosamente os limites e tokens de contexto. |
| **Confidence Theater** | Exibir score de confiança artificialmente alto sem base real. | `ConfidenceScore` calibrado matematicamente com base em cobertura RAG e acurácia. |
| **Preference Dictatorship** | Forçar respostas ultra-personalizadas sem chance de fuga. | Perfis de preferências são sugestivos, aceitando opt-out completo do usuário. |
| **Correction Overfit** | Criar regras de correção tão específicas que quebram outros fluxos. | Escopar de forma ampla e abstrata primeiro, refinando granularmente depois. |
| **Static Catalog** | Definir estaticamente todo catálogo de agentes em tempo de compilação. | `DynamicAgentService`  que cria e registra agentes sob demanda em runtime. |
| **Context Amnesia** | Perder o histórico e metadados ao delegar tarefas entre agentes. | Sessões estruturadas, channels compartilhados e `ISessionStore` nativo. |
| **Infinite Memory** | Manter discussões brutas longas infinitamente, poluindo o contexto. | `SessionConsolidator`  comprime logs brutos em summaries semânticos. |

---

## Decisões Arquiteturais Chave

### 1. Microsoft Agent Framework (MAF) nativo em vez de Orquestração Customizada
Sistemas de mensageria tradicionais (como MediatR puro) são fundamentalmente *fire-and-forget* ou de via única. Fluxos conversacionais com agentes exigem reidratação de estado por sessão, propagação transparente de contexto de tenant, e decisões complexas de delegação. O **Microsoft Agent Framework (MAF)** assume a gerência de ciclo de vida e hosting (`AddAIAgent()` e `InProcessExecution`), enquanto o roteamento ocorre nativamente via Tool Bindings.

### 2. Por que NÃO usar MediatR para a lógica interna de Agentes?
Apesar de ser excelente para desacoplamento em arquiteturas limpas tradicionais, o MediatR não oferece suporte a canais bi-direcionais estritos com histórico reidratado por chamada. O `MetaAgentOrchestrator` necessita reter controle sobre o fluxo conversacional por request para garantir auditoria, aplicar middlewares de barreira e acionar loops de correção antes de renderizar os tokens no hub SignalR.

### 3. Singleton para Maturity Services
Os serviços dos Platform Capabilities (como tracking de regras e histórico temporário) mantêm estado de alta performance em memória (`ConcurrentDictionary`/`ConcurrentBag`). O ciclo de vida como `Singleton` no container DI garante integridade transacional concorrente dentro do processo web, permitindo a substituição simples de sua infraestrutura por stores persistentes (PostgreSQL/Redis) alterando apenas suas implementações concretas de repositório.

### 4. Heuristic ReRanker em vez de Cross-Encoder Pesado
Cross-encoders profundos entregam qualidade excepcional de ranqueamento, mas introduzem uma penalidade severa de latência (200ms a 500ms por query). O `HeuristicReRanker` opera em menos de 5ms utilizando similaridade cosseno de embeddings combinada com ponderações de palavras-chave. Caso um caso de uso exija precisão extrema, a interface `IReRanker` permite a substituição indolor por Cross-Encoders locais via ONNX.

### 5. Query Compression pré-retrieval
Comprimir ou sumarizar dados após o retrieval de documentos é um desperdício inútil de tokens de contexto e dinheiro. Realizar a compressão da query antes da busca semântica reduz drasticamente ruídos no lookup de embeddings e maximiza a métrica de *precision@k* nos resultados retornados da base de dados vetorial.

### 6. Multi-provider LLM com failover automático
Qualquer provedor de linguagem (OpenAI, Gemini, Ollama, Claude) é consumido de forma padronizada sob o contrato de `IChatClient` (Microsoft.Extensions.AI). Caso ocorra uma falha de rede ou timeout (Circuit Breaker ativado no `ServiceGateway`), o pipeline redireciona a chamada para o provedor de contingência de forma transparente para o usuário final, eliminando lock-in técnico.

---

## Métricas de Saúde de Design e Engenharia

| Métrica | Meta Operacional | Como medir / Validar |
| :--- | :--- | :--- |
| **CanHandle Explícito** | 100% dos Agentes | Nenhum agente é registrado aceitando coringa `*` sem escopo definido. |
| **Independência de MLs** | 100% isolados | Garantir testes standalone para cada nível da suíte. |
| **Confiança Exposta** | 100% das respostas | Todo objeto `AgentResponse` enviado ao SignalR carrega o score numérico. |
| **Uso de Correção Humana**| > 0 regras ativas | Monitoramento de `CorrectionRule.TimesApplied > 0` no painel administrativo. |
| **Criação de Agentes Dinâmicos**| Suportado e ativo | Validação de registro via `DynamicAgentService.GetDynamicAgentsAsync()`. |
| **Sucesso de Delegação** | > 95% de acertos | Fração de requests que encontram o especialista no primeiro hop sem fallback. |
| **Consolidação de Sessão**| < 30 minutos em batch | Threshold máximo de processamento para logs de conversações pelo consolidador. |
| **Test Coverage** | 0 falhas | Suíte ampla de testes passando com sucesso (`dotnet test` verde). |

---

## Evolução Filosófica

```
v1.0 — Foundation 
       "Um sistema determinístico que responde com contexto relevante e aprende com correções humanas."

v2.0 — Autonomy 
       "Um ecossistema descentralizado que se adapta, cria novos agentes em runtime, delega tarefas e consolida sessões."

v3.0 — [Futuro / Próximos Passos]
       "Um organismo que se auto-otimiza, opera em federação descentralizada e corrige seus próprios bugs em runtime."
```

---

## Assinatura

Este manifesto é um documento vivo. Cada novo Platform Capability, cada decisão arquitetural relevante e cada anti-pattern combatido no código deve ser registrado aqui. O manifesto evolui com o sistema — nunca atrás dele.

> *"Automatizar o repetitivo para focar no criativo."*
> — Labs, Casas Bahia Tech
