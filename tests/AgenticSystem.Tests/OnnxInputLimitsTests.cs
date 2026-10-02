using System.Reflection;
using AgenticSystem.Api.Controllers;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Services.Ml;
using AgenticSystem.Core.Tools;
using AgenticSystem.Infrastructure.BackgroundServices;
using AgenticSystem.Infrastructure.Persistence;
using AgenticSystem.Infrastructure.Persistence.Entities;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.ML.OnnxRuntime.Tensors;
using NSubstitute;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace AgenticSystem.Tests;

public class OnnxInputLimitsTests
{
    [Theory]
    [InlineData(0, 512, 3, "positive")]
    [InlineData(-1, 512, 3, "positive")]
    [InlineData(512, 0, 3, "positive")]
    [InlineData(512, -1, 3, "positive")]
    [InlineData(512, 512, 0, "channels")]
    [InlineData(512, 512, 2, "channels")]
    [InlineData(512, 512, 4, "channels")]
    [InlineData(4097, 1, 3, "dimensions")]
    [InlineData(4096, 4096, 3, "budget")]
    [InlineData(int.MaxValue, int.MaxValue, 3, "overflow")]
    public void InvalidDimensions_AreRejectedWithoutAllocating(int w, int h, int c, string message)
    {
        new OnnxInputLimits().TryValidate(w, h, c, out var error).Should().BeFalse();
        error.Should().Contain(message);
    }

    [Theory]
    [InlineData(224, 224, 3)]
    [InlineData(512, 512, 3)]
    [InlineData(1024, 1024, 3)]
    [InlineData(512, 512, 1)]
    [InlineData(4096, 1, 3)]
    public void SupportedUsualDimensions_AreAccepted(int w, int h, int c)
        => new OnnxInputLimits().TryValidate(w, h, c, out _).Should().BeTrue();

    [Fact]
    public void ConfiguredBudget_IsInclusiveAndUsesFloatWorstCase()
    {
        var limits = new OnnxInputLimits { MaxDimension = 2, MaxPreprocessingBytes = 72 };
        limits.TryValidate(2, 2, 3, out _).Should().BeTrue();
        limits.MaxPreprocessingBytes = 71;
        limits.TryValidate(2, 2, 3, out var error).Should().BeFalse();
        error.Should().Contain("72 bytes");
        OnnxInputLimits.EstimatePreprocessingBytes(2, 2, 1).Should().Be(40);
    }

    [Fact]
    public void RaisedConfiguration_CannotExceedTensorArrayCapacity()
    {
        var limits = new OnnxInputLimits { MaxDimension = int.MaxValue, MaxPreprocessingBytes = long.MaxValue };
        limits.TryValidate(50000, 50000, 3, out var error).Should().BeFalse();
        error.Should().Contain("supported array length");
    }

    [Theory]
    [InlineData(0, 64)]
    [InlineData(4096, 0)]
    [InlineData(-1, -1)]
    public void InvalidConfiguration_FailsClosed(int dimension, long bytes)
    {
        var limits = new OnnxInputLimits { MaxDimension = dimension, MaxPreprocessingBytes = bytes };
        limits.IsValid.Should().BeFalse();
        limits.TryValidate(1, 1, 3, out _).Should().BeFalse();
    }

    [Fact]
    public void CombinedSourceAndTargetBudget_IsEnforced()
    {
        var limits = new OnnxInputLimits { MaxPreprocessingBytes = 83 };
        Action act = () => limits.ValidateSourceAndTarget(2, 2, 2, 2, 3);
        act.Should().Throw<ArgumentException>().WithMessage("*84 bytes*source image*");
        limits.MaxPreprocessingBytes = 84;
        limits.ValidateSourceAndTarget(2, 2, 2, 2, 3);
    }

    [Fact]
    public async Task Upload_InvalidDimensions_ReturnsBadRequestBeforeFileReadOrPersistence()
    {
        using var fixture = new Fixture();
        var result = await fixture.Controller.UploadModel(null!, "bad", "in", "out", inputWidth: int.MaxValue,
            inputHeight: int.MaxValue);
        result.Should().BeOfType<BadRequestObjectResult>();
        ((BadRequestObjectResult)result).Value.Should().NotBeNull();
        fixture.Db.CustomOnnxModels.Should().BeEmpty();
    }

    [Fact]
    public async Task Upload_SmallValidMetadata_IsPreserved()
    {
        using var fixture = new Fixture();
        using var stream = new MemoryStream([1]);
        var file = new FormFile(stream, 0, 1, "file", "test.onnx");
        var result = await fixture.Controller.UploadModel(file, "valid", "in", "out", inputWidth: 2, inputHeight: 2);
        result.Should().BeOfType<CreatedAtActionResult>();
        var model = fixture.Db.CustomOnnxModels.Single();
        model.InputWidth.Should().Be(2);
        model.ModelData.Should().Equal(1);
    }

    [Fact]
    public async Task PartialUpdate_ValidatesMergedMetadataBeforeMutation()
    {
        using var fixture = new Fixture();
        var model = fixture.AddModel(4096, 1, 3);
        await fixture.Db.SaveChangesAsync();
        var result = await fixture.Controller.UpdateModel(model.Id,
            new UpdateOnnxModelRequest { Name = "mutated", InputHeight = 4096 }, default);
        result.Should().BeOfType<BadRequestObjectResult>();
        model.Name.Should().Be("model");
        model.InputHeight.Should().Be(1);
    }

    [Fact]
    public async Task Update_CanRepairInvalidLegacyMetadata()
    {
        using var fixture = new Fixture();
        var model = fixture.AddModel(-1, int.MaxValue, 4);
        await fixture.Db.SaveChangesAsync();
        var result = await fixture.Controller.UpdateModel(model.Id,
            new UpdateOnnxModelRequest { InputWidth = 512, InputHeight = 512, Channels = 3 }, default);
        result.Should().BeOfType<OkObjectResult>();
        model.InputWidth.Should().Be(512);
        model.InputHeight.Should().Be(512);
        model.Channels.Should().Be(3);
    }

    [Fact]
    public async Task ConfiguredLimits_AreSharedByControllerAndTool()
    {
        using var fixture = new Fixture(new OnnxInputLimits { MaxDimension = 16, MaxPreprocessingBytes = 64 });
        var upload = await fixture.Controller.UploadModel(null!, "bad", "in", "out", inputWidth: 2, inputHeight: 2);
        upload.Should().BeOfType<BadRequestObjectResult>();
        var model = fixture.AddModel(2, 2, 3);
        await fixture.Db.SaveChangesAsync();
        var result = await fixture.Tool.ExecuteAsync(new ToolInput
        {
            Action = "process",
            Parameters = new Dictionary<string, object> { ["modelId"] = model.Id, ["imageData"] = "invalid-base64" }
        });
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("budget is 64 bytes");
    }

    [Fact]
    public async Task ManuallyRegisteredTool_ResolvesConfiguredOptionsFromServiceProvider()
    {
        using var fixture = new Fixture();
        fixture.Provider.GetService(typeof(IOptions<OnnxInputLimits>))
            .Returns(Options.Create(new OnnxInputLimits { MaxPreprocessingBytes = 64 }));
        var tool = new DynamicOnnxProcessorTool(fixture.Provider,
            Substitute.For<ILogger<DynamicOnnxProcessorTool>>(), fixture.SessionCache);
        var model = fixture.AddModel(2, 2, 3);
        await fixture.Db.SaveChangesAsync();
        var result = await tool.ExecuteAsync(new ToolInput
        {
            Action = "process",
            Parameters = new Dictionary<string, object> { ["modelId"] = model.Id, ["imageData"] = "invalid-base64" }
        });
        result.ErrorMessage.Should().Contain("budget is 64 bytes");
    }

    [Fact]
    public async Task Tool_RejectsSourceDimensionsBeforeSessionCreation()
    {
        using var fixture = new Fixture(new OnnxInputLimits { MaxDimension = 1 });
        var model = fixture.AddModel(1, 1, 3);
        model.ModelData = [1];
        await fixture.Db.SaveChangesAsync();
        using var image = new Image<Rgb24>(2, 1);
        using var stream = new MemoryStream();
        image.SaveAsPng(stream);
        var result = await fixture.Tool.ExecuteAsync(new ToolInput
        {
            Action = "process",
            Parameters = new Dictionary<string, object>
            {
                ["modelId"] = model.Id, ["imageData"] = Convert.ToBase64String(stream.ToArray())
            }
        });
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("dimensions must not exceed 1");
        fixture.SessionCache.DidNotReceiveWithAnyArgs().GetOrCreateSession(default!, default, default);
    }

    [Fact]
    public async Task TestEndpoint_RejectsLegacyMetadataBeforeQueueOrBase64Decode()
    {
        using var fixture = new Fixture();
        var model = fixture.AddModel(-1, 512, 3);
        await fixture.Db.SaveChangesAsync();
        var result = await fixture.Controller.TestModel(model.Id, null, "invalid-base64", default);
        result.Should().BeOfType<BadRequestObjectResult>();
        await fixture.Queue.DidNotReceive().EnqueueJobAsync(Arg.Any<OnnxInferenceJobRequest>(), Arg.Any<CancellationToken>());
        fixture.Db.CustomOnnxInferenceJobs.Should().BeEmpty();
    }

    [Fact]
    public async Task Tool_RejectsLegacyMetadataBeforeSessionOrImageDecode()
    {
        using var fixture = new Fixture();
        var model = fixture.AddModel(int.MaxValue, int.MaxValue, 3);
        await fixture.Db.SaveChangesAsync();
        var result = await fixture.Tool.ExecuteAsync(new ToolInput
        {
            Action = "process",
            Parameters = new Dictionary<string, object> { ["modelId"] = model.Id, ["imageData"] = "invalid-base64" }
        });
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("overflow");
        fixture.SessionCache.DidNotReceiveWithAnyArgs().GetOrCreateSession(default!, default, default);
    }

    [Fact]
    public async Task Worker_RejectsLegacyMetadataAndPersistsFailureBeforeToolOrFileRead()
    {
        using var fixture = new Fixture();
        var model = fixture.AddModel(4096, 4096, 3);
        var job = new CustomOnnxInferenceJobEntity { Id = "job", TenantId = "tenant", ModelId = model.Id, Status = "Pending" };
        fixture.Db.CustomOnnxInferenceJobs.Add(job);
        await fixture.Db.SaveChangesAsync();
        var manager = Substitute.For<IToolManager>();
        fixture.Provider.GetService(typeof(IToolManager)).Returns(manager);
        var broadcaster = Substitute.For<IOnnxEventBroadcaster>();
        var worker = new OnnxInferenceBackgroundWorker(fixture.Provider, fixture.Queue, broadcaster,
            Substitute.For<ILogger<OnnxInferenceBackgroundWorker>>());
        await worker.ProcessJobAsync(new OnnxInferenceJobRequest(job.Id, "tenant", model.Id, "absent.png"), default);
        job.Status.Should().Be("Failed");
        job.ErrorMessage.Should().Contain("budget");
        await manager.DidNotReceiveWithAnyArgs().ExecuteToolAsync(default!, default!, default);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void SmallRealImage_PreprocessingPreservesNchwPixels(int channels)
    {
        using var fixture = new Fixture();
        using var image = new Image<Rgb24>(2, 1);
        image[0, 0] = new Rgb24(10, 20, 30);
        image[1, 0] = new Rgb24(40, 50, 60);
        var method = typeof(DynamicOnnxProcessorTool).GetMethod("ImageToTensor", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var tensor = (DenseTensor<float>)method.Invoke(fixture.Tool, [image, 2, 1, channels, 0.5f, 1f, 2f, 3f])!;
        tensor.Dimensions.ToArray().Should().Equal(1, channels, 1, 2);
        tensor[0, 0, 0, 0].Should().Be(4f);
        tensor[0, 0, 0, 1].Should().Be(19f);
        if (channels == 3)
        {
            tensor[0, 1, 0, 0].Should().Be(8f);
            tensor[0, 2, 0, 1].Should().Be(27f);
        }
    }

    [Fact]
    public void SmallRealImage_ByteTensorPreservesPixels()
    {
        using var fixture = new Fixture();
        using var image = new Image<Rgb24>(1, 1);
        image[0, 0] = new Rgb24(10, 20, 30);
        var method = typeof(DynamicOnnxProcessorTool).GetMethod("ImageToByteTensor", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var tensor = (DenseTensor<byte>)method.Invoke(fixture.Tool, [image, 1, 1, 3, 1f, 0f, 0f, 0f])!;
        tensor.Dimensions.ToArray().Should().Equal(1, 3, 1, 1);
        tensor.ToArray().Should().Equal(10, 20, 30);
    }

    [Theory]
    [InlineData("ImageToTensor")]
    [InlineData("ImageToByteTensor")]
    public void TensorAllocation_ValidatesBeforeAllocating(string methodName)
    {
        using var fixture = new Fixture();
        using var image = new Image<Rgb24>(1, 1);
        var method = typeof(DynamicOnnxProcessorTool).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance)!;
        Action act = () => method.Invoke(fixture.Tool, [image, int.MaxValue, int.MaxValue, 3, 1f, 0f, 0f, 0f]);
        act.Should().Throw<TargetInvocationException>().WithInnerException<ArgumentException>().WithMessage("*overflow*");
    }

    private sealed class Fixture : IDisposable
    {
        public AgenticDbContext Db { get; }
        public IServiceProvider Provider { get; } = Substitute.For<IServiceProvider>();
        public IOnnxInferenceQueue Queue { get; } = Substitute.For<IOnnxInferenceQueue>();
        public IOnnxSessionCache SessionCache { get; } = Substitute.For<IOnnxSessionCache>();
        public OnnxModelController Controller { get; }
        public DynamicOnnxProcessorTool Tool { get; }

        public Fixture(OnnxInputLimits? configuredLimits = null)
        {
            var accessor = Substitute.For<ITenantContextAccessor>();
            accessor.CurrentTenantId.Returns("tenant");
            Db = new AgenticDbContext(new DbContextOptionsBuilder<AgenticDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, accessor);
            var scope = Substitute.For<IServiceScope>();
            var factory = Substitute.For<IServiceScopeFactory>();
            scope.ServiceProvider.Returns(Provider);
            factory.CreateScope().Returns(scope);
            Provider.GetService(typeof(IServiceScopeFactory)).Returns(factory);
            Provider.GetService(typeof(AgenticDbContext)).Returns(Db);
            Provider.GetService(typeof(ITenantContextAccessor)).Returns(accessor);
            var limits = Options.Create(configuredLimits ?? new OnnxInputLimits());
            Controller = new OnnxModelController(Db, Queue, Substitute.For<ILogger<OnnxModelController>>(),
                Substitute.For<IWebHostEnvironment>(), accessor, limits);
            Tool = new DynamicOnnxProcessorTool(Provider, Substitute.For<ILogger<DynamicOnnxProcessorTool>>(), SessionCache, limits);
        }

        public CustomOnnxModelEntity AddModel(int w, int h, int c)
        {
            var model = new CustomOnnxModelEntity { TenantId = "tenant", Name = "model", InputWidth = w, InputHeight = h, Channels = c };
            Db.CustomOnnxModels.Add(model);
            return model;
        }

        public void Dispose() => Db.Dispose();
    }
}
