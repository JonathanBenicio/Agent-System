# Roadmap: Dynamic ONNX In-Process Inference Engine

> **Status documental:** Approved
> **GitHub Issue:** [#74](https://github.com/JonathanBenicio/Agent-System/issues/74)
> **Escopo:** Upload, gerenciamento e execução dinâmica de modelos ONNX pela interface — sem código C# por modelo
> **Fonte de verdade operacional:** `docs/architecture/` + ADR 010 (ONNX Runtime)
> **Gerado em:** 2026-05-20
> **Projeto:** AgenticSystem

---

## Objetivo

Permitir que qualquer usuário admin faça upload de um modelo `.onnx` pré-treinado (ex: Real-ESRGAN para upscaling, SwinIR para denoising) pela interface web, configure os parâmetros de inferência (input/output nodes, dimensões, normalização) e execute inferência in-process via ONNX Runtime — sem escrever uma linha de C# específica para cada modelo. O motor genérico de inferência lê o modelo e as instruções diretamente do banco de dados.

Isso estende o runtime atual com uma `DynamicOnnxProcessorTool` que age como executor universal, tornando o sistema capaz de processar imagens com IA local de forma 100% configurável pela UI.

## Princípios de Implantação

1. **Zero código por modelo** — Um único `onnx_processor` tool genérico; novos modelos são adicionados via UI, não via código
2. **Dual storage inteligente** — Modelos ≤50MB no PostgreSQL (simplicidade); >50MB em disco com referência no DB (performance), com aviso ao usuário
3. **Tenant isolation obrigatório** — Seguir padrão `ITenantEntity` existente; cada tenant vê apenas seus modelos
4. **Fallback graceful** — Se ONNX Runtime falhar, retornar erro claro com diagnóstico; nunca crashar o runtime
5. **Integração nativa com Workflow Builder** — Modelos aparecem como opções configuráveis em tool nodes do React Flow

## Fases e Sequenciamento

| Ordem | Frente/Fase | Motivo do sequenciamento |
|---|---|---|
| 1 | Dependencies (ImageSharp) | Desbloqueia pré/pós-processamento de imagem em todas as fases seguintes |
| 2 | Entity + Config + Migration | Foundation de dados — sem isso, nada persiste |
| 3 | DynamicOnnxProcessorTool | Core logic de inferência — depende da entity e do ImageSharp |
| 4 | API Controller | Expõe a funcionalidade via HTTP para o frontend |
| 5 | Frontend Types + API + Hook | Bridge entre UI e backend |
| 6 | Frontend Page + Modals | Interface de upload, inspeção e teste |
| 7 | Sidebar + Route + Workflow Integration | Glue final — navegação e uso no Workflow Builder |
| 8 | Suporte a Modelos Split (.data / .bin) | Viabiliza o upload e inferência de modelos ONNX divididos em múltiplos arquivos |

---

## Detalhamento: Dynamic ONNX Model Manager

### Por que implementar?

- **Dor atual:** Para adicionar uma nova capacidade de processamento de imagem (upscaling, denoising, style transfer), é necessário escrever C#, compilar e redeployar
- **Valor:** Admins podem adicionar modelos pré-treinados da internet (Hugging Face, ONNX Model Zoo) em minutos, pela interface
- **Custo zero de inferência:** Modelos rodam localmente via ONNX Runtime (CPU), sem APIs externas pagas
- **Alinhamento com ADR 010:** O projeto já prevê ONNX Runtime; falta a camada de dinamismo e UI

### Arquitetura-alvore

```
┌─────────────────────────────────────────────────────────────────────┐
│                          Frontend (React)                           │
│  ┌──────────────┐  ┌───────────────┐  ┌──────────────────────────┐  │
│  │ OnnxModels   │  │ Upload Modal  │  │ Workflow Builder         │  │
│  │ Page         │  │ (file + form) │  │ (tool node → modelId)    │  │
│  └──────┬───────┘  └───────┬───────┘  └───────────┬──────────────┘  │
│         │                  │                       │                 │
│         └──────────────────┼───────────────────────┘                 │
│                            │ POST /api/onnx/models (multipart)       │
└────────────────────────────┼─────────────────────────────────────────┘
                             │
┌────────────────────────────┼─────────────────────────────────────────┐
│                     Backend (ASP.NET Core)                           │
│                            │                                         │
│  ┌─────────────────────────▼───────────────────────────────────┐    │
│  │              OnnxModelController                             │    │
│  │  GET/POST/PUT/DELETE /api/onnx/models                        │    │
│  │  POST /api/onnx/models/{id}/inspect                          │    │
│  │  POST /api/onnx/models/{id}/test                             │    │
│  └─────────────────────────┬───────────────────────────────────┘    │
│                            │                                         │
│  ┌─────────────────────────▼───────────────────────────────────┐    │
│  │           DynamicOnnxProcessorTool (ITool)                   │    │
│  │  1. Busca CustomOnnxModelEntity do DB                        │    │
│  │  2. Carrega modelo (bytes DB ou arquivo disco)               │    │
│  │  3. Pré-processa imagem → Tensor (ImageSharp)                │    │
│  │  4. Executa ONNX Runtime inference                           │    │
│  │  5. Pós-processa output → base64 image                       │    │
│  └─────────────────────────┬───────────────────────────────────┘    │
│                            │                                         │
└────────────────────────────┼─────────────────────────────────────────┘
                             │
┌────────────────────────────┼─────────────────────────────────────────┐
│                     Data Layer                                       │
│  ┌─────────────────────────▼───────────────────────────────────┐    │
│  │  PostgreSQL: custom_onnx_models                              │    │
│  │  ─────────────────────────────────────────────────────────── │    │
│  │  ≤50MB: model_data (bytea)                                   │    │
│  │  >50MB: model_file_path (text) → wwwroot/onnx-models/        │    │
│  └──────────────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────────────┘
```

### Componentes propostos

| Componente | Papel |
|---|---|
| `CustomOnnxModelEntity` | Entidade EF Core — armazena modelo + metadados de inferência |
| `DynamicOnnxProcessorTool` | Tool genérica (`ITool`) — executa inferência ONNX dinâmica |
| `OnnxModelController` | API REST — CRUD de modelos + inspect + test |
| `OnnxModelsPage` | UI — lista, upload, inspeção e teste de modelos |
| `useOnnxModels` | React Query hook — gerencia estado e chamadas API |

### Plano por etapas

#### Fase 1: Dependencies

1. Adicionar `SixLabors.ImageSharp` ao `AgenticSystem.Core.csproj` e `AgenticSystem.Infrastructure.csproj`
2. Verificar que `Microsoft.ML.OnnxRuntime` já está presente (v1.26.0)

#### Fase 2: Entity + Config + Migration

1. Adicionar `CustomOnnxModelEntity` em `PersistenceEntities.cs`:
   - `Id` (string, PK), `TenantId` (string), `Name` (string), `Description` (string?)
   - `ModelFileName` (string?) — path no disco se >50MB
   - `ModelData` (byte[]?) — bytes no DB se ≤50MB
   - `InputNodeName`, `OutputNodeName` (string)
   - `InputWidth`, `InputHeight`, `Channels` (int)
   - `ScaleFactor` (float, default 1/255)
   - `MeanRed`, `MeanGreen`, `MeanBlue` (float, default 0)
   - `OutputFormat` (string: "image" | "tensor" | "text")
   - `PostProcessConfigJson` (string, default "{}")
   - `FileSizeBytes` (long), `IsActive` (bool)
   - `CreatedAt`, `UpdatedAt` (DateTime)

2. Adicionar `CustomOnnxModelConfiguration` em `PersistenceConfigurations.cs`:
   - Tabela: `custom_onnx_models`
   - Índice unique: `TenantId + Name`
   - Índice: `IsActive`

3. Gerar migration:
   ```bash
   dotnet ef migrations add AddCustomOnnxModelEntity \
     --project src/AgenticSystem.Infrastructure \
     --startup-project src/AgenticSystem.Api \
     --output-dir Persistence/Migrations
   ```

#### Fase 3: DynamicOnnxProcessorTool

1. Adicionar `AI` ao enum `ToolCategory` em `ITool.cs`

2. Criar `src/AgenticSystem.Core/Tools/DynamicOnnxProcessorTool.cs`:
   - Implementa `ITool`
   - `Id = "onnx_processor"`, `Name = "ONNX Image Processor"`, `Category = AI`
   - `ExecuteAsync` suporta actions:
     - `"process"`: executa inferência na imagem
       - Params: `{ modelId, imageData (base64) }`
     - `"inspect"`: retorna metadata do modelo
       - Params: `{ modelId }`
   - Fluxo de process:
     1. Busca `CustomOnnxModelEntity` do DB via `DbContext` injetado
     2. Carrega modelo: `ModelData` (bytes) ou `File.ReadAllBytes(ModelFileName)`
     3. Decodifica imagem base64 → `Image<Rgb24>` (ImageSharp)
     4. Resize para `InputWidth × InputHeight`
     5. Converte pixels → `DenseTensor<float>` shape `[1, Channels, H, W]`
     6. Aplica `ScaleFactor` e `Mean` por canal
     7. Cria `InferenceSession` com modelo
     8. Executa `session.Run()` com input nomeado
     9. Extrai output tensor pelo `OutputNodeName`
     10. Pós-processa: tensor → imagem (se `OutputFormat = "image"`)
     11. Retorna imagem como base64 no `ToolResult`

3. Registrar tool em `SeedAgenticDefaults()` no `ServiceCollectionExtensions.cs`

#### Fase 4: API Controller

1. Criar `src/AgenticSystem.Api/Controllers/OnnxModelController.cs`:
   - `[Authorize]` + tenant middleware
   - `[RequestSizeLimit(1_073_741_824)]` para uploads grandes (1GB)

   **Endpoints:**
   - `GET /api/onnx/models` — lista modelos do tenant (summary)
   - `GET /api/onnx/models/{id}` — detalhes completos
   - `POST /api/onnx/models` — upload (multipart/form-data):
     - Campos: `file` (.onnx), `name`, `description`, `inputNodeName`, `outputNodeName`, `inputWidth`, `inputHeight`, `channels` (default 3), `scaleFactor` (default 0.0039216), `meanRed/Green/Blue` (default 0), `outputFormat` (default "image")
     - Se `file.Length > 50MB`: salva em `wwwroot/onnx-models/{tenantId}/{guid}.onnx`, guarda path
     - Se ≤50MB: salva bytes em `ModelData`
     - Retorna: `OnnxModelSummary`
   - `PUT /api/onnx/models/{id}` — atualizar metadata (não o arquivo)
   - `DELETE /api/onnx/models/{id}` — deletar modelo (remove arquivo do disco se aplicável)
   - `POST /api/onnx/models/{id}/inspect` — inspeciona modelo ONNX:
     - Abre `InferenceSession`, lista input/output nodes com nomes, shapes, tipos
     - Retorna: `{ inputNodes: [{name, shape, type}], outputNodes: [{name, shape, type}] }`
   - `POST /api/onnx/models/{id}/test` — teste rápido:
     - Recebe imagem (base64 ou multipart)
     - Executa inferência completa
     - Retorna: `{ outputImage (base64), latencyMs, inputShape, outputShape }`

#### Fase 5: Frontend Types + API + Hook

1. **`frontend/src/types/api.ts`** — Adicionar:
   ```typescript
   interface OnnxModelSummary {
     id: string; name: string; description: string;
     inputWidth: number; inputHeight: number; channels: number;
     outputFormat: string; isActive: boolean;
     fileSizeBytes: number; storedOnDisk: boolean;
     createdAt: string; updatedAt: string;
   }

   interface OnnxModelDetail extends OnnxModelSummary {
     inputNodeName: string; outputNodeName: string;
     scaleFactor: number; meanRed: number; meanGreen: number; meanBlue: number;
     postProcessConfigJson: string;
   }

   interface OnnxInspectResult {
     inputNodes: { name: string; shape: number[]; type: string }[];
     outputNodes: { name: string; shape: number[]; type: string }[];
   }

   interface OnnxTestResult {
     outputImage?: string;
     outputTensor?: number[];
     latencyMs: number;
     inputShape: number[];
     outputShape: number[];
   }
   ```

2. **`frontend/src/lib/api.ts`** — Adicionar:
   ```typescript
   export const onnxModelApi = {
     list: () => get<OnnxModelSummary[]>('/api/onnx/models'),
     get: (id: string) => get<OnnxModelDetail>(`/api/onnx/models/${id}`),
     create: (formData: FormData) => postForm<OnnxModelSummary>('/api/onnx/models', formData),
     update: (id: string, data: Partial<OnnxModelDetail>) => put(`/api/onnx/models/${id}`, data),
     delete: (id: string) => del(`/api/onnx/models/${id}`),
     inspect: (id: string) => post<OnnxInspectResult>(`/api/onnx/models/${id}/inspect`),
     test: (id: string, imageData: FormData) => postForm<OnnxTestResult>(`/api/onnx/models/${id}/test`, imageData),
   };
   ```

3. **`frontend/src/hooks/useOnnxModels.ts`** — Criar hook com TanStack Query:
   - `useOnnxModelsList()` — query para lista
   - `useUploadOnnxModel()` — mutation para upload com progress
   - `useDeleteOnnxModel()` — mutation para delete
   - `useInspectOnnxModel()` — mutation para inspect
   - `useTestOnnxModel()` — mutation para test

#### Fase 6: Frontend Page + Modals

1. **`frontend/src/components/onnx/OnnxModelsPage.tsx`**:
   - Header: título "Modelos IA" + botão "Upload Modelo"
   - Grid de cards (1-2 colunas):
     - Nome, descrição (truncate)
     - Dimensões: `512×512`, formato: `image`
     - Tamanho formatado: `12.4 MB` | `156 MB (disco)`
     - Badge de status: ativo/inativo
     - Ações: Inspecionar, Testar, Editar, Deletar
   - Empty state: "Nenhum modelo ONNX cadastrado. Faça upload de um modelo pré-treinado para habilitar inferência local."
   - Toast notifications para sucesso/erro

2. **`frontend/src/components/onnx/OnnxModelUploadModal.tsx`**:
   - File picker (.onnx) com drag & drop
   - Campos do formulário:
     - Nome (obrigatório)
     - Descrição
     - Input Node Name (obrigatório)
     - Output Node Name (obrigatório)
     - Input Width / Height (números)
     - Channels (select: 1, 3, 4)
     - Scale Factor (number, default 0.0039216)
     - Mean R / G / B (numbers, default 0)
     - Output Format (select: image, tensor, text)
   - Warning box se arquivo >50MB: "⚠️ Modelo grande (XX MB) — será salvo em disco para melhor performance"
   - Progress bar durante upload
   - Botões: Cancelar, Upload

3. **`frontend/src/components/onnx/OnnxModelInspectModal.tsx`**:
   - Tabela de input nodes: nome, shape, tipo
   - Tabela de output nodes: nome, shape, tipo
   - Botão para copiar informações

4. **`frontend/src/components/onnx/OnnxModelTestModal.tsx`**:
   - Upload de imagem de teste (drag & drop ou file picker)
   - Preview da imagem original
   - Botão "Executar Teste"
   - Preview da imagem resultado (lado a lado)
   - Métricas: latência, input/output shape

#### Fase 7: Integração Final

1. **`frontend/src/components/layout/Sidebar.tsx`**:
   - Adicionar import de `Brain` do lucide-react
   - Adicionar nav item: `{ icon: Brain, label: 'Modelos IA', path: '/onnx-models' }`

2. **`frontend/src/App.tsx`**:
   - Lazy import: `const OnnxModelsPage = lazy(() => import('@/components/onnx/OnnxModelsPage'))`
   - Route: `<Route path="/onnx-models" element={<RouteBoundary><OnnxModelsPage /></RouteBoundary>} />`

3. **`frontend/src/components/workflows/WorkflowBuilder.tsx`**:
   - No Properties Panel, quando `node.type === 'tool'` e `node.data.toolName === 'onnx_processor'`:
     - Adicionar dropdown "Modelo ONNX" com lista de modelos disponíveis (fetch via `onnxModelApi.list()`)
     - Ao selecionar, setar `node.data.modelId`
   - No `toWorkflowDefinition()`, incluir `modelId` no `input` do step:
     ```json
     {
       "toolName": "onnx_processor",
       "input": {
         "action": "process",
         "parameters": {
           "modelId": "uuid-do-modelo"
         }
       }
     }
     ```

#### Fase 8: Suporte a Modelos Split (.data / .bin)

1. **Alterações no Core (`DynamicOnnxProcessorTool.cs`)**:
   - Inicializar a `InferenceSession` a partir do caminho físico do arquivo `.onnx` (`model.ModelFileName`) quando o modelo estiver armazenado em disco, em vez de carregar os bytes em memória via `File.ReadAllBytes`. Isso permite que o ONNX Runtime resolva caminhos relativos de dados externos e localize automaticamente o arquivo de pesos companheiro (`.data` / `.bin`) contido no mesmo diretório.

2. **Alterações na API (`OnnxModelController.cs`)**:
   - Atualizar a rota de upload `POST /api/onnx/models` para aceitar um arquivo de pesos opcional via multipart: `[FromForm] IFormFile? dataFile = null`.
   - Se um arquivo de pesos (`dataFile`) for fornecido, forçar o armazenamento em disco e criar um diretório específico para o modelo: `wwwroot/onnx-models/{tenantId}/{modelId}/`.
   - Salvar tanto o arquivo `.onnx` principal quanto o arquivo `.data` / `.bin` secundário com seus nomes de arquivo originais preservados dentro desta pasta (critério mandatório para a resolução de caminhos relativos).
   - Ajustar a deleção (`DeleteModel`) para remover recursivamente toda a pasta do modelo em disco (`Directory.Delete(path, true)`).
   - Ajustar as rotas de inspeção (`InspectModel`) e teste (`TestModel`) para carregar a `InferenceSession` a partir do caminho do arquivo se o modelo estiver armazenado no disco.

3. **Alterações na UI (`OnnxModelUploadModal.tsx`)**:
   - Adicionar estado no modal para o arquivo secundário de pesos (`dataFile`) e feedback de drag-over.
   - Apresentar um dropzone secundário dedicado a arquivos de pesos companheiros (`.data`, `.bin`) que aparece dinamicamente assim que um arquivo `.onnx` principal é carregado.
   - Calcular o tamanho somado dos arquivos (`file.size + dataFile.size`) para exibir o aviso de armazenamento em disco (>50MB).
   - Anexar o arquivo de pesos companheiro (`dataFile`) ao payload `FormData` no envio para a API.

### Critérios de Aceite e SLOs

* [ ] Upload de modelo ≤50MB salva no DB; >50MB salva em disco com aviso na UI
* [ ] Modelo inspecionado retorna input/output nodes corretamente
* [ ] Teste com imagem retorna resultado em <30s para modelos típicos (Real-ESRGAN 512×512)
* [ ] Tool `onnx_processor` aparece na lista de tools disponíveis (`GET /api/agent/tools`)
* [ ] Tool executa via API (`POST /api/agent/tools/onnx_processor/execute`) com modelId
* [ ] Página de Modelos IA acessível pela sidebar
* [ ] Workflow Builder permite selecionar modelo ONNX em tool nodes
* [ ] Tenant isolation: tenant A não vê modelos do tenant B
* [ ] Deleção de modelo remove arquivo do disco se aplicável
* [ ] Erros de ONNX Runtime retornam mensagem clara (não stack trace)

### Riscos e Mitigações

| Risco | Mitigação |
|---|---|
| **ONNX Runtime native libs incompatíveis** | Já está no csproj (v1.26.0); testar em Windows/Linux. Se necessário, adicionar `Microsoft.ML.OnnxRuntime.DirectML` para GPU Windows |
| **Modelos muito grandes (>500MB) travam upload** | Limitar a 1GB no controller; mostrar warning progressivo; usar streaming upload |
| **Pós-processamento genérico não funciona para todos os modelos** | `PostProcessConfigJson` permite mapeamento customizado; começar com formato "image" padrão (tensor NCHW → RGB image) |
| **ImageSharp não suporta todos os formatos de imagem** | Suporta JPEG, PNG, WebP, BMP — cobrir 99% dos casos; retornar erro claro para formatos não suportados |
| **Memória insuficiente para modelos grandes** | ONNX Runtime usa CPU; modelos >200MB podem consumir >1GB RAM; documentar requirement mínimo |
| **Concorrência de inferências** | ONNX `InferenceSession` é thread-safe; mas modelos grandes podem saturar CPU; considerar pool de sessions no futuro |

---

## Arquivos a Criar/Modificar

### Criar
| Arquivo | Descrição |
|---|---|
| `src/AgenticSystem.Core/Tools/DynamicOnnxProcessorTool.cs` | Tool genérica de inferência ONNX |
| `src/AgenticSystem.Api/Controllers/OnnxModelController.cs` | API REST para CRUD de modelos |
| `frontend/src/components/onnx/OnnxModelsPage.tsx` | Página principal de modelos |
| `frontend/src/components/onnx/OnnxModelUploadModal.tsx` | Modal de upload |
| `frontend/src/components/onnx/OnnxModelInspectModal.tsx` | Modal de inspeção |
| `frontend/src/components/onnx/OnnxModelTestModal.tsx` | Modal de teste |
| `frontend/src/hooks/useOnnxModels.ts` | React Query hook |

### Modificar
| Arquivo | Alteração |
|---|---|
| `src/AgenticSystem.Core/AgenticSystem.Core.csproj` | Adicionar `SixLabors.ImageSharp` |
| `src/AgenticSystem.Infrastructure/AgenticSystem.Infrastructure.csproj` | Adicionar `SixLabors.ImageSharp` |
| `src/AgenticSystem.Infrastructure/Persistence/Entities/PersistenceEntities.cs` | Adicionar `CustomOnnxModelEntity` |
| `src/AgenticSystem.Infrastructure/Persistence/Configurations/PersistenceConfigurations.cs` | Adicionar `CustomOnnxModelConfiguration` |
| `src/AgenticSystem.Core/Interfaces/ITool.cs` | Adicionar `AI` ao enum `ToolCategory` |
| `src/AgenticSystem.Core/Extensions/ServiceCollectionExtensions.cs` | Registrar `DynamicOnnxProcessorTool` |
| `frontend/src/types/api.ts` | Adicionar tipos ONNX |
| `frontend/src/lib/api.ts` | Adicionar `onnxModelApi` |
| `frontend/src/components/layout/Sidebar.tsx` | Adicionar nav item "Modelos IA" |
| `frontend/src/App.tsx` | Adicionar route `/onnx-models` |
| `frontend/src/components/workflows/WorkflowBuilder.tsx` | Integrar seleção de modelo em tool nodes |
| `src/AgenticSystem.Api/Controllers/OnnxModelController.cs` | **Fase 8**: Suporte a `dataFile` no upload, preservação de nomes de arquivos originais em diretórios específicos e inicialização do `InferenceSession` via path físico. |
| `src/AgenticSystem.Core/Tools/DynamicOnnxProcessorTool.cs` | **Fase 8**: Inicializar `InferenceSession` via path físico do `.onnx` para resolução automática de arquivos de pesos externos (`.data`). |
| `frontend/src/components/onnx/OnnxModelUploadModal.tsx` | **Fase 8**: Adicionar dropzone/seletor secundário para arquivo companheiro de pesos (`.data`/`.bin`) e anexar ao FormData. |

### Gerar
| Arquivo | Comando |
|---|---|
| Migration EF Core | `dotnet ef migrations add AddCustomOnnxModelEntity --project src/AgenticSystem.Infrastructure --startup-project src/AgenticSystem.Api --output-dir Persistence/Migrations` |
