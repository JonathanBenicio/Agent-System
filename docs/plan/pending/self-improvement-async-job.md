# Plano: Automação do Self-Improvement Engine via Background Job

**ID do Plano:** `self-improvement-async-job`
**Status:** 🛠️ Em Execução (Fase 1)
**Objetivo:** Transformar o ciclo de melhoria contínua em um processo batch diário e resiliente.

## 1. Mudanças na Estrutura de Dados
Para suportar os novos requisitos:
- **`SelfImprovementRecord`**: Adicionar campo `ConfidenceLevel` (float) e `AutoApplied` (bool).
- **`SystemState`**: Uma nova tabela/registro para armazenar o `LastProcessedReflectionId`.

## 2. Arquitetura do Job
Utilizaremos a infraestrutura nativa do .NET 10:
- **`PeriodicTimer`**: Configurado para 24 horas.
- **`BackgroundService`**: Orquestrador que acorda, solicita a análise e dorme.

## 3. Etapas de Implementação

### Fase 1: Atualização do Core (Services/Models)
- [ ] Refatorar `AnalyzeAndImproveAsync` para aceitar um `sinceId` ou `sinceDate`.
- [ ] Implementar a lógica de cálculo de confiança (baseada na recorrência de reflexões críticas).
- [ ] Atualizar interfaces `ISelfImprovementEngine` e `IOperationalStore`.

### Fase 2: Infraestrutura (O Job)
- [ ] Criar `src/AgenticSystem.Infrastructure/BackgroundServices/SelfImprovementBackgroundJob.cs`.
- [ ] Implementar o loop de execução segura (com `try-catch` e logs detalhados).

### Fase 3: Registro e Configuração
- [ ] Adicionar o serviço no `Program.cs`.
- [ ] Configurar o tempo de execução via `appsettings.json`.

## 4. Critérios de Aceite
- [ ] O Job executa apenas uma vez por dia.
- [ ] Apenas agentes com novas reflexões críticas são analisados.
- [ ] Melhorias com confiança > 0.8 são aplicadas automaticamente.
- [ ] O sistema não re-processa reflexões antigas.
