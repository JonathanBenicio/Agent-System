# ADR-012: Esquema de Banco de Dados com Suporte a Multi-Tenant e Memória de Agente

## Status
Accepted (Retrospective)

## Context
Para que o sistema seja escalável e possa atender a diferentes clientes ou departamentos (tenants) isoladamente, e para que os agentes mantenham aprendizado e contexto ao longo do tempo, o banco de dados precisava suportar essas capacidades desde a base.

## Decision
Projetamos o esquema do banco de dados (via EF Core) com suporte nativo a **Multi-Tenancy** (isolamento de dados por cliente) e tabelas dedicadas para **Memória de Longo Prazo** dos agentes.

## Rationale
1. **Isolamento e Segurança**: Garante que os dados de um tenant não sejam acessados por outro.
2. **Continuidade do Agente**: A memória persistida permite que o agente "lembre" de interações passadas, preferências do usuário e aprendizados específicos.
3. **Escalabilidade**: Uma estrutura bem definida facilita a shardização ou migração de dados no futuro.

## Trade-offs
- **Complexidade nas Queries**: Todas as consultas precisam garantir o filtro por `TenantId` (geralmente via Global Query Filters no EF Core).
- **Gerenciamento de Memória**: Decidir o que guardar na memória do agente e o que descartar para evitar crescimento infinito da base.

## Consequences
- **Positive**: Sistema preparado para SaaS (Software as a Service); agentes muito mais inteligentes e contextuais.
- **Negative**: Risco de vazamento de dados se o filtro de tenant for esquecido em alguma query customizada.
- **Mitigation**: Uso de filtros globais no EF Core que aplicam automaticamente o `TenantId` baseado no contexto da requisição.
