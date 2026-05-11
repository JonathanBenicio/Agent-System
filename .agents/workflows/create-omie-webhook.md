---
description: Geração de endpoint de recepção rápida e worker assíncrono para Webhooks da Omie
---

# Criar Endpoint de Webhook Omie
Gera uma rota de recebimento rápido (200 OK) e um worker assíncrono para processar eventos da Omie de forma segura e escalável.

## Passo a Passo

1. **Coleta de Informações:**
   Pergunte ao usuário qual é o evento do webhook da Omie que ele deseja processar (ex: `produto.alterado`, `conta_pagar.alterada`, etc.) e qual será o nome do serviço ou worker responsável (ex: `ComissaoSyncService`).

2. **Criação do Endpoint de Recebimento:**
   - Crie uma rota `POST` no padrão do projeto.
   - O endpoint deve realizar apenas uma validação superficial do payload (JSON).
   - Envie os dados imediatamente para a fila de processamento em background (ex: Celery worker ou BackgroundTasks).
   - A rota DEVE retornar o status HTTP 200 (OK) instantaneamente para evitar o timeout de 7 segundos da Fila Principal da Omie.

3. **Criação do Worker (Processamento Assíncrono):**
   - Crie a função assíncrona ou task que processará a regra de negócio real.
   - Inclua logs estruturados no início e no fim da execução, utilizando o ID da transação da Omie.
   - Envolva a lógica em um bloco `try/except` robusto (error handling) para que falhas de negócio não quebrem o worker silenciosamente.

4. **Instruções Finais e Teste:**
   - Gere um payload de exemplo em JSON e um comando `curl` para o usuário testar a rota localmente.
   - Adicione um lembrete: "Lembre-se de que a configuração no painel do Omie exige acesso de Administrador e só tem efeito ao recarregar o aplicativo em uma nova sessão."
