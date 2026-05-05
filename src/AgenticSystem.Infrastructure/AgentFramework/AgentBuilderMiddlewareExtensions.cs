using Microsoft.Agents.AI;
using Microsoft.Extensions.Logging;
using AgenticSystem.Core.Interfaces;

namespace AgenticSystem.Infrastructure.AgentFramework;

/// <summary>
/// Extension methods para registrar middleware customizado no pipeline do AIAgentBuilder.
/// Equivalente a UseLogging() e UseOpenTelemetry() do MAF — porém com lógica de domínio.
/// </summary>
public static class AgentBuilderMiddlewareExtensions
{
    /// <summary>
    /// Adiciona middleware de reflexão pós-ação ao pipeline do agente.
    /// O ReflectionDelegatingAgent avalia confiança da resposta e gera learnings.
    /// </summary>
    public static AIAgentBuilder UseReflection(
        this AIAgentBuilder builder,
        IReflectionEngine reflectionEngine,
        ILogger logger)
    {
        return builder.Use(inner => new ReflectionDelegatingAgent(inner, reflectionEngine, logger));
    }

    /// <summary>
    /// Adiciona middleware de quality gates pré e pós-execução ao pipeline do agente.
    /// O QualityGateDelegatingAgent valida input e output via IQualityGateService.
    /// </summary>
    public static AIAgentBuilder UseQualityGates(
        this AIAgentBuilder builder,
        IQualityGateService qualityGateService,
        ILogger logger)
    {
        return builder.Use(inner => new QualityGateDelegatingAgent(inner, qualityGateService, logger));
    }
}
