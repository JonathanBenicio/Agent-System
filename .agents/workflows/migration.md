---
description: Como criar e aplicar migrações do EF Core no Supabase
---

Siga estes passos para atualizar o esquema do banco de dados no Supabase.

### 1. Criar a Migração
No terminal, execute o comando a partir da pasta raiz do projeto.
- Substitua `NomeDaMigracao` por um nome descritivo (ex: `AddVendedoresTable`).

```powershell
dotnet ef migrations add NomeDaMigracao --project src/Tabatine.Infrastructure --startup-project src/Tabatine.Worker --context AppDbContext
```

### 2. Revisar o Código Gerado
Verifique os arquivos criados na pasta `src/Tabatine.Infrastructure/Migrations/`.
- Verifique se os nomes das colunas e tipos de dados estão corretos (ex: `long` para IDs Omie, `jsonb` se necessário).
- Anote o nome completo do arquivo gerado (ex: `20260328045453_AddMessageIdToWebhookEvents`). O formato é `{timestamp}_{NomeDaMigracao}`.

### 3. Aplicar a Migração

Existem **duas formas** de aplicar a migração. Use a que for mais adequada:

#### Opção A: Via `dotnet ef` (se a connection string local estiver configurada)
```powershell
dotnet ef database update --project src/Tabatine.Infrastructure --startup-project src/Tabatine.Worker
```
> Este comando aplica o SQL **e** registra na tabela `__EFMigrationsHistory` automaticamente.

#### Opção B: Via Supabase MCP `apply_migration` (se não houver connection string local)

> [!IMPORTANT]
> **Ao usar o MCP do Supabase para aplicar migrations, você DEVE também inserir o registro na tabela `__EFMigrationsHistory`.**
> Sem isso, o EF Core tentará reaplicar a migração no próximo deploy, causando erros.

1. Aplique o SQL da migração via `apply_migration` do Supabase MCP.
2. **Imediatamente após**, registre a migração na tabela de histórico:

```sql
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('<timestamp_NomeDaMigracao>', '<versao_dotnet>');
```

- `MigrationId`: O nome completo do arquivo de migração (ex: `20260328045453_AddMessageIdToWebhookEvents`).
- `ProductVersion`: A versão do EF Core usada no projeto. Consulte o `.csproj` do Infrastructure para saber a versão atual (ex: `10.0.5`).

### 4. Verificar no Supabase
Acesse o painel do Supabase e verifique se as tabelas e colunas foram criadas conforme o esperado.
- Se houver políticas de **RLS** configuradas, verifique se elas precisam ser ajustadas para a nova tabela.
- Confirme que a migração aparece na tabela `__EFMigrationsHistory`:
```sql
SELECT * FROM "__EFMigrationsHistory" ORDER BY "MigrationId" DESC LIMIT 3;
```

---

### Dicas Úteis
- **Remover última migração** (se ainda não aplicada): `dotnet ef migrations remove --project src/Tabatine.Infrastructure --startup-project src/Tabatine.Worker`
- **Script SQL Offline**: Se precisar do script SQL para rodar manualmente: `dotnet ef migrations script --project src/Tabatine.Infrastructure --startup-project src/Tabatine.Worker`

---

### Resolução de Problemas (Troubleshooting)

**ERRO: "O nome solicitado é válido, mas não foram encontrados dados do tipo solicitado" (DNS)**
- O host direto (`db.xxx.supabase.co`) é apenas IPv6. Se sua rede for IPv4, use o host do Pooler (`aws-1-sa-east-1.pooler.supabase.com`).

**ERRO: "ObjectDisposedException: Cannot access a disposed object (ManualResetEventSlim)"**
- Causado por conflito entre o handhake do Npgsql e o Pooler do Supabase.
- **Solução 1**: Adicione `No Reset On Close=true;Pooling=false;` na string de conexão.
- **Solução 2**: Se estiver usando migrações automáticas no startup (`ApplyMigrations`), utilize o serviço `IMigrator` diretamente para pular a verificação de existência de banco (`Database.Exists()`):

```csharp
// Em Microsoft.EntityFrameworkCore.Infrastructure
var migrator = dbContext.GetService<IMigrator>();
migrator.Migrate();
```

