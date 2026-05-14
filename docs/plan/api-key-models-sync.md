# Plano de Implementação: Sincronização Automática de Modelos via API Key (api-key-models-sync)

---

## 1. Overview

### Contexto & Problema
Atualmente, no Agentic System, a listagem de modelos suportados para cada provedor de IA (OpenAI, Gemini, Claude, Ollama) é baseada em um catálogo estático inicializado no backend (`ProviderModelCatalog`). Quando o usuário insere ou altera sua chave de API no frontend (`ProvidersPage.tsx`), o sistema valida e salva a chave, mas não consulta dinamicamente quais modelos aquela chave específica tem permissão para acessar (como modelos finetuned, novos lançamentos ou acesso antecipado).

### Objetivo
Criar uma funcionalidade robusta de sincronização e descoberta automática de modelos de IA. Assim que o usuário inserir/colar uma chave de API válida na interface de configuração, o sistema deverá realizar uma busca nos endpoints oficiais dos provedores (OpenAI, Google Gemini, Anthropic Claude), identificar modelos disponíveis ainda não listados, apresentá-los na interface e persistir essa lista atualizada no banco/configurações do sistema associada ao respectivo provedor.

---

## 2. Project Type

**Target Assignment:** `WEB` (Frontend React 19 / Vite / Tailwind) & `BACKEND` (.NET 10 ASP.NET Core API).
**Assigned Specialist Agents:**
- `frontend-specialist`: Para gerenciamento de estado React, hooks, debouncing/validação de input e exibição da UI.
- `backend-specialist`: Para implementação de endpoints de descoberta, integração HTTP com as APIs dos provedores e persistência limpa de configuração.

---

## 3. Success Criteria

1. **Descoberta Dinâmica com Sucesso**: Ao inserir uma API Key válida para OpenAI, Gemini ou Claude, a lista de modelos disponíveis deve ser recuperada com sucesso em até 3 segundos.
2. **Filtragem Correta de Capacidades**: No caso do Google Gemini, filtrar e apresentar apenas os modelos que suportam `generateContent` (ex: família `gemini-1.5` ou superior).
3. **Persistência Consistente**: Ao clicar em "Salvar", a lista consolidada de modelos deve ser enviada via `UpdateProviderRequest` e persistida via `ILLMAdministrationService` no armazenamento de configurações sem perda do estado anterior.
4. **Resiliência a Falhas**: Chaves inválidas ou falhas de rede devem ser capturadas graciosamente, exibindo mensagens de erro tratadas na UI sem quebrar a tela de configurações.

---

## 4. Tech Stack & Trade-off Analysis

Para a arquitetura de consulta aos endpoints dos provedores (OpenAI `/v1/models`, Gemini `/v1beta/models`, Claude `/v1/models`), analisamos duas abordagens principais:

### Tabela Comparativa de Trade-offs

| Critério | Caminho 1: Client-Side Fetching (Navegador) | Caminho 2: Backend Proxy Discovery (Servidor) |
| :--- | :--- | :--- |
| **Segurança da API Key** | A chave fica exposta na memória do client durante o fetch direto; se o provedor registrar no console, pode vazar no client. | **Alta Segurança**: A chave viaja via TLS para o nosso backend, que faz a chamada protegida de servidor para servidor. |
| **Políticas de CORS** | **Crítico**: OpenAI e Anthropic ativamente bloqueiam requisições GET `/v1/models` originadas de navegadores web (CORS headers estritos). Apenas Gemini via URL param costuma funcionar. | **Sem Restrições**: Chamadas HTTP feitas via `HttpClient` no backend em C# não estão sujeitas a políticas de CORS do navegador. |
| **Complexidade de Código** | Baixa no backend, alta no frontend (necessidade de lidar com múltiplos parsers e tratamento de falhas de CORS no browser). | Média. Requer criação de um novo endpoint no backend (`POST /api/admin/llm/providers/{name}/discover-models`) ou integração na validação de teste. |
| **Performance & Cache** | Depende da rede do cliente para múltiplos provedores externos. | O backend pode usar `IHttpClientFactory` e aplicar timeouts padronizados e resiliência via Polly. |

### Decisão Arquitetural
**Escolha do Caminho 2 (Backend Proxy Discovery)**. 
A restrição de CORS imposta pela OpenAI e pela Anthropic torna a abordagem puramente client-side inviável para uma aplicação web corporativa de alta qualidade. O backend centralizará a comunicação com as APIs de LLM, garantindo robustez, padronização de logs e segurança absoluta das credenciais.

---

## 5. Risk Assessment & Risk Matrix

| Risco | Probabilidade | Impacto | Mitigação Estratégica |
| :--- | :--- | :--- | :--- |
| **R1: Bloqueio de Taxa (Rate Limiting) no Provedor** | Média | Médio | Implementar debouncing de 800ms no frontend antes de disparar a requisição de descoberta; no backend, configurar timeouts de 5s no `HttpClient`. |
| **R2: Chaves Inválidas ou Expiradas** | Alta | Baixo | Tratar códigos HTTP 401/403 no backend e retornar uma estrutura JSON padronizada `{ success: false, message: "Chave de API inválida ou sem permissão" }` para exibição na UI. |
| **R3: Mudança de Contrato da API do Provedor** | Baixa | Alto | Isolar o parsing dos retornos JSON em classes dedicadas no backend com desserialização tolerante (usando `System.Text.Json`). |
| **R4: Sobrescrita Indesejada de Modelos Customizados** | Média | Médio | Ao persistir, realizar o *merge* (união) dos modelos já existentes no catálogo com os novos modelos descobertos, evitando duplicidades através de `HashSet<string>(StringComparer.OrdinalIgnoreCase)`. |

---

## 6. File Structure

### Frontend (React / TypeScript)
- `frontend/src/components/llm/ProvidersPage.tsx` (Modificar: Adicionar lógica de disparo de descoberta ao alterar a API Key no formulário de edição)
- `frontend/src/hooks/useLLMProviders.ts` (Modificar: Adicionar função `discoverModels(name, apiKey)` chamando a nova rota da API)
- `frontend/src/services/llmApi.ts` (Modificar: Adicionar assinatura do endpoint de descoberta)

### Backend (.NET C#)
- `src/AgenticSystem.Core/LLM/Interfaces/LLMAdministrationModels.cs` (Modificar: Adicionar classes `DiscoverModelsRequest` e `DiscoverModelsResponse`, além de expandir `UpdateProviderRequest` se necessário)
- `src/AgenticSystem.Core/LLM/Interfaces/ILLMAdministrationService.cs` (Modificar: Adicionar método `DiscoverModelsAsync`)
- `src/AgenticSystem.Api/Controllers/LLMController.cs` (Modificar: Expor rota `POST /api/admin/llm/providers/{name}/discover-models`)
- `src/AgenticSystem.Infrastructure/LLM/LLMManager.cs` (Modificar: Implementar a consulta HTTP para OpenAI, Gemini e Claude e o merge de modelos)

---

## 7. Task Breakdown

### Task 1: Contratos e Modelos no Backend
- [x] **Agent**: `backend-specialist` | **Skill**: `api-patterns`
- **INPUT**: Modificar `LLMAdministrationModels.cs` e `ILLMAdministrationService.cs`.
- **OUTPUT**:
  - Classe `DiscoverModelsRequest` contendo `public string ApiKey { get; set; } = string.Empty;`.
  - Classe `DiscoverModelsResponse` contendo `public bool Success { get; set; }`, `public string? ErrorMessage { get; set; }`, `public IReadOnlyList<string> DiscoveredModels { get; set; } = [];`.
  - Propriedade `public IReadOnlyList<string>? DiscoveredModels { get; set; }` em `UpdateProviderRequest`.
  - Assinatura `Task<DiscoverModelsResponse> DiscoverModelsAsync(string name, DiscoverModelsRequest request, CancellationToken ct);` na interface `ILLMAdministrationService`.
- **VERIFY**: Compilar o projeto `AgenticSystem.Core` com sucesso (`dotnet build`).

### Task 2: Implementação do Discovery Service no Backend
- [x] **Agent**: `backend-specialist` | **Skill**: `clean-code`
- **INPUT**: Modificar `LLMManager.cs`.
- **OUTPUT**:
  - Implementar `DiscoverModelsAsync`. Dependendo do provedor (`OpenAI`, `Gemini`, `Claude`), instanciar requisição usando `HttpClient`.
  - **OpenAI**: `GET https://api.openai.com/v1/models` com cabeçalho `Authorization: Bearer {apiKey}`. Mapear propriedade `data[].id`.
  - **Gemini**: `GET https://generativelanguage.googleapis.com/v1beta/models?key={apiKey}`. Filtrar onde `supportedGenerationMethods` contenha `generateContent`. Mapear `models[].name` (removendo prefixo `models/`).
  - **Claude**: `GET https://api.anthropic.com/v1/models` com cabeçalhos `x-api-key: {apiKey}` e `anthropic-version: 2023-06-01`. Mapear propriedade `data[].id` (ou fallback seguro para o catálogo caso endpoint indisponível/restrito).
  - Em `UpdateProviderAsync`, se `request.DiscoveredModels` for fornecido, salvar a lista atualizada no banco/configurações via `UpsertConfigEntryAsync`.
- **VERIFY**: Executar testes unitários do backend e garantir que não há quebra de compilação.

### Task 3: Exposição do Endpoint no Controller
- [x] **Agent**: `backend-specialist` | **Skill**: `api-patterns`
- **INPUT**: Modificar `LLMController.cs`.
- **OUTPUT**:
  - Adicionar o endpoint `[HttpPost("providers/{name}/discover-models")]`.
  - Validar request, chamar `_llmAdministrationService.DiscoverModelsAsync(name, request, ct)` e retornar `Ok(response)`.
- **VERIFY**: O endpoint deve estar visível e acessível via Swagger ou Postman sem erros de roteamento.

### Task 4: Serviços e Hooks no Frontend
- [x] **Agent**: `frontend-specialist` | **Skill**: `frontend-design`
- **INPUT**: Modificar `llmApi.ts` e `useLLMProviders.ts`.
- **OUTPUT**:
  - Em `llmApi.ts`, adicionar método `discoverModels: (name: string, apiKey: string) => api.post<DiscoverModelsResponse>(.../discover-models, { apiKey })`.
  - Em `useLLMProviders.ts`, expor uma função assíncrona `discoverModels` que gerencie o estado de carregamento (`isDiscovering`) e erro na tela.
- **VERIFY**: O TypeScript do frontend deve compilar perfeitamente sem erros de tipo (`npx tsc --noEmit`).

### Task 5: Integração na Interface de Usuário (UI)
- [x] **Agent**: `frontend-specialist` | **Skill**: `frontend-design`
- **INPUT**: Modificar `ProvidersPage.tsx`.
- **OUTPUT**:
  - Ao editar um provedor, no input de `API Key`, ao perder o foco (`onBlur`) ou ao clicar num novo botão "Validar & Descobrir Modelos", invocar `discoverModels(provider.name, currentApiKey)`.
  - Exibir um indicador de carregamento (spinner/texto "Buscando modelos disponíveis...").
  - Em caso de sucesso, apresentar os novos modelos encontrados em formato de tags ou lista na seção de seleção de modelos da UI.
  - Ao clicar em "Salvar", enviar a lista de modelos descoberta junto com a requisição de update.
- **VERIFY**: Renderizar a página no navegador sem erros no console, garantindo o feedback visual imediato e rico.

---

## 8. Rollback Strategy

Caso alguma anomalia seja detectada durante a implantação ou testes:

1. **Rollback de UI**:
   - Reverter as alterações em `ProvidersPage.tsx` e `useLLMProviders.ts` usando controle de versão (`git checkout`). A tela voltará a usar puramente o catálogo estático.
2. **Rollback de API / Backend**:
   - Manter as novas classes de contrato para evitar quebra de compatibilidade binária, mas reverter o método do controller ou retornar diretamente uma lista vazia/catálogo padrão em `DiscoverModelsAsync`.
3. **Recuperação de Estado**:
   - Nenhuma migração de banco de dados estrutural é necessária, pois as configurações utilizam o sistema chave-valor existente (`IConfigManager`). Dados anteriores de chaves e modelos padrão não serão afetados.

---

## 9. Phase X: Verification Plan

Antes de declarar a tarefa concluída, o agente executará a seguinte esteira de validação:

```bash
# 1. Validação de Código e Tipagem Frontend
cd frontend && npm run lint && npx tsc --noEmit

# 2. Compilação e Testes Unitários Backend
cd ../src && dotnet build && dotnet test

# 3. Auditoria de Segurança e Dependências (Security Gate)
python ../.agents/skills/vulnerability-scanner/scripts/security_scan.py .

# 4. Verificação de Regras Globais (Checklist)
python ../.agents/scripts/checklist.py .
```

---

## ✅ STATUS DE APROVAÇÃO DO PLANO
- [x] Conductor Blueprint Validado por Socratic Gate
- [x] Aprovado pelo Usuário
- [x] **Épico Totalmente Implementado e Concluído (Issue #6)**
