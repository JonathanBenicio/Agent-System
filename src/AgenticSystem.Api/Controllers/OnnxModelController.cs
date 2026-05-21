using AgenticSystem.Infrastructure.Persistence;
using AgenticSystem.Infrastructure.Persistence.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.ML.OnnxRuntime;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace AgenticSystem.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/onnx/models")]
[RequestSizeLimit(1_073_741_824)] // 1 GB
public class OnnxModelController : ControllerBase
{
    private readonly AgenticDbContext _db;
    private readonly ILogger<OnnxModelController> _logger;
    private readonly IWebHostEnvironment _env;
    private const long MaxDbSize = 50 * 1024 * 1024; // 50 MB

    public OnnxModelController(AgenticDbContext db, ILogger<OnnxModelController> logger, IWebHostEnvironment env)
    {
        _db = db;
        _logger = logger;
        _env = env;
    }

    private string GetTenantId() => Request.Headers["X-Tenant-Id"].FirstOrDefault() ?? "default-tenant";

    /// <summary>GET /api/onnx/models — list models (summary, no binary data)</summary>
    [HttpGet]
    public async Task<IActionResult> ListModels(CancellationToken ct)
    {
        var models = await _db.CustomOnnxModels
            .Where(m => m.TenantId == GetTenantId())
            .Select(m => new
            {
                m.Id, m.Name, m.Description,
                m.InputWidth, m.InputHeight, m.Channels,
                m.OutputFormat, m.IsActive, m.FileSizeBytes,
                storedOnDisk = m.ModelFileName != null,
                m.CreatedAt, m.UpdatedAt
            })
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync(ct);

        return Ok(models);
    }

    /// <summary>GET /api/onnx/models/{id} — full detail (no binary)</summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetModel(string id, CancellationToken ct)
    {
        var model = await _db.CustomOnnxModels
            .Where(m => m.Id == id && m.TenantId == GetTenantId())
            .Select(m => new
            {
                m.Id, m.Name, m.Description,
                m.InputNodeName, m.OutputNodeName,
                m.InputWidth, m.InputHeight, m.Channels,
                m.ScaleFactor, m.MeanRed, m.MeanGreen, m.MeanBlue,
                m.OutputFormat, m.PostProcessConfigJson,
                m.IsActive, m.FileSizeBytes,
                storedOnDisk = m.ModelFileName != null,
                m.CreatedAt, m.UpdatedAt
            })
            .FirstOrDefaultAsync(ct);

        if (model == null) return NotFound();
        return Ok(model);
    }

    /// <summary>POST /api/onnx/models — upload model (multipart/form-data)</summary>
    [HttpPost]
    public async Task<IActionResult> UploadModel(
        IFormFile file,
        [FromForm] string name,
        [FromForm] string inputNodeName,
        [FromForm] string outputNodeName,
        [FromForm] IFormFile? dataFile = null,
        [FromForm] string? description = null,
        [FromForm] int inputWidth = 512,
        [FromForm] int inputHeight = 512,
        [FromForm] int channels = 3,
        [FromForm] float scaleFactor = 0.0039216f,
        [FromForm] float meanRed = 0,
        [FromForm] float meanGreen = 0,
        [FromForm] float meanBlue = 0,
        [FromForm] string outputFormat = "image",
        CancellationToken ct = default)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "A .onnx model file is required." });

        if (!file.FileName.EndsWith(".onnx", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { error = "Only .onnx files are accepted." });

        var tenantId = GetTenantId();
        var entity = new CustomOnnxModelEntity
        {
            TenantId = tenantId,
            Name = name,
            Description = description,
            InputNodeName = inputNodeName,
            OutputNodeName = outputNodeName,
            InputWidth = inputWidth,
            InputHeight = inputHeight,
            Channels = channels,
            ScaleFactor = scaleFactor,
            MeanRed = meanRed,
            MeanGreen = meanGreen,
            MeanBlue = meanBlue,
            OutputFormat = outputFormat,
            FileSizeBytes = file.Length + (dataFile?.Length ?? 0)
        };

        if (file.Length > MaxDbSize || dataFile != null)
        {
            // Store on disk inside an isolated subdirectory to preserve original filenames
            var dir = Path.Combine(_env.WebRootPath ?? "wwwroot", "onnx-models", tenantId, entity.Id);
            Directory.CreateDirectory(dir);
            
            var filePath = Path.Combine(dir, Path.GetFileName(file.FileName));
            await using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream, ct);
            }
            entity.ModelFileName = filePath;

            if (dataFile != null && dataFile.Length > 0)
            {
                var dataFilePath = Path.Combine(dir, Path.GetFileName(dataFile.FileName));
                await using (var dataStream = new FileStream(dataFilePath, FileMode.Create))
                {
                    await dataFile.CopyToAsync(dataStream, ct);
                }
                _logger.LogInformation("ONNX model weights stored on disk: {Path} ({Size} bytes)", dataFilePath, dataFile.Length);
            }

            _logger.LogInformation("ONNX model stored on disk: {Path} ({Size} bytes)", filePath, file.Length);
        }
        else
        {
            // Store in DB (only for small standalone models)
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, ct);
            entity.ModelData = ms.ToArray();
        }

        _db.CustomOnnxModels.Add(entity);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Uploaded ONNX model '{Name}' (id={Id}, tenant={Tenant}, {Size} bytes)",
            name, entity.Id, tenantId, entity.FileSizeBytes);

        return CreatedAtAction(nameof(GetModel), new { id = entity.Id }, new
        {
            entity.Id, entity.Name, entity.Description,
            entity.InputWidth, entity.InputHeight, entity.Channels,
            entity.OutputFormat, entity.IsActive, entity.FileSizeBytes,
            storedOnDisk = entity.ModelFileName != null,
            entity.CreatedAt, entity.UpdatedAt
        });
    }

    /// <summary>PUT /api/onnx/models/{id} — update metadata</summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateModel(string id, [FromBody] UpdateOnnxModelRequest req, CancellationToken ct)
    {
        var entity = await _db.CustomOnnxModels
            .FirstOrDefaultAsync(m => m.Id == id && m.TenantId == GetTenantId(), ct);
        if (entity == null) return NotFound();

        if (req.Name != null) entity.Name = req.Name;
        if (req.Description != null) entity.Description = req.Description;
        if (req.InputNodeName != null) entity.InputNodeName = req.InputNodeName;
        if (req.OutputNodeName != null) entity.OutputNodeName = req.OutputNodeName;
        if (req.InputWidth.HasValue) entity.InputWidth = req.InputWidth.Value;
        if (req.InputHeight.HasValue) entity.InputHeight = req.InputHeight.Value;
        if (req.Channels.HasValue) entity.Channels = req.Channels.Value;
        if (req.ScaleFactor.HasValue) entity.ScaleFactor = req.ScaleFactor.Value;
        if (req.MeanRed.HasValue) entity.MeanRed = req.MeanRed.Value;
        if (req.MeanGreen.HasValue) entity.MeanGreen = req.MeanGreen.Value;
        if (req.MeanBlue.HasValue) entity.MeanBlue = req.MeanBlue.Value;
        if (req.OutputFormat != null) entity.OutputFormat = req.OutputFormat;
        if (req.IsActive.HasValue) entity.IsActive = req.IsActive.Value;
        entity.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return Ok(new
        {
            entity.Id, entity.Name, entity.Description,
            entity.InputNodeName, entity.OutputNodeName,
            entity.InputWidth, entity.InputHeight, entity.Channels,
            entity.ScaleFactor, entity.MeanRed, entity.MeanGreen, entity.MeanBlue,
            entity.OutputFormat, entity.IsActive, entity.FileSizeBytes,
            storedOnDisk = entity.ModelFileName != null,
            entity.CreatedAt, entity.UpdatedAt
        });
    }

    /// <summary>DELETE /api/onnx/models/{id}</summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteModel(string id, CancellationToken ct)
    {
        var entity = await _db.CustomOnnxModels
            .FirstOrDefaultAsync(m => m.Id == id && m.TenantId == GetTenantId(), ct);
        if (entity == null) return NotFound();

        // Remove folder from disk if applicable
        if (!string.IsNullOrEmpty(entity.ModelFileName))
        {
            var dir = Path.GetDirectoryName(entity.ModelFileName);
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
            {
                try
                {
                    Directory.Delete(dir, true);
                    _logger.LogInformation("Deleted ONNX model directory: {Path}", dir);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to delete ONNX model directory: {Path}", dir);
                }
            }
        }

        _db.CustomOnnxModels.Remove(entity);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>POST /api/onnx/models/{id}/inspect — inspect model nodes</summary>
    [HttpPost("{id}/inspect")]
    public async Task<IActionResult> InspectModel(string id, CancellationToken ct)
    {
        var entity = await _db.CustomOnnxModels
            .FirstOrDefaultAsync(m => m.Id == id && m.TenantId == GetTenantId(), ct);
        if (entity == null) return NotFound();

        try
        {
            var modelBytes = !string.IsNullOrEmpty(entity.ModelFileName) && System.IO.File.Exists(entity.ModelFileName)
                ? null
                : await GetModelBytesAsync(entity);
            using var session = CreateSession(entity.ModelFileName, modelBytes);

            var inputNodes = session.InputMetadata.Select(kv => new
            {
                name = kv.Key,
                shape = kv.Value.Dimensions,
                type = kv.Value.ElementDataType.ToString()
            });

            var outputNodes = session.OutputMetadata.Select(kv => new
            {
                name = kv.Key,
                shape = kv.Value.Dimensions,
                type = kv.Value.ElementDataType.ToString()
            });

            return Ok(new { inputNodes, outputNodes });
        }
        catch (OnnxRuntimeException ex)
        {
            _logger.LogError(ex, "Failed to inspect ONNX model {Id}", id);
            return BadRequest(new { error = $"Failed to load model: {ex.Message}" });
        }
    }

    /// <summary>POST /api/onnx/models/{id}/test — quick test with image</summary>
    [HttpPost("{id}/test")]
    public async Task<IActionResult> TestModel(string id, [FromForm] IFormFile? image, [FromForm] string? imageData, CancellationToken ct)
    {
        var entity = await _db.CustomOnnxModels
            .FirstOrDefaultAsync(m => m.Id == id && m.TenantId == GetTenantId(), ct);
        if (entity == null) return NotFound();

        byte[]? imageBytes = null;
        if (image != null)
        {
            using var ms = new MemoryStream();
            await image.CopyToAsync(ms, ct);
            imageBytes = ms.ToArray();
        }
        else if (!string.IsNullOrEmpty(imageData))
        {
            imageBytes = Convert.FromBase64String(imageData);
        }

        if (imageBytes == null || imageBytes.Length == 0)
            return BadRequest(new { error = "Provide 'image' file or 'imageData' (base64)." });

        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();

            using var img = SixLabors.ImageSharp.Image.Load<SixLabors.ImageSharp.PixelFormats.Rgb24>(imageBytes);
            img.Mutate(x => x.Resize(entity.InputWidth, entity.InputHeight));

            var tensor = ImageToTensor(img, entity);

            var modelBytes = !string.IsNullOrEmpty(entity.ModelFileName) && System.IO.File.Exists(entity.ModelFileName)
                ? null
                : await GetModelBytesAsync(entity);
            using var session = CreateSession(entity.ModelFileName, modelBytes);
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor(entity.InputNodeName, tensor)
            };

            using var results = session.Run(inputs);
            var outputTensor = results.First(r => r.Name == entity.OutputNodeName).AsTensor<float>();
            sw.Stop();

            string? outputImageBase64 = null;
            if (entity.OutputFormat == "image")
            {
                var outputImg = TensorToImage(outputTensor, entity);
                using var outMs = new MemoryStream();
                await outputImg.SaveAsPngAsync(outMs, ct);
                outputImageBase64 = Convert.ToBase64String(outMs.ToArray());
            }

            return Ok(new
            {
                outputImage = outputImageBase64,
                latencyMs = sw.ElapsedMilliseconds,
                inputShape = new[] { 1, entity.Channels, entity.InputHeight, entity.InputWidth },
                outputShape = outputTensor.Dimensions.ToArray()
            });
        }
        catch (OnnxRuntimeException ex)
        {
            _logger.LogError(ex, "ONNX test error for model {Id}", id);
            return BadRequest(new { error = $"ONNX Runtime error: {ex.Message}" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Test error for model {Id}", id);
            return BadRequest(new { error = $"Test failed: {ex.Message}" });
        }
    }

    private static async Task<byte[]?> GetModelBytesAsync(CustomOnnxModelEntity entity)
    {
        if (entity.ModelData is { Length: > 0 })
            return entity.ModelData;

        if (!string.IsNullOrEmpty(entity.ModelFileName) && System.IO.File.Exists(entity.ModelFileName))
            return await System.IO.File.ReadAllBytesAsync(entity.ModelFileName);

        return null;
    }

    private static Microsoft.ML.OnnxRuntime.Tensors.DenseTensor<float> ImageToTensor(
        SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgb24> image,
        CustomOnnxModelEntity model)
    {
        int w = model.InputWidth, h = model.InputHeight, c = model.Channels;
        var tensor = new Microsoft.ML.OnnxRuntime.Tensors.DenseTensor<float>(new[] { 1, c, h, w });

        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < h; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (int x = 0; x < w; x++)
                {
                    var pixel = row[x];
                    tensor[0, 0, y, x] = (pixel.R * model.ScaleFactor) - model.MeanRed;
                    if (c > 1) tensor[0, 1, y, x] = (pixel.G * model.ScaleFactor) - model.MeanGreen;
                    if (c > 2) tensor[0, 2, y, x] = (pixel.B * model.ScaleFactor) - model.MeanBlue;
                }
            }
        });

        return tensor;
    }

    private static SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgb24> TensorToImage(
        Microsoft.ML.OnnxRuntime.Tensors.Tensor<float> tensor,
        CustomOnnxModelEntity model)
    {
        var dims = tensor.Dimensions.ToArray();
        int channels = dims.Length >= 4 ? dims[1] : 3;
        int height = dims.Length >= 4 ? dims[2] : dims[^2];
        int width = dims.Length >= 4 ? dims[3] : dims[^1];

        var image = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgb24>(width, height);
        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (int x = 0; x < width; x++)
                {
                    float r = dims.Length >= 4 ? tensor[0, 0, y, x] : tensor[0, y, x];
                    float g = channels > 1 ? (dims.Length >= 4 ? tensor[0, 1, y, x] : tensor[1, y, x]) : r;
                    float b = channels > 2 ? (dims.Length >= 4 ? tensor[0, 2, y, x] : tensor[2, y, x]) : r;

                    row[x] = new SixLabors.ImageSharp.PixelFormats.Rgb24(
                        (byte)Math.Clamp(r * 255f, 0, 255),
                        (byte)Math.Clamp(g * 255f, 0, 255),
                        (byte)Math.Clamp(b * 255f, 0, 255));
                }
            }
        });

        return image;
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

public class UpdateOnnxModelRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? InputNodeName { get; set; }
    public string? OutputNodeName { get; set; }
    public int? InputWidth { get; set; }
    public int? InputHeight { get; set; }
    public int? Channels { get; set; }
    public float? ScaleFactor { get; set; }
    public float? MeanRed { get; set; }
    public float? MeanGreen { get; set; }
    public float? MeanBlue { get; set; }
    public string? OutputFormat { get; set; }
    public bool? IsActive { get; set; }
}
