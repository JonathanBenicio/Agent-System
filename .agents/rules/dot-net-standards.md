# Padrões de Codificação — .NET 10

## Convenções de Nomes e Padrões C# Modernos

O agente DEVE utilizar os recursos mais recentes do C# para manter o código limpo e conciso:

- **File-scoped Namespaces**: Obrigatório em todos os arquivos. Não crie blocos `{ }` para namespaces.
- **Global Usings**: Use um arquivo `GlobalUsings.cs` na raiz de cada projeto para gerenciar namespaces comuns.
- **Primary Constructors**: Obrigatório para Injeção de Dependência.
- **Classes e Interfaces**:
  - Iniciais em Maiúsculo (PascalCase).
  - Interfaces começam com `I`.
- **Propriedades**: PascalCase.
- **Idiomas**:
  - **Domínio**: Usar o idioma predominante do projeto para nomes que refletem o domínio de negócio.
  - **Arquitetura e Infra**: Usar Inglês para padrões de projeto (EX: `Service`, `DbContext`, `Repository`, `Worker`).

### Exemplo
```csharp
namespace MyProject.Services;

public class MyService(IApiClient client, ILogger<MyService> logger) : IService
{
    public async Task DoWorkAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Work started...");
    }
}
```

---

## Fluxo de Controle e Tratamento de Erros (Result Pattern)

- **Result Pattern**: Utilize para operações que podem falhar previsivelmente.
- **Exceptions**: Reserve para falhas críticas de infraestrutura.
- **NUNCA utilize exceções para controle de fluxo de negócio.**

---

## Estrutura de Entidades (EF Core)

- **Mapeamento de Banco**: Use obrigatoriamente a Fluent API em classes isoladas de configuração.
- **Naming**: Siga os padrões de nomenclatura estabelecidos (ex: snake_case para o banco de dados).

---

## Coleções e Memória

- **Memory Safety**: Utilize fluxos assíncronos (`IAsyncEnumerable<T>`) para processamento de grandes volumes de dados.
- **Proibido `List<T>` em retornos massivos**.

---

## Padrões de Teste

- **Framework**: Utilize xUnit ou NUnit conforme a preferência do projeto.
- **Mocking**: Utilize NSubstitute ou Moq.
- **AAA Pattern**: Siga o padrão Arrange-Act-Assert em todos os testes.