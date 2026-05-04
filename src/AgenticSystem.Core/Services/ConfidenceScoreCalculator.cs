using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Services;

/// <summary>
/// Maturity Level 7 — Calcula e expõe score de confiança ao usuário.
/// </summary>
public class ConfidenceScoreCalculator : IConfidenceScoreCalculator
{
    public ConfidenceScore Calculate(AgentResponse response, RAGContext? ragContext = null, IEnumerable<Reflection>? reflections = null)
    {
        return Calculate(response, ragContext, reflections, toolAvailability: null);
    }

    public ConfidenceScore Calculate(AgentResponse response, RAGContext? ragContext, IEnumerable<Reflection>? reflections, ToolAvailabilityResult? toolAvailability)
    {
        var factors = new List<string>();
        double totalScore = 0;
        int factorCount = 0;

        // Factor 1: Response success
        if (response.Success)
        {
            totalScore += 1.0;
            factors.Add("Execução bem-sucedida");
        }
        else
        {
            totalScore += 0.1;
            factors.Add("Erro na execução");
        }
        factorCount++;

        // Factor 2: RAG context quality
        if (ragContext != null && ragContext.Chunks.Count > 0)
        {
            var avgScore = ragContext.Chunks.Average(c => c.ReRankedScore);
            totalScore += avgScore;
            factorCount++;

            if (avgScore >= 0.7)
                factors.Add($"Contexto RAG forte ({avgScore:F2})");
            else if (avgScore >= 0.4)
                factors.Add($"Contexto RAG moderado ({avgScore:F2})");
            else
                factors.Add($"Contexto RAG fraco ({avgScore:F2})");
        }
        else
        {
            totalScore += 0.3;
            factorCount++;
            factors.Add("Sem contexto RAG");
        }

        // Factor 3: Tools used (more tools = more grounded)
        if (response.ToolsUsed.Count > 0)
        {
            totalScore += 0.8;
            factors.Add($"{response.ToolsUsed.Count} tool(s) utilizada(s)");
        }
        else
        {
            totalScore += 0.5;
            factors.Add("Nenhuma tool utilizada");
        }
        factorCount++;

        // Factor 4: Reflection history
        if (reflections != null)
        {
            var recentReflections = reflections.ToList();
            if (recentReflections.Count > 0)
            {
                var avgConfidence = recentReflections.Average(r => r.ConfidenceInOutcome);
                totalScore += avgConfidence;
                factorCount++;
                factors.Add($"Histórico de reflexão: {avgConfidence:F2}");
            }
        }

        // Factor 5 (ML20): Tool Availability Coverage
        if (toolAvailability != null && toolAvailability.RequiredCount > 0)
        {
            var coverage = toolAvailability.CoverageRatio;
            // Penalidade severa: 0% coverage → 0.05, 50% → 0.4, 100% → 0.9
            var toolCoverageFactor = coverage < 0.01 ? 0.05 : 0.1 + (coverage * 0.8);
            totalScore += toolCoverageFactor;
            factorCount++;

            if (coverage < 0.01)
                factors.Add($"🚫 Nenhuma tool requerida disponível (coverage: 0%)");
            else if (coverage < 0.5)
                factors.Add($"❗ Cobertura de tools crítica ({coverage:P0})");
            else if (coverage < 1.0)
                factors.Add($"⚠️ Cobertura parcial de tools ({coverage:P0})");
            else
                factors.Add($"✅ Todas as tools disponíveis ({toolAvailability.RequiredCount})");
        }

        var finalScore = factorCount > 0 ? totalScore / factorCount : 0.5;

        var level = finalScore switch
        {
            >= 0.7 => ConfidenceLevel.High,
            >= 0.4 => ConfidenceLevel.Medium,
            >= 0.2 => ConfidenceLevel.Low,
            _ => ConfidenceLevel.RequiresHumanReview
        };

        var label = level switch
        {
            ConfidenceLevel.High => "✅ Alta confiança",
            ConfidenceLevel.Medium => "⚠️ Média confiança",
            ConfidenceLevel.Low => "❗ Baixa confiança",
            ConfidenceLevel.RequiresHumanReview => "🔴 Confirmação recomendada",
            _ => "Unknown"
        };

        return new ConfidenceScore
        {
            Value = Math.Round(finalScore, 3),
            Level = level,
            Label = label,
            Factors = factors,
            RequiresConfirmation = level is ConfidenceLevel.Low or ConfidenceLevel.RequiresHumanReview
        };
    }
}
