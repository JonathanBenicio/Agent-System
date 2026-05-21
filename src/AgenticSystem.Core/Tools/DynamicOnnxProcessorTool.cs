using AgenticSystem.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace AgenticSystem.Core.Tools;

/// <summary>
/// Universal ONNX inference tool — executes any uploaded model dynamically.
/// Actions: "process" (run inference on image), "inspect" (return model metadata).
/// Models are loaded from the database (CustomOnnxModelEntity) by modelId.
/// </summary>
public class DynamicOnnxProcessorTool : ITool
{
    public string Id => "onnx_processor";
    public string Name => "ONNX Image Processor";
    public string Description => "Execute ONNX model inference on images. Supports process and inspect actions.";
    public ToolCategory Category => ToolCategory.AI;
    public bool RequiresAuth => true;

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DynamicOnnxProcessorTool> _logger;

    public DynamicOnnxProcessorTool(IServiceProvider serviceProvider, ILogger<DynamicOnnxProcessorTool> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public Task<bool> IsAvailableAsync(CancellationToken ct = default) => Task.FromResult(true);

    public async Task<ToolResult> ExecuteAsync(ToolInput input, CancellationToken ct = default)
    {
        return input.Action.ToLowerInvariant() switch
        {
            "process" => await ProcessAsync(input, ct),
            "inspect" => await InspectAsync(input, ct),
            _ => ToolResult.Fail($"Unknown action '{input.Action}'. Supported: process, inspect.")
        };
    }

    private async Task<ToolResult> ProcessAsync(ToolInput input, CancellationToken ct)
    {
        try
        {
            var modelId = GetParam<string>(input, "modelId");
            var imageData = GetParam<string>(input, "imageData");
            if (string.IsNullOrEmpty(modelId) || string.IsNullOrEmpty(imageData))
                return ToolResult.Fail("Parameters 'modelId' and 'imageData' (base64) are required.");

            var model = await LoadModelEntityAsync(modelId, ct);
            if (model == null)
                return ToolResult.Fail($"Model '{modelId}' not found.");

            string? modelPath = model.ModelFileName;
            byte[]? modelBytes = null;
            if (string.IsNullOrEmpty(modelPath) || !System.IO.File.Exists(modelPath))
            {
                modelBytes = await GetModelBytesAsync(model);
                if (modelBytes == null || modelBytes.Length == 0)
                    return ToolResult.Fail("Model data is empty or file not found.");
            }

            var imageBytes = Convert.FromBase64String(imageData);
            var sw = System.Diagnostics.Stopwatch.StartNew();

            int inputW = (int)model.InputWidth;
            int inputH = (int)model.InputHeight;

            using var image = Image.Load<Rgb24>(imageBytes);
            image.Mutate(x => x.Resize(inputW, inputH));

            var tensor = ImageToTensor(image, (int)model.InputWidth, (int)model.InputHeight, (int)model.Channels, (float)model.ScaleFactor, (float)model.MeanRed, (float)model.MeanGreen, (float)model.MeanBlue);

            using var session = CreateSession(modelPath, modelBytes);
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor(model.InputNodeName, tensor)
            };

            using var results = session.Run(inputs);
            var outputTensor = results.First(r => r.Name == model.OutputNodeName).AsTensor<float>();
            sw.Stop();

            if ((string)model.OutputFormat == "image")
            {
                var outputImage = TensorToImage(outputTensor);
                using var ms = new MemoryStream();
                await outputImage.SaveAsPngAsync(ms, ct);
                var base64 = Convert.ToBase64String(ms.ToArray());

                return ToolResult.Ok(new
                {
                    outputImage = base64,
                    latencyMs = sw.ElapsedMilliseconds,
                    inputShape = new[] { 1, (int)model.Channels, (int)model.InputHeight, (int)model.InputWidth },
                    outputShape = outputTensor.Dimensions.ToArray()
                });
            }

            return ToolResult.Ok(new
            {
                outputTensor = outputTensor.ToArray(),
                latencyMs = sw.ElapsedMilliseconds,
                inputShape = new[] { 1, (int)model.Channels, (int)model.InputHeight, (int)model.InputWidth },
                outputShape = outputTensor.Dimensions.ToArray()
            });
        }
        catch (OnnxRuntimeException ex)
        {
            _logger.LogError(ex, "ONNX Runtime error during inference");
            return ToolResult.Fail($"ONNX Runtime error: {ex.Message}");
        }
        catch (FormatException)
        {
            return ToolResult.Fail("Invalid base64 image data.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during ONNX inference");
            return ToolResult.Fail($"Inference failed: {ex.Message}");
        }
    }

    private async Task<ToolResult> InspectAsync(ToolInput input, CancellationToken ct)
    {
        try
        {
            var modelId = GetParam<string>(input, "modelId");
            if (string.IsNullOrEmpty(modelId))
                return ToolResult.Fail("Parameter 'modelId' is required.");

            var model = await LoadModelEntityAsync(modelId, ct);
            if (model == null)
                return ToolResult.Fail($"Model '{modelId}' not found.");

            string? modelPath = model.ModelFileName;
            byte[]? modelBytes = null;
            if (string.IsNullOrEmpty(modelPath) || !System.IO.File.Exists(modelPath))
            {
                modelBytes = await GetModelBytesAsync(model);
                if (modelBytes == null || modelBytes.Length == 0)
                    return ToolResult.Fail("Model data is empty or file not found.");
            }

            using var session = CreateSession(modelPath, modelBytes);
            var inputNodes = session.InputMetadata.Select(kv => new
            {
                name = kv.Key,
                shape = kv.Value.Dimensions,
                type = kv.Value.ElementDataType.ToString()
            }).ToArray();

            var outputNodes = session.OutputMetadata.Select(kv => new
            {
                name = kv.Key,
                shape = kv.Value.Dimensions,
                type = kv.Value.ElementDataType.ToString()
            }).ToArray();

            return ToolResult.Ok(new { inputNodes, outputNodes });
        }
        catch (OnnxRuntimeException ex)
        {
            _logger.LogError(ex, "ONNX Runtime error during inspection");
            return ToolResult.Fail($"Failed to inspect model: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during model inspection");
            return ToolResult.Fail($"Inspection failed: {ex.Message}");
        }
    }

    private static DenseTensor<float> ImageToTensor(Image<Rgb24> image, int w, int h, int c, float scale, float meanR, float meanG, float meanB)
    {
        var tensor = new DenseTensor<float>(new[] { 1, c, h, w });

        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < h; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (int x = 0; x < w; x++)
                {
                    var pixel = row[x];
                    tensor[0, 0, y, x] = (pixel.R * scale) - meanR;
                    if (c > 1) tensor[0, 1, y, x] = (pixel.G * scale) - meanG;
                    if (c > 2) tensor[0, 2, y, x] = (pixel.B * scale) - meanB;
                }
            }
        });

        return tensor;
    }

    private static Image<Rgb24> TensorToImage(Tensor<float> tensor)
    {
        var dimsArray = tensor.Dimensions.ToArray();
        int channels = dimsArray.Length >= 4 ? dimsArray[1] : (dimsArray.Length >= 3 ? dimsArray[0] : 1);
        int height = dimsArray.Length >= 4 ? dimsArray[2] : dimsArray[^2];
        int width = dimsArray.Length >= 4 ? dimsArray[3] : dimsArray[^1];
        bool is4d = dimsArray.Length >= 4;

        var image = new Image<Rgb24>(width, height);
        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (int x = 0; x < width; x++)
                {
                    float r, g, b;
                    if (is4d)
                    {
                        r = tensor[0, 0, y, x];
                        g = channels > 1 ? tensor[0, 1, y, x] : r;
                        b = channels > 2 ? tensor[0, 2, y, x] : r;
                    }
                    else
                    {
                        r = tensor[0, y, x];
                        g = channels > 1 ? tensor[1, y, x] : r;
                        b = channels > 2 ? tensor[2, y, x] : r;
                    }

                    row[x] = new Rgb24(
                        (byte)Math.Clamp(r * 255f, 0, 255),
                        (byte)Math.Clamp(g * 255f, 0, 255),
                        (byte)Math.Clamp(b * 255f, 0, 255));
                }
            }
        });

        return image;
    }

    private async Task<dynamic?> LoadModelEntityAsync(string modelId, CancellationToken ct)
    {
        // Resolve DbContext from scoped service provider to access tenant-isolated models
        using var scope = _serviceProvider.CreateScope();
        var dbContextType = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
            .FirstOrDefault(t => t.Name == "AgenticDbContext");

        if (dbContextType == null) return null;

        var dbContext = scope.ServiceProvider.GetService(dbContextType);
        if (dbContext == null) return null;

        // Use reflection to access CustomOnnxModels DbSet
        var property = dbContextType.GetProperty("CustomOnnxModels");
        if (property == null) return null;

        var dbSet = property.GetValue(dbContext);
        if (dbSet == null) return null;

        // Use FindAsync via reflection
        var findMethod = dbSet.GetType().GetMethod("FindAsync", new[] { typeof(object[]), typeof(CancellationToken) });
        if (findMethod == null) return null;

        var task = findMethod.Invoke(dbSet, new object[] { new object[] { modelId }, ct })!;
        return await (dynamic)task;
    }

    private static async Task<byte[]?> GetModelBytesAsync(dynamic model)
    {
        byte[]? modelData = model.ModelData;
        if (modelData != null && modelData.Length > 0)
            return modelData;

        string? filePath = model.ModelFileName;
        if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
            return await File.ReadAllBytesAsync(filePath);

        return null;
    }

    private static T? GetParam<T>(ToolInput input, string key)
    {
        if (input.Parameters.TryGetValue(key, out var value))
        {
            if (value is T typed) return typed;
            if (value is System.Text.Json.JsonElement json)
            {
                return System.Text.Json.JsonSerializer.Deserialize<T>(json.GetRawText());
            }
            return (T)Convert.ChangeType(value, typeof(T));
        }
        return default;
    }

    private static InferenceSession CreateSession(string? modelPath, byte[]? modelBytes)
    {
        try
        {
            return !string.IsNullOrEmpty(modelPath) && System.IO.File.Exists(modelPath)
                ? new InferenceSession(modelPath)
                : new InferenceSession(modelBytes ?? throw new InvalidOperationException("Model data not available."));
        }
        catch (OnnxRuntimeException ex) when (ex.Message.Contains("two nodes with same node name") || ex.Message.Contains("invalid model") || ex.Message.Contains("ErrorCode:Fail"))
        {
            var fallbackOptions = new Microsoft.ML.OnnxRuntime.SessionOptions
            {
                GraphOptimizationLevel = GraphOptimizationLevel.ORT_DISABLE_ALL
            };
            return !string.IsNullOrEmpty(modelPath) && System.IO.File.Exists(modelPath)
                ? new InferenceSession(modelPath, fallbackOptions)
                : new InferenceSession(modelBytes!, fallbackOptions);
        }
    }
}
