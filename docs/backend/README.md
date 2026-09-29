# Backend — contratos, acesso e recursos

Baseline documental: `f8de7a6` (2026-09-28). [Epic #110](https://github.com/JonathanBenicio/Agent-System/issues/110) · [plano documental](../plan/backend-documentation-validation.md).

A correção ativa de #111–#117 está registrada em [ADR-035](../architecture/adr/035-backend-core-isolation-and-reliability.md) e [plano/status](../plan/backend-core-remediation.md). Resultados desta branch e gaps restantes: [relatório da correção](validation/backend-core-remediation.md). A validação de documentação do baseline está preservada em [2026-09-28](validation/2026-09-28.md), sem misturar resultados de branches distintas.

Comece por [operação](operations.md), [API do núcleo](api-core.md), [chat/sessões/configurações do tenant](chat-sessions-settings.md), [inventário de endpoints](endpoint-inventory.md) e [evidência atual](validation/chat-session-settings-2026-09-29.md). O inventário tem 216 métodos/rotas. [Schemas](request-schemas.md) e [OpenAPI Release](openapi-release.json) são snapshots; não incluem automaticamente as rotas adicionadas depois da geração. [Transportes](transports.md) cobre SSE, hubs e protocolos. [Acesso e tenants](access-tenants.md) descreve membership, papéis, grants e regras; [recursos e limites](resources-rules.md) informa teto, enforcement, persistência e limitações de medição.

A [arquitetura canônica](../architecture/backend-architecture-explained.md) descreve topologia. Este hub é a referência operacional; documentos históricos preservam contexto, mas não substituem evidências atuais. [Templates e processo](../../templates/README.md) orientam mudanças futuras.
