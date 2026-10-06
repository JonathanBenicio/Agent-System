namespace AgenticSystem.Infrastructure.Configuration;

public sealed class FidesSecuritySettings
{
    public int MediaScanTimeoutSeconds { get; set; } = 10;
    public string TessDataPath { get; set; } = string.Empty;
    public string OcrLanguage { get; set; } = "eng+por";
    public float MinimumOcrConfidence { get; set; } = 0.6f;
    public int MaximumMediaBytes { get; set; } = 10_000_000;
    public int MaximumPdfPages { get; set; } = 8;
    public int PdfRenderDpi { get; set; } = 150;
    public int PdfRenderWidth { get; set; } = 1600;
}
