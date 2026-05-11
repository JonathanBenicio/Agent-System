---
trigger: model_decision
description: Regras de integração e arquitetura estritas para consumo da API Omie (Tabatine Engine).
---

# Regras de Arquitetura e Integração: APIs Omie

Essas regras definem os limites e restrições de alto nível para a integração. Para detalhes técnicos de *como* implementar (DTOs, Paginação, Result Pattern), consulte a skill `omie-api-integration-skills`.

## 1. Paginação Estrita (Rule de Memória)
- **Constraint:** Todas as requisições de listagem (ex: `ListarClientes`, `ListarPedidos`) DEVEM definir o parâmetro `registros_por_pagina` com o valor máximo de **100**.
- **Constraint Antigravity:** É **estritamente proibido** retornar `List<T>` ou alocar todos os registros na memória em métodos que buscam dados massivos da Omie. O agente DEVE utilizar fluxos assíncronos (`IAsyncEnumerable<T>` com `yield return`).
- **Motivo:** Evitar alocações no LOH (Large Object Heap) e garantir escalabilidade do `Tabatine.Worker`.

## 2. Imutabilidade e Mapeamento de Modelos
- **Constraint Antigravity:** Qualquer classe na integração de API (Request/Response) DEVE ser implementada como um `record` imutável.
- **Padrão:** O mapeamento de campos com `[JsonPropertyName("nome_campo_omie")]` é obrigatório para compatibilidade semântica com o RPC da Omie.

## 3. Gestão de Erros e Fluxo (Result Pattern)
- **Constraint Antigravity:** NUNCA utilize controle de fluxo de erros lançando exceções (`throw new Exception()`). Exceptions no .NET devem ser minimizadas para alta performance.
- **Padrão:** Utilize o `Result Pattern` encapsulando falhas normais da Omie.

## 4. Estratégia Híbrida de Sincronização
- **Rule:** O Tabatine Engine utiliza uma estratégia híbrida para otimizar o consumo da API e garantir integridade:
  - **Incremental (Datalake):** Entidades de alto volume (**Clientes, Produtos, Pedidos, Financeiro, NFs**) DEVEM utilizar filtros temporais (`data_de`, `exibir_apenas_alterados`) para baixar apenas o que mudou desde o último ciclo.
  - **Full Sync (Lookup):** Tabelas de apoio de baixo volume (**Bancos, Etapas, Meios/Formas de Pagamento, Vendedores**) DEVEM realizar sincronização total em cada ciclo para garantir consistência absoluta, ignorando cursores temporais.
- **Constraint:** Adote sempre a política de **Upsert** (verificar existência pelo campo `OmieId`) para evitar duplicidade independente da estratégia.

## 5. Circuit Breaker e Respeito aos Rate Limits
- **Rate Limit**: Respeitar o limite de 240 req/min e o bloqueio de 60s entre chamadas para o mesmo `OmieId`.
- **Especial**: Em caso de **HTTP 425 (Too Early)**, a aplicação deve interromper o processamento por 30 minutos conforme as regras de segurança da Omie.
- **Padrão**: Qualquer código de integração DEVE prever a implementação de políticas de resiliência via Polly (Exponential Backoff e Circuit Breaker).

## 6. Garantia de Documento de Origem
- **Regra de Negócio (Omie):** É proibido inserir lançamentos financeiros avulsos originados em pedidos comerciais. A aplicação externa se limitará a integrar e confirmar o **Documento de Origem**.

## 7. Cursor de Sincronização — Guard de Cancelamento (Obrigatório)

- **Constraint CRÍTICA:** O cursor de data/estado (`SetLastSyncDateAsync`) DEVE ser salvo SOMENTE se a sincronização foi completada com sucesso E sem cancelamento.
- **Risco:** Salvar o cursor após cancelamento (`CancellationToken` sinalizado) marca a sincronização como bem-sucedida, fazendo o próximo ciclo **ignorar dados não processados**.
- **Escopo:** Aplica-se a TODOS os serviços, incluindo tabelas de apoio (Full Sync).

```csharp
// ✅ CORRETO: Guard antes de atualizar cursor
if (!ct.IsCancellationRequested)
{
    await syncState.SetLastSyncDateAsync("Bancos", DateTime.UtcNow, ct);
}

// ❌ PROIBIDO: Salvar cursor sem guard
await syncState.SetLastSyncDateAsync("Bancos", DateTime.UtcNow, ct);
```

## 8. Renomear `SyncKey` Exige Migração de Cursor Histórico

- **Regra:** Ao alterar o valor de uma constante `SyncKey` em um SyncService, é obrigatório garantir que o cursor histórico não seja perdido.
- **Risco:** Renomear sem migração causa Full Sync não intencional na primeira execução após o deploy, com alto custo para bases volumosas.
- **Ação Obrigatória:** Criar uma data migration SQL que copie o cursor do key antigo para o novo:

```sql
-- Migration obrigatória ao renomear SyncKey
UPDATE sync_states SET key = 'LancamentosPagar' WHERE key = 'ContasPagar';
```

---

## Validação

Este documento é verificado pelo checklist em [sync-service-patterns.md](sync-service-patterns.md) e pelo workflow `/pr-review`.

---
*Para padrões de código e templates de implementação, acesse:*
- [Omie API Core Skills](../skills/omie-api-skills/SKILL.md)
- [Omie Webhook Skills](../skills/omie-webhooks/SKILL.md)