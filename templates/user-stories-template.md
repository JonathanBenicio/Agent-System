# US-[Número]: [Título da História. Ex: Hand-off entre agentes por intenção do usuário]

**Épico:** [Nome do Épico - ex: Orquestração Multi-Agent]  
**Prioridade:** [Alta | Média | Baixa]  
**Estimativa (Story Points):** [X]

## Descrição

**Como um** [ator: usuário / sistema cliente / MetaAgent],  
**Eu quero** [ação: que a plataforma redirecione a sessão para um agente especialista],  
**Para que** [resultado: perguntas complexas de código sejam respondidas pelo `code-reviewer` com precisão].

## Regras de Negócio e Contexto
* [Explique restrições do cenário. Ex: A transferência de contexto (hand-off) deve preservar o histórico da sessão.]

## Critérios de Aceite (DoD)

- [ ] **Critério 1:** O roteador de intenções (`Intent Router`) deve identificar corretamente a solicitação de review de código.
- [ ] **Critério 2:** O evento de `hand-off` deve ser disparado via SignalR para o frontend.
- [ ] **Critério 3:** O histórico da conversa (janela de contexto) deve ser repassado ao novo agente.

## Dependências Técnicas
* [ ] ADR-[XXX] (Decisão sobre estado da sessão)
* [ ] Endpoint `POST /api/agent/hand-off` precisa estar implementado.