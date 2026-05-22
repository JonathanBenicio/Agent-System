using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Services.Ml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Text.Json;

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
    private readonly IOnnxSessionCache _sessionCache;

    public DynamicOnnxProcessorTool(
        IServiceProvider serviceProvider,
        ILogger<DynamicOnnxProcessorTool> logger,
        IOnnxSessionCache sessionCache)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _sessionCache = sessionCache;
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

            string resolvedModelPath = model.ModelFileName ?? string.Empty;
            byte[]? modelBytes = null;

            // 1. Handle Multi-part model files (.onnx + .data) using isolated temporary directories
            var associatedFiles = model.AssociatedFiles as System.Collections.IEnumerable;
            var filesList = new List<dynamic>();
            if (associatedFiles != null)
            {
                foreach (dynamic file in associatedFiles)
                {
                    filesList.Add(file);
                }
            }

            if (filesList.Count > 0)
            {
                var tenantId = (string)(model.TenantId ?? "default");
                var tempDir = Path.Combine(Path.GetTempPath(), "agentic-onnx", tenantId, modelId);
                Directory.CreateDirectory(tempDir);

                string tempOnnxPath = Path.Combine(tempDir, $"{modelId}.onnx");
                byte[]? dbModelBytes = model.ModelData;

                // Write main ONNX file if size differs or not exists (reduces I/O wear)
                bool shouldWriteOnnx = true;
                if (File.Exists(tempOnnxPath))
                {
                    var fileInfo = new FileInfo(tempOnnxPath);
                    long dbLength = dbModelBytes?.Length ?? (File.Exists(model.ModelFileName) ? new FileInfo(model.ModelFileName).Length : 0);
                    if (fileInfo.Length == dbLength)
                    {
                        shouldWriteOnnx = false;
                    }
                }

                if (shouldWriteOnnx)
                {
                    if (dbModelBytes != null && dbModelBytes.Length > 0)
                    {
                        await File.WriteAllBytesAsync(tempOnnxPath, dbModelBytes, ct);
                    }
                    else if (!string.IsNullOrEmpty(model.ModelFileName) && File.Exists(model.ModelFileName))
                    {
                        File.Copy(model.ModelFileName, tempOnnxPath, true);
                    }
                    else
                    {
                        return ToolResult.Fail("Model file not found and no model data in DB.");
                    }
                }

                // Write associated weights/data files if size differs or not exists
                foreach (var file in filesList)
                {
                    string filePath = Path.Combine(tempDir, file.FileName);
                    byte[] fileData = file.FileData;
                    bool shouldWriteFile = true;
                    if (File.Exists(filePath))
                    {
                        var fileInfo = new FileInfo(filePath);
                        if (fileInfo.Length == fileData.Length)
                        {
                            shouldWriteFile = false;
                        }
                    }

                    if (shouldWriteFile)
                    {
                        await File.WriteAllBytesAsync(filePath, fileData, ct);
                    }
                }

                resolvedModelPath = tempOnnxPath;
            }
            else
            {
                // Classic dual storage (single-file model)
                if (string.IsNullOrEmpty(resolvedModelPath) || !System.IO.File.Exists(resolvedModelPath))
                {
                    modelBytes = await GetModelBytesAsync(model);
                    if (modelBytes == null || modelBytes.Length == 0)
                        return ToolResult.Fail("Model data is empty or file not found.");
                }
            }

            var imageBytes = Convert.FromBase64String(imageData);
            var sw = System.Diagnostics.Stopwatch.StartNew();

            int inputW = (int)model.InputWidth;
            int inputH = (int)model.InputHeight;

            using var image = Image.Load<Rgb24>(imageBytes);
            int originalW = image.Width;
            int originalH = image.Height;

            // 2. Aspect Ratio Preserving Resizing
            float scale = Math.Min((float)inputW / originalW, (float)inputH / originalH);
            int newW = (int)Math.Round(originalW * scale);
            int newH = (int)Math.Round(originalH * scale);
            newW = Math.Max(1, newW);
            newH = Math.Max(1, newH);

            image.Mutate(x => x.Resize(newW, newH));

            // 3. Customizable Padding Color
            var padColor = Color.Black;
            string postProcessConfig = model.PostProcessConfigJson ?? "{}";
            try
            {
                using var doc = JsonDocument.Parse(postProcessConfig);
                if (doc.RootElement.TryGetProperty("PaddingColor", out var colorProp) && colorProp.ValueKind == JsonValueKind.String)
                {
                    var colorStr = colorProp.GetString();
                    if (!string.IsNullOrEmpty(colorStr))
                    {
                        padColor = Color.Parse(colorStr);
                    }
                }
            }
            catch
            {
                // Fallback to black
            }

            using var paddedImage = new Image<Rgb24>(inputW, inputH);
            int posX = (inputW - newW) / 2;
            int posY = (inputH - newH) / 2;

            paddedImage.ProcessPixelRows(accessorPadded =>
            {
                for (int y = 0; y < inputH; y++)
                {
                    var rowPadded = accessorPadded.GetRowSpan(y);
                    
                    int imgY = y - posY;
                    bool hasImageRow = imgY >= 0 && imgY < newH;

                    for (int x = 0; x < inputW; x++)
                    {
                        int imgX = x - posX;
                        if (hasImageRow && imgX >= 0 && imgX < newW)
                        {
                            rowPadded[x] = image[imgX, imgY];
                        }
                        else
                        {
                            rowPadded[x] = padColor;
                        }
                    }
                }
            });

            // 4. Retrieve session and dynamically detect input/output types (INT8 vs Float)
            var session = _sessionCache.GetOrCreateSession(modelId, resolvedModelPath, modelBytes);

            var inputMeta = session.InputMetadata[model.InputNodeName];
            bool isByteInput = inputMeta.ElementDataType == TensorElementType.UInt8;

            NamedOnnxValue onnxInput;
            if (isByteInput)
            {
                var byteTensor = ImageToByteTensor(paddedImage, inputW, inputH, (int)model.Channels, (float)model.ScaleFactor, (float)model.MeanRed, (float)model.MeanGreen, (float)model.MeanBlue);
                onnxInput = NamedOnnxValue.CreateFromTensor(model.InputNodeName, byteTensor);
            }
            else
            {
                var floatTensor = ImageToTensor(paddedImage, inputW, inputH, (int)model.Channels, (float)model.ScaleFactor, (float)model.MeanRed, (float)model.MeanGreen, (float)model.MeanBlue);
                onnxInput = NamedOnnxValue.CreateFromTensor(model.InputNodeName, floatTensor);
            }

            var inputs = new List<NamedOnnxValue> { onnxInput };

            using var results = session.Run(inputs);
            var resultVal = results.First(r => r.Name == model.OutputNodeName);
            sw.Stop();

            var outputMeta = session.OutputMetadata[model.OutputNodeName];
            bool isByteOutput = outputMeta.ElementDataType == TensorElementType.UInt8;

            if ((string)model.OutputFormat == "image")
            {
                using var outputImage = isByteOutput
                    ? TensorToImage(resultVal.AsTensor<byte>())
                    : TensorToImage(resultVal.AsTensor<float>());

                int outputW = outputImage.Width;
                int outputH = outputImage.Height;

                float upscaleFactorX = (float)outputW / inputW;
                float upscaleFactorY = (float)outputH / inputH;

                int cropX = (int)Math.Round(posX * upscaleFactorX);
                int cropY = (int)Math.Round(posY * upscaleFactorY);
                int cropW = (int)Math.Round(newW * upscaleFactorX);
                int cropH = (int)Math.Round(newH * upscaleFactorY);

                // Safe Crop clamping to avoid out of bounds exceptions
                cropX = Math.Max(0, Math.Min(cropX, outputW - 1));
                cropY = Math.Max(0, Math.Min(cropY, outputH - 1));
                cropW = Math.Max(1, Math.Min(cropW, outputW - cropX));
                cropH = Math.Max(1, Math.Min(cropH, outputH - cropY));

                outputImage.Mutate(ctx => ctx.Crop(new Rectangle(cropX, cropY, cropW, cropH)));

                using var ms = new MemoryStream();
                await outputImage.SaveAsPngAsync(ms, ct);
                var base64 = Convert.ToBase64String(ms.ToArray());

                return ToolResult.Ok(new
                {
                    outputImage = base64,
                    latencyMs = sw.ElapsedMilliseconds,
                    inputShape = new[] { 1, (int)model.Channels, inputH, inputW },
                    outputShape = new[] { 1, (int)model.Channels, cropH, cropW }
                });
            }

            return ToolResult.Ok(new
            {
                outputTensor = isByteOutput
                    ? (object)resultVal.AsTensor<byte>().ToArray()
                    : (object)resultVal.AsTensor<float>().ToArray(),
                latencyMs = sw.ElapsedMilliseconds,
                inputShape = new[] { 1, (int)model.Channels, inputH, inputW },
                outputShape = isByteOutput
                    ? resultVal.AsTensor<byte>().Dimensions.ToArray()
                    : resultVal.AsTensor<float>().Dimensions.ToArray()
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

            var session = _sessionCache.GetOrCreateSession(modelId, modelPath, modelBytes);
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

    private static DenseTensor<byte> ImageToByteTensor(Image<Rgb24> image, int w, int h, int c, float scale, float meanR, float meanG, float meanB)
    {
        var tensor = new DenseTensor<byte>(new[] { 1, c, h, w });

        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < h; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (int x = 0; x < w; x++)
                {
                    var pixel = row[x];
                    tensor[0, 0, y, x] = (byte)Math.Clamp((pixel.R * scale) - meanR, 0, 255);
                    if (c > 1) tensor[0, 1, y, x] = (byte)Math.Clamp((pixel.G * scale) - meanG, 0, 255);
                    if (c > 2) tensor[0, 2, y, x] = (byte)Math.Clamp((pixel.B * scale) - meanB, 0, 255);
                }
            }
        });

        return tensor;
    }

    private static Image<Rgb24> TensorToImage(Tensor<byte> tensor)
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
                    byte r, g, b;
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

                    row[x] = new Rgb24(r, g, b);
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
        var model = await (dynamic)task;

        if (model != null)
        {
            try
            {
                // Explicitly load AssociatedFiles using DbContext.Entry(entity).Collection("AssociatedFiles").Load() via reflection
                var entryMethod = dbContext.GetType().GetMethod("Entry", new[] { typeof(object) });
                var entry = entryMethod?.Invoke(dbContext, new[] { model });
                if (entry != null)
                {
                    var collectionMethod = entry.GetType().GetMethod("Collection", new[] { typeof(string) });
                    var collectionEntry = collectionMethod?.Invoke(entry, new[] { "AssociatedFiles" });
                    if (collectionEntry != null)
                    {
                        var loadMethod = collectionEntry.GetType().GetMethod("Load", Type.EmptyTypes);
                        loadMethod?.Invoke(collectionEntry, null);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to explicitly load AssociatedFiles for model '{ModelId}'", modelId);
            }
        }

        return model;
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
}
