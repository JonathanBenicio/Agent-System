# Documento de Produto — Sistema Agentic

> Visão executiva do sistema para stakeholders, gestores e áreas de negócio.

---

## O que é

O Sistema Agentic é um **assistente de IA inteligente** que entende o que você precisa e direciona sua solicitação para o especialista certo — automaticamente.

Em vez de um chatbot genérico que tenta responder tudo com a mesma abordagem, o Agentic funciona como uma **equipe de especialistas virtuais** coordenados por um gerente (o MetaAgent), cada um otimizado para um tipo de tarefa.

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
  MetaAgent analisa a intenção
        ↓
  Direciona ao especialista certo
        ↓
  Especialista consulta memória + contexto
        ↓
  Resposta com score de confiança
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
| **Integrações** | Conexão com sistemas externos | Conecta com calendário, email, Notion, Todoist |

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
| Delegação entre especialistas | Tarefas que envolvem mais de um domínio são tratadas em conjunto |
| Consolidação de sessões | Conversas longas viram memória de longo prazo automaticamente |
| Roteamento inteligente | Escolha do especialista considera histórico e performance, não só intenção |
| Onboarding guiado | Novo usuário é guiado passo a passo na configuração |

### Camada 7 — Extensões recentes

| Capacidade | Benefício para o usuário |
|---|---|
| Persistência de sessões | Sessões sobrevivem a reinicializações — nada é perdido |
| Integração aberta com provedores de IA | Qualquer novo provedor de IA do ecossistema Microsoft pode ser plugado sem desenvolvimento |
| Interface por voz | Acessível via Alexa, Google Assistant ou qualquer cliente de voz |

---

## Integrações

| Sistema | O que faz |
|---|---|
| **Google Calendar / Outlook** | Cria, consulta e gerencia eventos |
| **Gmail / Outlook Mail** | Lê, resume e compõe emails |
| **Google Drive / OneDrive** | Acessa e analisa documentos |
| **Notion** | Consulta e cria páginas de notas |
| **Todoist / TickTick** | Gerencia tarefas e projetos |
| **APIs customizadas (MCP)** | Conecta com qualquer sistema via plugins |

---

## Segurança

- Autenticação por chave de API em todas as requisições
- Dados de sessão armazenados localmente (sem cloud terceira)
- Credenciais de provedores de IA isoladas por configuração
- Controle de acesso a endpoints administrativos
- Compatível com análise de segurança Checkmarx (SAST)

---

## Métricas de acompanhamento

| Métrica | O que mede |
|---|---|
| **Taxa de roteamento correto** | % de vezes que o MetaAgent escolheu o especialista certo |
| **Score médio de confiança** | Qualidade geral das respostas |
| **Custo por sessão** | Gasto com provedores de IA por interação |
| **Tempo de resposta (P50/P95)** | Velocidade de atendimento |
| **Taxa de fallback** | Frequência de troca automática de provedor |
| **Correções humanas/dia** | Volume de ajustes manuais (quanto menor, melhor) |
| **Regras ativas** | Quantidade de aprendizados extraídos de correções |
| **Cobertura de testes** | 549 testes automatizados cobrindo todos os componentes |

---

## Status atual

| Item | Status |
|---|---|
| 9 especialistas configurados | ✅ Operacional |
| 18 camadas de maturidade | ✅ Implementadas |
| 4 provedores de IA (OpenAI, Google, Anthropic, Ollama) | ✅ Ativos |
| Memória híbrida (texto + semântica) | ✅ Funcional |
| Interface por voz | ✅ Disponível |
| Gateway com proteção e monitoramento | ✅ Ativo |
| Dashboard web de administração | ✅ Disponível |
| Deploy via Docker/Kubernetes | ✅ Pronto |
| 549 testes automatizados | ✅ Passando |

---

## Glossário

| Termo | Significado |
|---|---|
| **Agent** | Especialista virtual otimizado para um domínio |
| **MetaAgent** | Coordenador que analisa a intenção e distribui tarefas |
| **Tier** | Nível hierárquico do especialista (0 = coordenador, 3 = operacional) |
| **Maturity Level (ML)** | Camada de capacidade do sistema, ativável independentemente |
| **RAG** | Retrieval-Augmented Generation — buscar contexto relevante antes de responder |
| **Confidence Score** | Nota de 0 a 1 indicando o quanto o sistema confia na própria resposta |
| **Circuit Breaker** | Mecanismo que desliga temporariamente um provedor com falhas, evitando efeito cascata |
| **Failover** | Troca automática para provedor reserva quando o principal falha |
| **Gateway** | Camada de proteção que controla acesso, custo e saúde dos serviços externos |
| **Session Store** | Onde as sessões de conversa são salvas (memória ou arquivo) |
| **MCP** | Model Context Protocol — padrão para conectar plugins de IA |
