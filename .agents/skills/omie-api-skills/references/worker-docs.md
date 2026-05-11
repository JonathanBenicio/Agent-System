# Documentação do Background Worker (Tabatine Engine)

Este documento descreve a arquitetura unificada de processamento em segundo plano do Tabatine Engine. O antigo worker baseado em temporizador (cron de 15 minutos) foi descontinuado em favor de uma **Fila de Eventos Persistente**, gerenciada pelo `WebhookProcessorWorker`.

---

## 1. Visão Geral da Arquitetura (Fila no Supabase)

O motor central de background da aplicação agora roda unicamente através da tabela `WebhookEvents` no banco de dados (Supabase/PostgreSQL). O `WebhookProcessorWorker` é o único serviço injetado no `Program.cs` para consumir essa fila.

### Por que uma fila no Banco de Dados?
- **Persistência**: Ao reiniciar o servidor, tarefas pendentes ("Pending") não são perdidas.
- **Rastreabilidade**: É possível acompanhar o histórico de sucesso ("Processed") e falhas ("Failed") nativamente.
- **Concorrência (`FOR UPDATE SKIP LOCKED`)**: O worker busca a próxima tarefa da fila e trava (lock) exclusivamente aquela linha no banco. Isso significa que podemos ter múltiplos containers rodando no Azure (escalabilidade horizontal) sem o risco de dois servidores processarem a mesma linha ao mesmo tempo.

---

## 2. Tipos de Gatilhos (Triggers)

O `WebhookProcessorWorker` reage a tudo o que é postado na tabela `WebhookEvents`. Atualmente, existem duas origens principais de tarefas:

### A. Gatilho Externo (Webhooks da Omie)
Notificações em tempo real enviadas pela plataforma Omie (`POST /webhook/omie`).
**Exemplos de Eventos:** 
- `VendaProduto.Novo`
- `VendaProduto.Alterado`
- `Faturamento.NotaFiscalEmitida`

*Comportamento*: O endpoint responde HTTP 200 instantaneamente (Fast Acknowledge) para não estourar o limite de 7 segundos da Omie, salvando o Payload na fila. O worker extrai o ID, aciona o `SyncByIdAsync` do recurso, e emite uma notificação (Telegram/Supabase).

### B. Gatilho Interno (Sincronização Manual Completa)
Substitui a antiga sincronização de 15 minutos em loop infinito. Agora é invocada sob demanda via API proprietária (`POST /api/sync/trigger`).
**Evento Gerado no Banco:**
- `System.ManualSync`

*Comportamento*: O endpoint salva este comando fictício na fila e retorna HTTP 202 (Accepted). Quando o Worker retirar essa tarefa da fila, o handler `SystemManualSyncWebhookHandler` executará a varredura completa nas APIs da Omie (Clientes, Produtos, Vendedores, etc) utilizando paginação e as datas presentes na tabela `IntegrationSyncState`.

---

## 3. Padrão Strategy (Roteamento Limpo)

Para não poluir o arquivo do Worker com múltiplos `if` ou `switch` complexos, foi implementada a injeção da `WebhookHandlerFactory`.

### Como Funciona:
1. O processo puxa a linha `Event="System.ManualSync"` do banco.
2. Repassa a string "System.ManualSync" para a fábrica (`handlerFactory.GetHandler(string)`).
3. A fábrica devolve a classe que implementa a interface `IWebhookEventHandler` cujos `SupportedEvents` batem com a solicitação.
4. O Worker apenas executa `.HandleAsync()`.

Se amanhã for necessário adicionar a sincronização em tempo real de "Ordens de Serviço", basta criar a classe `OrdemServicoWebhookHandler`, assinar os métodos e ela automaticamente entrará no ciclo da fila sem encostar na infraestrutura base.

---

## 4. Retentativas (Retry e DLQ)

O tratamento de falhas segue a premissa de **Dead Letter Queue local**:
- Se um processamento de handler soltar erro (`throw exception`): o worker captura o erro (`catch`), atualiza a coluna `Status` daquela linha para `"Failed"` e grava o motivo na coluna `ErrorMessage`.
- Graças a esse registro do erro direto no Payload original, futuramente é possível reprocessar essa fila filtrando onde o `Status = 'Failed'`.
