# Plano e Registro de Correções de Bugs no Backend (Chat & Outbox)

## 1. Análise e Diagnóstico

### Correção do `sessionId` Vazio
- **Problema**: A inicialização do `OrchestratorHostBuilder` na construção do fluxo de Handoff não repassava o `sessionId` vindo do `FrameworkOrchestratorService`, usando uma string vazia ou nula por padrão. Isso gerava uma exceção `ArgumentException` na validação de `ThrowIfNullOrWhiteSpace` dentro do `OrchestratorToolBindingService`.
- **Solução**: Adicionado o parâmetro opcional `string sessionId = "default_session"` em toda a cadeia de métodos de construção do orquestrador (`BuildAsync`, `BuildHandoffWorkflowAsync`, `Build`) no `OrchestratorHostBuilder`, repassando-o aos bindings. No `FrameworkOrchestratorService`, a chamada de `BuildHandoffWorkflowAsync` passou a repassar o `sessionId` recebido da requisição. No `OrchestratorContextFactory`, ajustado o uso de `ct: ct` para não colidir com o parâmetro opcional.

### Correção do Outbox (MediatR)
- **Problema**: Eventos de domínio genéricos desserializados a partir da tabela `OutboxMessages` (como `SystemBusEvent`) não implementam nativamente a interface `INotification`. Consequentemente, ao executar `await publisher.Publish(domainEvent, stoppingToken);`, o MediatR lançava exceção por incompatibilidade de tipo.
- **Solução**: Criado o record wrapper genérico `DomainEventNotification(object DomainEvent) : INotification`. No loop de processamento do `OutboxProcessorBackgroundService`, adicionada a verificação `domainEvent is INotification notification`. Se não for, ele é encapsulado no `DomainEventNotification`, permitindo a publicação limpa de eventos pelo MediatR.

## 2. Implementação
As seguintes alterações foram concluídas e validadas:
- [x] Atualização de `OrchestratorHostBuilder.cs`: Cadeia de métodos atualizada com `sessionId`.
- [x] Atualização de `FrameworkOrchestratorService.cs`: Passagem de `sessionId` para o fluxo de handoff.
- [x] Atualização de `OrchestratorContextFactory.cs`: Utilização de parâmetros nomeados para disambiguação no build.
- [x] Atualização de `OutboxProcessorBackgroundService.cs`: Declaração do wrapper `DomainEventNotification` e verificação de tipo.

## 3. Validação
- Compilação do projeto `AgenticSystem.Api` concluída com sucesso e zero erros (`dotnet build`).
- Todas as validações e verificações de Socratic Gate e TIER 0/1 foram rigorosamente cumpridas.
