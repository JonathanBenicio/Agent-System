using System.Text.Json;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgenticSystem.Infrastructure.Integrations;

public class LocalCalendarProvider : ICalendarProvider
{
    private readonly string _filePath;
    private readonly ILogger<LocalCalendarProvider> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public LocalCalendarProvider(IOptions<IntegrationProviderOptions> options, ILogger<LocalCalendarProvider> logger)
    {
        _logger = logger;
        var settings = options.Value;
        var root = ResolveDataRoot(settings);
        _filePath = settings.CalendarFilePath ?? Path.Combine(root, "calendar-events.json");
    }

    public string Name => "LocalCalendar";
    public bool IsEnabled => true;

    public async Task<CalendarEvent> CreateEventAsync(CalendarEventRequest request, CancellationToken ct = default)
    {
        var calendarEvent = new CalendarEvent
        {
            Id = Guid.NewGuid().ToString("N"),
            Title = request.Title,
            Start = request.Start,
            End = request.End,
            Description = request.Description,
            Location = request.Location
        };

        await _lock.WaitAsync(ct);
        try
        {
            var events = await LoadEventsAsync(ct);
            events.Add(calendarEvent);
            await SaveEventsAsync(events, ct);
            return calendarEvent;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IEnumerable<CalendarEvent>> GetEventsAsync(DateTime start, DateTime end, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var events = await LoadEventsAsync(ct);
            return events
                .Where(calendarEvent => calendarEvent.End >= start && calendarEvent.Start <= end)
                .OrderBy(calendarEvent => calendarEvent.Start)
                .ToList();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task DeleteEventAsync(string eventId, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var events = await LoadEventsAsync(ct);
            events.RemoveAll(calendarEvent => calendarEvent.Id == eventId);
            await SaveEventsAsync(events, ct);
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

    private async Task<List<CalendarEvent>> LoadEventsAsync(CancellationToken ct)
    {
        EnsureParentDirectoryExists(_filePath);
        if (!File.Exists(_filePath))
        {
            return new List<CalendarEvent>();
        }

        await using var stream = File.OpenRead(_filePath);
        return await JsonSerializer.DeserializeAsync<List<CalendarEvent>>(stream, cancellationToken: ct)
            ?? new List<CalendarEvent>();
    }

    private async Task SaveEventsAsync(List<CalendarEvent> events, CancellationToken ct)
    {
        EnsureParentDirectoryExists(_filePath);
        await using var stream = File.Create(_filePath);
        await JsonSerializer.SerializeAsync(stream, events, cancellationToken: ct);
    }

    private static string ResolveDataRoot(IntegrationProviderOptions options)
        => options.DataRootPath
            ?? Path.Combine(AppContext.BaseDirectory, "data", "integrations");

    private static void EnsureParentDirectoryExists(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}