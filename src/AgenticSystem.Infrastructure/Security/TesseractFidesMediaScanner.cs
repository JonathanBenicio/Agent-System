using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PDFtoImage;
using SkiaSharp;
using SixLabors.ImageSharp;
using TesseractOCR;
using TesseractOCR.Enums;

namespace AgenticSystem.Infrastructure.Security;

/// <summary>
/// Local OCR scanner for images and PDFs. Sensitive lines are replaced by opaque
/// redaction rectangles; uncertain OCR or redaction is returned as Uncertain.
/// </summary>
public sealed class TesseractFidesMediaScanner : IFidesMediaScanner, IDisposable
{
    private readonly FidesSecuritySettings _settings;
    private readonly ILogger<TesseractFidesMediaScanner> _logger;
    private readonly SemaphoreSlim _scanLock = new(1, 1);
    private Engine? _engine;
    private bool _disposed;

    public TesseractFidesMediaScanner(
        IOptions<FidesSecuritySettings> options,
        ILogger<TesseractFidesMediaScanner> logger)
    {
        _settings = options.Value;
        _logger = logger;
    }

    public async Task<FidesMediaScanResult> ScanAndRedactAsync(
        ReadOnlyMemory<byte> content,
        string mediaType,
        FidesTenantPolicy policy,
        CancellationToken ct = default)
    {
        if (_disposed || content.IsEmpty || content.Length > _settings.MaximumMediaBytes)
            return Uncertain();

        var isPdf = mediaType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase);
        if (!isPdf && !mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            return Uncertain();

        await _scanLock.WaitAsync(ct);
        try
        {
            if (isPdf)
                return await ScanPdfAsync(content.ToArray(), policy, ct);

            return await Task.Run(() => ScanImage(content.ToArray(), policy, ct), ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                "FIDES OCR failed with {ErrorType} (root {RootErrorType}); media will be blocked.",
                ex.GetType().Name,
                ex.GetBaseException().GetType().Name);
            return Uncertain();
        }
        finally
        {
            _scanLock.Release();
        }
    }

    private FidesMediaScanResult ScanImage(byte[] input, FidesTenantPolicy policy, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var imageInfo = SixLabors.ImageSharp.Image.Identify(input);
        if (imageInfo is null || imageInfo.Width <= 0 || imageInfo.Height <= 0 ||
            (long)imageInfo.Width * imageInfo.Height > 20_000_000)
            return Uncertain();

        using var bitmap = SKBitmap.Decode(input);
        if (bitmap is null || bitmap.Width <= 0 || bitmap.Height <= 0 ||
            (long)bitmap.Width * bitmap.Height > 20_000_000)
            return Uncertain();

        var page = ScanBitmap(bitmap, policy, ct);
        if (page.Status == FidesMediaScanStatus.Uncertain)
            return Uncertain();

        var safeImage = EncodePng(bitmap);
        return new FidesMediaScanResult
        {
            Status = page.Status,
            SanitizedContent = safeImage,
            SanitizedMediaType = "image/png",
            RedactedContent = page.Status == FidesMediaScanStatus.SensitiveContentRedacted ? safeImage : null,
            RedactedMediaType = page.Status == FidesMediaScanStatus.SensitiveContentRedacted ? "image/png" : null,
            DetectedCategories = page.DetectedCategories
        };
    }

    private async Task<FidesMediaScanResult> ScanPdfAsync(byte[] input, FidesTenantPolicy policy, CancellationToken ct)
    {
        if (!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux())
            return Uncertain();

        var pageCount = Conversion.GetPageCount(input);
        if (pageCount < 1 || pageCount > _settings.MaximumPdfPages)
            return Uncertain();

        var pageSizes = Conversion.GetPageSizes(input);
        if (pageSizes.Count != pageCount || pageSizes.Any(size => size.Width <= 0 || size.Height <= 0))
            return Uncertain();

        var pageNumbers = Enumerable.Range(0, pageCount);
        var dpi = Math.Clamp(_settings.PdfRenderDpi, 72, 300);
        var width = Math.Clamp(_settings.PdfRenderWidth, 640, 2400);
        if (pageSizes.Any(size => (double)width * (size.Height / size.Width) * width > 20_000_000))
            return Uncertain();
        var options = new RenderOptions(
            Dpi: dpi,
            Width: width,
            WithAspectRatio: true);
        var renderedPages = new List<SKBitmap>(pageCount);
        try
        {
            await foreach (var renderedPage in Conversion
                               .ToImagesAsync(input, pageNumbers, password: null, options, ct)
                               .WithCancellation(ct))
                renderedPages.Add(renderedPage);
        }
        catch
        {
            foreach (var page in renderedPages)
                page.Dispose();
            throw;
        }
        var redactedPages = new List<SKBitmap>(pageCount);
        var categories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var foundSensitive = false;

        try
        {
            foreach (var renderedPage in renderedPages)
            {
                ct.ThrowIfCancellationRequested();
                if (renderedPage is null || renderedPage.Width <= 0 || renderedPage.Height <= 0 ||
                    (long)renderedPage.Width * renderedPage.Height > 20_000_000)
                    return Uncertain();

                var safePage = renderedPage.Copy();
                redactedPages.Add(safePage);
                var pageScan = ScanBitmap(safePage, policy, ct);
                if (pageScan.Status == FidesMediaScanStatus.Uncertain)
                    return Uncertain();

                if (pageScan.Status == FidesMediaScanStatus.SensitiveContentRedacted)
                    foundSensitive = true;
                foreach (var category in pageScan.DetectedCategories)
                    categories.Add(category);
            }

            if (redactedPages.Count != pageCount)
                return Uncertain();

            // Rasterizing also removes hidden PDF text layers and document metadata.
            var sanitizedPdf = EncodeRasterPdf(redactedPages, dpi);
            return new FidesMediaScanResult
            {
                Status = foundSensitive
                    ? FidesMediaScanStatus.SensitiveContentRedacted
                    : FidesMediaScanStatus.NoSensitiveContent,
                SanitizedContent = sanitizedPdf,
                SanitizedMediaType = "application/pdf",
                RedactedContent = foundSensitive ? sanitizedPdf : null,
                RedactedMediaType = foundSensitive ? "application/pdf" : null,
                DetectedCategories = categories.ToArray()
            };
        }
        finally
        {
            foreach (var page in renderedPages)
                page.Dispose();
            foreach (var page in redactedPages)
                page.Dispose();
        }
    }

    private ScanPageResult ScanBitmap(SKBitmap bitmap, FidesTenantPolicy policy, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var pix = TesseractOCR.Pix.Image.LoadFromMemory(EncodePng(bitmap));
        using var page = GetEngine().Process(pix);

        var pageText = page.Text ?? string.Empty;
        if (string.IsNullOrWhiteSpace(pageText))
            return ScanPageResult.Clean;
        if (page.MeanConfidence < _settings.MinimumOcrConfidence)
        {
            _logger.LogDebug("FIDES OCR confidence below threshold ({Confidence}).", page.MeanConfidence);
            return ScanPageResult.Uncertain;
        }

        var boxes = new List<SKRect>();
        var categories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var block in page.Layout)
        {
            foreach (var paragraph in block.Paragraphs)
            {
                foreach (var line in paragraph.TextLines)
                {
                    ct.ThrowIfCancellationRequested();
                    var text = line.Text ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(text))
                        continue;

                    var matchingRules = FidesBuiltInRules.All
                        .Where(rule => FidesBuiltInRules.IsEnabled(rule.Name, policy))
                        .Where(rule => rule.Pattern.IsMatch(text))
                        .ToArray();
                    if (matchingRules.Length == 0)
                        continue;

                    if (line.Confidence < _settings.MinimumOcrConfidence || line.BoundingBox is not { } bounds ||
                        bounds.Width <= 0 || bounds.Height <= 0)
                    {
                        _logger.LogDebug("FIDES could not confidently redact OCR category {Category}.", matchingRules[0].Name);
                        return ScanPageResult.Uncertain;
                    }

                    foreach (var rule in matchingRules)
                        categories.Add(rule.Name);
                    var padding = Math.Max(2, Math.Min(bounds.Height / 8, 12));
                    boxes.Add(new SKRect(
                        Math.Max(0, bounds.X1 - padding),
                        Math.Max(0, bounds.Y1 - padding),
                        Math.Min(bitmap.Width, bounds.X2 + padding),
                        Math.Min(bitmap.Height, bounds.Y2 + padding)));
                }
            }
        }

        var wholePageHasSensitiveData = FidesBuiltInRules.All
            .Where(rule => FidesBuiltInRules.IsEnabled(rule.Name, policy))
            .Any(rule => rule.Pattern.IsMatch(pageText));
        if (wholePageHasSensitiveData && boxes.Count == 0)
            return ScanPageResult.Uncertain;

        if (boxes.Count == 0)
            return ScanPageResult.Clean;

        using var canvas = new SKCanvas(bitmap);
        using var paint = new SKPaint { Color = SKColors.Black, Style = SKPaintStyle.Fill };
        foreach (var box in boxes)
            canvas.DrawRect(box, paint);
        canvas.Flush();

        return new ScanPageResult(FidesMediaScanStatus.SensitiveContentRedacted, categories.ToArray());
    }

    private Engine GetEngine()
    {
        if (_engine is not null)
            return _engine;

        var dataPath = ResolveTessDataPath();
        var languages = _settings.OcrLanguage.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (languages.Length == 0 || languages.Any(language => !File.Exists(Path.Combine(dataPath, $"{language}.traineddata"))))
            throw new DirectoryNotFoundException("Required Tesseract trained data is not installed.");

        _engine = new Engine(dataPath, _settings.OcrLanguage, EngineMode.Default);
        return _engine;
    }

    private string ResolveTessDataPath()
    {
        if (!string.IsNullOrWhiteSpace(_settings.TessDataPath))
            return Path.GetFullPath(_settings.TessDataPath);

        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "tessdata"),
            "/usr/share/tesseract-ocr/5/tessdata",
            "/usr/share/tesseract-ocr/4.00/tessdata",
            "/usr/share/tessdata"
        };
        return candidates.FirstOrDefault(Directory.Exists) ?? candidates[0];
    }

    private static byte[] EncodePng(SKBitmap bitmap)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static byte[] EncodeRasterPdf(IReadOnlyList<SKBitmap> pages, int dpi)
    {
        using var output = new MemoryStream();
        using (var document = SKDocument.CreatePdf(output))
        {
            foreach (var bitmap in pages)
            {
                var width = bitmap.Width * 72f / dpi;
                var height = bitmap.Height * 72f / dpi;
                var canvas = document.BeginPage(width, height);
                canvas.DrawBitmap(bitmap, new SKRect(0, 0, width, height), new SKSamplingOptions());
                document.EndPage();
            }
            document.Close();
        }
        return output.ToArray();
    }

    private static FidesMediaScanResult Uncertain() => new() { Status = FidesMediaScanStatus.Uncertain };

    public void Dispose()
    {
        _engine?.Dispose();
        _scanLock.Dispose();
        _disposed = true;
    }

    private sealed record ScanPageResult(FidesMediaScanStatus Status, IReadOnlyList<string> DetectedCategories)
    {
        public static ScanPageResult Clean { get; } = new(FidesMediaScanStatus.NoSensitiveContent, []);
        public static ScanPageResult Uncertain { get; } = new(FidesMediaScanStatus.Uncertain, []);
    }
}
