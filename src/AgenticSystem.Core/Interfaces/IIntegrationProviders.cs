namespace AgenticSystem.Core.Interfaces;

/// <summary>
/// Provider de calendário (Google Calendar, Outlook).
/// </summary>
public interface ICalendarProvider
{
    string Name { get; }
    bool IsEnabled { get; }
    Task<CalendarEvent> CreateEventAsync(CalendarEventRequest request, CancellationToken ct = default);
    Task<IEnumerable<CalendarEvent>> GetEventsAsync(DateTime start, DateTime end, CancellationToken ct = default);
    Task DeleteEventAsync(string eventId, CancellationToken ct = default);
    Task<bool> IsAvailableAsync(CancellationToken ct = default);
}

/// <summary>
/// Provider de email (Gmail, Outlook).
/// </summary>
public interface IEmailProvider
{
    string Name { get; }
    bool IsEnabled { get; }
    Task SendEmailAsync(EmailMessage message, CancellationToken ct = default);
    Task<IEnumerable<EmailMessage>> GetRecentEmailsAsync(int count = 10, CancellationToken ct = default);
    Task<bool> IsAvailableAsync(CancellationToken ct = default);
}

/// <summary>
/// Provider de armazenamento (Google Drive, OneDrive).
/// </summary>
public interface IStorageProvider
{
    string Name { get; }
    bool IsEnabled { get; }
    Task<string> UploadFileAsync(string fileName, Stream content, CancellationToken ct = default);
    Task<Stream> DownloadFileAsync(string fileId, CancellationToken ct = default);
    Task<IEnumerable<StorageFile>> ListFilesAsync(string? folder = null, CancellationToken ct = default);
    Task<bool> IsAvailableAsync(CancellationToken ct = default);
}

/// <summary>
/// Provider de notas (Notion).
/// </summary>
public interface INotesProvider
{
    string Name { get; }
    bool IsEnabled { get; }
    Task<string> CreateNoteAsync(string title, string content, CancellationToken ct = default);
    Task<string> GetNoteAsync(string noteId, CancellationToken ct = default);
    Task<IEnumerable<NoteEntry>> SearchNotesAsync(string query, CancellationToken ct = default);
    Task<bool> IsAvailableAsync(CancellationToken ct = default);
}

/// <summary>
/// Provider de tarefas (Todoist, TickTick).
/// </summary>
public interface ITaskProvider
{
    string Name { get; }
    bool IsEnabled { get; }
    Task<TaskItem> CreateTaskAsync(string title, DateTime? dueDate = null, CancellationToken ct = default);
    Task CompleteTaskAsync(string taskId, CancellationToken ct = default);
    Task<IEnumerable<TaskItem>> GetPendingTasksAsync(CancellationToken ct = default);
    Task<bool> IsAvailableAsync(CancellationToken ct = default);
}

// ═══════════════════════════════════════════════════════════
// Integration Models
// ═══════════════════════════════════════════════════════════

public class CalendarEventRequest
{
    public string Title { get; set; } = string.Empty;
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public string? Description { get; set; }
    public List<string> Attendees { get; set; } = new();
    public string? Location { get; set; }
}

public class CalendarEvent
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public string? Description { get; set; }
    public string? Location { get; set; }
}

public class EmailMessage
{
    public string Id { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string From { get; set; } = string.Empty;
    public List<string> To { get; set; } = new();
    public DateTime Date { get; set; }
    public bool IsRead { get; set; }
}

public class StorageFile
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public long Size { get; set; }
    public string MimeType { get; set; } = string.Empty;
    public DateTime ModifiedAt { get; set; }
}

public class NoteEntry
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Snippet { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
}

public class TaskItem
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public DateTime? DueDate { get; set; }
    public string Priority { get; set; } = "normal";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
