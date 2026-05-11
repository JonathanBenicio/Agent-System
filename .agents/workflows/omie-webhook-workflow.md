---
description: Processamento de Webhooks Omie
---

# Workflow: Processamento de Webhooks Omie

Este fluxo descreve a separação de responsabilidades (Sepation of Concerns) exigida para lidar com os eventos da Omie de forma escalável.

## Fase 1: Ingestão (API Endpoint)
[cite_start]Esta fase deve ocorrer em **menos de 3 segundos** (margem de segurança para o timeout de 7s [cite: 23]).
1. **Receber POST:** O endpoint `/webhook/omie` recebe a solicitação HTTP POST contendo o payload do evento (ex: `VendaProduto.Novo`, `Faturamento.NotaFiscalEmitida`).
2. **Desserialização Mínima:** Fazer o parse do JSON para extrair metadados essenciais (AppKey, Evento, Identificador do Recurso).
3. **Persistência Rápida (Enfileiramento):** Salvar o payload bruto (raw JSON) em uma fila de mensageria local (Message Broker) ou tabela de banco de dados orientada a fila (ex: `SyncQueue`).
4. [cite_start]**Acknowledge:** Retornar imediatamente `Results.Ok()` (HTTP 200) para a Omie, liberando a fila FIFO deles[cite: 17, 30].

## Fase 2: Processamento (Background Worker)
Esta fase é executada por um `IHostedService` ou `BackgroundWorker` independente da requisição HTTP.
1. **Dequeue:** Consumir a próxima mensagem disponível na fila interna.
2. **Roteamento:** Identificar o tipo do evento (`request.Event`) e encaminhar para o serviço de sincronização correspondente (ex: `PedidoSyncService`, `NotaFiscalSyncService`).
3. **Execução da Regra de Negócio:**
   - Consultar API da Omie para dados complementares, se necessário.
   - Atualizar/Inserir dados no banco de dados local.
   - Disparar serviços de notificação (Email, Supabase, Telegram).
4. **Tratamento de Falhas (Local Retry):**
   - Em caso de exceção (ex: banco de dados local fora do ar), registrar erro via Logger.
   - Manter a mensagem na fila local ou enviá-la para uma DLQ interna para retentativa futura, sem afetar o webhook da Omie.
