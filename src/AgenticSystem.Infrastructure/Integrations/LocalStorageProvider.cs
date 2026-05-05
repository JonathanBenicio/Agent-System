using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace AgenticSystem.Infrastructure.Integrations;

public class LocalStorageProvider : IStorageProvider
{
    private readonly string _storageRootPath;

    public LocalStorageProvider(IOptions<IntegrationProviderOptions> options)
    {
        var settings = options.Value;
        _storageRootPath = settings.StorageRootPath
            ?? Path.Combine(settings.DataRootPath ?? Path.Combine(AppContext.BaseDirectory, "data", "integrations"), "storage");
    }

    public string Name => "LocalStorage";
    public bool IsEnabled => true;

    public async Task<string> UploadFileAsync(string fileName, Stream content, CancellationToken ct = default)
    {
        Directory.CreateDirectory(_storageRootPath);

        var fileId = $"{Guid.NewGuid():N}-{SanitizeFileName(fileName)}";
        var path = Path.Combine(_storageRootPath, fileId);

        await using var target = File.Create(path);
        await content.CopyToAsync(target, ct);
        await target.FlushAsync(ct);

        return fileId;
    }

    public Task<Stream> DownloadFileAsync(string fileId, CancellationToken ct = default)
    {
        var path = Path.Combine(_storageRootPath, fileId);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Storage file '{fileId}' not found", path);
        }

        Stream stream = File.OpenRead(path);
        return Task.FromResult(stream);
    }

    public Task<IEnumerable<StorageFile>> ListFilesAsync(string? folder = null, CancellationToken ct = default)
    {
        var root = string.IsNullOrWhiteSpace(folder)
            ? _storageRootPath
            : Path.Combine(_storageRootPath, folder);

        Directory.CreateDirectory(root);

        var files = Directory.EnumerateFiles(root, "*", SearchOption.TopDirectoryOnly)
            .Select(path => new FileInfo(path))
            .Select(file => new StorageFile
            {
                Id = file.Name,
                Name = file.Name,
                Size = file.Length,
                MimeType = GuessMimeType(file.Extension),
                ModifiedAt = file.LastWriteTimeUtc
            })
            .OrderByDescending(file => file.ModifiedAt)
            .ToList();

        return Task.FromResult<IEnumerable<StorageFile>>(files);
    }

    public Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        Directory.CreateDirectory(_storageRootPath);
        return Task.FromResult(true);
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(fileName.Select(ch => invalidChars.Contains(ch) ? '-' : ch).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? "file.bin" : sanitized;
    }

    private static string GuessMimeType(string extension) => extension.ToLowerInvariant() switch
    {
        ".txt" => "text/plain",
        ".md" => "text/markdown",
        ".json" => "application/json",
        ".csv" => "text/csv",
        ".pdf" => "application/pdf",
        _ => "application/octet-stream"
    };
}