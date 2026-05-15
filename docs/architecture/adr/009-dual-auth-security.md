# ADR-009: Fundação de Segurança e Estratégia de Dual Auth

## Status
Accepted (Retrospective)

## Context
O sistema precisa atender a dois tipos de consumidores: usuários humanos (via interface web) e outros sistemas ou agentes programáticos. Cada um exige um método de autenticação adequado para garantir segurança e usabilidade.

## Decision
Adotamos uma estratégia de **Dual Auth** (Autenticação Dupla/Híbrida), combinando cookies seguros (HttpOnly) para a sessão do usuário na interface web e API Keys/Tokens JWT para a comunicação entre serviços e agentes.

## Rationale
1. **Segurança Web**: Cookies HttpOnly mitigam ataques de XSS (Cross-Site Scripting) no frontend.
2. **Flexibilidade**: API Keys são ideais para automação e integração entre sistemas (M2M).
3. **Isolamento**: Permite revogar o acesso de um agente sem afetar a sessão do usuário humano.

## Trade-offs
- **Complexidade de Implementação**: Exige manter e validar dois fluxos de autenticação distintos no backend.
- **Gestão de Segredos**: Requer um sistema seguro para gerar, armazenar e revogar API Keys.

## Consequences
- **Positive**: Arquitetura segura e preparada para crescimento tanto no front quanto no ecossistema de APIs.
- **Negative**: Duplicidade de lógica em alguns middlewares de autorização.
- **Mitigation**: Unificação da representação do "Principal" (Usuário/Agente) após a autenticação para que as regras de autorização baseadas em claims funcionem de forma idêntica.
