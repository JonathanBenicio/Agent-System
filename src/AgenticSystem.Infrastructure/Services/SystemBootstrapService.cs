using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.Persistence;
using AgenticSystem.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Infrastructure.Services;

/// <summary>
/// Serviço de auto-bootstrap executado no startup do sistema.
/// Garante a criação do tenant administrador inicial e de suas API Keys a partir do appsettings.json.
/// </summary>
public sealed class SystemBootstrapService : ISystemBootstrapService
{
    private readonly AgenticDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SystemBootstrapService> _logger;

    public SystemBootstrapService(
        AgenticDbContext dbContext,
        IConfiguration configuration,
        ILogger<SystemBootstrapService> logger)
    {
        _dbContext = dbContext;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task BootstrapAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // 1. Verifica se já existem Tenants cadastrados no banco de dados.
            // Ignora filtros de tenant se aplicados na instância (geralmente não aplicados no dbContext de startup).
            var tenantExists = await _dbContext.Tenants.IgnoreQueryFilters().AnyAsync(cancellationToken);
            if (tenantExists)
            {
                _logger.LogInformation("Database já possui tenants cadastrados. Ignorando auto-bootstrap.");
                return;
            }

            _logger.LogWarning("Nenhum tenant encontrado no banco de dados. Iniciando provisionamento do Tenant 'admin'...");

            // 2. Cria o Tenant admin padrão
            var adminTenant = new Tenant
            {
                Id = "admin",
                Name = "Administrator Tenant",
                Slug = "admin",
                Plan = TenantPlan.Pro,
                Limits = TenantLimits.ProTier(),
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _dbContext.Tenants.Add(adminTenant);

            // 3. Obtém e Husha a chave legada AdminApiKey do appsettings.json para garantir retrocompatibilidade de acesso.
            var legacyApiKey = _configuration["AgenticSystem:AdminApiKey"];
            if (!string.IsNullOrWhiteSpace(legacyApiKey))
            {
                var keyBytes = Encoding.UTF8.GetBytes(legacyApiKey.Trim());
                var hashBytes = SHA256.HashData(keyBytes);
                var keyHash = Convert.ToHexString(hashBytes).ToLowerInvariant();

                var adminAccessKey = new AccessApiKeyEntity
                {
                    Id = Guid.NewGuid(),
                    TenantId = adminTenant.Id,
                    KeyHash = keyHash,
                    Name = "Default Legacy Admin API Key",
                    Role = "Admin",
                    IsEnabled = true,
                    CreatedAt = DateTime.UtcNow
                };

                _dbContext.AccessApiKeys.Add(adminAccessKey);
                _logger.LogInformation("Chave API administrativa legada (AdminApiKey) migrada e hashed com sucesso no banco de dados para o Tenant 'admin'.");
            }
            else
            {
                _logger.LogCritical("AgenticSystem:AdminApiKey não está configurado no appsettings.json! O sistema iniciará sem uma chave padrão.");
            }

            // 3.5. Seed the Banner Production Workflow
            var bannerWorkflowExists = await _dbContext.WorkflowDefinitions.IgnoreQueryFilters()
                .AnyAsync(w => w.Id == "banner-production", cancellationToken);

            if (!bannerWorkflowExists)
            {
                var bannerDef = new WorkflowDefinition
                {
                    Id = "banner-production",
                    Name = "Banner Production Workflow",
                    Description = "Orquestra a produção de um banner imobiliário via MAF.",
                    Version = 1,
                    TriggerType = WorkflowTriggerType.Manual,
                    Steps = new System.Collections.Generic.List<WorkflowStep>
                    {
                        new WorkflowStep
                        {
                            Id = "step-1",
                            Name = "Gerar Banner Imobiliário",
                            StepType = WorkflowStepType.Action,
                            ToolName = "banner-production",
                            ActionDescription = "generate",
                            Input = new System.Collections.Generic.Dictionary<string, object>
                            {
                                // Valores padrão (podem ser sobrescritos pelo executor do workflow)
                                { "imagePath", "c:\\temp\\imovel.jpg" },
                                { "price", 650000 },
                                { "bedrooms", 3 }
                            }
                        }
                    }
                };

                var bannerEntity = new WorkflowDefinitionEntity
                {
                    Id = bannerDef.Id,
                    TenantId = adminTenant.Id,
                    Name = bannerDef.Name,
                    Version = bannerDef.Version,
                    DefinitionJson = System.Text.Json.JsonSerializer.Serialize(bannerDef),
                    CreatedAt = DateTime.UtcNow
                };

                _dbContext.WorkflowDefinitions.Add(bannerEntity);
                _logger.LogInformation("Workflow 'banner-production' provisionado no banco de dados para o Tenant 'admin'.");
            }

            // 4. Salva de forma transacional e resiliente
            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogWarning("Auto-bootstrap concluído com sucesso. Tenant 'admin' foi provisionado no PostgreSQL.");
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex, "Concorrência detectada: O tenant 'admin' já foi criado em outra instância de startup concorrente.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro fatal inesperado durante o auto-bootstrap do banco de dados.");
            throw;
        }
    }
}
