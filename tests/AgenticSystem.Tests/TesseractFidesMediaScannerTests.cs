using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.Configuration;
using AgenticSystem.Infrastructure.Security;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SkiaSharp;
using Xunit;

namespace AgenticSystem.Tests;

public sealed class TesseractFidesMediaScannerTests
{
    [RequiresFidesTessdataFact]
    public async Task ImageWithSensitiveTextIsOcrScannedAndRedacted()
    {
        using var scanner = CreateScanner();
        var image = CreateTextImage();

        var policy = new FidesTenantPolicy { EnabledDetectors = FidesDetectorCatalog.Normalize(null) };
        var result = await scanner.ScanAndRedactAsync(image, "image/png", policy);

        result.Status.Should().Be(FidesMediaScanStatus.SensitiveContentRedacted);
        result.DetectedCategories.Should().Contain(FidesDetectorCatalog.Cpf);
        result.SanitizedMediaType.Should().Be("image/png");
        result.RedactedContent.Should().NotBeNullOrEmpty();

        var rescanned = await scanner.ScanAndRedactAsync(result.RedactedContent!, "image/png", policy);
        rescanned.Status.Should().Be(FidesMediaScanStatus.NoSensitiveContent);
    }

    [RequiresFidesTessdataFact]
    public async Task ScannedPdfWithSensitiveTextIsRasterizedAndRedacted()
    {
        using var scanner = CreateScanner();
        var pdf = CreateTextPdf();

        var result = await scanner.ScanAndRedactAsync(
            pdf,
            "application/pdf",
            new FidesTenantPolicy { EnabledDetectors = FidesDetectorCatalog.Normalize(null) });

        result.Status.Should().Be(FidesMediaScanStatus.SensitiveContentRedacted);
        result.DetectedCategories.Should().Contain(FidesDetectorCatalog.Cpf);
        result.SanitizedMediaType.Should().Be("application/pdf");
        result.RedactedContent.Should().NotBeNullOrEmpty();
        System.Text.Encoding.Latin1.GetString(result.RedactedContent!).Should().NotContain("123.456.789-09");
    }

    private static TesseractFidesMediaScanner CreateScanner() => new(
        Options.Create(new FidesSecuritySettings
        {
            TessDataPath = Environment.GetEnvironmentVariable("FIDES_TESSDATA_PATH")!,
            OcrLanguage = "eng",
            MinimumOcrConfidence = 0.3f,
            PdfRenderDpi = 150,
            PdfRenderWidth = 1600,
            MaximumPdfPages = 4
        }),
        NullLogger<TesseractFidesMediaScanner>.Instance);

    private static byte[] CreateTextImage()
    {
        using var bitmap = new SKBitmap(1400, 240);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        using var paint = new SKPaint
        {
            Color = SKColors.Black,
            IsAntialias = true,
        };
        using var font = new SKFont { Size = 72 };
        canvas.DrawText("CPF 123.456.789-09", 30, 150, SKTextAlign.Left, font, paint);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static byte[] CreateTextPdf()
    {
        using var output = new MemoryStream();
        using (var document = SKDocument.CreatePdf(output))
        {
            var canvas = document.BeginPage(700, 140);
            using var paint = new SKPaint
            {
                Color = SKColors.Black,
                IsAntialias = true,
            };
            using var font = new SKFont { Size = 52 };
            canvas.DrawText("CPF 123.456.789-09", 20, 90, SKTextAlign.Left, font, paint);
            document.EndPage();
            document.Close();
        }
        return output.ToArray();
    }
}

public sealed class RequiresFidesTessdataFactAttribute : FactAttribute
{
    public RequiresFidesTessdataFactAttribute()
    {
        var path = Environment.GetEnvironmentVariable("FIDES_TESSDATA_PATH");
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            Skip = "Set FIDES_TESSDATA_PATH to a local Tesseract tessdata directory to run OCR integration tests.";
    }
}
