# US-43: Processamento Assíncrono de Inferência ONNX e Galeria de Resultados

**Épico:** Dynamic ONNX Model Management  
**Prioridade:** Alta  
**Estimativa (Story Points):** 8 SP  
**Issue Relacionada:** #81  

---

## Descrição

**Como** operador ou desenvolvedor de inteligência artificial do Agentic System,  
**Eu quero** submeter uma imagem para teste de inferência de forma assíncrona, liberar minha tela instantaneamente e ser notificado via SignalR quando o processamento for concluído,  
**Para que** eu possa processar modelos pesados de upscaling em CPU sem sofrer quedas de timeout do servidor Nginx e tenha um catálogo (galeria) permanente para auditar e baixar os resultados obtidos.

---

## Regras de Negócio e Contexto

1. **Fila de Execução Thread-Safe:**
   - As inferências de teste enviadas devem ser enfileiradas em um canal assíncrono em memória (`System.Threading.Channels`).
   - A fila de segundo plano deve respeitar um limite configurável de paralelismo (default: 1 execução concorrente por vez em CPU para evitar sobrecarga de hardware do contêiner Docker).

2. **Estados do Job de Inferência:**
   - Todo job deve começar com status `Pending`.
   - Ao iniciar o processamento no worker, passa para `Processing`.
   - Ao terminar com sucesso, passa para `Completed` e armazena o caminho da imagem gerada (`wwwroot/onnx-results/{tenantId}/{jobId}.png`).
   - Caso ocorra alguma exceção no ONNX Runtime ou na manipulação de imagem, passa para `Failed` e salva a mensagem de erro.

3. **Comunicação em Tempo Real:**
   - Ao mudar o estado do Job, o backend deve publicar o evento via SignalR.
   - O SignalR deve enviar as mensagens (`JobStarted`, `JobCompleted`, `JobFailed`) direcionadas especificamente para o grupo de conexões do respectivo `TenantId`.

4. **Isolamento e Limpeza (Multi-Tenant):**
   - As imagens enviadas (input) e geradas (output) devem ser armazenadas em diretórios separados de forma estrita por `TenantId`.
   - Uma API de limpeza deve permitir apagar jobs antigos se necessário.

5. **Interface de Usuário (Galeria):**
   - Um novo módulo no frontend ("Galeria de Resultados ONNX") listará os jobs executados de forma cronológica reversa.
   - Cada card do job exibirá: nome do modelo, imagem de entrada, imagem de saída (se concluída com sucesso), status do job com badge colorido, latência e botão de download.

---

## Critérios de Aceite (DoD)

- [ ] **Critério 1: Envio Assíncrono com HTTP 202**  
  Ao clicar em "Executar Teste" com uma imagem, a API `POST /api/onnx/models/{id}/test` deve persistir o job no banco de dados com status `Pending`, enfileirá-lo e retornar imediatamente `202 Accepted` contendo o `JobId`, liberando o modal do usuário.
- [ ] **Critério 2: Processamento Robusto em Worker**  
  O hosted service de background deve pegar os itens da fila, carregar o modelo ONNX (usando o cache de sessões ativas para performance), realizar a inferência, salvar o resultado no disco em pasta isolada por tenant e atualizar o banco.
- [ ] **Critério 3: Notificação em Tempo Real (SignalR)**  
  O frontend deve receber mensagens em tempo real através do SignalR (Hub `/hubs/onnx`) notificando o progresso do job. Caso o usuário esteja na tela, a interface deve exibir um toast notificando o término da tarefa.
- [ ] **Critério 4: Galeria Visual com Filtros**  
  A galeria de inferências deve exibir cards responsivos com as imagens geradas, permitindo abrir imagem cheia em modal, filtrar por modelo e ver o tempo de processamento.
- [ ] **Critério 5: Tratamento de Erros e Exceções**  
  Se a inferência falhar (ex: out of memory do contêiner, formato inválido, etc.), o status do job deve transicionar para `Failed` com o log de erro legível, e o usuário deve ser notificado via SignalR da falha.

---

## Dependências Técnicas

* [x] GitHub Issue #81 (Criada e Vinculada)
* [x] ADR-023 (Arquitetura de Processamento Assíncrono)
* [ ] Nova migração do EF Core para criar a tabela `custom_onnx_inference_jobs`.
* [ ] Criação do `OnnxInferenceBackgroundWorker` no projeto `AgenticSystem.Core`.
* [ ] Refatoração do `OnnxModelController.cs` para enfileiramento e criação dos novos endpoints de Jobs.
* [ ] Desenvolvimento dos componentes no React frontend.
