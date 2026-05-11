---
trigger: model_decision
description: Padrões arquiteturais e de codificação obrigatórios para C# e .NET 10 no projeto Tabatine Engine.
---

# Padrões de Codificação — .NET 10 (Tabatine Engine)

## Convenções de Nomes e Padrões C# Modernos

O agente DEVE utilizar os recursos mais recentes do C# para manter o código limpo e conciso:

- **File-scoped Namespaces:** Obrigatório em todos os arquivos. Não crie blocos `{ }` para namespaces. Seguir o padrão `Tabatine.<Projeto>.<Pasta>`. EX: `Tabatine.Infrastructure.Services`.
- **Global Usings:** Use um arquivo `GlobalUsings.cs` na raiz de cada projeto para gerenciar namespaces comuns (EFCore, Entities, etc.). Evite poluir o topo de cada arquivo `.cs`.
- **Primary Constructors:** Obrigatório para Injeção de Dependência. Não declare campos privados explicitamente nem crie o construtor clássico a menos que seja estritamente necessário para validações complexas.
- **Classes e Interfaces**:
  - Iniciais em Maiúsculo (PascalCase).
  - Interfaces começam com `I`. EX: `ISyncService`.
- **Propriedades**: PascalCase (ER: `RazaoSocial`).
- **Campos Privados**: CamelCase com underscore (ER: `_dbContext`). (Utilizar APENAS quando o uso de Primary Constructors não for possível).
- **Idiomas**:
  - **Entidades e Negócio**: Usar Português para nomes que refletem o domínio do ERP (EX: `Cliente`, `PedidoVenda`, `ItemPedido`).
  - **Arquitetura e Infra**: Usar Inglês para padrões de projeto (EX: `SyncService`, `DbContext`, `Repository`, `Worker`).

### Exemplo (The Golden Path)
```csharp
// DO THIS: File-scoped e Primary Constructor
namespace Tabatine.Infrastructure.Services;

public class ClienteSyncService(IOmieClient omieClient, ILogger<ClienteSyncService> logger) : ISyncService
{
    public async Task SyncAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Sync started...");
    }
}
```

---

## Estrutura de Entidades (EF Core)

- Todas as entidades da Omie devem herdar de `OmieEntityBase`.
- **Mapeamento de Banco**: Use obrigatoriamente a Fluent API em classes isoladas de configuração. **NUNCA** use `DataAnnotation` (como `[Column]` ou `[Table]`) para forçar o `snake_case`. Confie na convenção automática do projeto.
- **Campos Obrigatórios**:
  - `Id`: `Guid` (Chave Primária).
  - `OmieId`: `long` (ID numérico vindo da Omie).
  - `CreatedAt`, `UpdatedAt`, `OmieUpdatedAt`.

---

## Fluxo de Controle e Tratamento de Erros (Result Pattern)

- **NUNCA utilize exceções para controle de fluxo de negócio.** Lançar exceções (`throw new Exception()`) é extremamente custoso em termos de alocação de memória e performance no .NET.
- Utilize o **Result Pattern** (ex: `Result<T>`) para operações de integração que podem falhar previsivelmente (como um 404 da Omie ou erro de validação).
- Reserve os blocos `try/catch` APENAS para exceções não tratadas do framework (ex: falhas graves de I/O, banco fora do ar).

---

## Log e Monitoramento

- Usar `ILogger<T>` injetado via construtor principal.
- **Log Levels**:
  - `Information`: Início/fim de processos e marcos importantes (ex: sincronizada página X).
  - `Warning`: Erros recuperáveis ou inconsistências leves.
  - `Error`: Falhas críticas que interrompem a sincronização de um registro ou processo.
- **Table Logs**: Registros críticos de auditoria devem ser salvos na tabela `Logs` do banco de dados (usando `LogEntry`).

### Regra do Caminho Nulo Crítico

- **Regra:** Qualquer operação crítica (ex: update final de entidade, processamento de evento) que faça re-fetch (`FindAsync`, `FirstOrDefaultAsync`) DEVE logar em nível `Error` quando o retorno for `null`. O bloco `if (entity == null) { return; }` silencioso **é proibido** em processos críticos.

```csharp
// ✅ CORRETO: Log de erro + return explícito
var dbEvent = await dbContext.WebhookEvents.FindAsync(id, ct);
if (dbEvent == null)
{
    logger.LogError("WebhookEvent {Id} não encontrado. Evento pode ter sido perdido.", id);
    return;
}

// ❌ PROIBIDO: Silêncio no caminho nulo crítico
if (dbEvent != null) { /* processa */ } // O null não é logado
```

---

## Estruturas de Dados e Payloads (DTOs)

A forma como os dados transitam entre a API da Omie e o nosso domínio deve ser estritamente controlada:

- **DTOs devem ser `record`**: Qualquer classe na pasta `Models` ou no `Tabatine.Omie.Client` que represente um payload JSON de entrada/saída DEVE ser um record imutável.
- **Propriedades de DTOs**: Devem possuir `{ get; init; }` e utilizar o atributo `[JsonPropertyName("nome_campo_omie")]` para mapeamento seguro.

### Exemplo (DTO Padrão)
```csharp
namespace Tabatine.Omie.Client.Models.Clientes;

public record ClienteDto
{
    [JsonPropertyName("codigo_cliente_omie")]
    public long CodigoClienteOmie { get; init; }
    
    [JsonPropertyName("razao_social")]
    public string RazaoSocial { get; init; } = string.Empty;
}
```

---

## Coleções e Memória (Regra Antigravity)

Sincronizar milhares de registros de um ERP exige cuidado extremo com a memória (LOH - Large Object Heap).

- **Proibido `List<T>` em retornos massivos**: Nunca retorne listas genéricas em métodos que buscam dados paginados da Omie.
- **Uso Obrigatório de `IAsyncEnumerable<T>`**: Para listagens (Skills de paginação), utilize fluxos assíncronos com `yield return`. Isso mantém o footprint de memória constante.

### Exemplo de Paginação Correta
```csharp
// DO THIS: Lazy evaluation saving memory
public async IAsyncEnumerable<Cliente> FetchClientesAsync([EnumeratorCancellation] CancellationToken ct)
{
    int page = 1;
    while (true)
    {
        var response = await omieClient.ListarClientesAsync(page, ct);
        if (response.Clientes.Count == 0) break;

        foreach (var dto in response.Clientes)
        {
            yield return MapToDomain(dto);
        }
        page++;
    }
}
```

---

## Tratamento de API (Omie)

- **Models**: Todos os DTOs de request/response devem estar em `Tabatine.Omie.Client.Models` (conforme as regras de Estrutura de DTOs acima).
- **Sync Logic**:
  - Sempre buscar o último estado de sincronização via `ISyncStateRepository`.
  - Usar `OmieTimestampHelper` para converter datas da Omie (`DAlt`, `HAlt`) para `DateTime`.
  - Implementar lógica de "Upsert" (verificar se o `OmieId` existe antes de criar).
  - **Dica**: No update, compare o `OmieUpdatedAt` para evitar chamadas redundantes ao banco de dados.

---

## Padrões de Teste

- **Framework de Testes**: Utilize **xUnit**.
- **Asserções**: Utilize exclusivamente as asserções nativas do **xUnit** (`Assert.Equal`, `Assert.NotNull`, etc.).
- **PROIBIÇÃO (FluentAssertions)**: O uso da biblioteca `FluentAssertions` é **ESTRITAMENTE PROIBIDO** devido a mudanças de licenciamento. NUNCA adicione este pacote ou utilize a sintaxe `.Should()`.
- **Mocking**: Utilize **NSubstitute**.
- **AAA Pattern**: Siga o padrão Arrange-Act-Assert em todos os testes.
