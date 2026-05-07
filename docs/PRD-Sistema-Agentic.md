# Documento de Produto — Sistema Agentic

> Visão executiva do sistema para stakeholders, gestores e áreas de negócio.

---

## O que é

O Sistema Agentic é um **assistente de IA orientado a especialistas** com entrada unificada por chat, streaming e protocolos. A API abre a sessão, aplica governança e delega a execução principal para um orquestrador hospedado no Microsoft Agent Framework.

Em vez de um chatbot genérico que tenta responder tudo com a mesma abordagem, o Agentic funciona como uma **equipe de especialistas virtuais** coordenada por um runtime de orquestração, com memória, RAG, tools auxiliares e workflow colaborativo quando a tarefa exige mais de uma etapa.

---

## Problema que resolve

| Sem o Agentic | Com o Agentic |
|---|---|
| Usuário precisa saber qual ferramenta usar para cada tarefa | O sistema identifica a intenção e escolhe o especialista |
| Chatbots genéricos dão respostas rasas em temas técnicos | Cada especialista tem parâmetros calibrados para seu domínio |
| Sem memória entre conversas — o usuário repete contexto | Memória persistente: o sistema lembra preferências e histórico |
| Respostas inventadas sem aviso | Score de confiança visível — o sistema avisa quando não tem certeza |
| Uma única IA falha = tudo para | Múltiplos provedores com failover automático (OpenAI, Google, Anthropic, Ollama) |

---

## Como funciona (visão simplificada)

```
Usuário faz uma pergunta
        ↓
  API abre sessão + streaming
        ↓
  MetaAgentOrchestrator encaminha ao workflow
        ↓
  Orquestrador hospedado escolhe especialistas, contexto e tools
        ↓
  Resposta volta com confiança, auditoria e persistência
```

**Exemplo prático:**
- "Agende reunião com João amanhã às 14h" → Especialista de Calendário (precisão máxima)
- "Ideias criativas para campanha de Black Friday" → Especialista Criativo (criatividade máxima)
- "Analise esse relatório de vendas e extraia tendências" → Especialista de Análise (rigor máximo)

O usuário não precisa saber qual especialista existe — o sistema roteia automaticamente.

---

## Especialistas disponíveis

| Especialista | O que faz | Exemplo de uso |
|---|---|---|
| **Produtividade** | Calendário, tarefas, lembretes | "Me lembre de enviar o relatório sexta às 9h" |
| **Trabalho** | Email, documentos, reuniões | "Resuma os pontos da última reunião" |
| **Aprendizado** | Pesquisa, resumos, explicações | "Explique o conceito de margem de contribuição" |
| **Criatividade** | Brainstorming, escrita, ideação | "5 nomes para nossa nova linha de produtos" |
| **Análise** | Dados, insights, relatórios | "Qual a tendência de vendas dos últimos 3 meses?" |
| **Notificações** | Alertas e lembretes proativos | Avisos automáticos baseados em regras |
| **Protocolos & automação** | Conexão com superfícies externas | Orquestra MCP, A2A, AG-UI e compatibilidade OpenAI |

---

## Diferenciais

### 1. Transparência

Toda resposta vem com um **nível de confiança**:

| Nível | O que significa | O que o sistema faz |
|---|---|---|
| 🟢 Alto (>85%) | Alta certeza na resposta | Responde diretamente |
| 🟡 Médio (60-85%) | Boa confiança, com ressalvas | Responde com observações |
| 🟠 Baixo (30-60%) | Incerteza considerável | Responde com alertas explícitos |
| 🔴 Muito baixo (<30%) | Não sabe a resposta | **Não responde sozinho** — solicita revisão humana |

O sistema **nunca inventa** quando não sabe. Prefere admitir limitação a dar uma resposta errada.

### 2. Aprendizado contínuo

Quando o usuário corrige uma resposta, o sistema:
1. Registra a correção
2. Extrai uma regra a partir dela
3. Aplica a regra em respostas futuras

Quanto mais usado, mais preciso fica — **sem retreinamento manual**.

### 3. Memória inteligente

O sistema lembra:
- **Preferências do usuário** — estilo de comunicação, formato de resposta, idioma
- **Contexto de sessões anteriores** — decisões tomadas, temas discutidos
- **Documentos ingeridos** — manuais, políticas, bases de conhecimento

A memória é **híbrida**: notas legíveis em texto (Obsidian) + busca semântica por similaridade (pgvector).

### 4. Resiliência

- Se o provedor principal de IA cair, o sistema alterna automaticamente para outro
- Proteção contra falhas em cascata (circuit breaker)
- Controle de custos por sessão, por especialista e por dia
- Monitoramento de saúde em tempo real

### 5. Interface por voz

Compatível com assistentes de voz (Alexa, Google Assistant):
- Respostas limpas, sem formatação técnica
- Timeout de 7 segundos (adequado para interações por voz)
- Endpoint dedicado (`/api/voice/ask`)

---

## Capacidades por camada de maturidade

O sistema evolui em camadas independentes. Cada capacidade pode ser ativada ou desativada sem impactar as demais.

### Camada 1 — Fundação

| Capacidade | Benefício para o usuário |
|---|---|
| Ciclo de vida de dados | Informações antigas perdem relevância automaticamente — respostas sempre atualizadas |
| Orçamento de contexto | O sistema não gasta tokens desnecessários — custo controlado |

### Camada 2 — Inteligência

| Capacidade | Benefício para o usuário |
|---|---|
| Planejamento de tarefas | Tarefas complexas são decompostas em passos menores |
| Auto-reflexão | O sistema revisa a própria resposta antes de entregar |
| Correção humana | Feedback do usuário melhora respostas futuras automaticamente |

### Camada 3 — Qualidade

| Capacidade | Benefício para o usuário |
|---|---|
| Detecção de informação desatualizada | Avisa quando a base de conhecimento pode estar obsoleta |
| Score de confiança | Transparência total sobre a certeza de cada resposta |

### Camada 4 — Eficiência

| Capacidade | Benefício para o usuário |
|---|---|
| Compressão de sessões | Histórico longo vira resumo — sem perder os pontos-chave |
| Otimização de buscas | Perguntas mal formuladas são refinadas antes da consulta |

### Camada 5 — Personalização

| Capacidade | Benefício para o usuário |
|---|---|
| Perfil do usuário | O sistema adapta tom, formato e nível de detalhe às suas preferências |

### Camada 6 — Autonomia

| Capacidade | Benefício para o usuário |
|---|---|
| Criação dinâmica de especialistas | O sistema cria novos especialistas sob demanda via linguagem natural |
| Delegação entre especialistas | Tarefas multi-etapa podem ser tratadas por workflow colaborativo |
| Consolidação de sessões | Conversas longas viram memória de longo prazo automaticamente |
| Roteamento inteligente | Escolha do especialista considera histórico e performance, não só intenção |
| Onboarding guiado | Novo usuário é guiado passo a passo na configuração |

### Camada 7 — Extensões recentes

| Capacidade | Benefício para o usuário |
|---|---|
| Persistência de sessões | Sessões sobrevivem a reinicializações — nada é perdido |
| Superfícies abertas de protocolo | Clientes de chat e agents externos podem consumir o runtime por protocolos compatíveis |
| Interface por voz | Acessível via Alexa, Google Assistant ou qualquer cliente de voz |

---

## Integrações

| Sistema | O que faz |
|---|---|
| **REST + SignalR** | Consumo direto do produto com streaming e sessões |
| **MCP Plugins** | Conecta ferramentas e sistemas externos sob demanda |
| **A2A / AG-UI** | Integra agents e clientes protocol-aware |
| **OpenAI-compatible** | Compatibilidade com clientes que já falam o formato chat completions |

---

## Segurança

- Autenticação MultiAuth com API Key ou JWT
- Dados de sessão armazenados localmente (sem cloud terceira)
- Credenciais de provedores de IA isoladas por configuração
- Controle de acesso a endpoints administrativos
- Compatível com análise de segurança Checkmarx (SAST)

---

## Métricas de acompanhamento

| Métrica | O que mede |
|---|---|
| **Taxa de orquestração correta** | % de vezes que o runtime escolheu o fluxo e o especialista adequados |
| **Score médio de confiança** | Qualidade geral das respostas |
| **Custo por sessão** | Gasto com provedores de IA por interação |
| **Tempo de resposta (P50/P95)** | Velocidade de atendimento |
| **Taxa de fallback** | Frequência de troca automática de provedor |
| **Correções humanas/dia** | Volume de ajustes manuais (quanto menor, melhor) |
| **Regras ativas** | Quantidade de aprendizados extraídos de correções |
| **Cobertura de testes** | Saúde da suíte automatizada que protege fluxos centrais do backend |

---

## Status atual

| Item | Status |
|---|---|
| Runtime framework-first hospedado | ✅ Operacional |
| Especialistas e workflow colaborativo | ✅ Implementados |
| 4 provedores de IA (OpenAI, Google, Anthropic, Ollama) | ✅ Ativos |
| Memória híbrida (texto + semântica) | ✅ Funcional |
| Interface por voz | ✅ Disponível |
| Gateway com proteção e monitoramento | ✅ Ativo |
| Dashboard web de administração | ✅ Disponível |
| Deploy via Docker/Kubernetes | ✅ Pronto |
| Suíte automatizada do backend | ✅ Ativa |

---

## Glossário

| Termo | Significado |
|---|---|
| **Agent** | Especialista virtual otimizado para um domínio |
| **MetaAgentOrchestrator** | Fachada de entrada que gerencia sessão, streaming e encaminhamento |
| **Tier** | Nível hierárquico do especialista (0 = coordenador, 3 = operacional) |
| **Maturity Level (ML)** | Camada de capacidade do sistema, ativável independentemente |
| **RAG** | Retrieval-Augmented Generation — buscar contexto relevante antes de responder |
| **Confidence Score** | Nota de 0 a 1 indicando o quanto o sistema confia na própria resposta |
| **Circuit Breaker** | Mecanismo que desliga temporariamente um provedor com falhas, evitando efeito cascata |
| **Failover** | Troca automática para provedor reserva quando o principal falha |
| **Gateway** | Camada de proteção que controla acesso, custo e saúde dos serviços externos |
| **Session Store** | Onde as sessões de conversa são salvas (memória ou arquivo) |
| **MCP** | Model Context Protocol — padrão para conectar plugins de IA |
