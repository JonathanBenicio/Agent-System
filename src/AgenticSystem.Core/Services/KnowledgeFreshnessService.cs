using System.Collections.Concurrent;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Core.Services;

/// <summary>
/// Maturity Level 6 — Monitora frescor de documentos e detecta drift de conhecimento.
/// </summary>
public class KnowledgeFreshnessService : IKnowledgeFreshnessService
{
    private readonly ConcurrentDictionary<string, KnowledgeFreshness> _freshnessData = new();
    private readonly ILogger<KnowledgeFreshnessService> _logger;

    public KnowledgeFreshnessService(ILogger<KnowledgeFreshnessService> logger)
    {
        _logger = logger;
    }

    public Task<KnowledgeFreshness> GetFreshnessAsync(string documentId)
    {
        var freshness = _freshnessData.GetOrAdd(documentId, id => new KnowledgeFreshness
        {
            DocumentId = id,
            ContentDate = DateTime.UtcNow,
            FreshnessScore = 1.0
        });

        return Task.FromResult(freshness);
    }

    public Task SetValidityPeriodAsync(string documentId, TimeSpan validity)
    {
        var freshness = _freshnessData.GetOrAdd(documentId, id => new KnowledgeFreshness
        {
            DocumentId = id,
            ContentDate = DateTime.UtcNow
        });

        freshness.ValidityPeriod = validity;
        freshness.ExpiresAt = freshness.ContentDate + validity;

        _logger.LogInformation("Validity period set for document {DocumentId}: {Validity}", documentId, validity);
        return Task.CompletedTask;
    }

    public Task<IEnumerable<KnowledgeFreshness>> GetStaleDocumentsAsync()
    {
        var stale = _freshnessData.Values
            .Where(f => f.IsPotentiallyStale || (f.ExpiresAt.HasValue && f.ExpiresAt.Value < DateTime.UtcNow))
            .OrderBy(f => f.FreshnessScore)
            .AsEnumerable();

        return Task.FromResult(stale);
    }

    public Task<DriftReport> DetectDriftAsync(string documentId)
    {
        var freshness = _freshnessData.GetOrAdd(documentId, id => new KnowledgeFreshness
        {
            DocumentId = id,
            ContentDate = DateTime.UtcNow
        });

        var report = new DriftReport
        {
            DocumentId = documentId
        };

        // Temporal drift — document age
        var age = DateTime.UtcNow - freshness.ContentDate;
        if (age.TotalDays > 90)
        {
            report.DriftIndicators.Add($"Document is {age.TotalDays:F0} days old");
            report.DriftScore += 0.3;
        }

        // Validity expired
        if (freshness.ExpiresAt.HasValue && freshness.ExpiresAt.Value < DateTime.UtcNow)
        {
            var overdue = DateTime.UtcNow - freshness.ExpiresAt.Value;
            report.DriftIndicators.Add($"Validity expired {overdue.TotalDays:F0} days ago");
            report.DriftScore += 0.4;
        }

        // Not verified recently
        var sinceLast = DateTime.UtcNow - freshness.LastVerifiedAt;
        if (sinceLast.TotalDays > 30)
        {
            report.DriftIndicators.Add($"Not verified for {sinceLast.TotalDays:F0} days");
            report.DriftScore += 0.2;
        }

        report.HasDrift = report.DriftScore >= 0.3;
        report.DriftScore = Math.Min(1.0, report.DriftScore);

        // Update freshness based on drift analysis
        freshness.FreshnessScore = Math.Max(0.0, 1.0 - report.DriftScore);
        freshness.IsPotentiallyStale = report.HasDrift;
        if (report.HasDrift)
            freshness.DriftReason = string.Join("; ", report.DriftIndicators);

        _logger.LogDebug("Drift analysis for {DocumentId}: score={DriftScore:F2}, hasDrift={HasDrift}",
            documentId, report.DriftScore, report.HasDrift);

        return Task.FromResult(report);
    }

    public Task MarkVerifiedAsync(string documentId)
    {
        if (_freshnessData.TryGetValue(documentId, out var freshness))
        {
            freshness.LastVerifiedAt = DateTime.UtcNow;
            freshness.FreshnessScore = 1.0;
            freshness.IsPotentiallyStale = false;
            freshness.DriftReason = null;

            if (freshness.ValidityPeriod.HasValue)
                freshness.ExpiresAt = DateTime.UtcNow + freshness.ValidityPeriod.Value;

            _logger.LogInformation("Document {DocumentId} marked as verified", documentId);
        }

        return Task.CompletedTask;
    }

    public Task<double> CalculateFreshnessScoreAsync(string documentId)
    {
        if (!_freshnessData.TryGetValue(documentId, out var freshness))
            return Task.FromResult(1.0);

        var age = DateTime.UtcNow - freshness.ContentDate;
        var agePenalty = Math.Min(0.5, age.TotalDays / 365.0);
        var verificationBonus = (DateTime.UtcNow - freshness.LastVerifiedAt).TotalDays < 7 ? 0.2 : 0.0;

        var score = Math.Max(0.0, Math.Min(1.0, 1.0 - agePenalty + verificationBonus));
        freshness.FreshnessScore = score;

        return Task.FromResult(score);
    }
}
