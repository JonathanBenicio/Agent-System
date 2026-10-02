namespace AgenticSystem.Core.Services.Ml;

/// <summary>Per-image preprocessing budget; excludes ONNX weights and runtime/output allocations.</summary>
public sealed class OnnxInputLimits
{
    public const string SectionName = "AgenticSystem:Onnx:InputLimits";
    public int MaxDimension { get; set; } = 4096;
    public long MaxPreprocessingBytes { get; set; } = 64L * 1024 * 1024;

    public bool IsValid => MaxDimension > 0 && MaxPreprocessingBytes > 0;

    public bool TryValidate(int width, int height, int channels, out string? error)
    {
        error = null;
        if (!IsValid)
            error = "ONNX input limits must be positive.";
        else if (width <= 0 || height <= 0)
            error = "ONNX input width and height must be positive.";
        else if (channels is not (1 or 3))
            error = "ONNX image input supports only 1 or 3 channels (NCHW).";
        else
        {
            try
            {
                var bytes = EstimatePreprocessingBytes(width, height, channels);
                if (checked((long)width * height * channels) > Array.MaxLength)
                    error = "ONNX input tensor exceeds the supported array length.";
                else if (width > MaxDimension || height > MaxDimension)
                    error = $"ONNX input dimensions must not exceed {MaxDimension}.";
                else if (bytes > MaxPreprocessingBytes)
                    error = $"ONNX preprocessing requires {bytes} bytes; budget is {MaxPreprocessingBytes} bytes.";
            }
            catch (OverflowException)
            {
                error = "ONNX preprocessing byte calculation overflowed; input dimensions are too large.";
            }
        }
        return error is null;
    }

    public void Validate(int width, int height, int channels)
    {
        if (!TryValidate(width, height, channels, out var error))
            throw new ArgumentException(error);
    }

    public void ValidateSourceAndTarget(int sourceWidth, int sourceHeight, int width, int height, int channels)
    {
        Validate(sourceWidth, sourceHeight, 3);
        Validate(width, height, channels);
        long bytes;
        try
        {
            bytes = checked((long)sourceWidth * sourceHeight * 3
                + (long)width * height * (6L + (long)channels * sizeof(float)));
        }
        catch (OverflowException)
        {
            throw new ArgumentException("ONNX preprocessing byte calculation overflowed; image dimensions are too large.");
        }
        if (bytes > MaxPreprocessingBytes)
            throw new ArgumentException($"ONNX preprocessing requires {bytes} bytes including the source image; budget is {MaxPreprocessingBytes} bytes.");
    }

    // Decoded/resized RGB24 + padded RGB24 + worst-case float tensor, batch size 1.
    public static long EstimatePreprocessingBytes(int width, int height, int channels)
        => checked((long)width * height * checked(6L + (long)channels * sizeof(float)));
}
