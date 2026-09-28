# Backend — contratos, acesso e recursos
Baseline documental: f8de7a6 (2026-09-28). [Epic #110](https://github.com/JonathanBenicio/Agent-System/issues/110) • [Plano](../plan/backend-documentation-validation.md).

Comece por [operação](operations.md) para executar e por [API do núcleo](api-core.md) para consumir o backend. [Inventário HTTP](endpoint-inventory.md) cobre todos os controladores; [schemas dos DTOs](request-schemas.md) e [OpenAPI Release](openapi-release.json) detalham binding. [Transportes](transports.md) complementa com SSE, hubs e protocolos. [Acesso e tenants](access-tenants.md) diferencia regras atuais da hierarquia desejada. [Recursos e limites](resources-rules.md) mostra fontes e pontos de enforcement.

Consulte [validação](validation/2026-09-28.md) e [backlog](backlog.md) antes de concluir que uma capacidade está estável. "Documentado por leitura" não significa teste integrado aprovado. O alvo de acesso em [ADR-034](../architecture/adr/034-backend-contracts-and-access-target.md) ainda exige implementação.
As [correções da revisão do PR](validation/2026-09-28-review-fixes.md) registram asserções, consolidação de resultados e persistência de mensagens; não encerram os bugs de produto.

A [arquitetura canônica](../architecture/backend-architecture-explained.md) descreve topologia. Estes guias são a referência operacional dos contratos; documentos históricos não substituem as evidências datadas aqui. [Templates e processo](../../templates/README.md) orientam mudanças futuras.
