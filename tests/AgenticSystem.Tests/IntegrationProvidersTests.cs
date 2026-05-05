using System.Text;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.Configuration;
using AgenticSystem.Infrastructure.Integrations;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace AgenticSystem.Tests;

public class IntegrationProvidersTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "agentic-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task LocalCalendarProvider_CreateAndReadEvent_PersistsEvent()
    {
        var provider = new LocalCalendarProvider(CreateOptions(), Substitute.For<ILogger<LocalCalendarProvider>>());

        var created = await provider.CreateEventAsync(new CalendarEventRequest
        {
            Title = "Review técnico",
            Start = DateTime.UtcNow,
            End = DateTime.UtcNow.AddHours(1),
            Description = "Revisar arquitetura"
        });

        var events = await provider.GetEventsAsync(DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddHours(2));

        events.Should().ContainSingle(calendarEvent => calendarEvent.Id == created.Id);
    }

    [Fact]
    public async Task ObsidianNotesProvider_CreateAndSearchNote_ReturnsStoredNote()
    {
        var provider = new ObsidianNotesProvider(CreateOptions(), Substitute.For<ILogger<ObsidianNotesProvider>>());

        var noteId = await provider.CreateNoteAsync("Resumo Sprint", "Memória persistente por agente e tool A/B");
        var matches = await provider.SearchNotesAsync("tool A/B");

        matches.Should().Contain(match => match.Id == noteId);
    }

    [Fact]
    public async Task LocalStorageProvider_UploadAndListFile_ReturnsStoredFile()
    {
        var provider = new LocalStorageProvider(CreateOptions());
        await using var content = new MemoryStream(Encoding.UTF8.GetBytes("payload"));

        var fileId = await provider.UploadFileAsync("artifact.txt", content);
        var files = await provider.ListFilesAsync();

        files.Should().Contain(file => file.Id == fileId);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    private IOptions<IntegrationProviderOptions> CreateOptions()
        => Options.Create(new IntegrationProviderOptions
        {
            DataRootPath = _tempRoot
        });
}