# GEMINI.md - Backend Context (Tabatine Engine)

Este arquivo define as regras e padrões específicos para o desenvolvimento do Backend (.NET) no projeto Tabatine Engine.

---

## 🚀 Stack Tecnológica
- **.NET 10** / **C# 14**
- **EF Core** (Entity Framework Core)
- **PostgreSQL** (Supabase/Supavisor)
- **Worker Services** (Background processing)
- **xUnit** / **NSubstitute** (Testing)

---

## 🏗️ Arquitetura & Padrões

### Convenções Modernas de C#
- **File-scoped Namespaces**: Obrigatório.
- **Global Usings**: Gerenciado em `GlobalUsings.cs` na raiz de cada projeto.
- **Primary Constructors**: Obrigatório para Injeção de Dependência.
- **Records**: Obrigatório para DTOs (imultabilidade com `get; init;`).

### Naming & Idiomas
- **Entidades e Negócio**: Português (ex: `Cliente`, `PedidoVenda`).
- **Arquitetura e Infra**: Inglês (ex: `SyncService`, `DbContext`, `Repository`).
- **PascalCase**: Métodos, Propriedades, Classes, Interfaces (com prefixo `I`).
- **Private Fields**: `_camelCase` (apenas se não puder usar Primary Constructor).

### Database (EF Core)
- **Fluent API**: Obrigatório para mapeamento. Proibido usar Data Annotations (`[Column]`, `[Table]`).
- **Snake_case**: Automático via convenção. Não force manualmente.
- **Entidades Omie**: Devem herdar de `OmieEntityBase`.

---

## ⚙️ Lógica de Sincronização & Performance

### Memória (Regra Antigravity)
- **Proibido `List<T>`** para retornos massivos.
- **Uso Obrigatório de `IAsyncEnumerable<T>`** com `yield return` para paginação de APIs.

### Fluxo de Controle
- **Result Pattern**: Obrigatório. Nunca use exceções para controle de fluxo de negócio.
- **Caminho Nulo Crítico**: Sempre logue `LogError` quando um re-fetch crítico retornar `null`.

### API Omie
- **Rate Limits**: Respeite os limites (240 req/min, 4 simultâneas).
- **Incremental Sync**: Use `ISyncStateRepository` para gerenciar cursores de data.

---

## ✅ Qualidade & Testes
- **Framework**: xUnit.
- **Asserções**: Apenas nativas do xUnit. **FluentAssertions é proibido**.
- **Mocking**: NSubstitute.
- **AAA Pattern**: Arrange, Act, Assert.

---

## 🤖 Instruções para o Agente
Ao trabalhar no diretório `src/`:
1. Sempre verifique este arquivo.
2. Siga os padrões definidos em `.agents/rules/dot-net-standards.md`.
3. Garanta que novas entidades possuam configuração via Fluent API em `Data/Configurations/`.
