---
description: Consumo e Sincronização de APIs Omie
---

# Workflows: Consumo e Sincronização de APIs Omie

Estes fluxos descrevem os pipelines obrigatórios para manter a resiliência e integridade dos dados ao interagir com a Omie.

## Fluxo 1: Leitura Incremental com Paginação (Sync Inbound)
Este fluxo deve ser executado por Jobs de sincronização periódicos.

1. **Obter Estado (Checkpoint):** Recuperar na base de dados local a data e hora do último registo sincronizado com sucesso (`LastSyncDate`).
2. **Montar Requisição:**
   - Preencher a *AppKey* e *AppSecret*.
   - Definir `pagina` = 1 e `registros_por_pagina` = 100.
3. **Loop de Paginação:**
   - Executar a chamada à API.
   - Processar e persistir os 100 registos na base de dados local de forma idempotente (usar *Upsert* baseado no `codigo_..._omie`).
   - Avaliar a variável `total_de_paginas` devolvida no JSON.
   - Se `pagina_atual < total_de_paginas`, incrementar a `pagina` e repetir o passo 3.
4. **Atualizar Estado:** Gravar a nova `LastSyncDate` para a próxima execução.

## Fluxo 2: Escrita/Mutação (Sync Outbound)
1. **Validação de Payload:** Validar localmente os dados obrigatórios antes de enviar (ex: formato do NIF/CNPJ, códigos de clientes existentes).
2. **Executar Mutação:** Enviar o POST para a API correspondente (ex: `IncluirPedidoVenda`).
3. **Tratamento de Erros da API:**
   - **Erro de Negócio (ex: código não encontrado):** Interromper e gerar alerta/log descritivo.
   - **Instabilidade (`PROTO_BYEBYE`) ou Timeout:** Enfileirar a tarefa para nova tentativa (Retry) em 1 minuto.
   - **Sucesso:** Capturar o ID gerado na Omie (ex: `codigo_pedido`) e guardar na base de dados local para manter o vínculo (relacionamento externo).