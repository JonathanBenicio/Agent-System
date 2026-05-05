namespace AgenticSystem.Infrastructure.Configuration;

public class IntegrationProviderOptions
{
    public string? DataRootPath { get; set; }
    public string? CalendarFilePath { get; set; }
    public string? EmailOutboxFilePath { get; set; }
    public string? NotesRootPath { get; set; }
    public string? StorageRootPath { get; set; }
}