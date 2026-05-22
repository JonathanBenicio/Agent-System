# language: pt
Feature: Processamento Assíncrono de Inferência ONNX
  Como um desenvolvedor de IA no Agentic System,
  Eu quero disparar testes de inferência ONNX de forma assíncrona,
  Para que a tela não congele, eu não sofra timeouts e consiga ver o histórico na galeria de resultados.

  Background:
    Given que o usuário está autenticado no tenant "default-tenant"
    And possui o modelo ONNX "4xNomos8kDAT.onnx" cadastrado e ativo com ID "model-nomos-001"

  Scenario: Submissão de inferência de teste com sucesso em background
    Given que o usuário acessou a tela de teste do modelo "model-nomos-001"
    When ele seleciona a imagem "input-test.jpg" e clica em "Executar Teste"
    Then a API do backend deve retornar status code 202 com o "JobId"
    And a interface deve exibir um toast notificando "Iniciando processamento em background"
    And a tela deve ser liberada imediatamente para navegação
    And o job no banco de dados deve ser criado com status "Pending"

  Scenario: Conclusão do processamento com notificação em tempo real
    Given que o job com ID "job-inference-999" está na fila com status "Pending"
    When o worker de background "OnnxInferenceBackgroundWorker" processa o job com sucesso
    Then o status do job no banco de dados deve transicionar para "Completed"
    And a imagem gerada deve ser salva no diretório "wwwroot/onnx-results/default-tenant/job-inference-999.png"
    And o SignalR deve emitir o evento "JobCompleted" para o tenant "default-tenant" com a url da imagem de saída
    And a interface do usuário deve exibir uma notificação pop-up informando "Inferência do modelo finalizada com sucesso"

  Scenario Outline: Falha no processamento por erro do ONNX Runtime
    Given que o job com ID "job-inference-888" está em processamento com status "Processing"
    When ocorre uma exceção "<ErroException>" durante o processamento do modelo no worker
    Then o status do job no banco de dados deve transicionar para "Failed"
    And o SignalR deve emitir o evento "JobFailed" com o erro "<MensagemErro>"
    And a galeria de resultados deve exibir o badge "Falhou" com o log do erro

    Examples:
      | ErroException            | MensagemErro                                |
      | OnnxRuntimeException     | Model loading failed: invalid shape dims     |
      | OutOfMemoryException     | System out of memory while executing tensor |
