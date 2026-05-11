# Banco de Dados e Persistência

Este documento define as regras estritas para modelagem de banco de dados e persistência no projeto.

---

## 🏗️ Modelagem e Schema

- **Naming Convention**: Tabelas e colunas devem seguir o padrão estabelecido (ex: `snake_case` para PostgreSQL).
- **Chaves Primárias**: Utilize preferencialmente UUIDs ou chaves substitutas adequadas.
- **Timestamps**: Inclua `created_at` e `updated_at` em todas as tabelas principais.

---

## 🛠️ ORM e Mapeamento

- **Fluent API**: Utilize mapeamento explícito (Fluent API) em vez de Data Annotations para manter as entidades limpas.
- **Configurações Isoladas**: Crie classes de configuração separadas por entidade.
- **Relacionamentos**: Defina explicitamente as chaves estrangeiras e o comportamento de deleção (Cascade, Restrict, etc.).

---

## ⚙️ Performance e Consultas

- **Indexes**: Crie índices em colunas usadas frequentemente em filtros e joins.
- **N+1 Queries**: Evite carregamento preguiçoso (Lazy Loading) em loops. Utilize carregamento antecipado (Eager Loading).
- **Projections**: Selecione apenas as colunas necessárias (`Select` no LINQ ou campos específicos no SQL).

---

## 🛡️ Segurança e Integridade

- **RLS (Row Level Security)**: Se estiver usando Supabase ou PostgreSQL com suporte a RLS, garanta que as políticas estejam configuradas corretamente.
- **Migrations**: Gerencie todas as alterações de esquema através de ferramentas de migração controladas por versão.
- **Tratamento de Concorrência**: Utilize tokens de concorrência ou versionamento se necessário.

---

## 🤖 Instruções para o Agente
1. Sempre verifique o arquivo de mapeamento da entidade antes de sugerir alterações no banco.
2. Siga rigorosamente as convenções de nomenclatura do projeto.
3. Priorize a performance em consultas massivas, utilizando paginação e streaming de dados.