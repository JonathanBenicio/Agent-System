# ADR 023: Processamento Assíncrono de Inferência ONNX e Galeria de Resultados

**Status:** Proposto  
**Data:** 22 de Maio de 2026  
**Autor(es):** Antigravity DevOps & Architect Swarm

---

## Contexto

A inferência de modelos ONNX no backend (.NET) utilizando o `DynamicOnnxProcessorTool` é executada de forma síncrona dentro da thread de requisição HTTP (`POST /api/onnx/models/{id}/test`).
Quando o usuário envia uma imagem para testar um modelo pesado de upscaling (como o `4xNomos8kDAT.onnx` ou similares), a inferência rodando em CPU local em contêineres Docker excede frequentemente 60 segundos. 

Como o proxy reverso do Nginx no contêiner frontend possui um timeout de leitura padrão de 60 segundos (`proxy_read_timeout 60s`), o Nginx encerra prematuramente a conexão com o cliente e retorna um erro **504 Gateway Timeout** antes que o backend .NET conclua a inferência.

Para sanar este problema definitivamente em nível de produção e otimizar a experiência do usuário (UX):
1. A inferência não deve bloquear a thread HTTP.
2. A tela do frontend deve ser liberada imediatamente, permitindo execução paralela e assíncrona.
3. O frontend deve receber atualizações em tempo real do progresso/resultado via SignalR.
4. Os resultados de inferências executadas devem ser persistidos e disponibilizados em uma galeria visual dedicada.

## Decisão

Adotaremos uma **arquitetura orientada a eventos assíncronos baseada em fila de mensagens em memória com notificações em tempo real (SignalR) e persistência de Jobs**:

1. **Persistência de Jobs:** Criar uma entidade de banco de dados `CustomOnnxInferenceJobEntity` para gerenciar o estado (`Pending`, `Processing`, `Completed`, `Failed`) de cada inferência, com caminhos de entrada e saída isolados por tenant.
2. **Fila de Execução:** Utilizar `System.Threading.Channels` em memória no backend (.NET) para enfileirar as tarefas de inferência de forma thread-safe e de alta performance.
3. **Background Worker:** Implementar um `BackgroundService` hospedado (`OnnxInferenceBackgroundWorker`) para consumir e processar as tarefas da fila de forma assíncrona.
4. **Comunicação em Tempo Real:** Criar um hub SignalR dedicado `/hubs/onnx` (ou reaproveitar o `WorkflowHub`) para transmitir atualizações de status de jobs (`JobStarted`, `JobCompleted`, `JobFailed`) ao frontend.
5. **Galeria no Frontend:** Criar um componente de Galeria de Resultados no frontend para que o usuário possa consultar e baixar todas as inferências já realizadas.

## Justificativa

1. **Melhoria Absoluta na UX (User Experience):** A tela de teste de modelo não congela e não sofre quedas de conexão ou timeouts do navegador. O usuário é livre para navegar pelo sistema enquanto o modelo processa.
2. **Escalabilidade e Resiliência:** Evita o esgotamento do pool de threads do servidor web (.NET Kestrel) por requisições síncronas bloqueantes de longa duração.
3. **Persistência de Histórico:** O usuário não perde os resultados de inferências passadas, permitindo a comparação de qualidade entre diferentes imagens e modelos.
4. **Tecnologia Nativa .NET:** `System.Threading.Channels` é extremamente leve, nativo e de altíssima performance, ideal para filas locais de CPU simples sem a complexidade extra de brokers como RabbitMQ ou Hangfire neste estágio.

## Consequências

### Positivas
* **Zero Timeouts:** Nginx e navegador não sofrerão timeouts, pois a chamada HTTP retorna `202 Accepted` de forma quase instantânea.
* **Execuções Simultâneas:** O backend pode processar jobs em paralelo (ou serializado em fila única de CPU) de acordo com a capacidade do hardware configurada.
* **Rastreabilidade Completa:** Todo teste de modelo fica registrado com latência, sucesso/falha e payloads.

### Desafios / Pontos de Atenção (Negativas)
* **Complexidade de Armazenamento:** Imagens de entrada e saída precisarão ser armazenadas em disco (com isolamento de tenant) e expostas via arquivos estáticos (`wwwroot/onnx-results/{tenantId}/{jobId}.png`), demandando cuidado com limpeza periódica (políticas de retenção).
* **Migração de Banco de Dados:** Exige nova migration para criar a tabela `custom_onnx_inference_jobs`.
