# ADR-013: Uso de Nginx como Proxy Reverso com Suporte a WebSockets

## Status
Accepted (Retrospective)

## Context
O sistema possui um frontend SPA e um backend que se comunica em tempo real via WebSockets (SignalR). Precisávamos de um servidor web robusto para servir os arquivos estáticos e atuar como gateway/proxy reverso para a API.

## Decision
Adotamos o **Nginx** como proxy reverso, configurado especificamente para suportar o upgrade de conexões HTTP para **WebSockets**.

## Rationale
1. **Performance**: O Nginx é extremamente eficiente para servir arquivos estáticos do frontend.
2. **Suporte a SignalR**: A configuração correta de headers (`Upgrade` e `Connection`) é essencial para que o SignalR funcione sem fallback para polling lento.
3. **Segurança e SSL**: Facilita a centralização da terminação SSL e regras de CORS.

## Trade-offs
- **Mais um Componente**: Adiciona mais uma camada na infraestrutura (Frontend -> Nginx -> Backend).
- **Configuração**: Exige conhecimento específico de Nginx para evitar problemas de timeout em conexões longas.

## Consequences
- **Positive**: Comunicação em tempo real estável; excelente performance no carregamento do app.
- **Negative**: Necessidade de manter arquivos de configuração do Nginx sincronizados com as portas do backend.
- **Mitigation**: Uso de Docker Compose para orquestrar o Nginx e o backend na mesma rede virtual, facilitando a resolução de nomes.
