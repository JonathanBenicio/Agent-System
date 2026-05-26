# Multi-Tenant Architecture Security & Isolation Fix Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Resolver vulnerabilidades de segurança (spoofing), falhas de isolamento de dados em tempo real e padronizar o acesso ao contexto de tenant em todo o backend.

**Architecture:** A solução utiliza o `ITenantContextAccessor` (AsyncLocal) como fonte única da verdade, injetado via Middleware que valida o JWT. O isolamento de dados é garantido por filtros globais do EF Core através da interface `ITenantEntity`.

**Tech Stack:** C#, .NET 8, ASP.NET Core, Entity Framework Core, SignalR.

---

### Task 1: Middleware Hardening

**Files:**
- Modify: `src/AgenticSystem.Api/Middleware/TenantMiddleware.cs`

- [ ] **Step 1: Implement identity validation logic**
Ajustar o `TenantMiddleware` para validar se o `X-Tenant-Id` do header é permitido para o usuário autenticado.

```csharp
// No TenantMiddleware.cs
// Se o usuário não for Admin e houver divergência entre o header e a claim, retornar 403.
var jwtTenantId = context.User?.FindFirst("tenant_id")?.Value;
var isAdmin = context.User?.IsInRole("Admin") ?? false;

if (!string.IsNullOrEmpty(tenantId) && !string.IsNullOrEmpty(jwtTenantId) && tenantId != jwtTenantId && !isAdmin)
{
    context.Response.StatusCode = StatusCodes.Status403Forbidden;
    await context.Response.WriteAsJsonAsync(new { error = "Unauthorized tenant access." });
    return;
}
```

- [ ] **Step 2: Add integration tests for spoofing**
Criar um teste que envia um token do Tenant A com o header do Tenant B e espera um `403 Forbidden`.

- [ ] **Step 3: Commit**

---

### Task 2: OnnxHub Security

**Files:**
- Modify: `src/AgenticSystem.Api/Hubs/OnnxHub.cs`

- [ ] **Step 1: Add claim validation in SubscribeToTenant**
```csharp
public async Task SubscribeToTenant(string tenantId)
{
    var userTenantId = Context.User?.FindFirst("tenant_id")?.Value;
    if (tenantId != userTenantId && !(Context.User?.IsInRole("Admin") ?? false))
    {
        throw new HubException("Unauthorized to subscribe to this tenant.");
    }
    await Groups.AddToGroupAsync(Context.ConnectionId, $"tenant:{tenantId}");
}
```

- [ ] **Step 2: Add claim validation in UnsubscribeFromTenant**
Aplicar a mesma lógica de validação.

- [ ] **Step 3: Commit**

---

### Task 3: Entity Migration (ITenantEntity)

**Files:**
- Modify: `src/AgenticSystem.Infrastructure/Persistence/Entities/AgentPolicyEntity.cs`
- Modify: `src/AgenticSystem.Infrastructure/Persistence/Entities/AgentVersionEntity.cs`
- Modify: `src/AgenticSystem.Infrastructure/Persistence/Entities/PromptTemplateEntity.cs`
- Modify: `src/AgenticSystem.Infrastructure/Persistence/Entities/EvalSuiteResultEntity.cs`

- [ ] **Step 1: Implement ITenantEntity interface**
Adicionar a interface e a propriedade `TenantId` (se não existir).

- [ ] **Step 2: Generate EF Core Migration**
Executar `dotnet ef migrations add AddMultiTenancyToCoreEntities`.

- [ ] **Step 3: Apply Migration**
Executar `dotnet ef database update`.

- [ ] **Step 4: Commit**

---

### Task 4: Controller Standardization

**Files:**
- Modify: `src/AgenticSystem.Api/Controllers/AgentConfigurationController.cs`
- Modify: `src/AgenticSystem.Api/Controllers/DocumentController.cs`
- Modify: `src/AgenticSystem.Api/Controllers/KnowledgeRoomController.cs`

- [ ] **Step 1: Replace direct header access with ITenantContextAccessor**
```csharp
// De:
private string GetTenantId() => Request.Headers["X-Tenant-Id"].FirstOrDefault() ?? "default-tenant";
// Para:
private string GetTenantId() => _tenantContextAccessor.CurrentTenantId;
```

- [ ] **Step 2: Commit**

---

### Task 5: File System Path Protection

**Files:**
- Modify: `src/AgenticSystem.Api/Controllers/OnnxModelController.cs`

- [ ] **Step 1: Ensure paths use validated TenantId**
Garantir que o path físico não dependa de input direto do usuário/header, mas sim do `ITenantContextAccessor`.

- [ ] **Step 2: Commit**
