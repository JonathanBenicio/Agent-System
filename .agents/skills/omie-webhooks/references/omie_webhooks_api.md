# Referência Técnica: Integração de Webhooks Omie

## 1. Visão Geral do Sistema

O Webhook da Omie é um sistema de notificação passiva enviado quando eventos específicos ocorrem no aplicativo. O Omie agrupa os eventos para cada aplicativo e endpoint, enviando as notificações através de solicitações `HTTP POST`.

## 2. Regras de Processamento e Fila (CRÍTICO)

- **Ordem de Execução:** Os eventos são processados sequencialmente em formato **FIFO** (First In, First Out).
- **Confirmação de Sucesso:** Para que o evento seja considerado processado e removido da fila, o endpoint do Webhook deve retornar obrigatoriamente um código de resposta **HTTP 2XX** (como `200 OK`).
- **Bloqueio de Fila (Head-of-Line Blocking):** Se uma solicitação `POST` for malsucedida, nenhum outro `POST` do mesmo grupo será tentado. Os eventos subsequentes ficarão adiados na fila até que o primeiro evento problemático seja processado com sucesso.

## 3. Sistema de Retentativas e Timeouts (Retry Policy)

Se o endpoint não retornar um código `2XX` ou o tempo limite (timeout) for estourado, o Omie refaz a solicitação `POST` em intervalos regulares. A arquitetura possui duas filas:

### Fila Principal
- **Timeout:** 7 segundos
- **Limite de tentativas:** 3
- **Intervalo entre tentativas:** 1 a 4 segundos

*Se as tentativas falharem, o evento é movido para a Dead Letter Queue.*

### Dead Letter Queue (DLQ)
- **Timeout:** Até 20 segundos
- **Limite de tentativas:** Até 5 dias
- **Intervalo entre tentativas:** 1 tentativa a cada 10 minutos

## 4. Diretrizes de Arquitetura para o Código Gerado

- **Retorno Rápido:** O código do endpoint deve retornar o código `HTTP 2XX` o mais rápido possível para evitar timeouts de **7 segundos**.
- **Processamento Assíncrono:** O sistema deve apenas aceitar e armazenar os dados do payload recebido para que o processamento da regra de negócio ocorra posteriormente, garantindo que eventos não sejam perdidos.

## 5. Comportamentos de Gerenciamento

- **Permissões:** A configuração ou alteração de webhooks exige acesso de **Administrador** no painel da Omie.
- **Sessões e Testes:** Novas configurações só têm efeito em novas sessões do Omie; é preciso recarregar o aplicativo no navegador para iniciar os testes.
- **Alteração de Endpoint (Migração):** Ao modificar a URL de destino, os eventos antigos (represados) continuarão sofrendo tentativas de envio para o endpoint antigo. Somente os novos eventos gerados após a alteração serão direcionados ao novo endpoint.
- **Remoção:** A exclusão de um webhook não gera impactos no sistema Omie.ERP em si, apenas interrompe a comunicação e a geração de novas notificações para o sistema integrado.
