# ADR-015: Suporte ao Model Context Protocol (MCP)

## Status
Accepted (Retrospective)

## Context
Para que os agentes possam interagir com o mundo externo (ler arquivos, acessar bancos de dados, executar comandos, etc.) de forma padronizada e segura, precisávamos de um protocolo de comunicação que evitasse a criação de integrações ad-hoc para cada nova ferramenta.

## Decision
Adotamos o **Model Context Protocol (MCP)** como padrão para conectar nossos agentes a fontes de dados e ferramentas externas.

## Rationale
1. **Padronização**: O MCP é um padrão emergente (criado pela Anthropic) que está se tornando comum na indústria para estender capacidades de IAs.
2. **Reutilização**: Permite usar servidores MCP já existentes na comunidade sem precisar reescrever código.
3. **Segurança**: Define fronteiras claras sobre o que o agente pode ou não acessar através do protocolo.

## Trade-offs
- **Complexidade de Infraestrutura**: Exige rodar e gerenciar servidores MCP separados ou embutidos no sistema.
- **Latência**: Adiciona mais um salto de comunicação (Agente -> Protocolo MCP -> Ferramenta).

## Consequences
- **Positive**: Sistema altamente extensível; facilidade para adicionar novas habilidades aos agentes apenas plugando novos servidores MCP.
- **Negative**: Necessidade de documentar e mapear quais ferramentas estão disponíveis via MCP para cada agente.
- **Mitigation**: Implementação de um sistema de descoberta e registro de capacidades MCP para que os agentes saibam o que podem usar em tempo de execução.
