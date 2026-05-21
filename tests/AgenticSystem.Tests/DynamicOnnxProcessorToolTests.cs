using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services.Ml;
using AgenticSystem.Core.Tools;
using AgenticSystem.Infrastructure.Persistence;
using AgenticSystem.Infrastructure.Persistence.Entities;
using SixLabors.ImageSharp;
using Xunit;

namespace AgenticSystem.Tests;

public class DynamicOnnxProcessorToolTests
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DynamicOnnxProcessorTool> _logger;
    private readonly AgenticDbContext _dbContext;
    private readonly DynamicOnnxProcessorTool _sut;
    private readonly string _modelPath;

    public DynamicOnnxProcessorToolTests()
    {
        var dbName = $"onnx-tool-tests-{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<AgenticDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        var tenantAccessor = Substitute.For<ITenantContextAccessor>();
        tenantAccessor.Current.Returns(new TenantContext { TenantId = "test-tenant" });

        _dbContext = new AgenticDbContext(options, tenantAccessor);
        _dbContext.Database.EnsureCreated();

        _serviceProvider = Substitute.For<IServiceProvider>();
        _logger = Substitute.For<ILogger<DynamicOnnxProcessorTool>>();

        var serviceScope = Substitute.For<IServiceScope>();
        var serviceScopeFactory = Substitute.For<IServiceScopeFactory>();

        serviceScope.ServiceProvider.Returns(_serviceProvider);
        serviceScopeFactory.CreateScope().Returns(serviceScope);
        _serviceProvider.GetService(typeof(IServiceScopeFactory)).Returns(serviceScopeFactory);

        _serviceProvider.GetService(typeof(AgenticDbContext)).Returns(_dbContext);
        _serviceProvider.GetService(Arg.Is<Type>(t => t.Name == "AgenticDbContext")).Returns(_dbContext);

        var sessionCacheLogger = Substitute.For<ILogger<OnnxSessionCache>>();
        var sessionCache = new OnnxSessionCache(sessionCacheLogger);
        _sut = new DynamicOnnxProcessorTool(_serviceProvider, _logger, sessionCache);

        // Find the fastpath_model.onnx in the workspace by walking up the directory tree
        var dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            var testPath = Path.Combine(dir, "fastpath_model.onnx");
            if (File.Exists(testPath))
            {
                _modelPath = testPath;
                break;
            }
            dir = Path.GetDirectoryName(dir);
        }

        if (string.IsNullOrEmpty(_modelPath))
        {
            _modelPath = "fastpath_model.onnx"; // fallback
        }
    }

    [Fact]
    public async Task ExecuteAsync_WithUnknownAction_ReturnsFail()
    {
        var input = new ToolInput
        {
            Action = "invalid_action",
            Parameters = new Dictionary<string, object>()
        };

        var result = await _sut.ExecuteAsync(input, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Unknown action");
    }

    [Fact]
    public async Task ExecuteAsync_ProcessAction_MissingParameters_ReturnsFail()
    {
        var input = new ToolInput
        {
            Action = "process",
            Parameters = new Dictionary<string, object>()
        };

        var result = await _sut.ExecuteAsync(input, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Parameters 'modelId' and 'imageData' (base64) are required.");
    }

    [Fact]
    public async Task ExecuteAsync_InspectAction_MissingModelId_ReturnsFail()
    {
        var input = new ToolInput
        {
            Action = "inspect",
            Parameters = new Dictionary<string, object>()
        };

        var result = await _sut.ExecuteAsync(input, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Parameter 'modelId' is required.");
    }

    [Fact]
    public async Task ExecuteAsync_ModelNotFound_ReturnsFail()
    {
        var input = new ToolInput
        {
            Action = "inspect",
            Parameters = new Dictionary<string, object> { { "modelId", "non-existent-id" } }
        };

        var result = await _sut.ExecuteAsync(input, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Model 'non-existent-id' not found.");
    }

    [Fact]
    public async Task ExecuteAsync_ModelDataEmpty_ReturnsFail()
    {
        var modelId = Guid.NewGuid().ToString();
        _dbContext.CustomOnnxModels.Add(new CustomOnnxModelEntity
        {
            Id = modelId,
            TenantId = "test-tenant",
            Name = "Empty Model",
            ModelData = null,
            ModelFileName = null
        });
        await _dbContext.SaveChangesAsync();

        var input = new ToolInput
        {
            Action = "inspect",
            Parameters = new Dictionary<string, object> { { "modelId", modelId } }
        };

        var result = await _sut.ExecuteAsync(input, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Model data is empty or file not found.");
    }

    [Fact]
    public async Task ExecuteAsync_InspectAction_WithValidModelOnDisk_ReturnsMetadata()
    {
        if (string.IsNullOrEmpty(_modelPath)) return; // Skip if model not present

        var modelId = Guid.NewGuid().ToString();
        _dbContext.CustomOnnxModels.Add(new CustomOnnxModelEntity
        {
            Id = modelId,
            TenantId = "test-tenant",
            Name = "OnDisk Model",
            ModelFileName = _modelPath,
            InputNodeName = "Text",
            OutputNodeName = "PredictedLabel"
        });
        await _dbContext.SaveChangesAsync();

        var input = new ToolInput
        {
            Action = "inspect",
            Parameters = new Dictionary<string, object> { { "modelId", modelId } }
        };

        var result = await _sut.ExecuteAsync(input, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        
        var json = System.Text.Json.JsonSerializer.Serialize(result.Data);
        var data = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(json);
        
        data.GetProperty("inputNodes").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Array);
        data.GetProperty("outputNodes").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Array);
    }

    [Fact]
    public async Task ExecuteAsync_InspectAction_WithValidModelInMemory_ReturnsMetadata()
    {
        if (string.IsNullOrEmpty(_modelPath)) return; // Skip if model not present

        var modelBytes = await File.ReadAllBytesAsync(_modelPath);
        var modelId = Guid.NewGuid().ToString();
        _dbContext.CustomOnnxModels.Add(new CustomOnnxModelEntity
        {
            Id = modelId,
            TenantId = "test-tenant",
            Name = "InMemory Model",
            ModelData = modelBytes,
            InputNodeName = "Text",
            OutputNodeName = "PredictedLabel"
        });
        await _dbContext.SaveChangesAsync();

        var input = new ToolInput
        {
            Action = "inspect",
            Parameters = new Dictionary<string, object> { { "modelId", modelId } }
        };

        var result = await _sut.ExecuteAsync(input, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        
        var json = System.Text.Json.JsonSerializer.Serialize(result.Data);
        var data = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(json);
        
        data.GetProperty("inputNodes").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Array);
        data.GetProperty("outputNodes").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Array);
    }

    [Fact]
    public async Task ExecuteAsync_ProcessAction_WithInvalidBase64_ReturnsFail()
    {
        var modelId = Guid.NewGuid().ToString();
        _dbContext.CustomOnnxModels.Add(new CustomOnnxModelEntity
        {
            Id = modelId,
            TenantId = "test-tenant",
            Name = "Dummy Model",
            ModelFileName = _modelPath,
            InputNodeName = "Text",
            OutputNodeName = "PredictedLabel"
        });
        await _dbContext.SaveChangesAsync();

        var input = new ToolInput
        {
            Action = "process",
            Parameters = new Dictionary<string, object> 
            { 
                { "modelId", modelId },
                { "imageData", "invalid-base64-string" }
            }
        };

        var result = await _sut.ExecuteAsync(input, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Invalid base64 image data");
    }

    [Fact]
    public async Task ExecuteAsync_ProcessAction_GracefulOnnxRuntimeFailureForTypeMismatch()
    {
        if (string.IsNullOrEmpty(_modelPath)) return; // Skip if model not present

        var modelId = Guid.NewGuid().ToString();
        _dbContext.CustomOnnxModels.Add(new CustomOnnxModelEntity
        {
            Id = modelId,
            TenantId = "test-tenant",
            Name = "ImageToText Mismatch Model",
            ModelFileName = _modelPath,
            InputNodeName = "Text", // model actually expects text, but we will pass image tensor
            OutputNodeName = "PredictedLabel",
            InputWidth = 224,
            InputHeight = 224,
            Channels = 3,
            OutputFormat = "image"
        });
        await _dbContext.SaveChangesAsync();

        // Create a small 1x1 pixel dummy png image to encode to base64
        using var image = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgb24>(224, 224);
        using var ms = new MemoryStream();
        await image.SaveAsPngAsync(ms);
        var base64Image = Convert.ToBase64String(ms.ToArray());

        var input = new ToolInput
        {
            Action = "process",
            Parameters = new Dictionary<string, object> 
            { 
                { "modelId", modelId },
                { "imageData", base64Image }
            }
        };

        var result = await _sut.ExecuteAsync(input, CancellationToken.None);

        // It should catch the type mismatch exception and return fail gracefully
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("ONNX Runtime error");
    }
}
