using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgenticSystem.Infrastructure.Integrations;

public class ObsidianNotesProvider : INotesProvider
{
    private readonly string _notesRootPath;
    private readonly ILogger<ObsidianNotesProvider> _logger;

    public ObsidianNotesProvider(IOptions<IntegrationProviderOptions> options, ILogger<ObsidianNotesProvider> logger)
    {
        _logger = logger;
        var settings = options.Value;
        _notesRootPath = settings.NotesRootPath
            ?? Path.Combine(settings.DataRootPath ?? Path.Combine(AppContext.BaseDirectory, "data", "integrations"), "notes");
    }

    public string Name => "ObsidianNotes";
    public bool IsEnabled => true;

    public async Task<string> CreateNoteAsync(string title, string content, CancellationToken ct = default)
    {
        Directory.CreateDirectory(_notesRootPath);

        var noteId = $"{DateTime.UtcNow:yyyyMMddHHmmss}-{SanitizeFileName(title)}";
        var path = Path.Combine(_notesRootPath, noteId + ".md");
        var markdown = $"# {title}{Environment.NewLine}{Environment.NewLine}{content}";
        await File.WriteAllTextAsync(path, markdown, ct);

        _logger.LogInformation("📝 Note created in Obsidian provider: {NoteId}", noteId);
        return noteId;
    }

    public async Task<string> GetNoteAsync(string noteId, CancellationToken ct = default)
    {
        Directory.CreateDirectory(_notesRootPath);
        var path = ResolveNotePath(noteId);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Note '{noteId}' not found", path);
        }

        return await File.ReadAllTextAsync(path, ct);
    }

    public Task<IEnumerable<NoteEntry>> SearchNotesAsync(string query, CancellationToken ct = default)
    {
        Directory.CreateDirectory(_notesRootPath);

        var matches = Directory.EnumerateFiles(_notesRootPath, "*.md", SearchOption.TopDirectoryOnly)
            .Select(path => new FileInfo(path))
            .Select(file => new NoteEntry
            {
                Id = Path.GetFileNameWithoutExtension(file.Name),
                Title = Path.GetFileNameWithoutExtension(file.Name),
                Snippet = BuildSnippet(file.FullName, query),
                UpdatedAt = file.LastWriteTimeUtc
            })
            .Where(entry => entry.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
                || entry.Snippet.Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(entry => entry.UpdatedAt)
            .ToList();

        return Task.FromResult<IEnumerable<NoteEntry>>(matches);
    }

    public Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        Directory.CreateDirectory(_notesRootPath);
        return Task.FromResult(true);
    }

    private string ResolveNotePath(string noteId)
    {
        var candidate = noteId.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
            ? noteId
            : noteId + ".md";

        return Path.Combine(_notesRootPath, candidate);
    }

    private static string BuildSnippet(string path, string query)
    {
        var content = File.ReadAllText(path);
        if (string.IsNullOrWhiteSpace(query))
        {
            return Truncate(content, 180);
        }

        var index = content.IndexOf(query, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return Truncate(content, 180);
        }

        var start = Math.Max(index - 60, 0);
        var length = Math.Min(content.Length - start, 180);
        return content.Substring(start, length).Replace(Environment.NewLine, " ");
    }

    private static string SanitizeFileName(string title)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(title.Select(ch => invalidChars.Contains(ch) ? '-' : ch).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? "note" : sanitized;
    }

    private static string Truncate(string content, int maxLength)
        => content.Length <= maxLength ? content : content[..maxLength] + "...";
}