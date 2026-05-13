using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Infrastructure.AgentFramework;

/// <summary>
/// DTO intermediário para desserializar as configurações declarativas do agente a partir do YAML.
/// </summary>
public class AgentYamlDto
{
    public AgentYamlMetadataDto? Metadata { get; set; }
    public AgentYamlExecutionDto? Execution { get; set; }
    public AgentYamlGovernanceDto? Governance { get; set; }
    public AgentYamlAbilitiesDto? Abilities { get; set; }
    public string? Instructions { get; set; }
}

public class AgentYamlMetadataDto
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Tier { get; set; }
    public string? Domain { get; set; }
}

public class AgentYamlExecutionDto
{
    public string? AutonomyLevel { get; set; }
    public string? Model { get; set; }
    public double? Temperature { get; set; }
}

public class AgentYamlGovernanceDto
{
    public List<string>? Policies { get; set; }
}

public class AgentYamlAbilitiesDto
{
    public List<string>? AllowedTools { get; set; }
    public string? WorkflowTemplate { get; set; }
}

/// <summary>
/// Modelo contendo detalhes sobre eventuais erros de validação sintática ou semântica do YAML.
/// </summary>
public class YamlValidationError
{
    public int Line { get; set; }
    public int Column { get; set; }
    public string ErrorCode { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Severity { get; set; } = "Error"; // "Error" ou "Warning"
}

/// <summary>
/// Resultado da operação de validação de YAML.
/// </summary>
public class YamlValidationResult
{
    public bool IsValid { get; set; }
    public List<YamlValidationError> Errors { get; set; } = new();
    public AgentSpecification? Specification { get; set; }
}

/// <summary>
/// Validador declarativo de agentes para carregar e inspecionar YAMLs de configuração.
/// </summary>
public class AgentYamlValidator
{
    private readonly IToolManager? _toolManager;

    public AgentYamlValidator(IToolManager? toolManager = null)
    {
        _toolManager = toolManager;
    }

    /// <summary>
    /// Valida o YAML de forma sintática e semântica de acordo com o esquema da plataforma.
    /// </summary>
    public async Task<YamlValidationResult> ValidateAsync(string yaml, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(yaml))
        {
            return new YamlValidationResult
            {
                IsValid = false,
                Errors = new List<YamlValidationError>
                {
                    new YamlValidationError
                    {
                        Line = 1,
                        Column = 1,
                        ErrorCode = "EMPTY_CONTENT",
                        Message = "O conteúdo do arquivo YAML está vazio.",
                        Severity = "Error"
                    }
                }
            };
        }

        AgentYamlDto dto;
        try
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            dto = deserializer.Deserialize<AgentYamlDto>(yaml);
        }
        catch (YamlException ex)
        {
            return new YamlValidationResult
            {
                IsValid = false,
                Errors = new List<YamlValidationError>
                {
                    new YamlValidationError
                    {
                        Line = (int)ex.Start.Line,
                        Column = (int)ex.Start.Column,
                        ErrorCode = "YAML_SYNTAX_ERROR",
                        Message = $"Erro de sintaxe YAML: {ex.Message}",
                        Severity = "Error"
                    }
                }
            };
        }
        catch (Exception ex)
        {
            return new YamlValidationResult
            {
                IsValid = false,
                Errors = new List<YamlValidationError>
                {
                    new YamlValidationError
                    {
                        Line = 1,
                        Column = 1,
                        ErrorCode = "PARSING_ERROR",
                        Message = $"Erro genérico de processamento: {ex.Message}",
                        Severity = "Error"
                    }
                }
            };
        }

        var errors = new List<YamlValidationError>();

        // ─── Validação Semântica de Metadados ───
        if (dto.Metadata is null)
        {
            errors.Add(new YamlValidationError
            {
                Line = 1,
                Column = 1,
                ErrorCode = "METADATA_SECTION_REQUIRED",
                Message = "A seção 'metadata' é obrigatória no arquivo YAML.",
                Severity = "Error"
            });
        }
        else
        {
            if (string.IsNullOrWhiteSpace(dto.Metadata.Name))
            {
                errors.Add(new YamlValidationError
                {
                    Line = 1,
                    Column = 1,
                    ErrorCode = "NAME_REQUIRED",
                    Message = "O campo 'metadata.name' é obrigatório.",
                    Severity = "Error"
                });
            }
            else if (dto.Metadata.Name.Any(char.IsWhiteSpace) || dto.Metadata.Name.Any(c => !char.IsLetterOrDigit(c) && c != '-' && c != '_'))
            {
                errors.Add(new YamlValidationError
                {
                    Line = 1,
                    Column = 1,
                    ErrorCode = "INVALID_NAME_FORMAT",
                    Message = "O campo 'metadata.name' deve conter apenas letras, números, hifens ou sublinhados, sem espaços.",
                    Severity = "Error"
                });
            }

            if (string.IsNullOrWhiteSpace(dto.Metadata.Description))
            {
                errors.Add(new YamlValidationError
                {
                    Line = 1,
                    Column = 1,
                    ErrorCode = "DESCRIPTION_REQUIRED",
                    Message = "O campo 'metadata.description' é obrigatório.",
                    Severity = "Error"
                });
            }

            if (string.IsNullOrWhiteSpace(dto.Metadata.Tier))
            {
                errors.Add(new YamlValidationError
                {
                    Line = 1,
                    Column = 1,
                    ErrorCode = "TIER_REQUIRED",
                    Message = "O campo 'metadata.tier' é obrigatório (Valores: Chief, Master, Specialist, Support).",
                    Severity = "Error"
                });
            }
            else if (!Enum.TryParse<AgentTier>(dto.Metadata.Tier, true, out _))
            {
                errors.Add(new YamlValidationError
                {
                    Line = 1,
                    Column = 1,
                    ErrorCode = "INVALID_TIER",
                    Message = $"O tier '{dto.Metadata.Tier}' é inválido. Valores aceitos: Chief, Master, Specialist, Support.",
                    Severity = "Error"
                });
            }
        }

        // ─── Validação Semântica de Instruções ───
        if (string.IsNullOrWhiteSpace(dto.Instructions))
        {
            errors.Add(new YamlValidationError
            {
                Line = 1,
                Column = 1,
                ErrorCode = "INSTRUCTIONS_REQUIRED",
                Message = "A propriedade 'instructions' (System Prompt) do agente é obrigatória.",
                Severity = "Error"
            });
        }

        // ─── Validação Semântica do Nível de Autonomia ───
        if (dto.Execution is not null && !string.IsNullOrWhiteSpace(dto.Execution.AutonomyLevel))
        {
            if (!Enum.TryParse<AutonomyLevel>(dto.Execution.AutonomyLevel, true, out _))
            {
                errors.Add(new YamlValidationError
                {
                    Line = 1,
                    Column = 1,
                    ErrorCode = "INVALID_AUTONOMY_LEVEL",
                    Message = $"Nível de autonomia '{dto.Execution.AutonomyLevel}' inválido. Valores aceitos: Manual, Assisted, Supervised, SemiAutonomous, Autonomous, FullAutonomy.",
                    Severity = "Error"
                });
            }
        }

        // ─── Validação Semântica de Ferramentas (AllowedTools) ───
        if (_toolManager is not null && dto.Abilities?.AllowedTools is not null && dto.Abilities.AllowedTools.Count > 0)
        {
            try
            {
                var availableTools = await _toolManager.GetAvailableToolsAsync();
                var availableNames = availableTools.Select(t => t.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

                foreach (var tool in dto.Abilities.AllowedTools)
                {
                    if (!availableNames.Contains(tool))
                    {
                        errors.Add(new YamlValidationError
                        {
                            Line = 1,
                            Column = 1,
                            ErrorCode = "TOOL_NOT_FOUND",
                            Message = $"A ferramenta '{tool}' declarada em 'abilities.allowedTools' não existe no catálogo de habilidades do sistema.",
                            Severity = "Warning" // Emitido como alerta para permitir flexibilidade
                        });
                    }
                }
            }
            catch
            {
                // Se falhar ao listar ferramentas por causa do escopo, silencia ou emite log leve
            }
        }

        if (errors.Any(e => e.Severity == "Error"))
        {
            return new YamlValidationResult
            {
                IsValid = false,
                Errors = errors
            };
        }

        // Mapeando para o modelo oficial do core
        var specification = new AgentSpecification
        {
            Name = dto.Metadata?.Name ?? string.Empty,
            Description = dto.Metadata?.Description ?? string.Empty,
            Domain = dto.Metadata?.Domain ?? string.Empty,
            Instructions = dto.Instructions ?? string.Empty,
            AllowedTools = dto.Abilities?.AllowedTools ?? new(),
            WorkflowTemplate = dto.Abilities?.WorkflowTemplate,
            PolicyIds = dto.Governance?.Policies ?? new(),
        };

        if (dto.Metadata is not null && Enum.TryParse<AgentTier>(dto.Metadata.Tier, true, out var tierVal))
        {
            specification.Tier = tierVal;
        }

        if (dto.Execution is not null)
        {
            if (Enum.TryParse<AutonomyLevel>(dto.Execution.AutonomyLevel, true, out var autonomyVal))
            {
                specification.AutonomyLevel = autonomyVal;
            }

            if (!string.IsNullOrEmpty(dto.Execution.Model))
            {
                specification.Configuration["model"] = dto.Execution.Model;
            }

            if (dto.Execution.Temperature.HasValue)
            {
                specification.Configuration["temperature"] = dto.Execution.Temperature.Value;
            }
        }

        return new YamlValidationResult
        {
            IsValid = true,
            Errors = errors, // Pode conter "Warnings"
            Specification = specification
        };
    }
}
