---
description: Como gerenciar migrações de banco de dados (EF Core)
---

# Workflow: Migrações de Banco de Dados

Siga este workflow para adicionar ou atualizar o esquema do banco de dados utilizando EF Core.

### 1. Criar a Migração
Execute o comando para gerar a nova migração baseada nas alterações das entidades na pasta correta.

```bash
dotnet ef migrations add NomeDaMigracao --project src/AgenticSystem.Infrastructure --startup-project src/AgenticSystem.Api --output-dir Persistence/Migrations
```

### 2. Validar a Migração
Verifique os arquivos criados na pasta de migrações.
- Verifique se os nomes das colunas e tipos de dados estão corretos.
- Certifique-se de que relacionamentos e índices foram detectados.

### 3. Aplicar a Migração
Aplique as alterações ao banco de dados local para teste.

```bash
dotnet ef database update --project src/AgenticSystem.Infrastructure --startup-project src/AgenticSystem.Api
```

### 4. Boas Práticas
- **Commits Separados**: Tente manter a migração e as mudanças nas entidades no mesmo commit.
- **Rollback**: Se algo der errado, use `dotnet ef database update <MigracaoAnterior>`.
- **Zero-Downtime**: Para alterações em produção, considere se a mudança quebra a compatibilidade com a versão atual do código.

---

## Dicas Adicionais

- **Remover última migração** (se ainda não aplicada): `dotnet ef migrations remove`
- **Script SQL Offline**: Se precisar do script SQL para rodar manualmente: `dotnet ef migrations script`
