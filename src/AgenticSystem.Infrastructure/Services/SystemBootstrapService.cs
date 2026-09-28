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
    private readonly IServiceProvider _serviceProvider;

    public SystemBootstrapService(
        AgenticDbContext dbContext,
        IConfiguration configuration,
        ILogger<SystemBootstrapService> logger,
        IServiceProvider serviceProvider)
    {
        _dbContext = dbContext;
        _configuration = configuration;
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    public async Task BootstrapAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // 1. Verifica se já existem Tenants cadastrados no banco de dados.
            // Ignora filtros de tenant se aplicados na instância (geralmente não aplicados no dbContext de startup).
            var tenantExists = await _dbContext.Tenants.IgnoreQueryFilters().AnyAsync(cancellationToken);
            if (!tenantExists)
            {
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

                await _dbContext.SaveChangesAsync(cancellationToken);
                _logger.LogWarning("Tenant 'admin' foi provisionado no PostgreSQL.");
            }
            else
            {
                _logger.LogInformation("Database já possui tenants cadastrados. Pulando criação de tenant padrão.");
            }

            // 3.6. Semeia agentes dinâmicos de Banner Production se não existirem
            var visionAgentExists = await _dbContext.DynamicAgents.IgnoreQueryFilters()
                .AnyAsync(a => a.Name == "VisionAnalyst", cancellationToken);

            if (!visionAgentExists)
            {
                var visionAgent = new DynamicAgentEntity
                {
                    Name = "VisionAnalyst",
                    Description = "Analista Visual de Imóveis (Ollama Vision)",
                    Domain = "general",
                    Tier = (int)AgentTier.Specialist,
                    Instructions = "Voce e um analista visual de imoveis. Descreva os defeitos da foto (fios, postes) focando no topo da imagem, e identifique os pontos fortes. Fale em portugues de forma concisa.",
                    AutonomyLevel = (int)AutonomyLevel.Supervised,
                    AllowedToolsJson = "[]",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _dbContext.DynamicAgents.Add(visionAgent);
                _logger.LogInformation("Agente dinâmico 'VisionAnalyst' semeado com sucesso.");
            }

            var editorAgentExists = await _dbContext.DynamicAgents.IgnoreQueryFilters()
                .AnyAsync(a => a.Name == "EditorChefe", cancellationToken);

            if (!editorAgentExists)
            {
                var editorAgent = new DynamicAgentEntity
                {
                    Name = "EditorChefe",
                    Description = "Editor Chefe de Banner Publicitário",
                    Domain = "general",
                    Tier = (int)AgentTier.Specialist,
                    Instructions = "Voce recebe a analise visual. Siga ESTRITAMENTE estes passos na ordem: 1) Chame a ferramenta CleanImageAsync passando a analise. 2) Chame RenderBannerAsync informando o caminho limpo retornado, o preco, os quartos, o bairro (location) e o telefone do corretor (phone). 3) Retorne o caminho da foto final.",
                    AutonomyLevel = (int)AutonomyLevel.Supervised,
                    AllowedToolsJson = "[\"CleanImageAsync\",\"RenderBannerAsync\"]",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _dbContext.DynamicAgents.Add(editorAgent);
                _logger.LogInformation("Agente dinâmico 'EditorChefe' semeado com sucesso.");
            }

            // 3.7. Semeia/atualiza o workflow de banner dinâmico baseado em grafos do MAF
            var existingWorkflow = await _dbContext.WorkflowDefinitions.IgnoreQueryFilters()
                .FirstOrDefaultAsync(w => w.Id == "banner-production", cancellationToken);

            if (existingWorkflow == null || existingWorkflow.Version < 4 || !existingWorkflow.DefinitionJson.Contains("Edges"))
            {
                if (existingWorkflow != null)
                {
                    _dbContext.WorkflowDefinitions.Remove(existingWorkflow);
                }

                var bannerDef = new WorkflowDefinition
                {
                    Id = "banner-production",
                    Name = "Banner Production Workflow",
                    Description = "Orquestra a produção de um banner imobiliário via MAF.",
                    Version = 4, // Versão atualizada com múltiplos modelos (LLM Manager)
                    TriggerType = WorkflowTriggerType.Manual,
                    PromptTemplate = "Analise a foto, aplique a remocao de defeitos e desenhe um banner. Preco: {{price}}, Quartos: {{bedrooms}}. O caminho original e: {{imagePath}}",
                    Steps = new System.Collections.Generic.List<WorkflowStep>
                    {
                        new WorkflowStep
                        {
                            Id = "vision-step",
                            Name = "Analista Visual",
                            StepType = WorkflowStepType.Agent,
                            AgentName = "VisionAnalyst",
                            ModelOverride = "llama3.2-vision",
                            Input = new System.Collections.Generic.Dictionary<string, object>()
                        },
                        new WorkflowStep
                        {
                            Id = "editor-step",
                            Name = "Editor Chefe",
                            StepType = WorkflowStepType.Agent,
                            AgentName = "EditorChefe",
                            ModelOverride = "llama3-8b",
                            DependsOn = new System.Collections.Generic.List<string> { "vision-step" },
                            AllowedToolsOverride = new System.Collections.Generic.List<string> { "CleanImageAsync", "RenderBannerAsync" },
                            Input = new System.Collections.Generic.Dictionary<string, object>()
                        }
                    },
                    Edges = new System.Collections.Generic.List<WorkflowEdge>
                    {
                        new WorkflowEdge
                        {
                            FromStepId = "vision-step",
                            ToStepId = "editor-step"
                        }
                    }
                };

                var bannerEntity = new WorkflowDefinitionEntity
                {
                    Id = bannerDef.Id,
                    TenantId = "admin",
                    Name = bannerDef.Name,
                    Version = bannerDef.Version,
                    DefinitionJson = System.Text.Json.JsonSerializer.Serialize(bannerDef),
                    CreatedAt = DateTime.UtcNow
                };

                _dbContext.WorkflowDefinitions.Add(bannerEntity);
                _logger.LogInformation("Workflow dinâmico de grafo 'banner-production' provisionado no banco de dados (Versão 4 - Multi-Model).");
            }

            // 4. Salva de forma transacional e resiliente
            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogWarning("Auto-bootstrap e semeação dinâmica concluídos com sucesso no PostgreSQL.");
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex, "Concorrência detectada: O tenant 'admin' ou registros padrão já foram criados em outra instância concorrente.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro fatal inesperado durante o auto-bootstrap do banco de dados.");
            throw;
        }

    }
}
