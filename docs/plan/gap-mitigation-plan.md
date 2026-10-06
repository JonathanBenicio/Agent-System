# Roadmap: Mitigação de Gaps Técnicos de Segurança e Performance

> **Status documental:** Draft / Planejamento Futuro
> **Escopo:** Mitigação de gaps no processador ONNX dinâmico, SignalR tenant isolation, background workers multi-tenant scope e alinhamento de documentação de CI
> **Fonte de verdade operacional:** codebase_gap_analysis.md (referência local externa indisponível)
> **Gerado em:** 2026-05-21
> **Projeto:** AgenticSystem

---

## Objetivo

Descrever o roteiro técnico detalhado para sanar os gaps identificados no codebase do **Agentic System**. Esta iniciativa visa garantir um runtime de inferência local extremamente rápido, blindar a segurança de isolamento empresarial (multi-tenancy) na camada de mensageria em tempo real e de processamento assíncrono em background, e alinhar a governança de documentação com a infraestrutura ativa.

## Princípios de Implantação

1. **Blindagem Contra Falha Humana:** O isolamento de tenants deve ocorrer a nível infraestrutural e automático, eliminando a necessidade de wrappers manuais repetitivos por parte de desenvolvedores.
2. **Reuso Máximo de Recursos:** A inferência local de ONNX in-process deve reusar sessões quentes pré-compiladas, minimizando I/O e computação redundante.
3. **Garantia de Isolamento Assíncrono:** Threads secundárias e tarefas de background (Outbox, Consolidators) devem estar explicitamente cientes do Tenant ativado para garantir o funcionamento correto de filtros globais de consultas e persistência de dados.
4. **Retrocompatibilidade de Comunicação:** O SignalR deve aceitar conexões com ou sem cabeçalhos do tenant em ambiente de desenvolvimento, mas deve ser estrito em produção.

## Fases e Sequenciamento

| Ordem | Frente/Fase | Motivo do sequenciamento |
|---|---|---|
| 1 | **Fase 1: Alinhamento de Documentação (CI)** | Rápido ajuste corretivo para unificar as diretrizes de compilação da equipe. |
| 2 | **Fase 2: Blindagem Multi-tenant (SignalR & Workers)** | Bloqueio imediato da principal vulnerabilidade lógica mapeada no backend, no frontend e nos processadores em background. |
| 3 | **Fase 3: Otimização de Performance (ONNX Cache)** | Implementação do cache pool dinâmico concorrente in-process para reuso de sessões. |

---

## Detalhamento: Otimização Dinâmica de ONNX & Segurança Multi-Tenant

### Por que implementar?
- **ONNX Churn:** Atualmente, a reinstanciação consecutiva de `InferenceSession` na `DynamicOnnxProcessorTool` acarreta em latências massivas de segundos por requisição de imagem, degradando a experiência de chat agêntico.
- **Vulnerabilidade de Tenant (SignalR):** A necessidade de chamar `using var tenantScope = ...` manualmente em cada método de hub no backend corre o risco de introduzir vazamentos se novas implementações esquecerem essa linha.
- **Vulnerabilidade de Tenant (Workers):** O processamento de mensagens outbox e a consolidação de sessões em threads de background correm sob o `ITenantContextAccessor` sem escopo inicializado (nulo/vazio). Isso faz com que filtros globais de consulta do EF Core barrem registros legítimos de locatários ativos ou persistam dados incorretamente sob a identidade padrão.

### Arquitetura-alvo

```
[Frontend App] 
     │ (Handshake: Injeta activeWorkspaceId como X-Tenant-Id)
     ▼
[Nginx/SignalR Gateway]
     │
     ▼
[TenantHubFilter] ──(Auto-resolve tenant & inicializa escopo)──► [ChatHub.cs (Limpo)]
                                                                       │
                                                                       ▼
                                                          [DynamicOnnxProcessorTool]
                                                                       │
                                                        (Reutiliza InferenceSession)
                                                                       ▼
                                                            [OnnxSessionCache]
```

### Componentes propostos
| Componente | Papel |
|---|---|
| `IOnnxSessionCache` | Interface que define o contrato do pool de sessões ONNX. |
| `OnnxSessionCache` | Singleton thread-safe concorrente com políticas LRU para armazenamento em memória de `InferenceSession`. |
| `TenantHubFilter` | Filtro global de SignalR (`IHubFilter`) para automatização e blindagem de escopos de `TenantContext`. |
| `OutboxProcessorBackgroundService` | Consumidor asssíncrono ajustado para contornar o filtro de queries global (`IgnoreQueryFilters()`) na listagem inicial de mensagens do outbox e restaurar o escopo do tenant dinamicamente antes de processar/publicar cada evento. |
| `SessionAutoConsolidator` | Worker de consolidação de sessões ajustado para envolver as iterações de cada workspace ativo sob o escopo correspondente do `ITenantContextAccessor`. |

### Plano por etapas

#### Etapa 1: Governança & Alinhamento
- [ ] Atualizar o arquivo `AGENTS.md` removendo avisos obsoletos sobre .NET 8 no CI.
- [ ] Sincronizar índices do projeto.

#### Etapa 2: Blindagem de Segurança Multi-Tenant (SignalR & Workers)
- [ ] Criar o `TenantHubFilter.cs` em `src/AgenticSystem.Api/SignalR/`.
- [ ] Registrar o filtro global em `Program.cs` nas opções do SignalR.
- [ ] Refatorar `ChatHub.cs` removendo o código duplicado de inicialização de escopos.
- [ ] Atualizar `signalr.ts` e `signalr-gateway.ts` no frontend para sincronizar o workspace ativo.
- [ ] Modificar `OutboxProcessorBackgroundService.cs` para chamar `.IgnoreQueryFilters()` no carregamento de mensagens e encapsular a iteração de eventos via `ITenantContextAccessor.BeginScope(...)`.
- [ ] Modificar `SessionAutoConsolidator.cs` para inicializar explicitamente o escopo do `TenantContext` correspondente em cada iteração do loop de tenants.

#### Etapa 3: Cache Pool Concorrente do ONNX
- [ ] Criar interface e implementação de `OnnxSessionCache` em `src/AgenticSystem.Core/Services/Ml/`.
- [ ] Registrar como serviço Singleton em `ServiceCollectionExtensions.cs`.
- [ ] Modificar `DynamicOnnxProcessorTool.cs` para consumir sessões quentes do cache, removendo o `using var` de descarte imediato.

### Critérios de Aceite e SLOs
* [ ] **Desempenho ONNX:** Latência da inferência de imagem recorrente com o mesmo modelo reduzida para < 50ms (SLO do backend).
* [ ] **Isolamento de Segurança:** Nenhuma query é disparada fora do escopo de tenant correspondente do SignalR (validado via interceptador de auditoria).
* [ ] **Segurança em Workers:** Processamento de mensagens do Outbox e do Auto-Consolidator flui perfeitamente com o isolamento de dados do EF Core ativo por tenant, sem erros de filtros de banco.
* [ ] **Estabilidade:** 100% de sucesso na suíte de testes locais de integração (`dotnet test`).

### Riscos e Mitigações
| Risco | Mitigação |
|---|---|
| **Esgotamento de Memória por Cache ONNX** | Implementação estrita de expiração LRU e descarte (`session.Dispose()`) automático de sessões ociosas em background. |
| **Erros no Handshake sem JWT no WebSocket** | Fallback controlado para tenant default e logs estruturados em desenvolvimento; erro de handshake em produção. |
| **Locks Concorrentes no Outbox** | O escopo do `TenantContext` é inicializado e limpo imediatamente após a publicação de cada evento, mantendo o tempo de transação baixo. |cket** | Fallback controlado para tenant default e logs estruturados em desenvolvimento; erro de handshake em produção. |
