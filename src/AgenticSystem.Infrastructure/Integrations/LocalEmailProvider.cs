using System.Text.Json;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgenticSystem.Infrastructure.Integrations;

public class LocalEmailProvider : IEmailProvider
{
    private readonly string _filePath;
    private readonly ILogger<LocalEmailProvider> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public LocalEmailProvider(IOptions<IntegrationProviderOptions> options, ILogger<LocalEmailProvider> logger)
    {
        _logger = logger;
        var settings = options.Value;
        var root = settings.DataRootPath ?? Path.Combine(AppContext.BaseDirectory, "data", "integrations");
        _filePath = settings.EmailOutboxFilePath ?? Path.Combine(root, "email-outbox.json");
    }

    public string Name => "LocalEmailOutbox";
    public bool IsEnabled => true;

    public async Task SendEmailAsync(EmailMessage message, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var messages = await LoadMessagesAsync(ct);
            message.Id = string.IsNullOrWhiteSpace(message.Id) ? Guid.NewGuid().ToString("N") : message.Id;
            message.Date = message.Date == default ? DateTime.UtcNow : message.Date;
            messages.Add(message);
            await SaveMessagesAsync(messages, ct);

            _logger.LogInformation("📧 Email stored in local outbox: {Subject}", message.Subject);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IEnumerable<EmailMessage>> GetRecentEmailsAsync(int count = 10, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var messages = await LoadMessagesAsync(ct);
            return messages
                .OrderByDescending(message => message.Date)
                .Take(count)
                .ToList();
        }
        finally
        {
            _lock.Release();
        }
    }

    public Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        EnsureParentDirectoryExists(_filePath);
        return Task.FromResult(true);
    }

    private async Task<List<EmailMessage>> LoadMessagesAsync(CancellationToken ct)
    {
        EnsureParentDirectoryExists(_filePath);
        if (!File.Exists(_filePath))
        {
            return new List<EmailMessage>();
        }

        await using var stream = File.OpenRead(_filePath);
        return await JsonSerializer.DeserializeAsync<List<EmailMessage>>(stream, cancellationToken: ct)
            ?? new List<EmailMessage>();
    }

    private async Task SaveMessagesAsync(List<EmailMessage> messages, CancellationToken ct)
    {
        EnsureParentDirectoryExists(_filePath);
        await using var stream = File.Create(_filePath);
        await JsonSerializer.SerializeAsync(stream, messages, cancellationToken: ct);
    }

    private static void EnsureParentDirectoryExists(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}