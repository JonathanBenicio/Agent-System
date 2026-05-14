# Plano de Correção: Teste de Conectividade dos Provedores LLM

## 1. Objetivo
Corrigir o endpoint de teste de conectividade dos provedores (especificamente o Claude) que atualmente retorna `available: false` mesmo quando a requisição local da API responde com sucesso (HTTP 200). A causa raiz envolve verificações restritivas de estado (`IsEnabled`) e IDs de modelos padronizados incorretamente.

## 2. Escopo & Arquivos Afetados

### Arquivos de Configuração
- `src/AgenticSystem.Api/appsettings.json`: Correção do `DefaultModel` do Claude de `claude-sonnet-4-20250514` para um modelo válido (ex: `claude-3-5-sonnet-latest`).
- `src/AgenticSystem.Infrastructure/Configuration/AgenticSystemSettings.cs`: Correção do valor padrão correspondente.

### Gerenciador de LLMs
- `src/AgenticSystem.Infrastructure/LLM/LLMManager.cs`: Remoção de IDs de modelos inexistentes/futuristas (ex: `claude-sonnet-4-20250514`, `o4-mini`, `gemini-2.5-pro`) do `ProviderModelCatalog` para evitar falhas silenciosas de fallback e discovery.

### Provedores (Providers)
Modificar o comportamento do método `IsAvailableAsync` para que ele verifique apenas a presença da chave de API (`ApiKey`) ao invés do estado total de ativação (`IsEnabled`), permitindo assim que testes sejam executados na interface antes da ativação final.
- `src/AgenticSystem.Infrastructure/LLM/ClaudeProvider.cs`
- `src/AgenticSystem.Infrastructure/LLM/OpenAIProvider.cs`
- `src/AgenticSystem.Infrastructure/LLM/GeminiProvider.cs`

## 3. Passos de Implementação

1. **Ajuste nas Configurações:**
   - Atualizar a propriedade `DefaultModel` do Claude em `AgenticSystemSettings.cs` e `appsettings.json` para `claude-3-5-sonnet-latest`.
2. **Atualização do Catálogo Estático (`ProviderModelCatalog`):**
   - Em `LLMManager.cs`, atualizar as chaves de modelos para refletir apenas os modelos reais atuais de cada provedor (ex: remover `o4-mini` para OpenAI, usar família `gemini-1.5` ou `2.0` real para Google, etc).
3. **Correção do `IsAvailableAsync`:**
   - Em `ClaudeProvider`, substituir `if (!IsEnabled) return false;` por `if (string.IsNullOrWhiteSpace(_settings.ApiKey)) return false;`.
   - Repetir a mesma substituição no `OpenAIProvider`.
   - Repetir a mesma substituição no `GeminiProvider`.
4. **Verificação Adicional no Ping do Claude:**
   - Garantir que o *payload* de ping use o `DefaultModel` de forma segura (a requisição existente com `max_tokens = 1` e `ping` funciona se o ID do modelo for válido).

## 4. Testes & Validação
- Executar `dotnet build` e `dotnet test` para garantir a integridade do backend.
- O endpoint `POST /api/admin/llm/providers/Claude/test` não deve mais retornar `false` prematuramente se uma API Key válida for fornecida (seja ela injetada por variável ou salva nas configurações), mesmo com o provedor desabilitado.