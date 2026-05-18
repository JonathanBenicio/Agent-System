# ADR 020: Decisão Arquitetural - Padronização e Exposição de Protocolos (A2A e AgUI)

**Status:** Proposto  
**Data:** 18 de Maio de 2026  
**Autor:** Gemini CLI (em colaboração com o usuário)

---

## Contexto

O AgenticSystem foi concebido para ser uma plataforma interoperável. Já possuímos suporte básico para os protocolos **A2A (Agent-to-Agent)** e **AgUI (Agentic UI)** da Microsoft, permitindo que agentes internos sejam consumidos por sistemas externos ou interfaces protocol-aware.

No entanto, a exposição atual é "all-or-nothing" (ligada via Feature Flag global) e carece de metadados ricos exigidos pelos clientes desses protocolos. Além disso, não há uma interface para gerenciar quais agentes estão expostos e sob quais políticas de segurança.

## Decisão

Padronizaremos a infraestrutura de Protocol Hosting através de um **Unified Protocol Surface Manager**:

1.  **Capability Flags por Agente:** Adicionaremos flags na especificação do Agente (`IsA2AEnabled`, `IsAgUIEnabled`) para permitir exposição seletiva.
2.  **Metadata Provider:** Implementaremos um serviço que gera os manifestos JSON exigidos pelo protocolo AgUI (capabilities, actions sugeridas, schema de input) dinamicamente.
3.  **Scoped Runtime Bridge:** Refinaremos o `ScopedAgentProxy` para garantir que sessões iniciadas via `/a2a` ou `/agui` respeitem rigorosamente o contexto de `TenantId` e `UserId` vindo dos tokens JWT.
4.  **UI de Gerenciamento:** Criação de uma página `/admin/protocols` para visualizar endpoints, status de saúde e métricas de chamadas via protocolo.

## Justificativa

### Por que esta abordagem?

1.  **Interoperabilidade de Mercado:** Facilita a integração com o ecossistema Microsoft (Copilot Studio, Power Platform) e outros orquestradores que falam A2A.
2.  **Segurança Granular:** Sai de um modelo global para um modelo onde o administrador decide exatamente qual agente pode ser visto por sistemas externos.
3.  **Desenvolvimento agnóstico de UI:** O protocolo AgUI permite que criemos interfaces ricas que se adaptam automaticamente às capacidades do agente, sem precisar redeploy do frontend para cada nova "skill".

## Consequências

### Positivas

*   **Ecossistema Aberto:** Transforma o AgenticSystem em um hub de serviços de IA consumíveis por qualquer aplicação.
*   **Melhor Descoberta:** Permite que clientes automáticos façam introspecção das capacidades dos agentes.
*   **Isolamento de Sessão:** Garante que interações via protocolo sejam tão seguras quanto via ChatHub (SignalR).

### Desafios / Pontos de Atenção (Negativas)

*   **Versionamento de Protocolos:** A2A e AgUI ainda estão em evolução; mudanças no padrão podem exigir refatoração.
*   **Complexidade de Autenticação:** Exige que sistemas externos possuam tokens de serviço válidos e configurados no sistema.

---
*Nota: Este documento suporta a Track 3 do Roadmap Q2 2026.*
