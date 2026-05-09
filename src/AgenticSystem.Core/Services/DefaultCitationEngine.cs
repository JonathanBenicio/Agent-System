using System.Text.RegularExpressions;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Core.Services;

public class DefaultCitationEngine : ICitationEngine
{
    private readonly ILogger<DefaultCitationEngine> _logger;

    public DefaultCitationEngine(ILogger<DefaultCitationEngine> logger)
    {
        _logger = logger;
    }

    public Task<CitedResponse> GenerateWithCitationsAsync(string response, IReadOnlyList<RankedChunk> sourceChunks, CancellationToken ct = default)
    {
        _logger.LogInformation("🔗 Generating citations for response ({Length} chars) using {Count} chunks", response.Length, sourceChunks.Count);

        var citations = new List<Citation>();
        var citedText = response;
        int citationIndex = 1;

        // 1. Identify overlapping sentences or fragments
        var sentences = SplitIntoSentences(response);

        foreach (var chunk in sourceChunks)
        {
            bool chunkCited = false;

            foreach (var sentence in sentences)
            {
                if (IsOverlap(sentence, chunk.Content))
                {
                    var marker = $" [{citationIndex}]";
                    
                    // Inject marker at the end of the sentence
                    if (citedText.Contains(sentence) && !citedText.Contains($"{sentence}{marker}"))
                    {
                        citedText = citedText.Replace(sentence, $"{sentence}{marker}");
                        chunkCited = true;
                    }
                }
            }

            if (chunkCited)
            {
                citations.Add(new Citation
                {
                    SourceDocumentId = chunk.Id,
                    SourceDocumentName = chunk.Source ?? "Unknown",
                    RelevantExcerpt = chunk.Content.Length > 200 ? chunk.Content[..200] + "..." : chunk.Content,
                    Confidence = chunk.ReRankedScore,
                    Type = CitationType.Reference
                });
                citationIndex++;
            }
        }

        return Task.FromResult(new CitedResponse
        {
            ResponseText = response,
            CitedText = citedText,
            Citations = citations,
            OverallConfidence = sourceChunks.Any() ? sourceChunks.Average(c => c.ReRankedScore) : 0
        });
    }

    public Task<IReadOnlyList<Citation>> ExtractCitationsAsync(string responseText, CancellationToken ct = default)
    {
        var citations = new List<Citation>();
        var matches = Regex.Matches(responseText, @"\[(\d+)\]");

        foreach (Match match in matches)
        {
            citations.Add(new Citation { RelevantExcerpt = match.Value });
        }

        return Task.FromResult<IReadOnlyList<Citation>>(citations);
    }

    private List<string> SplitIntoSentences(string text)
    {
        // Matches non-empty strings ending with punctuation
        var matches = Regex.Matches(text, @"[^.!?]+[.!?]");
        var results = matches.Select(m => m.Value.Trim()).ToList();
        
        // Fallback if no punctuation matches
        if (results.Count == 0 && text.Length > 0) results.Add(text);
        
        return results;
    }

    private bool IsOverlap(string sentence, string chunk)
    {
        var sWords = GetWords(sentence);
        var cWords = GetWords(chunk);

        if (sWords.Count == 0) return false;

        int matches = sWords.Count(sw => cWords.Contains(sw));
        double ratio = (double)matches / sWords.Count;

        return ratio > 0.45; // 45% word overlap threshold
    }

    private HashSet<string> GetWords(string text)
    {
        return text.ToLowerInvariant()
            .Split(new[] { ' ', ',', '.', '!', '?', '(', ')', '"', ':', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 2)
            .ToHashSet();
    }
}
